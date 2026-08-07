using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerCannonPanel : MonoBehaviour
{
    private const int Columns = 4;
    private const int MaxVisibleCannons = 16;

    [Header("References")]
    [SerializeField] private CannonTargetingController targetingController;
    [SerializeField] private CannonCardUI cannonCardPrefab;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private BottomUpGridLayoutGroup grid;

    [Header("Runtime UI Theme")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite buttonSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 normalCellSize = new Vector2(160f, 100f);
    [SerializeField] private Vector2 compactCellSize = new Vector2(80f, 50f);
    [SerializeField] private Vector2 normalSpacing = new Vector2(8f, 8f);
    [SerializeField] private Vector2 compactSpacing = new Vector2(6f, 6f);
    [SerializeField] private Vector2 screenMargin = new Vector2(16f, 16f);
    [SerializeField] private int panelPadding = 10;

    private readonly Dictionary<CannonSystemRuntime, CannonCardUI> cards =
        new Dictionary<CannonSystemRuntime, CannonCardUI>();
    private readonly List<CannonSystemRuntime> orderedCannons = new List<CannonSystemRuntime>();

    private RectTransform panelRect;

    private void Awake()
    {
        panelRect = (RectTransform)transform;
        if (targetingController == null)
        {
            targetingController = FindAnyObjectByType<CannonTargetingController>();
        }

        EnsureView();
    }

    private void OnEnable()
    {
        CannonSystemRuntime.CannonRegistered += HandleCannonRegistered;
        CannonSystemRuntime.CannonUnregistered += HandleCannonUnregistered;
        SynchronizeExistingCannons();
    }

    private void OnDisable()
    {
        CannonSystemRuntime.CannonRegistered -= HandleCannonRegistered;
        CannonSystemRuntime.CannonUnregistered -= HandleCannonUnregistered;
    }

    private void SynchronizeExistingCannons()
    {
        List<CannonSystemRuntime> snapshot = new List<CannonSystemRuntime>(CannonSystemRuntime.AllCannons);
        foreach (CannonSystemRuntime cannon in snapshot)
        {
            HandleCannonRegistered(cannon);
        }

        List<CannonSystemRuntime> stale = new List<CannonSystemRuntime>();
        foreach (CannonSystemRuntime cannon in cards.Keys)
        {
            if (cannon == null || !snapshot.Contains(cannon) || !IsPlayerCannon(cannon))
            {
                stale.Add(cannon);
            }
        }

        foreach (CannonSystemRuntime cannon in stale)
        {
            RemoveCard(cannon);
        }
    }

    private void HandleCannonRegistered(CannonSystemRuntime cannon)
    {
        if (!IsPlayerCannon(cannon) || cards.ContainsKey(cannon))
        {
            return;
        }

        CannonCardUI card;
        if (cannonCardPrefab != null)
        {
            card = Instantiate(cannonCardPrefab, content);
        }
        else
        {
            Debug.LogWarning("PlayerCannonPanel: Cannon Card Prefab is not assigned; using a runtime card.", this);
            GameObject cardObject = new GameObject("CannonCard", typeof(RectTransform));
            cardObject.layer = LayerMask.NameToLayer("UI");
            cardObject.transform.SetParent(content, false);
            card = cardObject.AddComponent<CannonCardUI>();
        }

        card.name = cannon.Room != null ? $"CannonCard_{cannon.Room.Id}" : "CannonCard";
        card.ApplyButtonSprite(buttonSprite);
        card.Bind(cannon, targetingController);
        cards.Add(cannon, card);
        RebuildLayout();
    }

    private void HandleCannonUnregistered(CannonSystemRuntime cannon) => RemoveCard(cannon);

    private void RemoveCard(CannonSystemRuntime cannon)
    {
        if (!cards.TryGetValue(cannon, out CannonCardUI card))
        {
            return;
        }

        cards.Remove(cannon);
        if (card != null)
        {
            Destroy(card.gameObject);
        }
        RebuildLayout();
    }

    private bool IsPlayerCannon(CannonSystemRuntime cannon)
    {
        if (cannon == null || cannon.Room == null || targetingController == null ||
            targetingController.PlayerShip == null)
        {
            return false;
        }

        return cannon.Room.GetComponentInParent<ShipIdentity>() == targetingController.PlayerShip;
    }

    private void RebuildLayout()
    {
        orderedCannons.Clear();
        orderedCannons.AddRange(cards.Keys);
        orderedCannons.Sort(CompareCannons);

        bool compact = orderedCannons.Count > Columns;
        Vector2 cellSize = compact ? compactCellSize : normalCellSize;
        Vector2 spacing = compact ? compactSpacing : normalSpacing;
        CannonCardDisplayMode mode = compact ? CannonCardDisplayMode.Compact : CannonCardDisplayMode.Normal;

        for (int i = 0; i < orderedCannons.Count; i++)
        {
            CannonCardUI card = cards[orderedCannons[i]];
            card.transform.SetSiblingIndex(i);
            card.SetDisplayMode(mode);
        }

        grid.Columns = Columns;
        grid.CellSize = cellSize;
        grid.Spacing = spacing;

        int rows = Mathf.CeilToInt(orderedCannons.Count / (float)Columns);
        int visibleRows = Mathf.Min(rows, MaxVisibleCannons / Columns);
        int usedColumns = Mathf.Min(Columns, orderedCannons.Count);
        float contentHeight = panelPadding * 2f + rows * cellSize.y + Mathf.Max(0, rows - 1) * spacing.y;
        float viewportHeight = panelPadding * 2f + visibleRows * cellSize.y + Mathf.Max(0, visibleRows - 1) * spacing.y;
        float panelWidth = panelPadding * 2f + usedColumns * cellSize.x + Mathf.Max(0, usedColumns - 1) * spacing.x;

        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(viewportHeight, contentHeight));
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, panelWidth));
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, viewportHeight));

        bool enableScrolling = orderedCannons.Count > MaxVisibleCannons;
        scrollRect.vertical = enableScrolling;
        scrollRect.horizontal = false;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private static int CompareCannons(CannonSystemRuntime left, CannonSystemRuntime right)
    {
        int leftId = left != null && left.Room != null ? left.Room.Id : int.MaxValue;
        int rightId = right != null && right.Room != null ? right.Room.Id : int.MaxValue;
        int idComparison = leftId.CompareTo(rightId);
        return idComparison != 0 ? idComparison : string.CompareOrdinal(left?.name, right?.name);
    }

    private void EnsureView()
    {
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.anchoredPosition = screenMargin;

        if (content != null && scrollRect != null && grid != null)
        {
            return;
        }

        Image background = CreateImage("Background", transform, new Color(0.02f, 0.03f, 0.05f, 0.9f));
        RuntimeUiVisuals.ApplySlicedSprite(background, panelSprite);
        Stretch(background.rectTransform, 0f);
        background.raycastTarget = false;

        Image scrollImage = CreateImage("ScrollView", transform, Color.clear);
        Stretch(scrollImage.rectTransform, 0f);
        scrollRect = scrollImage.gameObject.AddComponent<ScrollRect>();
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 28f;

        Image viewportImage = CreateImage("Viewport", scrollImage.transform, new Color(1f, 1f, 1f, 0.01f));
        Stretch(viewportImage.rectTransform, 0f);
        viewportImage.gameObject.AddComponent<RectMask2D>();
        viewport = viewportImage.rectTransform;

        content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(1f, 0f);
        content.pivot = new Vector2(0.5f, 0f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        grid = content.gameObject.AddComponent<BottomUpGridLayoutGroup>();
        grid.padding = new RectOffset(panelPadding, panelPadding, panelPadding, panelPadding);

        scrollRect.viewport = viewport;
        scrollRect.content = content;
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

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

}
