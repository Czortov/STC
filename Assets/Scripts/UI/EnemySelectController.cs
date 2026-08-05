using UnityEngine;
using UnityEngine.SceneManagement;

public class EnemySelectController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenuScene";

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(MainMenuSceneName);
    }
}