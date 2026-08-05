using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewShipHull",
    menuName = "Pirates/Ships/Hull Definition")]
public sealed class ShipHullDefinition : ScriptableObject
{
    [SerializeField] private string hullName = "Brig";

    [Tooltip("0 — клетки нет, 1 — обычная клетка, 2 — лестница.")]
    [TextArea(4, 12)]
    [SerializeField] private string hullMatrix =
        "120000000\n" +
        "121111211\n" +
        "111111210\n" +
        "001111200";

    [Header("Available modifications")]
    [Tooltip("Модификации, которые можно установить на этот корпус.")]
    [SerializeField]
    private List<ShipLayoutDefinition> availableLayouts =
        new List<ShipLayoutDefinition>();

    public string HullName => hullName;
    public string HullMatrix => hullMatrix;

    public IReadOnlyList<ShipLayoutDefinition> AvailableLayouts =>
        availableLayouts;

    public bool SupportsLayout(ShipLayoutDefinition layout)
    {
        return layout != null && availableLayouts.Contains(layout);
    }

    [ContextMenu("Validate Available Layouts")]
    private void ValidateAvailableLayouts()
    {
        if (availableLayouts.Count == 0)
        {
            Debug.LogWarning(
                $"У корпуса \"{hullName}\" нет доступных модификаций.",
                this
            );

            return;
        }

        HashSet<ShipLayoutDefinition> checkedLayouts =
            new HashSet<ShipLayoutDefinition>();

        foreach (ShipLayoutDefinition layout in availableLayouts)
        {
            if (layout == null)
            {
                Debug.LogError(
                    $"В списке модификаций корпуса \"{hullName}\" " +
                    "есть пустой элемент.",
                    this
                );

                continue;
            }

            if (!checkedLayouts.Add(layout))
            {
                Debug.LogWarning(
                    $"Модификация \"{layout.LayoutName}\" добавлена " +
                    $"к корпусу \"{hullName}\" несколько раз.",
                    this
                );

                continue;
            }

            if (ShipBlueprintBuilder.TryCreateData(
                    this,
                    layout,
                    out ShipBlueprintData data,
                    out List<string> errors))
            {
                Debug.Log(
                    $"Корпус \"{hullName}\" совместим с модификацией " +
                    $"\"{layout.LayoutName}\". " +
                    $"Отсеков: {data.Rooms.Count}.",
                    this
                );

                continue;
            }

            foreach (string error in errors)
            {
                Debug.LogError(
                    $"{hullName} + {layout.LayoutName}: {error}",
                    this
                );
            }
        }
    }
}