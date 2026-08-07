using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    private const string EnemySelectSceneName = "EnemySelectScene";
    private const string EditorSceneName = "EditorScene";

    public void StartGame()
    {
        SceneManager.LoadScene(EnemySelectSceneName);
    }

    public void Editor()
    {
        SceneManager.LoadScene(EditorSceneName);
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
