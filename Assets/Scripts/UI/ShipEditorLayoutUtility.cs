using System;

public static class ShipEditorLayoutUtility
{
    public static bool TryGetHullCell(
        string matrix,
        int x,
        int y,
        out HullCellType cellType)
    {
        cellType = HullCellType.None;
        string[] rows = GetRows(matrix);

        if (y < 0 || y >= rows.Length || x < 0 || x >= rows[y].Length)
        {
            return false;
        }

        char value = rows[y][x];

        if (value < '0' || value > '4')
        {
            return false;
        }

        cellType = (HullCellType)(value - '0');
        return true;
    }

    public static bool TryReplaceModule(
        ref string matrix,
        int targetX,
        int targetY,
        ShipModuleType moduleType)
    {
        if ((int)moduleType < 0 || (int)moduleType > 6)
        {
            return false;
        }

        string[] rows = GetRows(matrix);

        if (targetY < 0 || targetY >= rows.Length)
        {
            return false;
        }

        char[] row = rows[targetY].ToCharArray();
        int compactX = 0;

        for (int i = 0; i < row.Length; i++)
        {
            if (row[i] == ';')
            {
                continue;
            }

            if (compactX == targetX)
            {
                row[i] = (char)('0' + (int)moduleType);
                rows[targetY] = new string(row);
                matrix = string.Join("\n", rows);
                return true;
            }

            compactX++;
        }

        return false;
    }

    public static string ClearModules(string matrix)
    {
        char[] characters = (matrix ?? string.Empty)
            .Replace("\r", string.Empty)
            .ToCharArray();

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] >= '0' && characters[i] <= '6')
            {
                characters[i] = '0';
            }
        }

        return new string(characters);
    }

    public static string CreateEmptyMatrix(string hullMatrix)
    {
        string[] hullRows = GetRows(hullMatrix);
        string[] result = new string[hullRows.Length];

        for (int y = 0; y < hullRows.Length; y++)
        {
            result[y] = new string('0', hullRows[y].Length);
        }

        return string.Join("\n", result);
    }

    public static string[] GetRows(string matrix)
    {
        return (matrix ?? string.Empty)
            .Replace("\r", string.Empty)
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
