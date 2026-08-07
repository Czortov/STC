using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BattleResultPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Button mainMenuButton;

    [Header("Scene transition")]
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    private void Awake()
    {
        Hide();
    }

    public static BattleResultPanel Create(
        Transform canvasTransform,
        Sprite windowBackground)
    {
        GameObject root = CreateUiObject(
            "BattleResultPanel",
            canvasTransform
        );

        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        GameObject dim = CreateUiObject("DimBackground", root.transform);
        Stretch(dim.GetComponent<RectTransform>());
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.78f);
        dimImage.raycastTarget = true;

        GameObject window = CreateUiObject("Window", root.transform);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(620f, 360f);
        Image windowImage = window.AddComponent<Image>();
        windowImage.sprite = windowBackground;
        windowImage.type = windowBackground != null
            ? Image.Type.Sliced
            : Image.Type.Simple;
        windowImage.color = windowBackground != null
            ? Color.white
            : new Color(0.10f, 0.12f, 0.16f, 0.98f);

        TMP_Text createdResultText = CreateText(
            "ResultText",
            window.transform,
            new Vector2(0f, 105f),
            new Vector2(560f, 90f),
            52f,
            FontStyles.Bold
        );

        TMP_Text createdReasonText = CreateText(
            "ReasonText",
            window.transform,
            new Vector2(0f, 15f),
            new Vector2(540f, 80f),
            28f,
            FontStyles.Normal
        );

        GameObject buttonObject = CreateUiObject(
            "MainMenuButton",
            window.transform
        );
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -110f);
        buttonRect.sizeDelta = new Vector2(320f, 70f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.72f, 0.48f, 0.16f, 1f);
        Button createdButton = buttonObject.AddComponent<Button>();
        createdButton.targetGraphic = buttonImage;

        TMP_Text buttonText = CreateText(
            "Text",
            buttonObject.transform,
            Vector2.zero,
            new Vector2(300f, 60f),
            27f,
            FontStyles.Bold
        );
        buttonText.text = "Главное меню";

        BattleResultPanel panel = root.AddComponent<BattleResultPanel>();
        panel.Configure(
            createdResultText,
            createdReasonText,
            createdButton
        );

        return panel;
    }

    private void Configure(
        TMP_Text newResultText,
        TMP_Text newReasonText,
        Button newMainMenuButton)
    {
        resultText = newResultText;
        reasonText = newReasonText;
        mainMenuButton = newMainMenuButton;

        mainMenuButton.onClick.RemoveListener(LoadMainMenu);
        mainMenuButton.onClick.AddListener(LoadMainMenu);
    }

    private void OnDestroy()
    {
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(LoadMainMenu);
        }
    }

    public void Show(
        BattleResult result,
        BattleEndReason reason)
    {
        if (resultText != null)
        {
            resultText.text = result == BattleResult.Victory
                ? "ПОБЕДА"
                : "ПОРАЖЕНИЕ";
        }

        if (reasonText != null)
        {
            reasonText.text = GetReasonText(result, reason);
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError(
                $"{name}: не указано имя сцены главного меню.",
                this
            );
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private static string GetReasonText(
        BattleResult result,
        BattleEndReason reason)
    {
        if (reason == BattleEndReason.HullDestroyed)
        {
            return result == BattleResult.Victory
                ? "Корабль противника потоплен"
                : "Ваш корабль потоплен";
        }

        return result == BattleResult.Victory
            ? "Экипаж противника уничтожен"
            : "Весь ваш экипаж погиб";
    }

    private static GameObject CreateUiObject(
        string objectName,
        Transform parent)
    {
        GameObject uiObject = new GameObject(
            objectName,
            typeof(RectTransform)
        );
        uiObject.layer = LayerMask.NameToLayer("UI");
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        Vector2 position,
        Vector2 size,
        float fontSize,
        FontStyles fontStyle)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
