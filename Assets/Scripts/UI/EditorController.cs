using UnityEngine;
using UnityEngine.SceneManagement;

public class EditorController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenuScene";

    public void BackToMainMenu()
    {
        SceneManager.LoadScene(MainMenuSceneName);
    }
}