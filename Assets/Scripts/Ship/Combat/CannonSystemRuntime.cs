using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ShipRoomRuntime))]
public sealed class CannonSystemRuntime : MonoBehaviour
{
    public static event Action<CannonSystemRuntime> CannonRegistered;
    public static event Action<CannonSystemRuntime> CannonUnregistered;

    public event Action<CannonSystemRuntime> StateChanged;

    private static readonly List<CannonSystemRuntime> allCannons =
        new List<CannonSystemRuntime>();

    public static IReadOnlyList<CannonSystemRuntime> AllCannons =>
        allCannons;

    [Header("Reload")]

    [Tooltip("Base reload duration before applying the selected ammo multiplier.")]
    [Min(0.01f)]
    [SerializeField] private float baseReloadDuration = 5f;

    [SerializeField] private bool startLoaded = true;

    [Header("Ammo")]

    [Tooltip(
        "Начальный выбранный боеприпас. " +
        "Если не указан, берётся Default Ammo корабля.")]
    [SerializeField]
    private CannonAmmoDefinition loadedAmmo;

    private readonly List<CannonAmmoDefinition> availableAmmo =
        new List<CannonAmmoDefinition>();

    private readonly Dictionary<CannonAmmoDefinition, int>
        ammoCounts =
            new Dictionary<CannonAmmoDefinition, int>();

    private ShipRoomRuntime room;
    private ShipHullHealth shipHullHealth;

    private float reloadProgress;
    private bool isLoaded;

    public ShipRoomRuntime Room =>
        room;

    public CannonAmmoDefinition LoadedAmmo =>
        loadedAmmo;

    public IReadOnlyList<CannonAmmoDefinition> AvailableAmmo =>
        availableAmmo;

    public bool IsLoaded =>
        isLoaded;

    public int CurrentAmmoCount =>
        GetAmmoCount(loadedAmmo);

    public int CurrentAmmoMaximum =>
        loadedAmmo != null
            ? loadedAmmo.MaxAmmoPerCannon
            : 0;

    public bool HasCurrentAmmo =>
        loadedAmmo != null &&
        CurrentAmmoCount > 0;

    public bool CanFire =>
        room != null &&
        room.IsOperational &&
        loadedAmmo != null &&
        isLoaded &&
        CurrentAmmoCount > 0 &&
        (
            shipHullHealth == null ||
            !shipHullHealth.IsDestroyed
        );

    public float CurrentReloadDuration
    {
        get
        {
            float multiplier =
                loadedAmmo != null
                    ? loadedAmmo.ReloadDurationMultiplier
                    : 1f;

            return Mathf.Max(0.01f, baseReloadDuration) *
                   Mathf.Max(0.01f, multiplier);
        }
    }

    public float ReloadProgress01
    {
        get
        {
            if (isLoaded)
            {
                return 1f;
            }

            if (!HasCurrentAmmo)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                reloadProgress /
                CurrentReloadDuration
            );
        }
    }

    public float ReloadRemaining
    {
        get
        {
            if (isLoaded ||
                !HasCurrentAmmo)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                CurrentReloadDuration - reloadProgress
            );
        }
    }

    private void Awake()
    {
        room = GetComponent<ShipRoomRuntime>();

        shipHullHealth =
            GetComponentInParent<ShipHullHealth>();

        InitializeAmmoStorage();
        ResolveStartingAmmo();

        isLoaded =
            startLoaded &&
            loadedAmmo != null &&
            GetAmmoCount(loadedAmmo) > 0;

        reloadProgress =
            isLoaded
                ? CurrentReloadDuration
                : 0f;
    }

    private void OnEnable()
    {
        if (!allCannons.Contains(this))
        {
            allCannons.Add(this);
            CannonRegistered?.Invoke(this);
        }
    }

    private void OnDisable()
    {
        if (allCannons.Remove(this))
        {
            CannonUnregistered?.Invoke(this);
        }
    }

    private void Update()
    {
        if (room == null ||
            !room.IsOperational ||
            isLoaded ||
            loadedAmmo == null ||
            GetAmmoCount(loadedAmmo) <= 0)
        {
            return;
        }

        if (shipHullHealth != null &&
            shipHullHealth.IsDestroyed)
        {
            return;
        }

        reloadProgress += Time.deltaTime;

        if (reloadProgress < CurrentReloadDuration)
        {
            return;
        }

        reloadProgress = CurrentReloadDuration;
        isLoaded = true;
        StateChanged?.Invoke(this);

        Debug.Log(
            $"Пушка в отсеке {room.Id} зарядила " +
            $"{loadedAmmo.DisplayName}. " +
            $"Запас: {CurrentAmmoCount}/{CurrentAmmoMaximum}.",
            this
        );
    }

    public bool TryFire(
        ShipRoomRuntime targetRoom)
    {
        if (room == null)
        {
            Debug.LogError(
                "CannonSystemRuntime не нашёл ShipRoomRuntime.",
                this
            );

            return false;
        }

        if (targetRoom == null)
        {
            Debug.LogWarning(
                $"Для пушки в отсеке {room.Id} не выбрана цель.",
                this
            );

            return false;
        }

        if (shipHullHealth != null &&
            shipHullHealth.IsDestroyed)
        {
            Debug.LogWarning(
                $"Пушка в отсеке {room.Id} не может стрелять: " +
                "корабль уничтожен.",
                this
            );

            return false;
        }

        if (IsSameShip(targetRoom))
        {
            Debug.LogWarning(
                "Нельзя стрелять по собственному кораблю.",
                this
            );

            return false;
        }

        if (!room.IsOperational)
        {
            Debug.LogWarning(
                $"Пушка в отсеке {room.Id} не может стрелять: " +
                "нет оператора или отсек повреждён.",
                this
            );

            return false;
        }

        if (loadedAmmo == null)
        {
            Debug.LogWarning(
                $"В пушке отсека {room.Id} " +
                "не выбран тип боеприпаса.",
                this
            );

            return false;
        }

        if (GetAmmoCount(loadedAmmo) <= 0)
        {
            Debug.Log(
                $"В пушке отсека {room.Id} закончились снаряды " +
                $"типа {loadedAmmo.DisplayName}.",
                this
            );

            return false;
        }

        if (!isLoaded)
        {
            Debug.Log(
                $"Пушка в отсеке {room.Id} перезаряжается. " +
                $"Осталось {ReloadRemaining:0.0} сек.",
                this
            );

            return false;
        }

        if (room.ControlPointCell == null)
        {
            Debug.LogError(
                $"У пушки в отсеке {room.Id} " +
                "не найден пункт управления.",
                this
            );

            return false;
        }

        Vector3 cannonPosition =
            room.ControlPointCell.transform.position;

        Vector3 targetPosition =
            GetRoomWorldCenter(targetRoom);

        Vector2 fireDirection =
            targetPosition - cannonPosition;

        if (fireDirection.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning(
                "Пушка и цель находятся в одной точке.",
                this
            );

            return false;
        }

        fireDirection.Normalize();

        Vector3 muzzlePosition =
            cannonPosition +
            (Vector3)(
                fireDirection *
                room.ControlPointCell.CellSize *
                0.65f
            );

        CannonProjectile projectile =
            CannonProjectile.Create(
                muzzlePosition,
                targetRoom,
                loadedAmmo
            );

        if (projectile == null)
        {
            Debug.LogError(
                $"Пушка в отсеке {room.Id} " +
                "не смогла создать снаряд.",
                this
            );

            return false;
        }

        ConsumeAmmo(loadedAmmo, 1);

        isLoaded = false;
        reloadProgress = 0f;
        StateChanged?.Invoke(this);

        Debug.Log(
            $"Пушка в отсеке {room.Id} выстрелила " +
            $"{loadedAmmo.DisplayName} по отсеку {targetRoom.Id}. " +
            $"Осталось: {CurrentAmmoCount}/{CurrentAmmoMaximum}.",
            this
        );

        if (CurrentAmmoCount <= 0)
        {
            Debug.Log(
                $"В пушке отсека {room.Id} закончились " +
                $"{loadedAmmo.DisplayName}.",
                this
            );
        }

        return true;
    }

    public bool SetAmmo(
        CannonAmmoDefinition newAmmo)
    {
        if (newAmmo == null)
        {
            Debug.LogWarning(
                $"Пушка в отсеке {room?.Id}: " +
                "передан пустой боеприпас.",
                this
            );

            return false;
        }

        if (!ammoCounts.ContainsKey(newAmmo))
        {
            Debug.LogWarning(
                $"Боеприпас {newAmmo.DisplayName} " +
                $"недоступен для пушки в отсеке {room?.Id}.",
                this
            );

            return false;
        }

        if (loadedAmmo == newAmmo)
        {
            return true;
        }

        loadedAmmo = newAmmo;

        // После смены типа боеприпаса
        // пушку необходимо зарядить заново.
        isLoaded = false;
        reloadProgress = 0f;
        StateChanged?.Invoke(this);

        Debug.Log(
            $"Пушка в отсеке {room?.Id} выбрала " +
            $"{loadedAmmo.DisplayName}. " +
            $"Запас: {CurrentAmmoCount}/{CurrentAmmoMaximum}.",
            this
        );

        return true;
    }

    public int GetAmmoCount(
        CannonAmmoDefinition ammo)
    {
        if (ammo == null)
        {
            return 0;
        }

        return ammoCounts.TryGetValue(
            ammo,
            out int count)
                ? count
                : 0;
    }

    public int GetMaximumAmmoCount(
        CannonAmmoDefinition ammo)
    {
        if (ammo == null ||
            !ammoCounts.ContainsKey(ammo))
        {
            return 0;
        }

        return ammo.MaxAmmoPerCannon;
    }

    public bool HasAmmo(
        CannonAmmoDefinition ammo)
    {
        return GetAmmoCount(ammo) > 0;
    }

    public bool IsAmmoFull
    {
        get
        {
            if (availableAmmo.Count == 0)
            {
                return false;
            }

            foreach (CannonAmmoDefinition ammo
                    in availableAmmo)
            {
                if (ammo == null)
                {
                    continue;
                }

                if (GetAmmoCount(ammo) <
                    ammo.MaxAmmoPerCannon)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public bool RefillAllAmmo()
    {
        if (availableAmmo.Count == 0)
        {
            Debug.LogWarning(
                $"Пушка в отсеке {room?.Id} не имеет " +
                "доступных типов боеприпасов.",
                this
            );

            return false;
        }

        bool refilledAnything = false;

        foreach (CannonAmmoDefinition ammo
                in availableAmmo)
        {
            if (ammo == null)
            {
                continue;
            }

            int maximum =
                ammo.MaxAmmoPerCannon;

            int current =
                GetAmmoCount(ammo);

            if (current >= maximum)
            {
                continue;
            }

            ammoCounts[ammo] = maximum;
            refilledAnything = true;
        }

        if (!refilledAnything)
        {
            Debug.Log(
                $"Боезапас пушки в отсеке {room?.Id} " +
                "уже полностью заполнен.",
                this
            );

            return false;
        }

        StateChanged?.Invoke(this);

        Debug.Log(
            $"Пушка в отсеке {room?.Id} полностью пополнена.",
            this
        );

        return true;
    }

    // Совместимость со старым тестовым вызовом.
    public bool TryFire()
    {
        Debug.LogWarning(
            $"Пушка в отсеке {room?.Id} не выстрелила: " +
            "вызов выполнен без выбранной цели.",
            this
        );

        return false;
    }

    private void InitializeAmmoStorage()
    {
        availableAmmo.Clear();
        ammoCounts.Clear();

        ShipAmmoLoadout loadout =
            GetComponentInParent<ShipAmmoLoadout>();

        if (loadout != null)
        {
            foreach (CannonAmmoDefinition ammo
                     in loadout.AvailableAmmo)
            {
                AddAmmoType(ammo);
            }

            AddAmmoType(loadout.DefaultAmmo);
        }

        // Сохраняет совместимость, если боеприпас
        // был назначен непосредственно на компоненте пушки.
        AddAmmoType(loadedAmmo);
    }

    private void AddAmmoType(
        CannonAmmoDefinition ammo)
    {
        if (ammo == null ||
            ammoCounts.ContainsKey(ammo))
        {
            return;
        }

        availableAmmo.Add(ammo);

        ammoCounts.Add(
            ammo,
            ammo.MaxAmmoPerCannon
        );
    }

    private void ResolveStartingAmmo()
    {
        if (loadedAmmo != null &&
            ammoCounts.ContainsKey(loadedAmmo))
        {
            return;
        }

        ShipAmmoLoadout loadout =
            GetComponentInParent<ShipAmmoLoadout>();

        if (loadout != null &&
            loadout.DefaultAmmo != null &&
            ammoCounts.ContainsKey(loadout.DefaultAmmo))
        {
            loadedAmmo = loadout.DefaultAmmo;
            return;
        }

        loadedAmmo =
            availableAmmo.Count > 0
                ? availableAmmo[0]
                : null;

        if (loadedAmmo == null)
        {
            Debug.LogError(
                $"Пушка в отсеке {room?.Id} не получила " +
                "ни одного типа боеприпасов. " +
                "Проверь ShipAmmoLoadout на корабле.",
                this
            );
        }
    }

    private void ConsumeAmmo(
        CannonAmmoDefinition ammo,
        int amount)
    {
        if (ammo == null ||
            amount <= 0 ||
            !ammoCounts.TryGetValue(
                ammo,
                out int currentCount))
        {
            return;
        }

        ammoCounts[ammo] =
            Mathf.Max(
                0,
                currentCount - amount
            );
    }

    private bool IsSameShip(
        ShipRoomRuntime targetRoom)
    {
        ShipIdentity sourceShip =
            room.GetComponentInParent<ShipIdentity>();

        ShipIdentity targetShip =
            targetRoom.GetComponentInParent<ShipIdentity>();

        if (sourceShip != null &&
            targetShip != null)
        {
            return sourceShip == targetShip;
        }

        return room.transform.parent ==
               targetRoom.transform.parent;
    }

    private static Vector3 GetRoomWorldCenter(
        ShipRoomRuntime targetRoom)
    {
        Collider2D roomCollider =
            targetRoom.GetComponent<Collider2D>();

        if (roomCollider != null)
        {
            return roomCollider.bounds.center;
        }

        return targetRoom.transform.position;
    }

    private void OnValidate()
    {
        baseReloadDuration =
            Mathf.Max(0.01f, baseReloadDuration);
    }
}
