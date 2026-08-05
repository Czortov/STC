using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    private const string EnemySelectSceneName = "EnemySelectScene";

    public void StartGame()
    {
        SceneManager.LoadScene(EnemySelectSceneName);
    }

    public void ExitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
