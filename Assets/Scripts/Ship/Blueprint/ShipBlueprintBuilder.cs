using System;
using System.Collections.Generic;
using UnityEngine;

public static class ShipBlueprintBuilder
{
    public static bool TryCreateData(
        ShipHullDefinition hullDefinition,
        ShipLayoutDefinition layoutDefinition,
        out ShipBlueprintData data,
        out List<string> errors)
    {
        return TryCreateData(
            hullDefinition,
            layoutDefinition,
            true,
            out data,
            out errors
        );
    }

    public static bool TryCreateEditorData(
        ShipHullDefinition hullDefinition,
        ShipLayoutDefinition layoutDefinition,
        out ShipBlueprintData data,
        out List<string> errors)
    {
        return TryCreateData(
            hullDefinition,
            layoutDefinition,
            false,
            out data,
            out errors
        );
    }

    private static bool TryCreateData(
        ShipHullDefinition hullDefinition,
        ShipLayoutDefinition layoutDefinition,
        bool requireCatalogMembership,
        out ShipBlueprintData data,
        out List<string> errors)
    {
        data = null;
        errors = new List<string>();

        if (hullDefinition == null)
        {
            errors.Add("Не выбран корпус корабля.");
        }

        if (layoutDefinition == null)
        {
            errors.Add("Не выбрана модификация корабля.");
        }

        if (errors.Count > 0)
        {
            return false;
        }

        if (requireCatalogMembership &&
            !hullDefinition.SupportsLayout(layoutDefinition))
        {
            errors.Add(
                $"Модификация \"{layoutDefinition.LayoutName}\" " +
                $"не разрешена для корпуса " +
                $"\"{hullDefinition.HullName}\".");

            return false;
        }

        List<string> hullRows = GetRows(
            hullDefinition.HullMatrix
        );

        List<string> roomRows = GetRows(
            layoutDefinition.RoomMatrix
        );

        if (hullRows.Count == 0)
        {
            errors.Add("Матрица корпуса пустая.");
            return false;
        }

        if (roomRows.Count == 0)
        {
            errors.Add("Матрица модификации пустая.");
            return false;
        }

        if (hullRows.Count != roomRows.Count)
        {
            errors.Add(
                $"Количество палуб не совпадает. " +
                $"Корпус: {hullRows.Count}, " +
                $"модификация: {roomRows.Count}.");

            return false;
        }

        int width = hullRows[0].Length;
        int height = hullRows.Count;

        ValidateHullRows(
            hullRows,
            width,
            errors
        );

        ValidateRoomRows(
            roomRows,
            width,
            errors
        );

        if (errors.Count > 0)
        {
            return false;
        }

        HullCellType[,] hullTypes =
            new HullCellType[width, height];

        ShipModuleType[,] moduleTypes =
            new ShipModuleType[width, height];

        int[,] roomIds =
            new int[width, height];

        InitializeRoomIds(
            roomIds,
            width,
            height
        );

        ParseHull(
            hullRows,
            hullTypes,
            width,
            height
        );

        ValidateLadders(
            hullTypes,
            width,
            height,
            errors
        );

        if (errors.Count > 0)
        {
            return false;
        }

        List<ShipRoomBlueprint> rooms = ParseRooms(
            roomRows,
            hullTypes,
            moduleTypes,
            roomIds,
            width,
            errors
        );

        ValidateEveryHullCellHasRoom(
            hullTypes,
            roomIds,
            width,
            height,
            errors
        );

        if (errors.Count > 0)
        {
            return false;
        }

        ShipCellBlueprint[,] cells = CreateCells(
            hullTypes,
            moduleTypes,
            roomIds,
            width,
            height
        );

        string generatedName =
            $"{hullDefinition.HullName} / " +
            $"{layoutDefinition.LayoutName}";

        data = new ShipBlueprintData(
            generatedName,
            width,
            height,
            cells,
            rooms
        );

        return true;
    }

    private static List<string> GetRows(string matrix)
    {
        List<string> rows = new List<string>();

        if (string.IsNullOrWhiteSpace(matrix))
        {
            return rows;
        }

        string normalized =
            matrix.Replace("\r", string.Empty);

        string[] rawRows = normalized.Split(
            new[] { '\n' },
            StringSplitOptions.RemoveEmptyEntries
        );

        foreach (string rawRow in rawRows)
        {
            string row = rawRow
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("\t", string.Empty);

            if (!string.IsNullOrEmpty(row))
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static void ValidateHullRows(
        List<string> rows,
        int expectedWidth,
        List<string> errors)
    {
        if (expectedWidth <= 0)
        {
            errors.Add("Ширина корпуса равна нулю.");
            return;
        }

        for (int y = 0; y < rows.Count; y++)
        {
            string row = rows[y];

            if (row.Length != expectedWidth)
            {
                errors.Add(
                    $"Строка корпуса {y + 1} содержит " +
                    $"{row.Length} клеток вместо {expectedWidth}.");

                continue;
            }

            for (int x = 0; x < row.Length; x++)
            {
                char symbol = row[x];

                if (symbol != '0' &&
                    symbol != '1' &&
                    symbol != '2' &&
                    symbol != '3' &&
                    symbol != '4')
                {
                    errors.Add(
                        $"Недопустимый символ '{symbol}' " +
                        $"в корпусе: строка {y + 1}, " +
                        $"столбец {x + 1}.");
                }
            }
        }
    }

    private static void ValidateRoomRows(
        List<string> rows,
        int expectedWidth,
        List<string> errors)
    {
        for (int y = 0; y < rows.Count; y++)
        {
            string row = rows[y];

            if (row.StartsWith(";") ||
                row.EndsWith(";") ||
                row.Contains(";;"))
            {
                errors.Add(
                    $"Некорректное разделение отсеков " +
                    $"в строке модификации {y + 1}.");
            }

            foreach (char symbol in row)
            {
                bool isModule =
                    symbol >= '0' && symbol <= '6';

                if (!isModule && symbol != ';')
                {
                    errors.Add(
                        $"Недопустимый символ '{symbol}' " +
                        $"в модификации: строка {y + 1}.");
                }
            }

            string compactRow =
                row.Replace(";", string.Empty);

            if (compactRow.Length != expectedWidth)
            {
                errors.Add(
                    $"Строка модификации {y + 1} содержит " +
                    $"{compactRow.Length} клеток вместо " +
                    $"{expectedWidth}.");
            }
        }
    }

    private static void InitializeRoomIds(
        int[,] roomIds,
        int width,
        int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                roomIds[x, y] = -1;
            }
        }
    }

    private static void ParseHull(
        List<string> rows,
        HullCellType[,] hullTypes,
        int width,
        int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                hullTypes[x, y] =
                    (HullCellType)(rows[y][x] - '0');
            }
        }
    }

    private static void ValidateLadders(
        HullCellType[,] hullTypes,
        int width,
        int height,
        List<string> errors)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!hullTypes[x, y].IsLadder())
                {
                    continue;
                }

                bool ladderAbove =
                    y > 0 &&
                    hullTypes[x, y - 1].IsLadder();

                bool ladderBelow =
                    y < height - 1 &&
                    hullTypes[x, y + 1].IsLadder();

                if (!ladderAbove && !ladderBelow)
                {
                    errors.Add(
                        $"Одиночная лестница: " +
                        $"X={x}, палуба={y}.");
                }
            }
        }
    }

    private static List<ShipRoomBlueprint> ParseRooms(
        List<string> roomRows,
        HullCellType[,] hullTypes,
        ShipModuleType[,] moduleTypes,
        int[,] roomIds,
        int width,
        List<string> errors)
    {
        List<ShipRoomBlueprint> rooms =
            new List<ShipRoomBlueprint>();

        int nextRoomId = 0;

        for (int y = 0; y < roomRows.Count; y++)
        {
            string[] segments =
                roomRows[y].Split(';');

            int currentX = 0;

            foreach (string segment in segments)
            {
                int startX = currentX;
                int endX =
                    currentX + segment.Length - 1;

                List<Vector2Int> roomCells =
                    new List<Vector2Int>();

                bool hasControlPoint = false;

                Vector2Int controlPointCoordinates =
                    Vector2Int.zero;

                int firstExistingX = -1;
                int lastExistingX = -1;

                for (int i = 0; i < segment.Length; i++)
                {
                    int x = currentX + i;

                    ShipModuleType moduleType =
                        (ShipModuleType)(segment[i] - '0');

                    moduleTypes[x, y] = moduleType;

                    HullCellType hullType =
                        hullTypes[x, y];

                    if (hullType == HullCellType.None)
                    {
                        if (moduleType != ShipModuleType.None)
                        {
                            errors.Add(
                                $"Модуль {moduleType} установлен " +
                                $"за пределами корпуса: " +
                                $"X={x}, палуба={y}.");
                        }

                        continue;
                    }

                    if (hullType.IsLadder() &&
                        moduleType != ShipModuleType.None)
                    {
                        errors.Add(
                            $"На лестнице нельзя устанавливать " +
                            $"модуль: X={x}, палуба={y}.");
                    }

                    if (firstExistingX == -1)
                    {
                        firstExistingX = x;
                    }

                    lastExistingX = x;

                    Vector2Int coordinates =
                        new Vector2Int(x, y);

                    roomCells.Add(coordinates);

                    if (!ShipModuleRules.CreatesControlPoint(
                            moduleType))
                    {
                        continue;
                    }

                    if (hasControlPoint)
                    {
                        errors.Add(
                            $"В отсеке X={startX}–{endX}, " +
                            $"палуба={y}, находится больше " +
                            $"одного пункта управления.");
                    }
                    else
                    {
                        hasControlPoint = true;
                        controlPointCoordinates = coordinates;
                    }
                }

                if (roomCells.Count > 0)
                {
                    ValidateRoomContinuity(
                        hullTypes,
                        y,
                        firstExistingX,
                        lastExistingX,
                        errors
                    );

                    foreach (Vector2Int coordinates in roomCells)
                    {
                        roomIds[
                            coordinates.x,
                            coordinates.y
                        ] = nextRoomId;
                    }

                    rooms.Add(
                        new ShipRoomBlueprint(
                            nextRoomId,
                            y,
                            roomCells,
                            hasControlPoint,
                            controlPointCoordinates
                        )
                    );

                    nextRoomId++;
                }

                currentX += segment.Length;
            }

            if (currentX != width)
            {
                errors.Add(
                    $"После разбора палубы {y} получено " +
                    $"{currentX} клеток вместо {width}.");
            }
        }

        return rooms;
    }

    private static void ValidateRoomContinuity(
        HullCellType[,] hullTypes,
        int y,
        int firstExistingX,
        int lastExistingX,
        List<string> errors)
    {
        if (firstExistingX < 0 ||
            lastExistingX < 0)
        {
            return;
        }

        for (int x = firstExistingX;
             x <= lastExistingX;
             x++)
        {
            if (hullTypes[x, y] != HullCellType.None)
            {
                continue;
            }

            errors.Add(
                $"Отсек на палубе {y} разорван " +
                $"отсутствующей клеткой корпуса в X={x}.");

            return;
        }
    }

    private static void ValidateEveryHullCellHasRoom(
        HullCellType[,] hullTypes,
        int[,] roomIds,
        int width,
        int height,
        List<string> errors)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (hullTypes[x, y] == HullCellType.None)
                {
                    continue;
                }

                if (roomIds[x, y] >= 0)
                {
                    continue;
                }

                errors.Add(
                    $"Клетка корпуса X={x}, палуба={y} " +
                    "не принадлежит ни одному отсеку.");
            }
        }
    }

    private static ShipCellBlueprint[,] CreateCells(
        HullCellType[,] hullTypes,
        ShipModuleType[,] moduleTypes,
        int[,] roomIds,
        int width,
        int height)
    {
        ShipCellBlueprint[,] cells =
            new ShipCellBlueprint[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cells[x, y] =
                    new ShipCellBlueprint(
                        new Vector2Int(x, y),
                        hullTypes[x, y],
                        moduleTypes[x, y],
                        roomIds[x, y]
                    );
            }
        }

        return cells;
    }
}
