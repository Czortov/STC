using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BattleEndController : MonoBehaviour
{
    [Header("Ships")]
    [SerializeField] private ShipIdentity playerShip;
    [SerializeField] private ShipIdentity enemyShip;
    [SerializeField] private ShipHullHealth playerHull;
    [SerializeField] private ShipHullHealth enemyHull;
    [SerializeField] private BunkCrewSpawner playerCrewSpawner;
    [SerializeField] private BunkCrewSpawner enemyCrewSpawner;

    [Header("Result UI")]
    [SerializeField] private BattleResultPanel resultPanel;
    [SerializeField] private Sprite resultPanelBackground;

    [Header("Disabled when battle ends")]
    [SerializeField] private Behaviour[] battleControllers;

    private readonly HashSet<CrewHealth> playerCrew =
        new HashSet<CrewHealth>();

    private readonly HashSet<CrewHealth> enemyCrew =
        new HashSet<CrewHealth>();

    private bool battleEnded;

    public bool BattleEnded => battleEnded;

    public event Action<BattleResult, BattleEndReason> BattleFinished;

    private void Awake()
    {
        battleEnded = false;
        resultPanel?.Hide();
    }

    private void OnEnable()
    {
        ResolveSceneReferences();

        SubscribeToHull(playerHull);
        SubscribeToHull(enemyHull);

        SubscribeToSpawner(playerCrewSpawner);
        SubscribeToSpawner(enemyCrewSpawner);

        CrewUnit.UnitRegistered += HandleCrewRegistered;

        foreach (CrewUnit unit in CrewUnit.ActiveUnits)
        {
            RegisterCrew(unit);
        }

        TryArmCrewChecks();
    }

    private void ResolveSceneReferences()
    {
        if (playerShip == null || enemyShip == null)
        {
            ShipIdentity[] ships = FindObjectsByType<ShipIdentity>(
                FindObjectsInactive.Include
            );

            foreach (ShipIdentity ship in ships)
            {
                if (ship.Team == ShipTeam.Player && playerShip == null)
                {
                    playerShip = ship;
                }
                else if (ship.Team == ShipTeam.Enemy && enemyShip == null)
                {
                    enemyShip = ship;
                }
            }
        }

        playerHull ??= playerShip != null
            ? playerShip.GetComponent<ShipHullHealth>()
            : null;
        enemyHull ??= enemyShip != null
            ? enemyShip.GetComponent<ShipHullHealth>()
            : null;
        playerCrewSpawner ??= playerShip != null
            ? playerShip.GetComponent<BunkCrewSpawner>()
            : null;
        enemyCrewSpawner ??= enemyShip != null
            ? enemyShip.GetComponent<BunkCrewSpawner>()
            : null;

        if (resultPanel == null)
        {
            resultPanel = FindAnyObjectByType<BattleResultPanel>(
                FindObjectsInactive.Include
            );
        }

        if (resultPanel == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                resultPanel = BattleResultPanel.Create(
                    canvas.transform,
                    resultPanelBackground
                );
            }
        }

        if (battleControllers == null || battleControllers.Length == 0)
        {
            battleControllers = new Behaviour[]
            {
                FindAnyObjectByType<CrewCommandController>(),
                FindAnyObjectByType<CannonTargetingController>(),
                enemyShip != null
                    ? enemyShip.GetComponent<EnemyCannonController>()
                    : null,
                enemyShip != null
                    ? enemyShip.GetComponent<EnemyCrewWorkAssigner>()
                    : null
            };
        }

        if (playerHull == null || enemyHull == null ||
            playerCrewSpawner == null || enemyCrewSpawner == null ||
            resultPanel == null)
        {
            Debug.LogError(
                $"{name}: не удалось найти все зависимости завершения боя.",
                this
            );
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromHull(playerHull);
        UnsubscribeFromHull(enemyHull);

        UnsubscribeFromSpawner(playerCrewSpawner);
        UnsubscribeFromSpawner(enemyCrewSpawner);

        CrewUnit.UnitRegistered -= HandleCrewRegistered;

        UnsubscribeFromCrew(playerCrew);
        UnsubscribeFromCrew(enemyCrew);
    }

    private void SubscribeToHull(ShipHullHealth hull)
    {
        if (hull != null)
        {
            hull.Destroyed += HandleHullDestroyed;
        }
    }

    private void UnsubscribeFromHull(ShipHullHealth hull)
    {
        if (hull != null)
        {
            hull.Destroyed -= HandleHullDestroyed;
        }
    }

    private void SubscribeToSpawner(BunkCrewSpawner spawner)
    {
        if (spawner != null)
        {
            spawner.SpawnCompleted += HandleCrewSpawnCompleted;
        }
    }

    private void UnsubscribeFromSpawner(BunkCrewSpawner spawner)
    {
        if (spawner != null)
        {
            spawner.SpawnCompleted -= HandleCrewSpawnCompleted;
        }
    }

    private void HandleHullDestroyed(ShipHullHealth destroyedHull)
    {
        if (destroyedHull == playerHull)
        {
            EndBattle(BattleResult.Defeat, BattleEndReason.HullDestroyed);
        }
        else if (destroyedHull == enemyHull)
        {
            EndBattle(BattleResult.Victory, BattleEndReason.HullDestroyed);
        }
    }

    private void HandleCrewRegistered(CrewUnit unit)
    {
        RegisterCrew(unit);
    }

    private void HandleCrewSpawnCompleted(BunkCrewSpawner spawner)
    {
        foreach (CrewUnit unit in spawner.SpawnedCrew)
        {
            RegisterCrew(unit);
        }

        TryArmCrewChecks();
    }

    private void RegisterCrew(CrewUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        CrewHealth health = unit.GetComponent<CrewHealth>();
        if (health == null)
        {
            return;
        }

        HashSet<CrewHealth> crew = GetCrewSet(unit);
        if (crew == null || !crew.Add(health))
        {
            return;
        }

        health.Died += HandleCrewDied;
    }

    private HashSet<CrewHealth> GetCrewSet(CrewUnit unit)
    {
        Transform unitTransform = unit.transform;

        if (playerShip != null &&
            unitTransform.IsChildOf(playerShip.transform))
        {
            return playerCrew;
        }

        if (enemyShip != null &&
            unitTransform.IsChildOf(enemyShip.transform))
        {
            return enemyCrew;
        }

        return unit.IsPlayerOwned
            ? playerCrew
            : enemyCrew;
    }

    private void HandleCrewDied(CrewHealth deadCrewMember)
    {
        if (battleEnded || deadCrewMember == null)
        {
            return;
        }

        if (playerCrew.Contains(deadCrewMember) &&
            !HasLivingCrew(playerCrew))
        {
            EndBattle(BattleResult.Defeat, BattleEndReason.CrewEliminated);
        }
        else if (enemyCrew.Contains(deadCrewMember) &&
                 !HasLivingCrew(enemyCrew))
        {
            EndBattle(BattleResult.Victory, BattleEndReason.CrewEliminated);
        }
    }

    private void TryArmCrewChecks()
    {
        if (battleEnded ||
            playerCrewSpawner == null ||
            enemyCrewSpawner == null ||
            !playerCrewSpawner.HasSpawned ||
            !enemyCrewSpawner.HasSpawned)
        {
            return;
        }

        if (!HasLivingCrew(playerCrew))
        {
            EndBattle(BattleResult.Defeat, BattleEndReason.CrewEliminated);
        }
        else if (!HasLivingCrew(enemyCrew))
        {
            EndBattle(BattleResult.Victory, BattleEndReason.CrewEliminated);
        }
    }

    private static bool HasLivingCrew(HashSet<CrewHealth> crew)
    {
        foreach (CrewHealth member in crew)
        {
            if (member != null &&
                member.IsInitialized &&
                !member.IsDead)
            {
                return true;
            }
        }

        return false;
    }

    private void UnsubscribeFromCrew(HashSet<CrewHealth> crew)
    {
        foreach (CrewHealth member in crew)
        {
            if (member != null)
            {
                member.Died -= HandleCrewDied;
            }
        }

        crew.Clear();
    }

    private void EndBattle(
        BattleResult result,
        BattleEndReason reason)
    {
        if (battleEnded)
        {
            return;
        }

        battleEnded = true;

        if (battleControllers != null)
        {
            foreach (Behaviour controller in battleControllers)
            {
                if (controller != null)
                {
                    controller.enabled = false;
                }
            }
        }

        resultPanel?.Show(result, reason);
        BattleFinished?.Invoke(result, reason);

        Debug.Log($"Бой завершён: {result}, причина: {reason}.", this);
    }
}
