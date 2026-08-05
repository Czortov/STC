using System.Collections.Generic;
using UnityEngine;

public enum HullCellType
{
    None = 0,
    Floor = 1,
    Ladder = 2
}

public enum ShipModuleType
{
    None = 0,
    Rudder = 1,
    Supplies = 2,
    Cannon = 3,
    OpenDeck = 4,
    Sails = 5,
    Bunks = 6
}

public static class ShipModuleRules
{
    public static bool CreatesControlPoint(ShipModuleType moduleType)
    {
        switch (moduleType)
        {
            case ShipModuleType.Rudder:
            case ShipModuleType.Cannon:
            case ShipModuleType.Sails:
                return true;

            default:
                return false;
        }
    }
}

public sealed class ShipCellBlueprint
{
    public Vector2Int Coordinates { get; }
    public HullCellType HullType { get; }
    public ShipModuleType ModuleType { get; }
    public int RoomId { get; }

    public bool Exists => HullType != HullCellType.None;
    public bool IsLadder => HullType == HullCellType.Ladder;

    public ShipCellBlueprint(
        Vector2Int coordinates,
        HullCellType hullType,
        ShipModuleType moduleType,
        int roomId)
    {
        Coordinates = coordinates;
        HullType = hullType;
        ModuleType = moduleType;
        RoomId = roomId;
    }
}

public sealed class ShipRoomBlueprint
{
    private readonly List<Vector2Int> cells;

    public int Id { get; }
    public int DeckIndex { get; }

    public IReadOnlyList<Vector2Int> Cells => cells;

    public bool HasControlPoint { get; }
    public Vector2Int ControlPointCoordinates { get; }

    public ShipRoomBlueprint(
        int id,
        int deckIndex,
        List<Vector2Int> cells,
        bool hasControlPoint,
        Vector2Int controlPointCoordinates)
    {
        Id = id;
        DeckIndex = deckIndex;
        this.cells = cells;

        HasControlPoint = hasControlPoint;
        ControlPointCoordinates = controlPointCoordinates;
    }
}

public sealed class ShipBlueprintData
{
    private readonly List<ShipRoomBlueprint> rooms;

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }

    public ShipCellBlueprint[,] Cells { get; }
    public IReadOnlyList<ShipRoomBlueprint> Rooms => rooms;

    public ShipBlueprintData(
        string name,
        int width,
        int height,
        ShipCellBlueprint[,] cells,
        List<ShipRoomBlueprint> rooms)
    {
        Name = name;
        Width = width;
        Height = height;
        Cells = cells;
        this.rooms = rooms;
    }

    public ShipCellBlueprint GetCell(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return null;
        }

        return Cells[x, y];
    }
}