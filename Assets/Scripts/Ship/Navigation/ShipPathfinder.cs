using System.Collections.Generic;
using UnityEngine;

public static class ShipPathfinder
{
    private static readonly Vector2Int[] horizontalDirections =
    {
        Vector2Int.left,
        Vector2Int.right
    };

    private static readonly Vector2Int[] verticalDirections =
    {
        Vector2Int.up,
        Vector2Int.down
    };

    public static bool TryBuildPath(
        ShipCellView start,
        ShipCellView destination,
        out List<ShipCellView> path,
        out string error)
    {
        path = new List<ShipCellView>();
        error = string.Empty;

        if (start == null)
        {
            error = "Не определена стартовая клетка.";
            return false;
        }

        if (destination == null)
        {
            error = "Не определена конечная клетка.";
            return false;
        }

        if (!start.CanTraverse)
        {
            error = "Стартовая клетка недоступна для движения.";
            return false;
        }

        if (!destination.CanBeDestination)
        {
            error =
                "Конечная клетка не подходит для остановки экипажа.";

            return false;
        }

        if (start.ShipRoot == null ||
            destination.ShipRoot == null)
        {
            error = "Не удалось определить корабль клеток.";
            return false;
        }

        if (start.ShipRoot != destination.ShipRoot)
        {
            error =
                "Стартовая и конечная клетки находятся " +
                "на разных кораблях.";

            return false;
        }

        Queue<ShipCellView> frontier =
            new Queue<ShipCellView>();

        HashSet<ShipCellView> visited =
            new HashSet<ShipCellView>();

        Dictionary<ShipCellView, ShipCellView> cameFrom =
            new Dictionary<ShipCellView, ShipCellView>();

        frontier.Enqueue(start);
        visited.Add(start);
        cameFrom[start] = null;

        while (frontier.Count > 0)
        {
            ShipCellView current = frontier.Dequeue();

            if (current == destination)
            {
                BuildResultPath(
                    destination,
                    cameFrom,
                    path
                );

                return true;
            }

            foreach (ShipCellView neighbour
                     in GetNeighbours(current))
            {
                if (neighbour == null ||
                    visited.Contains(neighbour))
                {
                    continue;
                }

                visited.Add(neighbour);
                cameFrom[neighbour] = current;
                frontier.Enqueue(neighbour);
            }
        }

        error =
            $"Маршрут от X={start.Coordinates.x}, " +
            $"Y={start.Coordinates.y} до " +
            $"X={destination.Coordinates.x}, " +
            $"Y={destination.Coordinates.y} не найден.";

        return false;
    }

    private static IEnumerable<ShipCellView> GetNeighbours(
        ShipCellView current)
    {
        foreach (Vector2Int direction
                 in horizontalDirections)
        {
            ShipCellView neighbour = FindCell(
                current.ShipRoot,
                current.Coordinates + direction
            );

            if (neighbour != null &&
                neighbour.CanTraverse)
            {
                yield return neighbour;
            }
        }

        // Вертикальный переход разрешён только
        // из клетки лестницы в соседнюю клетку лестницы.
        if (!current.IsLadder)
        {
            yield break;
        }

        foreach (Vector2Int direction
                 in verticalDirections)
        {
            ShipCellView neighbour = FindCell(
                current.ShipRoot,
                current.Coordinates + direction
            );

            if (neighbour != null &&
                neighbour.IsLadder)
            {
                yield return neighbour;
            }
        }
    }

    private static ShipCellView FindCell(
        Transform shipRoot,
        Vector2Int coordinates)
    {
        foreach (ShipCellView cell
                 in ShipCellView.AllCells)
        {
            if (cell == null ||
                cell.ShipRoot != shipRoot)
            {
                continue;
            }

            if (cell.Coordinates == coordinates)
            {
                return cell;
            }
        }

        return null;
    }

    private static void BuildResultPath(
        ShipCellView destination,
        Dictionary<ShipCellView, ShipCellView> cameFrom,
        List<ShipCellView> path)
    {
        ShipCellView current = destination;

        while (current != null)
        {
            path.Add(current);
            current = cameFrom[current];
        }

        path.Reverse();
    }
}