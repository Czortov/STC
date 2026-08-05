using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemyCard : MonoBehaviour
{
    [SerializeField] private EnemyId enemyId = EnemyId.MerchantSloop;
    [SerializeField] private string battleSceneName = "BattleScene";

    public void StartBattle()
    {
        if (enemyId == EnemyId.None)
        {
            Debug.LogError($"У карточки {gameObject.name} не выбран противник.");
            return;
        }

        GameSession.SelectEnemy(enemyId);
        SceneManager.LoadScene(battleSceneName);
    }
}