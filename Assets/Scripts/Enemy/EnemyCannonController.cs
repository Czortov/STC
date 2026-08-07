using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ShipIdentity))]
public sealed class EnemyCannonController : MonoBehaviour
{
    [Header("Automatic fire")]

    [SerializeField] private bool fireAutomatically = true;

    [Tooltip(
        "Как часто ИИ проверяет состояние пушек.")]
    [Min(0.05f)]
    [SerializeField] private float fireCheckInterval = 0.2f;

    [Tooltip(
        "Минимальная дополнительная задержка перед выстрелом " +
        "после того, как пушка стала готова.")]
    [Min(0f)]
    [SerializeField] private float minimumFireDelay = 0.2f;

    [Tooltip(
        "Максимальная дополнительная задержка перед выстрелом.")]
    [Min(0f)]
    [SerializeField] private float maximumFireDelay = 1f;

    [Header("Target selection")]

    [Tooltip(
        "Разрешает выбирать уже уничтоженные отсеки. " +
        "Экипаж внутри такого отсека всё равно получит урон.")]
    [SerializeField] private bool allowDestroyedTargets = true;

    [Header("Initialization")]

    [Tooltip(
        "Сколько кадров ждать генерацию кораблей и пушек.")]
    [Min(1)]
    [SerializeField] private int initializationFrames = 300;

    private readonly List<CannonSystemRuntime> enemyCannons =
        new List<CannonSystemRuntime>();

    private readonly List<ShipRoomRuntime> playerRooms =
        new List<ShipRoomRuntime>();

    private readonly Dictionary<CannonSystemRuntime, float>
        nextFireTimes =
            new Dictionary<CannonSystemRuntime, float>();

    private ShipIdentity enemyShip;
    private ShipIdentity playerShip;

    private IEnumerator Start()
    {
        enemyShip = GetComponent<ShipIdentity>();

        if (enemyShip == null)
        {
            Debug.LogError(
                $"{name}: не найден ShipIdentity.",
                this
            );

            yield break;
        }

        if (enemyShip.Team != ShipTeam.Enemy)
        {
            Debug.LogError(
                $"{name}: EnemyCannonController можно добавлять " +
                "только на корабль с Team = Enemy.",
                this
            );

            yield break;
        }

        if (!fireAutomatically)
        {
            yield break;
        }

        bool initialized = false;

        for (int frame = 0;
             frame < initializationFrames;
             frame++)
        {
            if (TryInitializeBattleObjects())
            {
                initialized = true;
                break;
            }

            yield return null;
        }

        if (!initialized)
        {
            Debug.LogError(
                $"{name}: не удалось найти вражеские пушки " +
                "или отсеки корабля игрока. " +
                "Проверь генерацию кораблей и ShipIdentity.",
                this
            );

            yield break;
        }

        InitializeFireTimers();

        WaitForSeconds wait =
            new WaitForSeconds(
                Mathf.Max(0.05f, fireCheckInterval)
            );

        while (enabled)
        {
            TryFireReadyCannons();

            yield return wait;
        }
    }

    private bool TryInitializeBattleObjects()
    {
        FindPlayerShip();

        if (playerShip == null)
        {
            return false;
        }

        enemyCannons.Clear();

        CannonSystemRuntime[] foundCannons =
            GetComponentsInChildren<CannonSystemRuntime>(
                true
            );

        foreach (CannonSystemRuntime cannon
                 in foundCannons)
        {
            if (cannon != null)
            {
                enemyCannons.Add(cannon);
            }
        }

        playerRooms.Clear();

        ShipRoomRuntime[] foundRooms =
            playerShip.GetComponentsInChildren
                <ShipRoomRuntime>(true);

        foreach (ShipRoomRuntime room in foundRooms)
        {
            if (room != null)
            {
                playerRooms.Add(room);
            }
        }

        return enemyCannons.Count > 0 &&
               playerRooms.Count > 0;
    }

    private void FindPlayerShip()
    {
        ShipIdentity[] ships =
            FindObjectsByType<ShipIdentity>(
                FindObjectsInactive.Include
            );

        foreach (ShipIdentity ship in ships)
        {
            if (ship != null &&
                ship.Team == ShipTeam.Player)
            {
                playerShip = ship;
                return;
            }
        }

        playerShip = null;
    }

    private void InitializeFireTimers()
    {
        nextFireTimes.Clear();

        foreach (CannonSystemRuntime cannon
                 in enemyCannons)
        {
            if (cannon == null)
            {
                continue;
            }

            nextFireTimes[cannon] =
                Time.time + GetRandomFireDelay();
        }
    }

    private void TryFireReadyCannons()
    {
        if (playerShip == null)
        {
            FindPlayerShip();

            if (playerShip == null)
            {
                return;
            }
        }

        RemoveMissingObjects();

        List<ShipRoomRuntime> availableTargets =
            GetAvailableTargets();

        if (availableTargets.Count == 0)
        {
            return;
        }

        foreach (CannonSystemRuntime cannon
                 in enemyCannons)
        {
            if (cannon == null)
            {
                continue;
            }

            if (!nextFireTimes.TryGetValue(
                    cannon,
                    out float nextFireTime))
            {
                nextFireTime =
                    Time.time + GetRandomFireDelay();

                nextFireTimes[cannon] =
                    nextFireTime;
            }

            if (Time.time < nextFireTime)
            {
                continue;
            }

            /*
             * CanFire уже проверяет:
             * — наличие оператора;
             * — полную прочность отсека;
             * — завершение перезарядки;
             * — наличие боеприпаса.
             */
            if (!cannon.CanFire)
            {
                continue;
            }

            ShipRoomRuntime targetRoom =
                availableTargets[
                    Random.Range(
                        0,
                        availableTargets.Count
                    )
                ];

            bool fired =
                cannon.TryFire(targetRoom);

            if (!fired)
            {
                /*
                 * Повторяем попытку немного позже,
                 * но не создаём новый снаряд.
                 */
                nextFireTimes[cannon] =
                    Time.time +
                    Mathf.Max(
                        0.05f,
                        fireCheckInterval
                    );

                continue;
            }

            nextFireTimes[cannon] =
                Time.time + GetRandomFireDelay();

            Debug.Log(
                $"{name}: вражеская пушка из отсека " +
                $"{cannon.Room.Id} выбрала случайную цель — " +
                $"отсек игрока {targetRoom.Id}.",
                cannon
            );
        }
    }

    private List<ShipRoomRuntime> GetAvailableTargets()
    {
        List<ShipRoomRuntime> availableTargets =
            new List<ShipRoomRuntime>();

        foreach (ShipRoomRuntime room in playerRooms)
        {
            if (room == null ||
                !room.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!allowDestroyedTargets &&
                room.IsDestroyed)
            {
                continue;
            }

            availableTargets.Add(room);
        }

        return availableTargets;
    }

    private void RemoveMissingObjects()
    {
        enemyCannons.RemoveAll(
            cannon => cannon == null
        );

        playerRooms.RemoveAll(
            room => room == null
        );

        List<CannonSystemRuntime> missingTimers =
            new List<CannonSystemRuntime>();

        foreach (
            KeyValuePair<CannonSystemRuntime, float>
                pair in nextFireTimes)
        {
            if (pair.Key == null ||
                !enemyCannons.Contains(pair.Key))
            {
                missingTimers.Add(pair.Key);
            }
        }

        foreach (CannonSystemRuntime cannon
                 in missingTimers)
        {
            nextFireTimes.Remove(cannon);
        }
    }

    private float GetRandomFireDelay()
    {
        float minimum =
            Mathf.Max(0f, minimumFireDelay);

        float maximum =
            Mathf.Max(
                minimum,
                maximumFireDelay
            );

        return Random.Range(
            minimum,
            maximum
        );
    }

}
