using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class CrewCommandController : MonoBehaviour
{
    public event Action<CrewUnit> SelectionChanged;

    [Header("References")]

    [SerializeField] private Camera worldCamera;
    [SerializeField] private BattleOverviewCameraController overviewCamera;

    [Header("Selection")]

    [SerializeField] private bool deselectOnEmptyClick = true;

    private CrewUnit selectedUnit;

    public CrewUnit SelectedUnit => selectedUnit;

    private void Awake()
    {
        if (worldCamera == null)
        {
            Debug.LogError(
                "CrewCommandController: Player Camera is not assigned.",
                this
            );
        }
    }

    private void Update()
    {
        if (worldCamera == null ||
            Mouse.current == null)
        {
            return;
        }

        // Пока включён режим стрельбы,
        // команды экипажу не обрабатываются.
        if (CannonTargetingController.IsTargetingMode)
        {
            return;
        }

        /*
         * Защита на случай, если выбранный юнит был уничтожен,
         * перенесён на другой корабль или выбран сторонним кодом.
         */
        if (selectedUnit != null &&
            !IsPlayerCrew(selectedUnit))
        {
            SelectUnit(null);
        }

        if (IsPointerOverUI())
        {
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (mousePosition.x >= Screen.width * 0.5f ||
            overviewCamera != null &&
            overviewCamera.IsPointerInsideOverviewViewport(mousePosition))
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleLeftClick();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            HandleRightClick();
        }
    }

    private void HandleLeftClick()
    {
        Vector2 worldPoint =
            GetMouseWorldPosition();

        CrewUnit clickedUnit =
            FindComponentAtPoint<CrewUnit>(
                worldPoint
            );

        if (clickedUnit != null)
        {
            // Вражеский экипаж игрок выбрать не может.
            if (!IsPlayerCrew(clickedUnit))
            {
                return;
            }

            SelectUnit(clickedUnit);
            return;
        }

        if (deselectOnEmptyClick)
        {
            SelectUnit(null);
        }
    }

    private void HandleRightClick()
    {
        if (selectedUnit == null)
        {
            Debug.Log(
                "Сначала выбери члена экипажа " +
                "левой кнопкой мыши.",
                this
            );

            return;
        }

        if (!IsPlayerCrew(selectedUnit))
        {
            SelectUnit(null);
            return;
        }

        Vector2 worldPoint =
            GetMouseWorldPosition();

        ShipRoomRuntime clickedRoom =
            FindComponentAtPoint<ShipRoomRuntime>(
                worldPoint
            );

        if (clickedRoom == null)
        {
            Debug.Log(
                "ПКМ была нажата вне отсеков корабля.",
                this
            );

            return;
        }

        // Собственный экипаж можно отправлять
        // только в отсеки собственного корабля.
        if (!IsPlayerRoom(clickedRoom))
        {
            Debug.Log(
                "Нельзя отправить экипаж на вражеский корабль.",
                this
            );

            return;
        }

        selectedUnit.AssignRoom(clickedRoom);
    }

    public void SelectUnit(
        CrewUnit newSelectedUnit)
    {
        if (newSelectedUnit != null &&
            !IsPlayerCrew(newSelectedUnit))
        {
            return;
        }

        if (selectedUnit == newSelectedUnit)
        {
            return;
        }

        if (selectedUnit != null)
        {
            selectedUnit.SetSelected(false);
        }

        selectedUnit = newSelectedUnit;

        if (selectedUnit != null)
        {
            selectedUnit.SetSelected(true);

            Debug.Log(
                $"Выбран член экипажа: " +
                $"{selectedUnit.name}.",
                selectedUnit
            );
        }

        SelectionChanged?.Invoke(selectedUnit);
    }

    private Vector2 GetMouseWorldPosition()
    {
        Vector2 screenPosition =
            Mouse.current.position.ReadValue();

        Vector3 worldPosition =
            worldCamera.ScreenToWorldPoint(
                screenPosition
            );

        return new Vector2(
            worldPosition.x,
            worldPosition.y
        );
    }

    private static T FindComponentAtPoint<T>(
        Vector2 worldPoint)
        where T : Component
    {
        Collider2D[] hits =
            Physics2D.OverlapPointAll(worldPoint);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            T component =
                hit.GetComponentInParent<T>();

            if (component != null)
            {
                return component;
            }
        }

        return null;
    }

    public static bool IsPlayerCrew(
        CrewUnit crew)
    {
        if (crew == null)
        {
            return false;
        }

        ShipIdentity ship =
            crew.GetComponentInParent<ShipIdentity>();

        /*
         * Запасной вариант: Crew может находиться
         * вне иерархии корабля, но занимать его клетку.
         */
        if (ship == null &&
            crew.CurrentCell != null &&
            crew.CurrentCell.Room != null)
        {
            ship = crew.CurrentCell.Room
                .GetComponentInParent<ShipIdentity>();
        }

        return ship != null &&
               ship.Team == ShipTeam.Player;
    }

    private static bool IsPlayerRoom(
        ShipRoomRuntime room)
    {
        if (room == null)
        {
            return false;
        }

        ShipIdentity ship =
            room.GetComponentInParent<ShipIdentity>();

        return ship != null &&
               ship.Team == ShipTeam.Player;
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }
}
