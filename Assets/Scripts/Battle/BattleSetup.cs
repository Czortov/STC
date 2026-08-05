using TMPro;
using UnityEngine;

public class BattleSetup : MonoBehaviour
{
    [SerializeField] private TMP_Text enemyNameText;

    private void Start()
    {
        string enemyName = GetEnemyName(GameSession.SelectedEnemy);

        if (enemyNameText != null)
        {
            enemyNameText.text = $"Противник: {enemyName}";
        }
        else
        {
            Debug.LogWarning("В BattleSetup не назначен Enemy Name Text.");
        }

        Debug.Log($"Выбранный противник: {GameSession.SelectedEnemy}");
    }

    private string GetEnemyName(EnemyId enemyId)
    {
        switch (enemyId)
        {
            case EnemyId.MerchantSloop:
                return "Торговый шлюп";

            case EnemyId.NavyBrig:
                return "Военный бриг";

            default:
                return "Не выбран";
        }
    }
}