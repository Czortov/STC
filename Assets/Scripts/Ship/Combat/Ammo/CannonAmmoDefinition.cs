using System.Collections.Generic;
using UnityEngine;

public enum AmmoImpactType
{
    Direct,
    Area,
    Fire
}

public abstract class CannonAmmoDefinition : ScriptableObject
{
    [Header("Information")]
    [SerializeField] private string displayName = "Боеприпас";

    [Header("Interface")]
    [Tooltip("Иконка боеприпаса для интерфейса выбранной пушки.")]
    [SerializeField] private Sprite icon;

    [Header("Projectile")]
    [Tooltip("Скорость полёта снаряда.")]
    [Min(0.1f)]
    [SerializeField] private float projectileSpeed = 6f;

    [Tooltip("Максимальное время существования снаряда.")]
    [Min(0.1f)]
    [SerializeField] private float projectileLifetime = 5f;

    [Tooltip("Визуальный размер снаряда.")]
    [Min(0.02f)]
    [SerializeField] private float projectileSize = 0.18f;

    [Tooltip("Высота параболической дуги полёта в мировых единицах.")]
    [Min(0f)]
    [SerializeField] private float projectileArcHeight = 8f;

    [Tooltip("Цвет снаряда.")]
    [SerializeField] private Color projectileColor = Color.black;

    [Header("Ammunition")]
    [Tooltip("Multiplier applied to the cannon base reload duration for this ammo type.")]
    [Min(0.01f)]
    [SerializeField] private float reloadDurationMultiplier = 1f;
    [Tooltip("Максимальное количество снарядов этого типа, которое помещается в одной пушке.")]
    [Min(1)]
    [SerializeField] private int maxAmmoPerCannon = 6;

    [Header("Damage")]
    [Tooltip("Урон общему здоровью корабля.")]
    [Min(0)]
    [SerializeField] private int hullDamage = 10;

    [Tooltip("Урон прочности выбранного отсека.")]
    [Min(0)]
    [SerializeField] private int roomDamage = 20;

    [Tooltip("Множитель урона прочности отсека парусов.")]
    [Min(0f)]
    [SerializeField] private float sailsDamageMultiplier = 1f;

    [Tooltip("Урон каждому члену экипажа внутри выбранного отсека.")]
    [Min(0)]
    [SerializeField] private int crewDamage = 25;

    [Header("Impact")]
    [SerializeField] private AmmoImpactType impactType =
        AmmoImpactType.Direct;

    [Tooltip("Radius of an area impact in world units.")]
    [Min(0f)]
    [SerializeField] private float areaRadius;

    [Tooltip("Damage dealt once to every room in the area.")]
    [Min(0)]
    [SerializeField] private int areaRoomDamage;

    [Tooltip("Damage dealt once to every crew unit in the area.")]
    [Min(0)]
    [SerializeField] private int areaCrewDamage;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    public Sprite Icon => icon;
    public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
    public float ProjectileLifetime => Mathf.Max(0.1f, projectileLifetime);
    public float ProjectileSize => Mathf.Max(0.02f, projectileSize);
    public float ProjectileArcHeight => Mathf.Max(0f, projectileArcHeight);
    public Color ProjectileColor => projectileColor;
    public float ReloadDurationMultiplier =>
        Mathf.Max(0.01f, reloadDurationMultiplier);
    public int MaxAmmoPerCannon => Mathf.Max(1, maxAmmoPerCannon);
    public int HullDamage => Mathf.Max(0, hullDamage);
    public int RoomDamage => Mathf.Max(0, roomDamage);
    public float SailsDamageMultiplier =>
        Mathf.Max(0f, sailsDamageMultiplier);
    public int CrewDamage => Mathf.Max(0, crewDamage);
    public AmmoImpactType ImpactType => impactType;
    public float AreaRadius => Mathf.Max(0f, areaRadius);
    public int AreaRoomDamage => Mathf.Max(0, areaRoomDamage);
    public int AreaCrewDamage => Mathf.Max(0, areaCrewDamage);

    public void ApplyImpact(ShipRoomRuntime targetRoom)
    {
        ApplyImpact(
            targetRoom,
            GetRoomWorldCenter(targetRoom)
        );
    }

    public void ApplyImpact(
        ShipRoomRuntime targetRoom,
        Vector2 impactPosition)
    {
        if (targetRoom == null)
        {
            Debug.LogError($"{DisplayName}: целевой отсек отсутствует.", this);
            return;
        }

        switch (ImpactType)
        {
            case AmmoImpactType.Direct:
                ApplyDirectImpact(targetRoom);
                break;

            case AmmoImpactType.Area:
                ApplyAreaImpact(impactPosition);
                break;

            default:
                Debug.LogError(
                    $"{DisplayName}: impact type {ImpactType} is not implemented.",
                    this
                );
                break;
        }
    }

    private void ApplyDirectImpact(ShipRoomRuntime targetRoom)
    {

        int appliedRoomDamage =
            ApplyRoomDamage(targetRoom);
        ApplyHullDamage(targetRoom);

        int damagedCrewCount = ApplyCrewDamage(targetRoom);

        ApplyAdditionalEffect(targetRoom);

        Debug.Log(
            $"{DisplayName} попал в отсек {targetRoom.Id}. " +
            $"Урон отсеку: {appliedRoomDamage}. " +
            $"Урон корпусу: {HullDamage}. " +
            $"Урон экипажу: {CrewDamage}. " +
            $"Пострадавших: {damagedCrewCount}.",
            targetRoom
        );
    }

    private void ApplyAreaImpact(Vector2 impactPosition)
    {
        Collider2D[] hitColliders =
            Physics2D.OverlapCircleAll(
                impactPosition,
                AreaRadius
            );

        HashSet<ShipRoomRuntime> damagedRooms =
            new HashSet<ShipRoomRuntime>();

        HashSet<CrewHealth> damagedCrew =
            new HashSet<CrewHealth>();

        foreach (Collider2D hitCollider in hitColliders)
        {
            if (hitCollider == null)
            {
                continue;
            }

            ShipRoomRuntime room =
                hitCollider.GetComponentInParent<ShipRoomRuntime>();

            if (room != null)
            {
                damagedRooms.Add(room);
            }

            CrewHealth crewHealth =
                hitCollider.GetComponentInParent<CrewHealth>();

            if (crewHealth != null && !crewHealth.IsDead)
            {
                damagedCrew.Add(crewHealth);
            }
        }

        if (AreaRoomDamage > 0)
        {
            foreach (ShipRoomRuntime room in damagedRooms)
            {
                room.TakeDamage(AreaRoomDamage);
            }
        }

        if (AreaCrewDamage > 0)
        {
            foreach (CrewHealth crewHealth in damagedCrew)
            {
                crewHealth.TakeDamage(AreaCrewDamage);
            }
        }

        Debug.Log(
            $"{DisplayName} affected an area at {impactPosition}. " +
            $"Rooms hit: {damagedRooms.Count}; " +
            $"crew hit: {damagedCrew.Count}.",
            this
        );
    }

    private static Vector2 GetRoomWorldCenter(
        ShipRoomRuntime room)
    {
        if (room == null)
        {
            return Vector2.zero;
        }

        Collider2D roomCollider =
            room.GetComponent<Collider2D>();

        return roomCollider != null && roomCollider.enabled
            ? roomCollider.bounds.center
            : room.transform.position;
    }

    private int ApplyRoomDamage(ShipRoomRuntime targetRoom)
    {
        int damage = RoomDamage;

        if (targetRoom.SystemModule == ShipModuleType.Sails)
        {
            damage = Mathf.RoundToInt(
                damage * SailsDamageMultiplier
            );
        }

        if (damage > 0)
        {
            targetRoom.TakeDamage(damage);
        }

        return damage;
    }

    private void ApplyHullDamage(ShipRoomRuntime targetRoom)
    {
        if (HullDamage <= 0)
        {
            return;
        }

        ShipHullHealth shipHull =
            targetRoom.GetComponentInParent<ShipHullHealth>();

        if (shipHull == null)
        {
            Debug.LogWarning(
                $"{DisplayName}: на корабле отсека {targetRoom.Id} не найден ShipHullHealth.",
                targetRoom
            );
            return;
        }

        shipHull.TakeDamage(HullDamage);
    }

    private int ApplyCrewDamage(ShipRoomRuntime targetRoom)
    {
        if (CrewDamage <= 0)
        {
            return 0;
        }

        HashSet<CrewHealth> damagedCrew = new HashSet<CrewHealth>();

        foreach (ShipCellView cell in targetRoom.Cells)
        {
            if (cell == null || cell.Occupant == null)
            {
                continue;
            }

            CrewHealth crewHealth =
                cell.Occupant.GetComponent<CrewHealth>();

            if (crewHealth == null ||
                crewHealth.IsDead ||
                !damagedCrew.Add(crewHealth))
            {
                continue;
            }

            crewHealth.TakeDamage(CrewDamage);
        }

        return damagedCrew.Count;
    }

    protected virtual void ApplyAdditionalEffect(ShipRoomRuntime targetRoom)
    {
    }

    private void OnValidate()
    {
        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
        projectileSize = Mathf.Max(0.02f, projectileSize);
        projectileArcHeight = Mathf.Max(0f, projectileArcHeight);
        reloadDurationMultiplier =
            Mathf.Max(0.01f, reloadDurationMultiplier);
        maxAmmoPerCannon = Mathf.Max(1, maxAmmoPerCannon);
        hullDamage = Mathf.Max(0, hullDamage);
        roomDamage = Mathf.Max(0, roomDamage);
        sailsDamageMultiplier =
            Mathf.Max(0f, sailsDamageMultiplier);
        crewDamage = Mathf.Max(0, crewDamage);
        areaRadius = Mathf.Max(0f, areaRadius);
        areaRoomDamage = Mathf.Max(0, areaRoomDamage);
        areaCrewDamage = Mathf.Max(0, areaCrewDamage);
    }
}
