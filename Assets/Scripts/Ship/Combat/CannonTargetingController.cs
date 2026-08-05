using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class CannonTargetingController : MonoBehaviour
{
    public event Action<CannonSystemRuntime> SelectedCannonChanged;

    public static bool IsTargetingMode
    {
        get;
        private set;
    }

    [Header("References")]

    [SerializeField] private Camera worldCamera;
    [SerializeField] private Camera enemyCamera;
    [SerializeField] private BattleOverviewCameraController overviewCamera;

    [Tooltip("Ship Identity корабля игрока.")]
    [SerializeField] private ShipIdentity playerShip;

    [Header("Cannon State Colors")]

    [SerializeField] private Color readyCannonColor =
        new Color(0.2f, 1f, 0.25f, 1f);

    [SerializeField] private Color reloadingCannonColor =
        new Color(1f, 0.55f, 0.1f, 1f);

    [SerializeField] private Color unavailableCannonColor =
        new Color(0.45f, 0.45f, 0.45f, 1f);

    [SerializeField] private Color enemyTargetColor =
        new Color(1f, 0.15f, 0.15f, 1f);

    [SerializeField] private Color areaImpactPreviewColor =
        new Color(1f, 0.45f, 0.08f, 0.85f);

    [Header("Outline Settings")]

    [Tooltip("Толщина рамки невыбранной пушки.")]
    [Min(0.01f)]
    [SerializeField] private float normalOutlineWidth = 0.045f;

    [Tooltip("Толщина рамки выбранной пушки.")]
    [Min(0.01f)]
    [SerializeField] private float selectedOutlineWidth = 0.08f;

    [Tooltip(
        "Насколько рамка выбранной пушки будет больше обычной.")]
    [Min(0f)]
    [SerializeField] private float selectedOutlineExtraPadding = 0.06f;

    [Min(0.01f)]
    [SerializeField] private float targetOutlineWidth = 0.07f;

    [Min(0f)]
    [SerializeField] private float outlinePadding = 0.07f;

    private readonly Dictionary
        <CannonSystemRuntime, RoomOutlineView>
        cannonOutlines =
            new Dictionary
                <CannonSystemRuntime, RoomOutlineView>();

    private readonly List<CannonSystemRuntime>
        cannonsToRemove =
            new List<CannonSystemRuntime>();

    private CannonSystemRuntime selectedCannon;
    private ShipRoomRuntime hoveredEnemyRoom;

    private RoomOutlineView hoveredTargetOutline;
    private AreaImpactPreview areaImpactPreview;

    public CannonSystemRuntime SelectedCannon =>
        selectedCannon;

    public ShipRoomRuntime HoveredEnemyRoom =>
        hoveredEnemyRoom;

    public ShipIdentity PlayerShip => playerShip;

    public bool TrySelectCannon(CannonSystemRuntime cannon)
    {
        if (!IsPlayerCannon(cannon))
        {
            return false;
        }

        SetSelectedCannon(cannon);
        return true;
    }

    private void Awake()
    {
        if (enemyCamera == null)
        {
            Debug.LogError(
                "CannonTargetingController: Enemy Camera is not assigned.",
                this
            );
        }

        hoveredTargetOutline =
            CreateOutline(
                "HoveredEnemyRoomOutline",
                103
            );

        GameObject previewObject =
            new GameObject("AreaImpactPreview");

        previewObject.transform.SetParent(
            transform,
            false
        );

        areaImpactPreview =
            previewObject.AddComponent<AreaImpactPreview>();

        areaImpactPreview.Initialize(101);

        if (worldCamera == null)
        {
            Debug.LogError(
                "CannonTargetingController: " +
                "не назначена игровая камера.",
                this
            );
        }

        if (playerShip == null)
        {
            Debug.LogError(
                "CannonTargetingController: " +
                "не назначен Ship Identity корабля игрока.",
                this
            );
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey
            .wasPressedThisFrame)
        {
            SetTargetingMode(!IsTargetingMode);
            return;
        }

        if (!IsTargetingMode)
        {
            return;
        }

        RefreshPlayerCannonOutlines();
        UpdateHoveredEnemyRoom();
        RefreshAreaImpactPreview();

        if (Mouse.current == null ||
            IsPointerOverUI())
        {
            return;
        }

        if (Mouse.current.leftButton
            .wasPressedThisFrame)
        {
            HandleCannonSelectionClick();
        }

        if (Mouse.current.rightButton
            .wasPressedThisFrame)
        {
            HandleFireClick();
        }
    }

    private void SetTargetingMode(bool enabled)
    {
        IsTargetingMode = enabled;

        if (!enabled)
        {
            ClearTargetingState();

            Debug.Log(
                "Режим стрельбы выключен.",
                this
            );

            return;
        }

        RefreshPlayerCannonOutlines();

        Debug.Log(
            "Режим стрельбы включён. " +
            "ЛКМ — выбрать пушку, " +
            "ПКМ — выстрелить по вражескому отсеку.",
            this
        );
    }

    private void RefreshPlayerCannonOutlines()
    {
        if (playerShip == null)
        {
            return;
        }

        if (!IsPlayerCannon(selectedCannon))
        {
            selectedCannon = null;
        }

        cannonsToRemove.Clear();

        foreach (
            KeyValuePair
                <CannonSystemRuntime, RoomOutlineView>
            pair in cannonOutlines)
        {
            cannonsToRemove.Add(pair.Key);
        }

        foreach (
            CannonSystemRuntime cannon
            in CannonSystemRuntime.AllCannons)
        {
            if (!IsPlayerCannon(cannon))
            {
                continue;
            }

            cannonsToRemove.Remove(cannon);

            if (!cannonOutlines.TryGetValue(
                    cannon,
                    out RoomOutlineView outline))
            {
                outline =
                    CreateOutline(
                        $"CannonOutline_{cannon.Room.Id}",
                        100
                    );

                cannonOutlines.Add(
                    cannon,
                    outline
                );
            }

            bool isSelected =
                cannon == selectedCannon;

            float width =
                isSelected
                    ? selectedOutlineWidth
                    : normalOutlineWidth;

            float padding =
                isSelected
                    ? outlinePadding +
                      selectedOutlineExtraPadding
                    : outlinePadding;

            /*
             * Цвет всегда показывает состояние пушки.
             * Выбор обозначается только увеличением рамки.
             */
            outline.Show(
                cannon.Room,
                GetCannonStateColor(cannon),
                width,
                padding
            );
        }

        foreach (
            CannonSystemRuntime cannon
            in cannonsToRemove)
        {
            if (!cannonOutlines.TryGetValue(
                    cannon,
                    out RoomOutlineView outline))
            {
                continue;
            }

            if (outline != null)
            {
                Destroy(outline.gameObject);
            }

            cannonOutlines.Remove(cannon);
        }
    }

    private bool IsPlayerCannon(
        CannonSystemRuntime cannon)
    {
        if (cannon == null ||
            cannon.Room == null ||
            playerShip == null)
        {
            return false;
        }

        ShipIdentity cannonShip =
            cannon.Room.GetComponentInParent
                <ShipIdentity>();

        return cannonShip == playerShip;
    }

    private Color GetCannonStateColor(
        CannonSystemRuntime cannon)
    {
        if (cannon == null ||
            cannon.Room == null ||
            !cannon.Room.IsOperational)
        {
            return unavailableCannonColor;
        }

        if (!cannon.IsLoaded)
        {
            return reloadingCannonColor;
        }

        return readyCannonColor;
    }

    private void HandleCannonSelectionClick()
    {
        if (worldCamera == null ||
            playerShip == null ||
            !IsPointerInLeftHalf())
        {
            return;
        }

        ShipRoomRuntime clickedRoom =
            FindRoomAtPoint(
                GetMouseWorldPosition()
            );

        if (clickedRoom == null)
        {
            Debug.Log(
                "ЛКМ нажата вне отсеков.",
                this
            );

            return;
        }

        ShipIdentity clickedShip =
            clickedRoom.GetComponentInParent
                <ShipIdentity>();

        if (clickedShip != playerShip)
        {
            Debug.Log(
                "ЛКМ в режиме стрельбы выбирает " +
                "только пушку корабля игрока.",
                clickedRoom
            );

            return;
        }

        CannonSystemRuntime cannon =
            clickedRoom.GetComponent
                <CannonSystemRuntime>();

        if (cannon == null)
        {
            Debug.Log(
                $"Отсек {clickedRoom.Id} " +
                "не является пушечным.",
                clickedRoom
            );

            return;
        }

        SetSelectedCannon(cannon);

        string state;

        if (!clickedRoom.IsOperational)
        {
            state =
                "недоступна: нет оператора " +
                "или отсек разрушен";
        }
        else if (!cannon.IsLoaded)
        {
            state =
                $"перезаряжается, осталось " +
                $"{cannon.ReloadRemaining:0.0} сек.";
        }
        else
        {
            state = "готова к выстрелу";
        }

        Debug.Log(
            $"Выбрана пушка в отсеке " +
            $"{clickedRoom.Id}: {state}.",
            cannon
        );
    }

    private void HandleFireClick()
    {
        if (!IsPointerInRightHalf())
        {
            return;
        }

        if (selectedCannon == null)
        {
            Debug.Log(
                "Сначала выбери пушку левой кнопкой мыши.",
                this
            );

            return;
        }

        if (hoveredEnemyRoom == null)
        {
            Debug.Log(
                "ПКМ нажата вне доступного " +
                "вражеского отсека.",
                this
            );

            return;
        }

        selectedCannon.TryFire(
            hoveredEnemyRoom
        );

        RefreshPlayerCannonOutlines();
    }

    private void UpdateHoveredEnemyRoom()
    {
        if (enemyCamera == null ||
            playerShip == null ||
            Mouse.current == null ||
            IsPointerOverUI() ||
            !IsPointerInRightHalf())
        {
            SetHoveredEnemyRoom(null);
            return;
        }

        ShipRoomRuntime room =
            FindRoomAtPoint(
                GetMouseWorldPosition(enemyCamera)
            );

        if (!IsEnemyTarget(room))
        {
            SetHoveredEnemyRoom(null);
            return;
        }

        SetHoveredEnemyRoom(room);
    }

    private bool IsEnemyTarget(
        ShipRoomRuntime room)
    {
        if (room == null ||
            playerShip == null)
        {
            return false;
        }

        ShipIdentity roomShip =
            room.GetComponentInParent
                <ShipIdentity>();

        if (roomShip == null)
        {
            return false;
        }

        return roomShip.Team != playerShip.Team;
    }

    private void SetSelectedCannon(
        CannonSystemRuntime cannon)
    {
        if (selectedCannon == cannon)
        {
            return;
        }

        selectedCannon = cannon;

        RefreshPlayerCannonOutlines();
        SelectedCannonChanged?.Invoke(selectedCannon);
    }

    private void SetHoveredEnemyRoom(
        ShipRoomRuntime room)
    {
        if (hoveredEnemyRoom == room)
        {
            return;
        }

        hoveredEnemyRoom = room;

        if (hoveredTargetOutline == null)
        {
            return;
        }

        if (hoveredEnemyRoom == null)
        {
            hoveredTargetOutline.Hide();
            areaImpactPreview?.Hide();
            return;
        }

        hoveredTargetOutline.Show(
            hoveredEnemyRoom,
            enemyTargetColor,
            targetOutlineWidth,
            outlinePadding
        );

        RefreshAreaImpactPreview();
    }

    private void RefreshAreaImpactPreview()
    {
        if (areaImpactPreview == null ||
            hoveredEnemyRoom == null ||
            selectedCannon == null)
        {
            areaImpactPreview?.Hide();
            return;
        }

        CannonAmmoDefinition ammo =
            selectedCannon.LoadedAmmo;

        if (!(ammo is ShrapnelAmmoDefinition) ||
            ammo.ImpactType != AmmoImpactType.Area ||
            ammo.AreaRadius <= 0f)
        {
            areaImpactPreview.Hide();
            return;
        }

        areaImpactPreview.Show(
            GetRoomWorldCenter(hoveredEnemyRoom),
            ammo.AreaRadius,
            areaImpactPreviewColor
        );
    }

    private void ClearTargetingState()
    {
        bool hadSelection = selectedCannon != null;
        selectedCannon = null;
        hoveredEnemyRoom = null;

        if (hoveredTargetOutline != null)
        {
            hoveredTargetOutline.Hide();
        }

        areaImpactPreview?.Hide();

        foreach (
            RoomOutlineView outline
            in cannonOutlines.Values)
        {
            if (outline != null)
            {
                outline.Hide();
            }
        }

        if (hadSelection)
        {
            SelectedCannonChanged?.Invoke(null);
        }
    }

    private RoomOutlineView CreateOutline(
        string objectName,
        int sortingOrder)
    {
        GameObject outlineObject =
            new GameObject(objectName);

        outlineObject.transform.SetParent(
            transform,
            false
        );

        RoomOutlineView outline =
            outlineObject.AddComponent
                <RoomOutlineView>();

        outline.Initialize(sortingOrder);

        return outline;
    }

    private Vector2 GetMouseWorldPosition()
    {
        return GetMouseWorldPosition(worldCamera);
    }

    private static Vector2 GetMouseWorldPosition(Camera camera)
    {
        Vector2 screenPosition =
            Mouse.current.position.ReadValue();

        Vector3 worldPosition =
            camera.ScreenToWorldPoint(
                screenPosition
            );

        return new Vector2(
            worldPosition.x,
            worldPosition.y
        );
    }

    private static ShipRoomRuntime FindRoomAtPoint(
        Vector2 worldPoint)
    {
        Collider2D[] hits =
            Physics2D.OverlapPointAll(
                worldPoint
            );

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            ShipRoomRuntime room =
                hit.GetComponentInParent
                    <ShipRoomRuntime>();

            if (room != null)
            {
                return room;
            }
        }

        return null;
    }

    private bool IsPointerInLeftHalf()
    {
        if (Mouse.current == null)
        {
            return false;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();

        return screenPosition.x < Screen.width * 0.5f &&
               !IsPointerInsideOverview(screenPosition);
    }

    private bool IsPointerInRightHalf()
    {
        if (Mouse.current == null)
        {
            return false;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();

        return screenPosition.x >= Screen.width * 0.5f &&
               !IsPointerInsideOverview(screenPosition);
    }

    private bool IsPointerInsideOverview(Vector2 screenPosition)
    {
        return overviewCamera != null &&
               overviewCamera.IsPointerInsideOverviewViewport(screenPosition);
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

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current
                   .IsPointerOverGameObject();
    }

    private void OnDisable()
    {
        IsTargetingMode = false;
        ClearTargetingState();
    }
}
