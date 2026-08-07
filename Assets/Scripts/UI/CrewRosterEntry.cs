using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class CrewRosterEntry : MonoBehaviour
{
    [SerializeField] private RawImage portrait;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject selectionHighlight;

    private Button button;
    private CrewUnit unit;
    private CrewHealth health;
    private CrewCommandController commandController;
    private string lastStatus;
    private bool healthWasInitialized;
    private RuntimeWorldIconCamera portraitCamera;

    public CrewUnit Unit => unit;

    internal void ApplyButtonSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return;
        }

        Image background = GetComponent<Image>();

        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        RuntimeUiVisuals.ApplyUndimmedButton(button, background, sprite);
    }

    private void Awake()
    {
        EnsureView();
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    private void EnsureView()
    {
        if (nameText != null && healthFill != null && healthText != null &&
            statusText != null && selectionHighlight != null)
        {
            return;
        }

        RectTransform rootRect = (RectTransform)transform;
        rootRect.sizeDelta = new Vector2(272f, 94f);

        Image background = GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
        }
        background.color = Color.white;

        LayoutElement layout = GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = gameObject.AddComponent<LayoutElement>();
        }
        layout.preferredHeight = 94f;
        layout.minHeight = 94f;

        selectionHighlight = CreateImage("SelectionHighlight", transform,
            new Color(1f, 0.76f, 0.16f, 0.32f)).gameObject;
        Stretch((RectTransform)selectionHighlight.transform, 0f);

        portrait = CreateRawImage("Portrait", transform);
        SetRect(portrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(48f, 48f), new Vector2(34f, 0f), new Vector2(0.5f, 0.5f));

        nameText = CreateText("NameText", transform, 16f, TextAlignmentOptions.Left);
        SetRect(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(-82f, 24f), new Vector2(78f, -7f), new Vector2(0f, 1f));

        RectTransform bar = CreateRect("HealthBar", transform);
        SetRect(bar, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-92f, 14f), new Vector2(78f, 2f), new Vector2(0f, 0.5f));
        Image barBackground = CreateImage("Background", bar, new Color(0.18f, 0.06f, 0.06f, 1f));
        Stretch(barBackground.rectTransform, 0f);
        healthFill = CreateImage("Fill", bar, new Color(0.20f, 0.78f, 0.32f, 1f));
        Stretch(healthFill.rectTransform, 0f);
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;

        healthText = CreateText("HealthText", transform, 11f, TextAlignmentOptions.Center);
        SetRect(healthText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-92f, 16f), new Vector2(78f, 2f), new Vector2(0f, 0.5f));

        statusText = CreateText("StatusText", transform, 13f, TextAlignmentOptions.Left);
        statusText.color = new Color(0.72f, 0.78f, 0.84f, 1f);
        SetRect(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(-86f, 22f), new Vector2(78f, 7f), new Vector2(0f, 0f));

        selectionHighlight.SetActive(false);
    }

    public void Initialize(CrewUnit target, CrewCommandController controller)
    {
        Unsubscribe();

        unit = target;
        commandController = controller;
        health = unit != null ? unit.GetComponent<CrewHealth>() : null;
        healthWasInitialized = health != null && health.IsInitialized;

        if (health != null)
        {
            health.HealthChanged += HandleHealthChanged;
            health.Died += HandleDied;
        }

        CrewUnit.SelectionChanged += HandleSelectionChanged;

        EnsurePortraitCamera();

        if (nameText != null)
        {
            nameText.text = unit != null
                ? unit.DisplayName
                : CrewNameGenerator.DefaultName;
        }

        RefreshHealth();
        RefreshSelection();
        RefreshStatus(force: true);
    }

    private void Update()
    {
        if (health != null && !healthWasInitialized && health.IsInitialized)
        {
            healthWasInitialized = true;
            RefreshHealth();
        }

        if (unit != null)
        {
            RefreshStatus(force: false);
        }
    }

    private void HandleClick()
    {
        if (unit != null && commandController != null)
        {
            commandController.SelectUnit(unit);
        }
    }

    private void HandleHealthChanged(CrewHealth changedHealth)
    {
        if (changedHealth == health)
        {
            RefreshHealth();
        }
    }

    private void HandleDied(CrewHealth deadHealth)
    {
        if (deadHealth == health && button != null)
        {
            button.interactable = false;
        }
    }

    private void HandleSelectionChanged(CrewUnit changedUnit, bool selected)
    {
        if (changedUnit == unit)
        {
            RefreshSelection();
        }
    }

    private void RefreshHealth()
    {
        int current = health != null
            ? health.IsInitialized ? health.CurrentHealth : health.MaxHealth
            : 0;
        int maximum = health != null ? health.MaxHealth : 0;
        float ratio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;

        if (healthText != null)
        {
            healthText.text = $"{current}/{maximum}";
        }

        if (healthFill != null)
        {
            healthFill.fillAmount = ratio;
        }
    }

    private void RefreshSelection()
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.SetActive(unit != null && unit.IsSelected);
        }
    }

    private void RefreshStatus(bool force)
    {
        string status = BuildStatus();

        if (!force && status == lastStatus)
        {
            return;
        }

        lastStatus = status;

        if (statusText != null)
        {
            statusText.text = status;
        }
    }

    private string BuildStatus()
    {
        if (unit == null)
        {
            return "Unavailable";
        }

        if (health != null && health.IsDead)
        {
            return "Dead";
        }

        if (unit.IsMoving)
        {
            return "Moving";
        }

        if (unit.CurrentCell == null)
        {
            return "Idle";
        }

        string room = $"Room {unit.CurrentCell.RoomId}";

        if (health != null && health.IsHealingInBunk)
        {
            return $"{room} · Healing";
        }

        if (unit.IsRepairing)
        {
            return $"{room} · Repairing";
        }

        if (unit.CurrentCell.IsControlPoint && unit.CurrentCell.Room != null &&
            unit.CurrentCell.Room.Operator == unit)
        {
            return $"{room} · Operating";
        }

        return room;
    }

    private void Unsubscribe()
    {
        CrewUnit.SelectionChanged -= HandleSelectionChanged;

        if (health != null)
        {
            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandleDied;
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (portraitCamera != null)
        {
            portraitCamera.Dispose();
            portraitCamera = null;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = LayerMask.NameToLayer("UI");
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RawImage CreateRawImage(string objectName, Transform parent)
    {
        RectTransform rect = CreateRect(objectName, parent);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private void EnsurePortraitCamera()
    {
        Transform cameraTarget = unit != null ? unit.transform : null;

        if (portraitCamera == null)
        {
            portraitCamera = RuntimeWorldIconCamera.Create(
                $"{name}_PortraitCamera",
                portrait,
                cameraTarget,
                0.65f
            );
            return;
        }

        portraitCamera.SetTarget(cameraTarget);
    }

    private static TMP_Text CreateText(string objectName, Transform parent, float size,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 size, Vector2 position, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }
}
