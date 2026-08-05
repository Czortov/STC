public static class GameSession
{
    public static EnemyId SelectedEnemy { get; private set; } = EnemyId.None;

    public static void SelectEnemy(EnemyId enemyId)
    {
        SelectedEnemy = enemyId;
    }
}