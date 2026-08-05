using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyCrewWorkAssigner : MonoBehaviour
{
    [Header("Initialization")]

    [SerializeField] private bool assignOnStart = true;

    [Min(1)]
    [SerializeField] private int initializationFrames = 300;

    [Header("Automatic reassignment")]

    [SerializeField] private bool keepWorkstationsStaffed = true;

    [Min(0.1f)]
    [SerializeField] private float reassignmentInterval = 1f;

    [Tooltip(
        "Если свободного экипажа нет, оператор менее важной " +
        "системы может перейти на более важное рабочее место.")]
    [SerializeField]
    private bool allowLowerPriorityReassignment = true;

    [Header("Medical retreat")]

    [Tooltip(
        "При здоровье ниже этого значения пират покидает работу " +
        "и направляется в каюту с койкой.")]
    [Min(1)]
    [SerializeField] private int retreatHealthThreshold = 40;

    [Tooltip(
        "Пират остаётся в каюте до полного восстановления здоровья.")]
    [SerializeField] private bool waitForFullRecovery = true;

    [Header("Cannon resupply")]

    [Tooltip(
        "Включает автоматическую доставку ящиков с боеприпасами " +
        "к вражеским пушкам.")]
    [SerializeField] private bool manageCannonResupply = true;

    [Tooltip(
        "Ассет обычного ядра. Бот использует и отслеживает " +
        "только этот тип боеприпаса.")]
    [SerializeField] private CannonballAmmoDefinition cannonballAmmo;

    [Tooltip(
        "Пушка запрашивает пополнение, когда доля оставшихся ядер " +
        "становится строго меньше этого значения.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float cannonballResupplyThreshold = 0.4f;

    private readonly HashSet<CrewUnit> recoveringCrew =
        new HashSet<CrewUnit>();

    /*
     * Один ключ-пушка может иметь только одного доставщика.
     * Это не позволяет двум пиратам одновременно пополнять
     * одну и ту же пушку.
     */
    private readonly Dictionary<CannonSystemRuntime, CrewUnit>
        cannonCouriers =
            new Dictionary<CannonSystemRuntime, CrewUnit>();

    private ShipIdentity shipIdentity;

    private IEnumerator Start()
    {
        shipIdentity = GetComponent<ShipIdentity>();

        if (shipIdentity == null)
        {
            Debug.LogError(
                $"{name}: не найден компонент ShipIdentity.",
                this
            );

            yield break;
        }

        if (shipIdentity.Team != ShipTeam.Enemy)
        {
            Debug.LogError(
                $"{name}: EnemyCrewWorkAssigner должен находиться " +
                "только на корабле с Team = Enemy.",
                this
            );

            yield break;
        }

        if (!assignOnStart)
        {
            yield break;
        }

        bool initialized = false;

        for (int frame = 0;
             frame < initializationFrames;
             frame++)
        {
            if (IsShipAndCrewReady())
            {
                initialized = true;
                break;
            }

            yield return null;
        }

        if (!initialized)
        {
            Debug.LogError(
                $"{name}: не удалось дождаться экипажа " +
                "или рабочих отсеков.",
                this
            );

            yield break;
        }

        UpdateMedicalAssignments();
        UpdateCannonResupply();

        if (keepWorkstationsStaffed)
        {
            AssignAvailableCrew();
        }

        WaitForSeconds wait =
            new WaitForSeconds(
                Mathf.Max(0.1f, reassignmentInterval)
            );

        while (enabled)
        {
            yield return wait;

            /*
             * Лечение имеет самый высокий приоритет.
             * Раненый доставщик сначала освобождается от задания,
             * затем отправляется в каюту.
             */
            UpdateMedicalAssignments();
            UpdateCannonResupply();

            if (keepWorkstationsStaffed)
            {
                AssignAvailableCrew();
            }
        }
    }

    private bool IsShipAndCrewReady()
    {
        List<ShipRoomRuntime> workRooms =
            GetWorkRooms();

        if (workRooms.Count == 0)
        {
            return false;
        }

        CrewUnit[] crew =
            GetComponentsInChildren<CrewUnit>(true);

        if (crew.Length == 0)
        {
            return false;
        }

        foreach (CrewUnit unit in crew)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (unit.CurrentCell == null)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateMedicalAssignments()
    {
        CrewUnit[] allCrew =
            GetComponentsInChildren<CrewUnit>(true);

        RemoveInvalidRecoveryEntries();

        foreach (CrewUnit unit in allCrew)
        {
            if (unit == null ||
                !unit.gameObject.activeInHierarchy)
            {
                continue;
            }

            CrewHealth health =
                unit.GetComponent<CrewHealth>();

            if (health == null || health.IsDead)
            {
                recoveringCrew.Remove(unit);
                continue;
            }

            bool isRecovering =
                recoveringCrew.Contains(unit);

            if (!isRecovering &&
                health.CurrentHealth <
                retreatHealthThreshold)
            {
                recoveringCrew.Add(unit);

                RemoveCourierAssignment(unit);

                Debug.Log(
                    $"{unit.name}: здоровье упало до " +
                    $"{health.CurrentHealth}/{health.MaxHealth}. " +
                    "Пират направляется в каюту.",
                    unit
                );

                SendCrewToBunk(
                    unit,
                    showWarning: true
                );

                continue;
            }

            if (!isRecovering)
            {
                continue;
            }

            bool recoveryCompleted =
                waitForFullRecovery
                    ? health.CurrentHealth >= health.MaxHealth
                    : health.CurrentHealth >=
                      retreatHealthThreshold;

            if (recoveryCompleted)
            {
                recoveringCrew.Remove(unit);

                Debug.Log(
                    $"{unit.name}: лечение завершено. " +
                    $"Здоровье: {health.CurrentHealth}/" +
                    $"{health.MaxHealth}. " +
                    "Пират снова доступен для работы.",
                    unit
                );

                continue;
            }

            if (IsStandingInBunk(unit))
            {
                continue;
            }

            if (IsMovingToBunk(unit))
            {
                continue;
            }

            SendCrewToBunk(
                unit,
                showWarning: false
            );
        }
    }

    private bool SendCrewToBunk(
        CrewUnit unit,
        bool showWarning)
    {
        if (unit == null)
        {
            return false;
        }

        ShipRoomRuntime bunkRoom =
            FindNearestAvailableBunkRoom(unit);

        if (bunkRoom == null)
        {
            if (showWarning)
            {
                Debug.LogWarning(
                    $"{unit.name}: на вражеском корабле нет " +
                    "доступной койки. Пират не может начать лечение.",
                    unit
                );
            }

            return false;
        }

        unit.AssignRoom(bunkRoom);

        Debug.Log(
            $"{unit.name} направлен в каюту, " +
            $"отсек {bunkRoom.Id}.",
            unit
        );

        return true;
    }

    private ShipRoomRuntime FindNearestAvailableBunkRoom(
        CrewUnit unit)
    {
        ShipRoomRuntime[] allRooms =
            GetComponentsInChildren<ShipRoomRuntime>(true);

        ShipRoomRuntime nearestRoom = null;
        float nearestDistance = float.MaxValue;

        Vector3 unitPosition =
            unit.CurrentCell != null
                ? unit.CurrentCell.transform.position
                : unit.transform.position;

        foreach (ShipRoomRuntime room in allRooms)
        {
            if (room == null)
            {
                continue;
            }

            foreach (ShipCellView cell in room.Cells)
            {
                if (cell == null ||
                    cell.ModuleType != ShipModuleType.Bunks ||
                    !cell.IsAvailableFor(unit))
                {
                    continue;
                }

                float distance =
                    (cell.transform.position -
                     unitPosition).sqrMagnitude;

                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestRoom = room;
            }
        }

        return nearestRoom;
    }

    private static bool IsStandingInBunk(
        CrewUnit unit)
    {
        return unit != null &&
               !unit.IsMoving &&
               unit.CurrentCell != null &&
               unit.CurrentCell.ModuleType ==
               ShipModuleType.Bunks;
    }

    private static bool IsMovingToBunk(
        CrewUnit unit)
    {
        if (unit == null ||
            !unit.IsMoving ||
            unit.TargetRoom == null)
        {
            return false;
        }

        return RoomContainsBunk(unit.TargetRoom);
    }

    private static bool RoomContainsBunk(
        ShipRoomRuntime room)
    {
        if (room == null)
        {
            return false;
        }

        foreach (ShipCellView cell in room.Cells)
        {
            if (cell != null &&
                cell.ModuleType == ShipModuleType.Bunks)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveInvalidRecoveryEntries()
    {
        recoveringCrew.RemoveWhere(
            unit =>
                unit == null ||
                !unit.gameObject.activeInHierarchy
        );
    }

    private void UpdateCannonResupply()
    {
        if (!manageCannonResupply)
        {
            return;
        }

        EnsureEnemyCannonsUseCannonballs();
        RemoveInvalidCourierAssignments();
        HandleEmptyCannonOperators();
        AdvanceCourierAssignments();
        CreateCourierAssignments();
    }

    private void EnsureEnemyCannonsUseCannonballs()
    {
        foreach (CannonSystemRuntime cannon in GetCannons())
        {
            if (!TryGetCannonballAmmo(
                    cannon,
                    out CannonballAmmoDefinition ammo))
            {
                continue;
            }

            if (cannon.LoadedAmmo != ammo)
            {
                cannon.SetAmmo(ammo);
            }
        }
    }

    private void RemoveInvalidCourierAssignments()
    {
        List<CannonSystemRuntime> assignmentsToRemove =
            new List<CannonSystemRuntime>();

        foreach (
            KeyValuePair<CannonSystemRuntime, CrewUnit> pair
            in cannonCouriers)
        {
            CannonSystemRuntime cannon = pair.Key;
            CrewUnit courier = pair.Value;

            if (cannon == null ||
                courier == null ||
                !courier.gameObject.activeInHierarchy ||
                recoveringCrew.Contains(courier))
            {
                assignmentsToRemove.Add(cannon);
                continue;
            }

            CrewHealth health =
                courier.GetComponent<CrewHealth>();

            CrewAmmoCarrier carrier =
                courier.GetComponent<CrewAmmoCarrier>();

            if (health != null && health.IsDead)
            {
                assignmentsToRemove.Add(cannon);
                continue;
            }

            if (carrier == null)
            {
                Debug.LogWarning(
                    $"{courier.name}: отсутствует CrewAmmoCarrier. " +
                    "Пират исключён из доставки боеприпасов.",
                    courier
                );

                assignmentsToRemove.Add(cannon);
                continue;
            }

            /*
             * Если пушка уже вышла из зоны пополнения,
             * резерв снимается. Обычно это происходит после
             * доставки ящика и полного восстановления запасов.
             */
            if (!NeedsCannonResupply(cannon))
            {
                assignmentsToRemove.Add(cannon);
            }
        }

        foreach (CannonSystemRuntime cannon
                 in assignmentsToRemove)
        {
            cannonCouriers.Remove(cannon);
        }
    }

    private void HandleEmptyCannonOperators()
    {
        foreach (CannonSystemRuntime cannon in GetCannons())
        {
            if (!IsCannonballEmpty(cannon) ||
                cannon.Room == null)
            {
                continue;
            }

            CrewUnit currentOperator =
                cannon.Room.Operator;

            if (currentOperator == null ||
                recoveringCrew.Contains(currentOperator))
            {
                continue;
            }

            /*
             * Если для этой пушки уже назначен другой доставщик,
             * оператор просто освобождает бесполезный пост.
             */
            if (cannonCouriers.TryGetValue(
                    cannon,
                    out CrewUnit assignedCourier))
            {
                if (assignedCourier == currentOperator)
                {
                    SendCourierToCurrentObjective(
                        cannon,
                        currentOperator
                    );
                }
                else
                {
                    SendCrewAwayFromRoom(
                        currentOperator,
                        cannon.Room
                    );
                }

                continue;
            }

            /*
             * При нуле ядер оператор сам становится доставщиком,
             * если он здоров и имеет компонент переноски ящика.
             */
            if (CanActAsCourier(
                    currentOperator,
                    allowWorkingCrew: true))
            {
                cannonCouriers.Add(
                    cannon,
                    currentOperator
                );

                Debug.Log(
                    $"{currentOperator.name}: в пушке отсека " +
                    $"{cannon.Room.Id} закончились ядра. " +
                    "Оператор покидает пост и идёт за ящиком.",
                    currentOperator
                );

                SendCourierToCurrentObjective(
                    cannon,
                    currentOperator
                );

                continue;
            }

            SendCrewAwayFromRoom(
                currentOperator,
                cannon.Room
            );
        }
    }

    private void AdvanceCourierAssignments()
    {
        foreach (
            KeyValuePair<CannonSystemRuntime, CrewUnit> pair
            in cannonCouriers)
        {
            CannonSystemRuntime cannon = pair.Key;
            CrewUnit courier = pair.Value;

            if (cannon == null ||
                courier == null ||
                recoveringCrew.Contains(courier))
            {
                continue;
            }

            SendCourierToCurrentObjective(
                cannon,
                courier
            );
        }
    }

    private void CreateCourierAssignments()
    {
        List<CannonSystemRuntime> cannons =
            GetCannons();

        /*
         * Сначала обслуживаем пушку с наименьшей долей ядер.
         */
        cannons.Sort(
            (first, second) =>
                GetCannonballRatio(first)
                    .CompareTo(
                        GetCannonballRatio(second)
                    )
        );

        foreach (CannonSystemRuntime cannon in cannons)
        {
            if (!NeedsCannonResupply(cannon) ||
                cannonCouriers.ContainsKey(cannon))
            {
                continue;
            }

            CrewUnit courier =
                FindBestResupplyCourier(cannon);

            if (courier == null)
            {
                continue;
            }

            cannonCouriers.Add(cannon, courier);

            Debug.Log(
                $"{courier.name} назначен на пополнение пушки " +
                $"в отсеке {cannon.Room?.Id}. " +
                $"Остаток ядер: " +
                $"{GetCannonballCount(cannon)}/" +
                $"{GetCannonballMaximum(cannon)}.",
                courier
            );

            SendCourierToCurrentObjective(
                cannon,
                courier
            );
        }
    }

    private CrewUnit FindBestResupplyCourier(
        CannonSystemRuntime targetCannon)
    {
        CrewUnit[] allCrew =
            GetComponentsInChildren<CrewUnit>(true);

        CrewUnit closestCarrierWithBox = null;
        float closestCarrierDistance = float.MaxValue;

        CrewUnit closestFreeCrew = null;
        float closestFreeDistance = float.MaxValue;

        foreach (CrewUnit unit in allCrew)
        {
            if (!CanActAsCourier(
                    unit,
                    allowWorkingCrew: false))
            {
                continue;
            }

            CrewAmmoCarrier carrier =
                unit.GetComponent<CrewAmmoCarrier>();

            if (carrier == null)
            {
                continue;
            }

            if (carrier.HasAmmoBox)
            {
                Vector3 targetPosition =
                    targetCannon.Room != null
                        ? targetCannon.Room.transform.position
                        : targetCannon.transform.position;

                float distance =
                    (unit.transform.position -
                     targetPosition).sqrMagnitude;

                if (distance < closestCarrierDistance)
                {
                    closestCarrierDistance = distance;
                    closestCarrierWithBox = unit;
                }

                continue;
            }

            ShipRoomRuntime suppliesRoom =
                FindNearestAvailableSuppliesRoom(unit);

            if (suppliesRoom == null)
            {
                continue;
            }

            float suppliesDistance =
                (unit.transform.position -
                 suppliesRoom.transform.position).sqrMagnitude;

            if (suppliesDistance < closestFreeDistance)
            {
                closestFreeDistance = suppliesDistance;
                closestFreeCrew = unit;
            }
        }

        return closestCarrierWithBox != null
            ? closestCarrierWithBox
            : closestFreeCrew;
    }

    private bool CanActAsCourier(
        CrewUnit unit,
        bool allowWorkingCrew)
    {
        if (unit == null ||
            !unit.gameObject.activeInHierarchy ||
            unit.CurrentCell == null ||
            unit.IsMoving ||
            recoveringCrew.Contains(unit) ||
            IsAssignedCourier(unit))
        {
            return false;
        }

        CrewHealth health =
            unit.GetComponent<CrewHealth>();

        if (health != null &&
            (
                health.IsDead ||
                health.CurrentHealth <
                retreatHealthThreshold
            ))
        {
            return false;
        }

        if (unit.GetComponent<CrewAmmoCarrier>() == null)
        {
            return false;
        }

        if (!allowWorkingCrew &&
            unit.CurrentCell.IsControlPoint)
        {
            return false;
        }

        return true;
    }

    private bool SendCourierToCurrentObjective(
        CannonSystemRuntime cannon,
        CrewUnit courier)
    {
        if (cannon == null ||
            cannon.Room == null ||
            courier == null)
        {
            return false;
        }

        CrewAmmoCarrier carrier =
            courier.GetComponent<CrewAmmoCarrier>();

        if (carrier == null)
        {
            return false;
        }

        if (carrier.HasAmmoBox)
        {
            if (IsStandingInRoom(
                    courier,
                    cannon.Room) ||
                IsMovingToRoom(
                    courier,
                    cannon.Room))
            {
                return true;
            }

            courier.AssignRoom(cannon.Room);
            return true;
        }

        ShipRoomRuntime suppliesRoom =
            FindNearestAvailableSuppliesRoom(courier);

        if (suppliesRoom == null)
        {
            Debug.LogWarning(
                $"{courier.name}: не найден доступный отсек " +
                "припасов для получения ящика.",
                courier
            );

            return false;
        }

        if (IsStandingInSupplies(courier) ||
            IsMovingToRoom(courier, suppliesRoom))
        {
            return true;
        }

        courier.AssignRoom(suppliesRoom);
        return true;
    }

    private ShipRoomRuntime FindNearestAvailableSuppliesRoom(
        CrewUnit unit)
    {
        if (unit == null)
        {
            return null;
        }

        ShipRoomRuntime[] allRooms =
            GetComponentsInChildren<ShipRoomRuntime>(true);

        ShipRoomRuntime nearestRoom = null;
        float nearestDistance = float.MaxValue;

        Vector3 unitPosition =
            unit.CurrentCell != null
                ? unit.CurrentCell.transform.position
                : unit.transform.position;

        foreach (ShipRoomRuntime room in allRooms)
        {
            if (room == null)
            {
                continue;
            }

            foreach (ShipCellView cell in room.Cells)
            {
                if (cell == null ||
                    cell.ModuleType != ShipModuleType.Supplies ||
                    !cell.IsAvailableFor(unit))
                {
                    continue;
                }

                float distance =
                    (cell.transform.position -
                     unitPosition).sqrMagnitude;

                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestRoom = room;
            }
        }

        return nearestRoom;
    }

    private static bool IsStandingInSupplies(
        CrewUnit unit)
    {
        return unit != null &&
               !unit.IsMoving &&
               unit.CurrentCell != null &&
               unit.CurrentCell.ModuleType ==
               ShipModuleType.Supplies;
    }

    private static bool IsStandingInRoom(
        CrewUnit unit,
        ShipRoomRuntime room)
    {
        return unit != null &&
               room != null &&
               !unit.IsMoving &&
               unit.CurrentCell != null &&
               unit.CurrentCell.Room == room;
    }

    private static bool IsMovingToRoom(
        CrewUnit unit,
        ShipRoomRuntime room)
    {
        return unit != null &&
               room != null &&
               unit.IsMoving &&
               unit.TargetRoom == room;
    }

    private bool SendCrewAwayFromRoom(
        CrewUnit unit,
        ShipRoomRuntime roomToLeave)
    {
        if (unit == null ||
            roomToLeave == null)
        {
            return false;
        }

        if (unit.IsMoving &&
            unit.TargetRoom != null &&
            unit.TargetRoom != roomToLeave)
        {
            return true;
        }

        ShipRoomRuntime idleRoom =
            FindNearestIdleRoom(
                unit,
                roomToLeave,
                avoidSupplies: true
            );

        if (idleRoom == null)
        {
            idleRoom =
                FindNearestIdleRoom(
                    unit,
                    roomToLeave,
                    avoidSupplies: false
                );
        }

        if (idleRoom == null)
        {
            Debug.LogWarning(
                $"{unit.name}: не найден свободный отсек, " +
                "чтобы покинуть пустую пушку.",
                unit
            );

            return false;
        }

        unit.AssignRoom(idleRoom);

        Debug.Log(
            $"{unit.name} покидает пустую пушку в отсеке " +
            $"{roomToLeave.Id}.",
            unit
        );

        return true;
    }

    private ShipRoomRuntime FindNearestIdleRoom(
        CrewUnit unit,
        ShipRoomRuntime excludedRoom,
        bool avoidSupplies)
    {
        ShipRoomRuntime[] allRooms =
            GetComponentsInChildren<ShipRoomRuntime>(true);

        ShipRoomRuntime nearestRoom = null;
        float nearestDistance = float.MaxValue;

        Vector3 unitPosition =
            unit.CurrentCell != null
                ? unit.CurrentCell.transform.position
                : unit.transform.position;

        foreach (ShipRoomRuntime room in allRooms)
        {
            if (room == null ||
                room == excludedRoom ||
                room.HasSystem)
            {
                continue;
            }

            if (avoidSupplies &&
                RoomContainsSupplies(room))
            {
                continue;
            }

            foreach (ShipCellView cell in room.Cells)
            {
                if (cell == null ||
                    !cell.IsAvailableFor(unit))
                {
                    continue;
                }

                float distance =
                    (cell.transform.position -
                     unitPosition).sqrMagnitude;

                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestRoom = room;
            }
        }

        return nearestRoom;
    }

    private static bool RoomContainsSupplies(
        ShipRoomRuntime room)
    {
        if (room == null)
        {
            return false;
        }

        foreach (ShipCellView cell in room.Cells)
        {
            if (cell != null &&
                cell.ModuleType == ShipModuleType.Supplies)
            {
                return true;
            }
        }

        return false;
    }

    private bool NeedsCannonResupply(
        CannonSystemRuntime cannon)
    {
        if (!TryGetCannonballAmmo(
                cannon,
                out CannonballAmmoDefinition ammo))
        {
            return false;
        }

        int maximum =
            cannon.GetMaximumAmmoCount(ammo);

        if (maximum <= 0)
        {
            return false;
        }

        int current =
            cannon.GetAmmoCount(ammo);

        float ratio =
            (float)current / maximum;

        return ratio <
               Mathf.Clamp01(
                   cannonballResupplyThreshold
               );
    }

    private bool IsCannonballEmpty(
        CannonSystemRuntime cannon)
    {
        return TryGetCannonballAmmo(
                   cannon,
                   out CannonballAmmoDefinition ammo
               ) &&
               cannon.GetAmmoCount(ammo) <= 0;
    }

    private float GetCannonballRatio(
        CannonSystemRuntime cannon)
    {
        int maximum =
            GetCannonballMaximum(cannon);

        if (maximum <= 0)
        {
            return 1f;
        }

        return (float)GetCannonballCount(cannon) /
               maximum;
    }

    private int GetCannonballCount(
        CannonSystemRuntime cannon)
    {
        return TryGetCannonballAmmo(
                   cannon,
                   out CannonballAmmoDefinition ammo
               )
            ? cannon.GetAmmoCount(ammo)
            : 0;
    }

    private int GetCannonballMaximum(
        CannonSystemRuntime cannon)
    {
        return TryGetCannonballAmmo(
                   cannon,
                   out CannonballAmmoDefinition ammo
               )
            ? cannon.GetMaximumAmmoCount(ammo)
            : 0;
    }

    private bool TryGetCannonballAmmo(
        CannonSystemRuntime cannon,
        out CannonballAmmoDefinition ammo)
    {
        ammo = null;

        if (cannon == null)
        {
            return false;
        }

        if (cannonballAmmo != null &&
            cannon.GetMaximumAmmoCount(cannonballAmmo) > 0)
        {
            ammo = cannonballAmmo;
            return true;
        }

        foreach (CannonAmmoDefinition candidate
                 in cannon.AvailableAmmo)
        {
            CannonballAmmoDefinition cannonball =
                candidate as CannonballAmmoDefinition;

            if (cannonball == null)
            {
                continue;
            }

            ammo = cannonball;
            return true;
        }

        return false;
    }

    private List<CannonSystemRuntime> GetCannons()
    {
        CannonSystemRuntime[] foundCannons =
            GetComponentsInChildren<CannonSystemRuntime>(true);

        List<CannonSystemRuntime> cannons =
            new List<CannonSystemRuntime>();

        foreach (CannonSystemRuntime cannon in foundCannons)
        {
            if (cannon != null &&
                cannon.Room != null)
            {
                cannons.Add(cannon);
            }
        }

        return cannons;
    }

    private bool IsAssignedCourier(
        CrewUnit unit)
    {
        if (unit == null)
        {
            return false;
        }

        foreach (CrewUnit courier in cannonCouriers.Values)
        {
            if (courier == unit)
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveCourierAssignment(
        CrewUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        List<CannonSystemRuntime> assignmentsToRemove =
            new List<CannonSystemRuntime>();

        foreach (
            KeyValuePair<CannonSystemRuntime, CrewUnit> pair
            in cannonCouriers)
        {
            if (pair.Value == unit)
            {
                assignmentsToRemove.Add(pair.Key);
            }
        }

        foreach (CannonSystemRuntime cannon
                 in assignmentsToRemove)
        {
            cannonCouriers.Remove(cannon);
        }
    }

    private void AssignAvailableCrew()
    {
        List<ShipRoomRuntime> workRooms =
            GetWorkRooms();

        foreach (ShipRoomRuntime room in workRooms)
        {
            if (!NeedsWorker(room))
            {
                continue;
            }

            CrewUnit replacement =
                FindBestReplacement(room);

            if (replacement == null)
            {
                continue;
            }

            ShipRoomRuntime previousRoom =
                replacement.CurrentCell != null
                    ? replacement.CurrentCell.Room
                    : null;

            replacement.AssignRoom(room);

            bool commandAccepted =
                replacement.TargetRoom == room ||
                (
                    replacement.CurrentCell != null &&
                    replacement.CurrentCell.Room == room &&
                    replacement.CurrentCell ==
                    room.ControlPointCell
                );

            if (!commandAccepted)
            {
                Debug.LogWarning(
                    $"{name}: не удалось отправить " +
                    $"{replacement.name} в отсек {room.Id}.",
                    replacement
                );

                continue;
            }

            string previousPlace =
                previousRoom != null &&
                previousRoom.HasSystem
                    ? GetSystemName(previousRoom)
                    : "свободной позиции";

            Debug.Log(
                $"{name}: {replacement.name} направлен " +
                $"с {previousPlace} на приоритетное место " +
                $"{GetSystemName(room)} в отсеке {room.Id}.",
                replacement
            );
        }
    }

    private CrewUnit FindBestReplacement(
        ShipRoomRuntime targetRoom)
    {
        if (targetRoom == null ||
            targetRoom.ControlPointCell == null)
        {
            return null;
        }

        CrewUnit[] allCrew =
            GetComponentsInChildren<CrewUnit>(true);

        CrewUnit closestFreeCrew = null;
        float closestFreeDistance = float.MaxValue;

        CrewUnit closestLowerPriorityWorker = null;
        float closestWorkerDistance = float.MaxValue;

        int targetPriority =
            GetSystemPriority(
                targetRoom.SystemModule
            );

        Vector3 targetPosition =
            targetRoom.ControlPointCell
                .transform.position;

        foreach (CrewUnit unit in allCrew)
        {
            if (!CanReceiveWorkAssignment(unit))
            {
                continue;
            }

            float distance =
                (
                    unit.transform.position -
                    targetPosition
                ).sqrMagnitude;

            bool isWorking =
                unit.CurrentCell.IsControlPoint &&
                unit.CurrentCell.Room != null &&
                unit.CurrentCell.Room.HasSystem;

            if (!isWorking)
            {
                if (distance < closestFreeDistance)
                {
                    closestFreeDistance = distance;
                    closestFreeCrew = unit;
                }

                continue;
            }

            if (!allowLowerPriorityReassignment)
            {
                continue;
            }

            ShipRoomRuntime currentWorkRoom =
                unit.CurrentCell.Room;

            if (currentWorkRoom == targetRoom)
            {
                continue;
            }

            int currentPriority =
                GetSystemPriority(
                    currentWorkRoom.SystemModule
                );

            if (currentPriority <= targetPriority)
            {
                continue;
            }

            if (distance < closestWorkerDistance)
            {
                closestWorkerDistance = distance;
                closestLowerPriorityWorker = unit;
            }
        }

        return closestFreeCrew != null
            ? closestFreeCrew
            : closestLowerPriorityWorker;
    }

    private bool CanReceiveWorkAssignment(
        CrewUnit unit)
    {
        if (unit == null ||
            !unit.gameObject.activeInHierarchy ||
            unit.CurrentCell == null ||
            unit.IsMoving ||
            recoveringCrew.Contains(unit) ||
            IsAssignedCourier(unit))
        {
            return false;
        }

        CrewHealth health =
            unit.GetComponent<CrewHealth>();

        if (health != null)
        {
            if (health.IsDead ||
                health.CurrentHealth <
                retreatHealthThreshold)
            {
                return false;
            }
        }

        CrewAmmoCarrier carrier =
            unit.GetComponent<CrewAmmoCarrier>();

        /*
         * Пират с ящиком не занимает рабочее место.
         * Сначала он должен доставить боеприпасы.
         */
        if (carrier != null &&
            carrier.HasAmmoBox)
        {
            return false;
        }

        return true;
    }

    private List<ShipRoomRuntime> GetWorkRooms()
    {
        ShipRoomRuntime[] allRooms =
            GetComponentsInChildren<ShipRoomRuntime>(true);

        List<ShipRoomRuntime> workRooms =
            new List<ShipRoomRuntime>();

        foreach (ShipRoomRuntime room in allRooms)
        {
            if (room == null ||
                !room.HasSystem ||
                room.ControlPointCell == null)
            {
                continue;
            }

            workRooms.Add(room);
        }

        workRooms.Sort(
            (first, second) =>
            {
                int priorityComparison =
                    GetSystemPriority(first.SystemModule)
                    .CompareTo(
                        GetSystemPriority(second.SystemModule)
                    );

                if (priorityComparison != 0)
                {
                    return priorityComparison;
                }

                return first.Id.CompareTo(second.Id);
            }
        );

        return workRooms;
    }

    private bool NeedsWorker(
        ShipRoomRuntime room)
    {
        if (room == null ||
            !room.HasSystem ||
            room.ControlPointCell == null)
        {
            return false;
        }

        /*
         * Пустая пушка не получает нового оператора.
         * Сначала к ней должен быть доставлен ящик.
         */
        if (room.SystemModule == ShipModuleType.Cannon)
        {
            CannonSystemRuntime cannon =
                room.GetComponent<CannonSystemRuntime>();

            if (cannon == null ||
                IsCannonballEmpty(cannon))
            {
                return false;
            }
        }

        if (room.Operator != null)
        {
            return false;
        }

        ShipCellView controlPoint =
            room.ControlPointCell;

        if (controlPoint.Occupant != null ||
            controlPoint.ReservedBy != null)
        {
            return false;
        }

        return true;
    }

    private static int GetSystemPriority(
        ShipModuleType module)
    {
        switch (module)
        {
            case ShipModuleType.Cannon:
                return 0;

            case ShipModuleType.Rudder:
                return 1;

            case ShipModuleType.Sails:
                return 2;

            default:
                return 100;
        }
    }

    private static string GetSystemName(
        ShipRoomRuntime room)
    {
        if (room == null)
        {
            return "неизвестной системы";
        }

        switch (room.SystemModule)
        {
            case ShipModuleType.Cannon:
                return "пушки";

            case ShipModuleType.Rudder:
                return "руля";

            case ShipModuleType.Sails:
                return "парусов";

            default:
                return room.SystemModule.ToString();
        }
    }

    private void OnValidate()
    {
        initializationFrames =
            Mathf.Max(1, initializationFrames);

        reassignmentInterval =
            Mathf.Max(0.1f, reassignmentInterval);

        retreatHealthThreshold =
            Mathf.Max(1, retreatHealthThreshold);

        cannonballResupplyThreshold =
            Mathf.Clamp01(
                cannonballResupplyThreshold
            );
    }
}