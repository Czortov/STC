public static class GameSession
{
    public static EnemyId SelectedEnemy { get; private set; } = EnemyId.None;

    public static ShipHullDefinition PlayerHull { get; private set; }
    public static ShipLayoutDefinition PlayerLayout { get; private set; }
    public static ShipHullDefinition EnemyHull { get; private set; }
    public static ShipLayoutDefinition EnemyLayout { get; private set; }

    public static void SelectEnemy(EnemyId enemyId)
    {
        SelectedEnemy = enemyId;
    }

    public static void SelectPlayerShip(
        ShipHullDefinition hull,
        ShipLayoutDefinition layout)
    {
        PlayerHull = hull;
        PlayerLayout = layout;

        ShipSelectionState.SelectHull(hull);

        if (layout != null &&
            !ShipSelectionState.TrySelectLayout(layout, out string error))
        {
            UnityEngine.Debug.LogError(error);
        }
    }

    public static void SelectEnemyShip(
        ShipHullDefinition hull,
        ShipLayoutDefinition layout)
    {
        EnemyHull = hull;
        EnemyLayout = layout;
    }

    public static bool HasCompleteShipSelection =>
        PlayerHull != null &&
        PlayerLayout != null &&
        EnemyHull != null &&
        EnemyLayout != null;
}
