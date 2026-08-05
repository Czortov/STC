using System.Collections.Generic;
using UnityEngine;

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

    [Tooltip("Цвет снаряда.")]
    [SerializeField] private Color projectileColor = Color.black;

    [Header("Ammunition")]
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

    [Tooltip("Урон каждому члену экипажа внутри выбранного отсека.")]
    [Min(0)]
    [SerializeField] private int crewDamage = 25;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    public Sprite Icon => icon;
    public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
    public float ProjectileLifetime => Mathf.Max(0.1f, projectileLifetime);
    public float ProjectileSize => Mathf.Max(0.02f, projectileSize);
    public Color ProjectileColor => projectileColor;
    public int MaxAmmoPerCannon => Mathf.Max(1, maxAmmoPerCannon);
    public int HullDamage => Mathf.Max(0, hullDamage);
    public int RoomDamage => Mathf.Max(0, roomDamage);
    public int CrewDamage => Mathf.Max(0, crewDamage);

    public void ApplyImpact(ShipRoomRuntime targetRoom)
    {
        if (targetRoom == null)
        {
            Debug.LogError($"{DisplayName}: целевой отсек отсутствует.", this);
            return;
        }

        ApplyRoomDamage(targetRoom);
        ApplyHullDamage(targetRoom);

        int damagedCrewCount = ApplyCrewDamage(targetRoom);

        ApplyAdditionalEffect(targetRoom);

        Debug.Log(
            $"{DisplayName} попал в отсек {targetRoom.Id}. " +
            $"Урон отсеку: {RoomDamage}. " +
            $"Урон корпусу: {HullDamage}. " +
            $"Урон экипажу: {CrewDamage}. " +
            $"Пострадавших: {damagedCrewCount}.",
            targetRoom
        );
    }

    private void ApplyRoomDamage(ShipRoomRuntime targetRoom)
    {
        if (RoomDamage > 0)
        {
            targetRoom.TakeDamage(RoomDamage);
        }
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
        maxAmmoPerCannon = Mathf.Max(1, maxAmmoPerCannon);
        hullDamage = Mathf.Max(0, hullDamage);
        roomDamage = Mathf.Max(0, roomDamage);
        crewDamage = Mathf.Max(0, crewDamage);
    }
}