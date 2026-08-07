using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CrewUnit : MonoBehaviour
{
    private static readonly HashSet<CrewUnit> activeUnits = new HashSet<CrewUnit>();

    public static event Action<CrewUnit> UnitRegistered;
    public static event Action<CrewUnit> UnitUnregistered;
    public static event Action<CrewUnit, bool> SelectionChanged;

    public static IReadOnlyCollection<CrewUnit> ActiveUnits => activeUnits;

    [Header("Identity")]

    [SerializeField] private string displayName;

    [Header("Selection")]

    [SerializeField] private Color normalColor =
        new Color(0.75f, 0.25f, 0.20f, 1f);

    [SerializeField] private Color selectedColor =
        new Color(1f, 0.85f, 0.20f, 1f);

    [SerializeField] private int sortingOrder = 20;

    [Header("Movement")]

    [Min(0.1f)]
    [SerializeField] private float movementSpeed = 2.5f;

    [Range(0.1f, 1f)]
    [SerializeField] private float ladderSpeedMultiplier = 0.7f;

    [Min(0.001f)]
    [SerializeField] private float arrivalDistance = 0.01f;

    [SerializeField] private bool drawPathGizmos = true;

    [Header("Repair")]

    [Tooltip(
        "Сколько единиц прочности юнит восстанавливает за секунду.")]
    [Min(0.1f)]
    [SerializeField] private float repairPointsPerSecond = 10f;

    [SerializeField] private bool repairAutomatically = true;

    [Header("Starting Cell")]

    [Min(0.1f)]
    [SerializeField] private float maxStartCellDistance = 0.8f;

    [SerializeField] private bool snapToStartingCell = true;

    [Min(1)]
    [SerializeField] private int initializationFrames = 120;

    private readonly List<ShipCellView> activePath =
        new List<ShipCellView>();

    private SpriteRenderer unitRenderer;
    private CrewWalkSway walkSway;
    private CrewRepairAnimation repairAnimation;
    private Coroutine movementCoroutine;

    private ShipRoomRuntime currentRepairRoom;
    private float repairAccumulator;
    private bool wasRepairing;

    public bool IsSelected { get; private set; }
    public bool IsMoving => movementCoroutine != null;
    public bool IsRepairing => wasRepairing;
    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? CrewNameGenerator.DefaultName
            : displayName;

    public ShipCellView CurrentCell { get; private set; }
    public ShipCellView TargetCell { get; private set; }

    public ShipRoomRuntime TargetRoom { get; private set; }

    public bool IsPlayerOwned
    {
        get
        {
            ShipIdentity ship = GetComponentInParent<ShipIdentity>();

            if (ship == null && CurrentCell != null && CurrentCell.Room != null)
            {
                ship = CurrentCell.Room.GetComponentInParent<ShipIdentity>();
            }

            return ship != null && ship.Team == ShipTeam.Player;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        activeUnits.Clear();
        UnitRegistered = null;
        UnitUnregistered = null;
        SelectionChanged = null;
    }

    private void Awake()
    {
        EnsureDisplayName();

        unitRenderer = GetComponent<SpriteRenderer>();
        unitRenderer.sortingOrder = sortingOrder;
        walkSway = GetComponent<CrewWalkSway>();
        repairAnimation = GetComponent<CrewRepairAnimation>();

        ApplySelectionVisual();
    }

    private void EnsureDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        ShipIdentity ship = GetComponentInParent<ShipIdentity>();

        displayName = ship != null
            ? CrewNameGenerator.Generate(ship.Team)
            : CrewNameGenerator.DefaultName;
    }

    private void OnEnable()
    {
        if (activeUnits.Add(this))
        {
            UnitRegistered?.Invoke(this);
        }
    }

    private void OnDisable()
    {
        SetRepairingState(false);

        if (activeUnits.Remove(this))
        {
            UnitUnregistered?.Invoke(this);
        }
    }

    private IEnumerator Start()
    {
        for (int frame = 0;
             frame < initializationFrames;
             frame++)
        {
            if (TryDetectStartingCell())
            {
                yield break;
            }

            yield return null;
        }

        Debug.LogError(
            $"{name}: не удалось определить стартовую клетку. " +
            "Поставь юнита ближе к центру обычной клетки корабля.",
            this
        );
    }

    private void Update()
    {
        UpdateRepair();
    }

    public void SetSelected(bool selected)
    {
        if (IsSelected == selected)
        {
            return;
        }

        IsSelected = selected;
        ApplySelectionVisual();
        SelectionChanged?.Invoke(this, selected);
    }

    public void AssignRoom(ShipRoomRuntime room)
    {
        if (room == null)
        {
            Debug.LogWarning(
                $"{name}: невозможно назначить пустой отсек.",
                this
            );

            return;
        }

        if (CurrentCell == null)
        {
            Debug.LogWarning(
                $"{name}: стартовая клетка ещё не определена.",
                this
            );

            return;
        }

        ShipCellView previousTargetCell = TargetCell;

        /*
         * Старую команду пока не отменяем.
         * Сначала проверяем новую цель и маршрут.
         */
        if (!room.TryReserveDestination(
                this,
                out ShipCellView newDestination))
        {
            Debug.LogWarning(
                $"{name}: в отсеке {room.Id} " +
                "нет доступных клеток. " +
                "Предыдущая команда продолжает выполняться.",
                this
            );

            return;
        }

        bool reusedPreviousReservation =
            newDestination == previousTargetCell;

        if (!ShipPathfinder.TryBuildPath(
                CurrentCell,
                newDestination,
                out List<ShipCellView> newPath,
                out string pathError))
        {
            if (!reusedPreviousReservation)
            {
                newDestination.ReleaseReservation(this);
            }

            Debug.LogWarning(
                $"{name}: {pathError} " +
                "Предыдущая команда продолжает выполняться.",
                this
            );

            return;
        }

        StopMovementCoroutine();

        if (previousTargetCell != null &&
            previousTargetCell != newDestination)
        {
            previousTargetCell.ReleaseReservation(this);
        }

        TargetRoom = room;
        TargetCell = newDestination;

        activePath.Clear();
        activePath.AddRange(newPath);

        string destinationType =
            newDestination.IsControlPoint
                ? "пункт управления"
                : "обычная клетка";

        Debug.Log(
            $"{name}: построен маршрут из " +
            $"{Mathf.Max(0, newPath.Count - 1)} переходов. " +
            $"Цель X={newDestination.Coordinates.x}, " +
            $"Y={newDestination.Coordinates.y}, " +
            $"{destinationType}.",
            this
        );

        walkSway?.SetMoving(true);

        movementCoroutine =
            StartCoroutine(MoveAlongPath(newPath));
    }

    private IEnumerator MoveAlongPath(
        List<ShipCellView> path)
    {
        if (path == null || path.Count == 0)
        {
            movementCoroutine = null;
            walkSway?.SetMoving(false);
            yield break;
        }

        LeaveCurrentCell();

        /*
         * При изменении команды между клетками
         * возвращаемся к последней достигнутой клетке.
         */
        yield return MoveToCell(
            path[0],
            movementSpeed
        );

        CurrentCell = path[0];

        for (int i = 1; i < path.Count; i++)
        {
            ShipCellView previousCell = path[i - 1];
            ShipCellView nextCell = path[i];

            bool isVerticalLadderMovement =
                previousCell.IsLadder &&
                nextCell.IsLadder &&
                previousCell.Coordinates.x ==
                nextCell.Coordinates.x &&
                previousCell.Coordinates.y !=
                nextCell.Coordinates.y;

            float currentSpeed =
                isVerticalLadderMovement
                    ? movementSpeed * ladderSpeedMultiplier
                    : movementSpeed;

            yield return MoveToCell(
                nextCell,
                currentSpeed
            );

            CurrentCell = nextCell;
        }

        CompleteMovement();
    }

    private IEnumerator MoveToCell(
        ShipCellView cell,
        float speed)
    {
        if (cell == null)
        {
            yield break;
        }

        Vector3 targetPosition =
            new Vector3(
                cell.transform.position.x,
                cell.transform.position.y,
                transform.position.z
            );

        while ((transform.position - targetPosition)
               .sqrMagnitude >
               arrivalDistance * arrivalDistance)
        {
            transform.position =
                Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    speed * Time.deltaTime
                );

            yield return null;
        }

        transform.position = targetPosition;
    }

    private void CompleteMovement()
    {
        movementCoroutine = null;
        walkSway?.SetMoving(false);

        if (TargetCell == null)
        {
            activePath.Clear();
            return;
        }

        ShipCellView reachedCell = TargetCell;
        ShipRoomRuntime reachedRoom = TargetRoom;

        if (!reachedCell.TryOccupy(this))
        {
            reachedCell.ReleaseReservation(this);

            Debug.LogError(
                $"{name}: конечная клетка оказалась занята " +
                "во время движения.",
                this
            );

            TargetCell = null;
            TargetRoom = null;
            activePath.Clear();

            return;
        }

        CurrentCell = reachedCell;

        /*
         * Повреждённый пункт управления не активируется.
         * Юнит сначала автоматически отремонтирует отсек.
         */
        if (reachedCell.IsControlPoint &&
            reachedRoom.IsFullyRepaired)
        {
            reachedRoom.TryActivateSystem(
                this,
                reachedCell
            );
        }

        Debug.Log(
            $"{name}: прибыл в отсек {reachedRoom.Id}, " +
            $"клетка X={reachedCell.Coordinates.x}, " +
            $"Y={reachedCell.Coordinates.y}.",
            this
        );

        TargetCell = null;
        TargetRoom = null;
        activePath.Clear();
    }

    private void UpdateRepair()
    {
        if (!repairAutomatically ||
            IsMoving ||
            CurrentCell == null ||
            CurrentCell.Room == null)
        {
            ResetRepairState();
            return;
        }

        ShipRoomRuntime room =
            CurrentCell.Room;

        if (currentRepairRoom != room)
        {
            currentRepairRoom = room;
            repairAccumulator = 0f;
            SetRepairingState(false);
        }

        if (!room.IsDamaged)
        {
            SetRepairingState(false);
            repairAccumulator = 0f;
            return;
        }

        if (!wasRepairing)
        {
            SetRepairingState(true);

            Debug.Log(
                $"{name} начал ремонтировать отсек {room.Id}. " +
                $"Прочность: {room.CurrentHealth}/{room.MaxHealth}.",
                this
            );
        }

        repairAccumulator +=
            repairPointsPerSecond * Time.deltaTime;

        int repairPoints =
            Mathf.FloorToInt(repairAccumulator);

        if (repairPoints <= 0)
        {
            return;
        }

        repairAccumulator -= repairPoints;

        room.Repair(repairPoints);

        if (!room.IsFullyRepaired)
        {
            return;
        }

        SetRepairingState(false);
        repairAccumulator = 0f;

        Debug.Log(
            $"{name} завершил ремонт отсека {room.Id}.",
            this
        );
    }

    private void ResetRepairState()
    {
        currentRepairRoom = null;
        repairAccumulator = 0f;
        SetRepairingState(false);
    }

    private void SetRepairingState(bool repairing)
    {
        if (wasRepairing == repairing)
        {
            return;
        }

        wasRepairing = repairing;
        repairAnimation?.SetRepairing(repairing);
    }

    private bool TryDetectStartingCell()
    {
        ShipCellView nearestCell = null;
        float nearestDistance = float.MaxValue;

        foreach (ShipCellView cell
                 in ShipCellView.AllCells)
        {
            if (cell == null ||
                !cell.CanBeDestination)
            {
                continue;
            }

            float distance =
                (cell.transform.position -
                 transform.position).sqrMagnitude;

            if (distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestCell = cell;
        }

        if (nearestCell == null)
        {
            return false;
        }

        float allowedDistance = Mathf.Max(
            maxStartCellDistance,
            nearestCell.CellSize * 0.75f
        );

        if (nearestDistance >
            allowedDistance * allowedDistance)
        {
            return false;
        }

        if (!nearestCell.TryOccupy(this))
        {
            Debug.LogError(
                $"{name}: ближайшая клетка " +
                $"X={nearestCell.Coordinates.x}, " +
                $"Y={nearestCell.Coordinates.y} " +
                "уже занята другим юнитом.",
                this
            );

            return false;
        }

        CurrentCell = nearestCell;

        if (snapToStartingCell)
        {
            Vector3 cellPosition =
                nearestCell.transform.position;

            transform.position =
                new Vector3(
                    cellPosition.x,
                    cellPosition.y,
                    transform.position.z
                );
        }

        if (CurrentCell.IsControlPoint &&
            CurrentCell.Room != null &&
            CurrentCell.Room.IsFullyRepaired)
        {
            CurrentCell.Room.TryActivateSystem(
                this,
                CurrentCell
            );
        }

        Debug.Log(
            $"{name}: стартовая клетка определена. " +
            $"X={CurrentCell.Coordinates.x}, " +
            $"Y={CurrentCell.Coordinates.y}, " +
            $"отсек {CurrentCell.RoomId}.",
            this
        );

        return true;
    }

    private void LeaveCurrentCell()
    {
        ResetRepairState();

        if (CurrentCell == null)
        {
            return;
        }

        if (CurrentCell.IsControlPoint &&
            CurrentCell.Room != null)
        {
            CurrentCell.Room.DeactivateSystem(this);
        }

        CurrentCell.Vacate(this);
    }

    private void StopMovementCoroutine()
    {
        walkSway?.SetMoving(false);

        if (movementCoroutine == null)
        {
            return;
        }

        StopCoroutine(movementCoroutine);
        movementCoroutine = null;
    }

    private void CancelCurrentCommand()
    {
        StopMovementCoroutine();
        ReleaseTargetReservation();
        activePath.Clear();
    }

    private void ReleaseTargetReservation()
    {
        if (TargetCell != null)
        {
            TargetCell.ReleaseReservation(this);
        }

        TargetCell = null;
        TargetRoom = null;
    }

    private void OnDestroy()
    {
        CancelCurrentCommand();
        LeaveCurrentCell();
    }

    private void ApplySelectionVisual()
    {
        if (unitRenderer == null)
        {
            return;
        }

        unitRenderer.color =
            IsSelected
                ? selectedColor
                : normalColor;
    }

    private void OnValidate()
    {
        SpriteRenderer spriteRenderer =
            GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder =
                sortingOrder;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPathGizmos ||
            activePath == null ||
            activePath.Count < 2)
        {
            return;
        }

        Gizmos.color = Color.cyan;

        for (int i = 1;
             i < activePath.Count;
             i++)
        {
            if (activePath[i - 1] == null ||
                activePath[i] == null)
            {
                continue;
            }

            Gizmos.DrawLine(
                activePath[i - 1].transform.position,
                activePath[i].transform.position
            );
        }
    }
}
