using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BunkCrewSpawner : MonoBehaviour
{
    [Header("Crew")]

    [SerializeField] private CrewUnit crewPrefab;

    [Tooltip("Родитель для созданных членов экипажа. " +
             "Если не назначен, объект Crew создастся автоматически.")]
    [SerializeField] private Transform crewContainer;

    [Header("Spawn")]

    [SerializeField] private bool spawnOnStart = true;

    [Tooltip("Смещение экипажа относительно центра клетки койки.")]
    [SerializeField] private Vector3 spawnOffset =
        new Vector3(0f, 0f, -0.1f);

    [Tooltip("Сколько кадров ждать генерацию корабля.")]
    [Min(1)]
    [SerializeField] private int generationWaitFrames = 120;

    private readonly List<CrewUnit> spawnedCrew =
        new List<CrewUnit>();

    private bool hasSpawned;

    public IReadOnlyList<CrewUnit> SpawnedCrew =>
        spawnedCrew;

    private IEnumerator Start()
    {
        if (!spawnOnStart)
        {
            yield break;
        }

        yield return SpawnWhenShipIsReady();
    }

    private IEnumerator SpawnWhenShipIsReady()
    {
        // Даём ShipGenerator и вручную размещённым юнитам
        // закончить первоначальную настройку.
        yield return null;

        for (int frame = 0;
             frame < generationWaitFrames;
             frame++)
        {
            List<ShipCellView> bunkCells =
                FindBunkCells();

            if (bunkCells.Count > 0)
            {
                SpawnCrew(bunkCells);
                yield break;
            }

            yield return null;
        }

        Debug.LogError(
            $"{name}: не найдены клетки коек. " +
            "Проверь, что корабль сгенерирован, " +
            "а в матрице модификации есть символы 6.",
            this
        );
    }

    private List<ShipCellView> FindBunkCells()
    {
        List<ShipCellView> bunkCells =
            new List<ShipCellView>();

        foreach (ShipCellView cell
                 in ShipCellView.AllCells)
        {
            if (cell == null ||
                cell.ShipRoot == null)
            {
                continue;
            }

            // Берём клетки только этого корабля.
            if (!cell.ShipRoot.IsChildOf(transform))
            {
                continue;
            }

            if (cell.ModuleType != ShipModuleType.Bunks)
            {
                continue;
            }

            if (!cell.CanBeDestination)
            {
                Debug.LogWarning(
                    $"{name}: койка X={cell.Coordinates.x}, " +
                    $"Y={cell.Coordinates.y} находится " +
                    "на клетке, недоступной для остановки.",
                    cell
                );

                continue;
            }

            bunkCells.Add(cell);
        }

        // Стабильный порядок создания:
        // сверху вниз, затем слева направо.
        bunkCells.Sort(
            (first, second) =>
            {
                int deckComparison =
                    second.Coordinates.y.CompareTo(
                        first.Coordinates.y
                    );

                if (deckComparison != 0)
                {
                    return deckComparison;
                }

                return first.Coordinates.x.CompareTo(
                    second.Coordinates.x
                );
            }
        );

        return bunkCells;
    }

    private void SpawnCrew(
        List<ShipCellView> bunkCells)
    {
        if (hasSpawned)
        {
            Debug.LogWarning(
                $"{name}: экипаж уже создан.",
                this
            );

            return;
        }

        if (crewPrefab == null)
        {
            Debug.LogError(
                $"{name}: не назначен Crew Prefab.",
                this
            );

            return;
        }

        EnsureCrewContainer();

        int createdCount = 0;

        foreach (ShipCellView bunkCell
                 in bunkCells)
        {
            if (bunkCell == null)
            {
                continue;
            }

            if (bunkCell.IsOccupied ||
                bunkCell.IsReserved)
            {
                Debug.LogWarning(
                    $"{name}: койка X={bunkCell.Coordinates.x}, " +
                    $"Y={bunkCell.Coordinates.y} уже занята. " +
                    "Создание юнита пропущено.",
                    bunkCell
                );

                continue;
            }

            Vector3 spawnPosition =
                bunkCell.transform.position +
                spawnOffset;

            CrewUnit newCrew =
                Instantiate(
                    crewPrefab,
                    spawnPosition,
                    Quaternion.identity,
                    crewContainer
                );

            newCrew.name =
                $"Crew_Bunk_{bunkCell.Coordinates.x}_" +
                $"{bunkCell.Coordinates.y}";

            spawnedCrew.Add(newCrew);
            createdCount++;
        }

        hasSpawned = true;

        Debug.Log(
            $"{name}: создано членов экипажа: " +
            $"{createdCount}. Коек найдено: {bunkCells.Count}.",
            this
        );
    }

    private void EnsureCrewContainer()
    {
        if (crewContainer != null)
        {
            return;
        }

        Transform existingContainer =
            transform.Find("Crew");

        if (existingContainer != null)
        {
            crewContainer = existingContainer;
            return;
        }

        GameObject containerObject =
            new GameObject("Crew");

        containerObject.transform.SetParent(
            transform,
            false
        );

        crewContainer =
            containerObject.transform;
    }
}