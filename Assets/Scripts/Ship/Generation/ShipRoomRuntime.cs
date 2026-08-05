using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class ShipRoomRuntime : MonoBehaviour
{
    private readonly List<ShipCellView> cells =
        new List<ShipCellView>();

    private BoxCollider2D roomCollider;
    private RoomHealthBarView healthBarView;

    private Vector2Int controlPointCoordinates;

    public int Id { get; private set; }
    public int DeckIndex { get; private set; }

    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }

    public bool HasControlPoint { get; private set; }

    public bool IsDestroyed =>
        CurrentHealth <= 0;

    public bool IsDamaged =>
        CurrentHealth < MaxHealth;

    public bool IsFullyRepaired =>
        CurrentHealth >= MaxHealth;

    public ShipCellView ControlPointCell { get; private set; }

    public CrewUnit Operator { get; private set; }

    public IReadOnlyList<ShipCellView> Cells => cells;

    public ShipModuleType SystemModule
    {
        get
        {
            if (ControlPointCell == null)
            {
                return ShipModuleType.None;
            }

            return ControlPointCell.ModuleType;
        }
    }

    public bool HasSystem =>
        HasControlPoint &&
        ControlPointCell != null &&
        SystemModule != ShipModuleType.None;

    /*
     * Система работает только при полной прочности.
     * Даже 99 из 100 здоровья недостаточно.
     */
    public bool IsOperational =>
        HasSystem &&
        Operator != null &&
        IsFullyRepaired;

    public void Initialize(
        ShipRoomBlueprint blueprint,
        int maxHealth)
    {
        if (blueprint == null)
        {
            Debug.LogError(
                "ShipRoomRuntime получил пустой blueprint.",
                this
            );

            return;
        }

        Id = blueprint.Id;
        DeckIndex = blueprint.DeckIndex;

        MaxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = MaxHealth;

        HasControlPoint =
            blueprint.HasControlPoint;

        controlPointCoordinates =
            blueprint.ControlPointCoordinates;

        roomCollider =
            GetComponent<BoxCollider2D>();

        roomCollider.isTrigger = true;

        EnsureHealthBar();
    }

    public void RegisterCell(ShipCellView cell)
    {
        if (cell == null || cells.Contains(cell))
        {
            return;
        }

        cells.Add(cell);

        if (HasControlPoint &&
            cell.Coordinates == controlPointCoordinates)
        {
            ControlPointCell = cell;

            EnsureRuntimeSystem();
        }
    }

    private void EnsureRuntimeSystem()
    {
        switch (SystemModule)
        {
            case ShipModuleType.Cannon:
                if (GetComponent<CannonSystemRuntime>() == null)
                {
                    gameObject.AddComponent<CannonSystemRuntime>();
                }

                break;
        }
    }

    private void EnsureHealthBar()
    {
        healthBarView =
            GetComponent<RoomHealthBarView>();

        if (healthBarView == null)
        {
            healthBarView =
                gameObject.AddComponent<RoomHealthBarView>();
        }

        healthBarView.Initialize(this);
    }

    public bool TryReserveDestination(
        CrewUnit unit,
        out ShipCellView destination)
    {
        destination = null;

        if (unit == null)
        {
            return false;
        }

        /*
         * Разрушенные отсеки тоже доступны.
         * Иначе экипаж не сможет зайти внутрь и починить их.
         */
        if (ControlPointCell != null &&
            ControlPointCell.IsAvailableFor(unit) &&
            ControlPointCell.TryReserve(unit))
        {
            destination = ControlPointCell;
            return true;
        }

        Vector3 referencePosition =
            unit.CurrentCell != null
                ? unit.CurrentCell.transform.position
                : unit.transform.position;

        float bestDistance = float.MaxValue;

        foreach (ShipCellView cell in cells)
        {
            if (cell == null ||
                cell == ControlPointCell ||
                !cell.IsAvailableFor(unit))
            {
                continue;
            }

            float distance =
                (cell.transform.position -
                 referencePosition).sqrMagnitude;

            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            destination = cell;
        }

        if (destination == null)
        {
            return false;
        }

        if (!destination.TryReserve(unit))
        {
            destination = null;
            return false;
        }

        return true;
    }

    public bool TryActivateSystem(
        CrewUnit unit,
        ShipCellView occupiedCell)
    {
        if (unit == null || occupiedCell == null)
        {
            return false;
        }

        if (!HasSystem)
        {
            return false;
        }

        if (!IsFullyRepaired)
        {
            Debug.LogWarning(
                $"Отсек {Id} повреждён. " +
                $"Система {GetSystemName()} станет доступна " +
                $"только после полного ремонта. " +
                $"Прочность: {CurrentHealth}/{MaxHealth}.",
                this
            );

            return false;
        }

        if (occupiedCell != ControlPointCell)
        {
            Debug.LogWarning(
                $"{unit.name} находится не в пункте " +
                $"управления отсека {Id}.",
                unit
            );

            return false;
        }

        if (occupiedCell.Occupant != unit)
        {
            Debug.LogWarning(
                $"{unit.name} не занимает пункт " +
                $"управления отсека {Id}.",
                unit
            );

            return false;
        }

        if (Operator != null && Operator != unit)
        {
            Debug.LogWarning(
                $"Системой {GetSystemName()} уже управляет " +
                $"{Operator.name}.",
                this
            );

            return false;
        }

        if (Operator == unit)
        {
            return true;
        }

        Operator = unit;

        Debug.Log(
            $"{unit.name} начал управлять системой " +
            $"{GetSystemName()} в отсеке {Id}.",
            this
        );

        return true;
    }

    public void DeactivateSystem(CrewUnit unit)
    {
        if (unit == null || Operator != unit)
        {
            return;
        }

        Operator = null;

        Debug.Log(
            $"{unit.name} покинул систему " +
            $"{GetSystemName()} в отсеке {Id}.",
            this
        );
    }

    public void ConfigureClickArea(
        Vector2 localCenter,
        Vector2 size)
    {
        if (roomCollider == null)
        {
            roomCollider =
                GetComponent<BoxCollider2D>();
        }

        roomCollider.offset = localCenter;
        roomCollider.size = size;
        roomCollider.isTrigger = true;

        healthBarView?.Refresh();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDestroyed)
        {
            return;
        }

        CrewUnit previousOperator = Operator;

        CurrentHealth = Mathf.Max(
            0,
            CurrentHealth - damage
        );

        /*
         * Любое повреждение сразу отключает систему.
         * Член экипажа остаётся в клетке и начинает ремонт.
         */
        if (previousOperator != null)
        {
            Operator = null;

            Debug.Log(
                $"Система {GetSystemName()} в отсеке {Id} " +
                $"отключена из-за повреждения. " +
                $"Для запуска требуется полный ремонт.",
                this
            );
        }

        healthBarView?.Refresh();

        Debug.Log(
            $"Отсек {Id} получил {damage} урона. " +
            $"Прочность: {CurrentHealth}/{MaxHealth}.",
            this
        );
    }

    public void Repair(int amount)
    {
        if (amount <= 0 || !IsDamaged)
        {
            return;
        }

        bool wasDamaged = IsDamaged;

        CurrentHealth = Mathf.Min(
            MaxHealth,
            CurrentHealth + amount
        );

        healthBarView?.Refresh();

        if (wasDamaged && IsFullyRepaired)
        {
            Debug.Log(
                $"Отсек {Id} полностью отремонтирован. " +
                $"Прочность: {CurrentHealth}/{MaxHealth}.",
                this
            );

            /*
             * Если экипаж уже стоит у пункта управления,
             * после полного ремонта система включится автоматически.
             */
            if (ControlPointCell != null &&
                ControlPointCell.Occupant != null)
            {
                TryActivateSystem(
                    ControlPointCell.Occupant,
                    ControlPointCell
                );
            }
        }
    }

    [ContextMenu("Test/Take 20 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(20);
    }

    [ContextMenu("Test/Repair 20")]
    private void TestRepair()
    {
        Repair(20);
    }

    private string GetSystemName()
    {
        switch (SystemModule)
        {
            case ShipModuleType.Rudder:
                return "Руль";

            case ShipModuleType.Cannon:
                return "Пушка";

            case ShipModuleType.Sails:
                return "Паруса";

            default:
                return "Неизвестная система";
        }
    }
}