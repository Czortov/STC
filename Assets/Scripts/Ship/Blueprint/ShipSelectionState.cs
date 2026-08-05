using System.Collections.Generic;

public static class ShipSelectionState
{
    public static ShipHullDefinition SelectedHull
    {
        get;
        private set;
    }

    public static ShipLayoutDefinition SelectedLayout
    {
        get;
        private set;
    }

    public static void SelectHull(ShipHullDefinition hull)
    {
        SelectedHull = hull;

        // При смене корпуса старая модификация сбрасывается.
        SelectedLayout = null;
    }

    public static bool TrySelectLayout(
        ShipLayoutDefinition layout,
        out string error)
    {
        error = string.Empty;

        if (SelectedHull == null)
        {
            error =
                "Нельзя выбрать модификацию: " +
                "сначала выбери корпус.";

            return false;
        }

        if (layout == null)
        {
            error = "Передана пустая модификация.";
            return false;
        }

        if (!SelectedHull.SupportsLayout(layout))
        {
            error =
                $"Модификация \"{layout.LayoutName}\" " +
                $"не подходит корпусу " +
                $"\"{SelectedHull.HullName}\".";

            return false;
        }

        SelectedLayout = layout;
        return true;
    }

    public static bool TryBuildSelectedShip(
        out ShipBlueprintData data,
        out List<string> errors)
    {
        return ShipBlueprintBuilder.TryCreateData(
            SelectedHull,
            SelectedLayout,
            out data,
            out errors
        );
    }

    public static void Clear()
    {
        SelectedHull = null;
        SelectedLayout = null;
    }
}