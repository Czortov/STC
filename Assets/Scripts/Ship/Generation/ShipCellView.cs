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

    public bool IsRoom =>
        HullType.IsRoom();

    public bool IsLadder =>
        HullType.IsLadder();

    public bool IsInterior =>
        HullType.IsInterior();

    public bool IsExterior =>
        HullType.IsExterior();

    // Через лестницы можно прокладывать маршрут.
    public bool CanTraverse =>
        Exists;

    // Но останавливаться экипаж может только на обычных клетках.
    public bool CanBeDestination =>
        Exists;

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
        Sprite wallSprite,
        ShipModuleVisualEntry moduleVisual,
        Sprite moduleSprite,
        Sprite ladderSprite,
        int ladderSortingOrder,
        float cellSize,
        Color roomColor,
        Color borderColor,
        Color moduleColor,
        Color ladderColor,
        int wallSortingOrder)
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

        if (!IsExterior)
        {
            CreatePart(
                "Border",
                squareSprite,
                borderColor,
                Vector2.zero,
                new Vector2(cellSize, cellSize),
                wallSortingOrder - 1
            );
        }

        if (wallSprite != null)
        {
            CreateNativeSpritePart(
                "BackgroundSprite",
                wallSprite,
                Color.white,
                Vector2.zero,
                wallSortingOrder
            );
        }
        else if (!IsExterior)
        {
            CreatePart(
                "BackgroundFallback",
                squareSprite,
                roomColor,
                Vector2.zero,
                new Vector2(cellSize, cellSize),
                wallSortingOrder
            );
        }

        if (IsLadder)
        {
            if (ladderSprite != null)
            {
                CreateNativeSpritePart(
                    "LadderSprite",
                    ladderSprite,
                    Color.white,
                    Vector2.zero,
                    ladderSortingOrder
                );
            }
            else
            {
                CreateLadderVisual(
                    squareSprite,
                    ladderColor,
                    cellSize,
                    wallSortingOrder + 1
                );
            }
        }
        else if (ModuleType != ShipModuleType.None &&
                 moduleVisual != null &&
                 moduleSprite != null)
        {
            if (moduleVisual.UsesNativeCellTransform)
            {
                CreateNativeSpritePart(
                    "ModuleSprite",
                    moduleSprite,
                    Color.white,
                    Vector2.zero,
                    moduleVisual.SortingOrder
                );
            }
            else
            {
                CreatePart(
                    "ModuleSprite",
                    moduleSprite,
                    Color.white,
                    moduleVisual.OffsetInCells * cellSize,
                    moduleVisual.SizeInCells * cellSize,
                    moduleVisual.SortingOrder
                );
            }
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
                wallSortingOrder + 1
            );
        }

        if (IsControlPoint && moduleSprite == null)
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
                wallSortingOrder + 2
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

        Vector2 spriteSize = sprite.bounds.size;

        part.transform.localScale = new Vector3(
            spriteSize.x > 0f ? size.x / spriteSize.x : 1f,
            spriteSize.y > 0f ? size.y / spriteSize.y : 1f,
            1f
        );

        SpriteRenderer spriteRenderer =
            part.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private void CreateNativeSpritePart(
        string objectName,
        Sprite sprite,
        Color color,
        Vector2 localPosition,
        int sortingOrder)
    {
        GameObject part = new GameObject(objectName);

        part.transform.SetParent(transform, false);
        part.transform.localPosition = new Vector3(
            localPosition.x,
            localPosition.y,
            0f
        );
        part.transform.localScale = Vector3.one;

        SpriteRenderer spriteRenderer =
            part.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
    }
}
