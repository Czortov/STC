using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum CannonCardDisplayMode
{
    Normal,
    Compact
}

[DisallowMultipleComponent]
public sealed class CannonCardUI : MonoBehaviour
{
    [Header("Optional authored view")]
    [SerializeField] private Image background;
    [SerializeField] private Image selectionHighlight;
    [SerializeField] private Image cannonIcon;
    [SerializeField] private TMP_Text cannonNameText;
    [SerializeField] private TMP_Text ammoCountText;
    [SerializeField] private Image reloadFill;
    [SerializeField] private Button button;

    private CannonSystemRuntime cannon;
    private CannonTargetingController targetingController;
    private CannonCardDisplayMode displayMode;
    private string lastName;
    private string lastAmmo;
    private bool lastSelected;

    public CannonSystemRuntime Cannon => cannon;

    private void Awake()
    {
        EnsureView();
        button.onClick.AddListener(HandleClick);
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshAll();
    }

    private void OnDisable() => Unsubscribe();

    private void Update()
    {
        if (cannon == null)
        {
            return;
        }

        RefreshReloadBar();
        RefreshChangedValues();
    }

    public void Bind(CannonSystemRuntime target, CannonTargetingController controller)
    {
        Unsubscribe();
        cannon = target;
        targetingController = controller;
        Subscribe();
        RefreshAll();
    }

    public void SetDisplayMode(CannonCardDisplayMode mode)
    {
        displayMode = mode;
        bool compact = mode == CannonCardDisplayMode.Compact;
        cannonNameText.fontSizeMin = compact ? 9f : 12f;
        cannonNameText.fontSizeMax = compact ? 14f : 21f;
        ammoCountText.fontSizeMin = compact ? 9f : 10f;
        ammoCountText.fontSizeMax = compact ? 14f : 18f;

        RectTransform iconRect = cannonIcon.rectTransform;
        iconRect.sizeDelta = compact ? new Vector2(24f, 24f) : new Vector2(42f, 42f);

        float textLeft = compact ? 38f : 58f;
        SetHorizontalInsets(cannonNameText.rectTransform, textLeft, 5f);
        SetHorizontalInsets(ammoCountText.rectTransform, textLeft, 5f);
        RefreshAll();
    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (cannon != null)
        {
            cannon.StateChanged -= HandleCannonChanged;
            cannon.StateChanged += HandleCannonChanged;
        }

        if (targetingController != null)
        {
            targetingController.SelectedCannonChanged -= HandleSelectionChanged;
            targetingController.SelectedCannonChanged += HandleSelectionChanged;
        }
    }

    private void Unsubscribe()
    {
        if (cannon != null)
        {
            cannon.StateChanged -= HandleCannonChanged;
        }

        if (targetingController != null)
        {
            targetingController.SelectedCannonChanged -= HandleSelectionChanged;
        }
    }

    private void HandleClick()
    {
        if (cannon != null && targetingController != null)
        {
            targetingController.TrySelectCannon(cannon);
        }
    }

    private void HandleCannonChanged(CannonSystemRuntime _) => RefreshAll();
    private void HandleSelectionChanged(CannonSystemRuntime _) => RefreshSelection();

    private void RefreshAll()
    {
        lastName = null;
        lastAmmo = null;
        RefreshChangedValues();
    }

    private void RefreshChangedValues()
    {
        if (cannon == null)
        {
            return;
        }

        string cannonName = cannon.Room != null ? $"Пушка {cannon.Room.Id}" : "Пушка";
        string ammoName = cannon.LoadedAmmo != null ? cannon.LoadedAmmo.DisplayName : "Нет боеприпаса";
        string ammo = displayMode == CannonCardDisplayMode.Compact
            ? cannon.CurrentAmmoCount.ToString()
            : $"{ammoName}: {cannon.CurrentAmmoCount}/{cannon.CurrentAmmoMaximum}";
        if (lastName != cannonName)
        {
            lastName = cannonName;
            cannonNameText.text = cannonName;
        }

        if (lastAmmo != ammo)
        {
            lastAmmo = ammo;
            ammoCountText.text = ammo;
        }

        RefreshReloadBar();
        RefreshSelection();
    }

    private void RefreshReloadBar()
    {
        reloadFill.fillAmount = cannon.ReloadProgress01;

        if (cannon.Room == null || cannon.Room.IsDestroyed)
        {
            reloadFill.color = new Color(0.45f, 0.08f, 0.08f, 1f);
            return;
        }

        if (cannon.Room.Operator == null)
        {
            reloadFill.color = new Color(0.95f, 0.16f, 0.12f, 1f);
            return;
        }

        if (!cannon.Room.IsFullyRepaired)
        {
            reloadFill.color = new Color(1f, 0.38f, 0.06f, 1f);
            return;
        }

        if (!cannon.HasCurrentAmmo)
        {
            reloadFill.color = new Color(0.4f, 0.43f, 0.48f, 1f);
            return;
        }

        reloadFill.color = cannon.IsLoaded
            ? new Color(0.2f, 0.85f, 0.3f, 1f)
            : new Color(1f, 0.8f, 0.08f, 1f);
    }

    private void RefreshSelection()
    {
        bool selected = targetingController != null && targetingController.SelectedCannon == cannon;
        if (lastSelected == selected && selectionHighlight.enabled == selected)
        {
            return;
        }

        lastSelected = selected;
        selectionHighlight.enabled = selected;
    }

    private void EnsureView()
    {
        if (button != null && cannonNameText != null && reloadFill != null)
        {
            return;
        }

        background = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.085f, 0.96f);
        button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        selectionHighlight = CreateImage("SelectionHighlight", transform, new Color(0.18f, 0.85f, 1f, 0.32f));
        Stretch(selectionHighlight.rectTransform, 2f);
        selectionHighlight.raycastTarget = false;

        cannonIcon = CreateImage("CannonIcon", transform, new Color(0.72f, 0.75f, 0.78f, 1f));
        SetRect(cannonIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(10f, 0f), new Vector2(42f, 42f), new Vector2(0f, 0.5f));

        cannonNameText = CreateText("CannonNameText", transform, TextAlignmentOptions.TopLeft);
        cannonNameText.enableAutoSizing = true;
        cannonNameText.fontSizeMin = 12f;
        cannonNameText.fontSizeMax = 21f;
        cannonNameText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(cannonNameText.rectTransform, new Vector2(0f, 0.45f), new Vector2(1f, 1f),
            new Vector2(58f, -6f), new Vector2(-64f, -4f), new Vector2(0f, 1f));

        ammoCountText = CreateText("AmmoCountText", transform, TextAlignmentOptions.BottomLeft);
        ammoCountText.enableAutoSizing = true;
        ammoCountText.fontSizeMin = 10f;
        ammoCountText.fontSizeMax = 18f;
        ammoCountText.overflowMode = TextOverflowModes.Ellipsis;
        SetRect(ammoCountText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.55f),
            new Vector2(58f, 13f), new Vector2(-6f, -2f), new Vector2(0f, 0f));

        Image reloadBackground = CreateImage("ReloadBackground", transform, new Color(0f, 0f, 0f, 0.75f));
        SetRect(reloadBackground.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(5f, 5f), new Vector2(-5f, 10f), new Vector2(0.5f, 0f));
        reloadFill = CreateImage("ReloadFill", reloadBackground.transform, new Color(1f, 0.8f, 0.08f, 1f));
        Stretch(reloadFill.rectTransform, 1f);
        reloadFill.type = Image.Type.Filled;
        reloadFill.fillMethod = Image.FillMethod.Horizontal;
        reloadFill.fillOrigin = 0;

    }

    private static TMP_Text CreateText(string objectName, Transform parent, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.alignment = alignment;
        text.fontSize = 20f;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        child.layer = LayerMask.NameToLayer("UI");
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetHorizontalInsets(RectTransform rect, float left, float right)
    {
        rect.offsetMin = new Vector2(left, rect.offsetMin.y);
        rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
    }
}
