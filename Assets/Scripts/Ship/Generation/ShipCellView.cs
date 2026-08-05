using System.Collections.Generic;
using UnityEngine;

public sealed class ShipCellView : MonoBehaviour
{
    private static readonly List<ShipCellView> allCells =
        new List<ShipCellView>();

    public static IReadOnlyList<ShipCellView> AllCells => allCells;

    public Vector2Int Coordinates { get; private set; }
    public HullCellType HullType { get; private set; }
    public ShipModuleType ModuleType { get; private set; }
    public int RoomId { get; private set; }

    public ShipRoomRuntime Room { get; private set; }

    public bool IsControlPoint { get; private set; }
    public float CellSize { get; private set; }

    public CrewUnit Occupant { get; private set; }
    public CrewUnit ReservedBy { get; private set; }

    public bool Exists =>
        HullType != HullCellType.None;

    public bool IsLadder =>
        HullType == HullCellType.Ladder;

    // Через лестницы можно прокладывать маршрут.
    public bool CanTraverse =>
        Exists;

    // Но останавливаться экипаж может только на обычных клетках.
    public bool CanBeDestination =>
        HullType == HullCellType.Floor ||
        HullType == HullCellType.Ladder;

    // Оставлено для совместимости со старым кодом.
    public bool IsWalkable =>
        CanBeDestination;

    // У всех клеток одного корабля одинаковый GeneratedShip.
    public Transform ShipRoot =>
        Room != null ? Room.transform.parent : null;

    public bool IsOccupied =>
        Occupant != null;

    public bool IsReserved =>
        ReservedBy != null;

    public void Initialize(
        ShipCellBlueprint blueprint,
        ShipRoomRuntime room,
        bool isControlPoint,
        Sprite squareSprite,
        float cellSize,
        float cellGap,
        Color roomColor,
        Color borderColor,
        Color moduleColor,
        Color ladderColor)
    {
        if (blueprint == null)
        {
            Debug.LogError(
                "ShipCellView получил пустой blueprint.",
                this
            );

            return;
        }

        Coordinates = blueprint.Coordinates;
        HullType = blueprint.HullType;
        ModuleType = blueprint.ModuleType;
        RoomId = blueprint.RoomId;

        Room = room;
        IsControlPoint = isControlPoint;
        CellSize = cellSize;

        if (!allCells.Contains(this))
        {
            allCells.Add(this);
        }

        float innerCellSize = Mathf.Max(
            0.05f,
            cellSize - cellGap
        );

        CreatePart(
            "Border",
            squareSprite,
            borderColor,
            Vector2.zero,
            new Vector2(cellSize, cellSize),
            0
        );

        CreatePart(
            "Background",
            squareSprite,
            roomColor,
            Vector2.zero,
            new Vector2(innerCellSize, innerCellSize),
            1
        );

        if (IsLadder)
        {
            CreateLadderVisual(
                squareSprite,
                ladderColor,
                cellSize,
                2
            );
        }
        else if (ModuleType != ShipModuleType.None)
        {
            float moduleSize = cellSize * 0.38f;

            CreatePart(
                $"Module_{ModuleType}",
                squareSprite,
                moduleColor,
                Vector2.zero,
                new Vector2(moduleSize, moduleSize),
                2
            );
        }

        if (IsControlPoint)
        {
            float markerSize = cellSize * 0.13f;

            CreatePart(
                "ControlPointMarker",
                squareSprite,
                Color.white,
                new Vector2(
                    cellSize * 0.28f,
                    cellSize * 0.28f
                ),
                new Vector2(markerSize, markerSize),
                3
            );
        }
    }

    public bool IsAvailableFor(CrewUnit unit)
    {
        if (unit == null || !CanBeDestination)
        {
            return false;
        }

        bool occupantAllowsUnit =
            Occupant == null || Occupant == unit;

        bool reservationAllowsUnit =
            ReservedBy == null || ReservedBy == unit;

        return occupantAllowsUnit &&
               reservationAllowsUnit;
    }

    public bool TryOccupy(CrewUnit unit)
    {
        if (!IsAvailableFor(unit))
        {
            return false;
        }

        Occupant = unit;

        if (ReservedBy == unit)
        {
            ReservedBy = null;
        }

        return true;
    }

    public void Vacate(CrewUnit unit)
    {
        if (Occupant == unit)
        {
            Occupant = null;
        }
    }

    public bool TryReserve(CrewUnit unit)
    {
        if (!IsAvailableFor(unit))
        {
            return false;
        }

        ReservedBy = unit;
        return true;
    }

    public void ReleaseReservation(CrewUnit unit)
    {
        if (ReservedBy == unit)
        {
            ReservedBy = null;
        }
    }

    private void OnDestroy()
    {
        allCells.Remove(this);
    }

    private void CreateLadderVisual(
        Sprite squareSprite,
        Color color,
        float cellSize,
        int sortingOrder)
    {
        float railHeight = cellSize * 0.65f;
        float railWidth = cellSize * 0.07f;
        float railOffset = cellSize * 0.16f;

        CreatePart(
            "LadderLeftRail",
            squareSprite,
            color,
            new Vector2(-railOffset, 0f),
            new Vector2(railWidth, railHeight),
            sortingOrder
        );

        CreatePart(
            "LadderRightRail",
            squareSprite,
            color,
            new Vector2(railOffset, 0f),
            new Vector2(railWidth, railHeight),
            sortingOrder
        );

        float rungWidth = cellSize * 0.38f;
        float rungHeight = cellSize * 0.055f;

        for (int i = -1; i <= 1; i++)
        {
            CreatePart(
                $"LadderRung_{i + 2}",
                squareSprite,
                color,
                new Vector2(
                    0f,
                    i * cellSize * 0.18f
                ),
                new Vector2(
                    rungWidth,
                    rungHeight
                ),
                sortingOrder
            );
        }
    }

    private void CreatePart(
        string objectName,
        Sprite sprite,
        Color color,
        Vector2 localPosition,
        Vector2 size,
        int sortingOrder)
    {
        GameObject part =
            new GameObject(objectName);

        part.transform.SetParent(
            transform,
            false
        );

        part.transform.localPosition =
            new Vector3(
                localPosition.x,
                localPosition.y,
                0f
            );

        part.transform.localScale =
            new Vector3(
                size.x,
                size.y,
                1f
            );

        SpriteRenderer spriteRenderer =
            part.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
    }
}