using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerCrewPanel : MonoBehaviour
{
    private const int MaxVisibleEntries = 5;
    private const float EntryHeight = 94f;
    private const float EntrySpacing = 6f;
    private const float VerticalPadding = 20f;

    [SerializeField] private CrewRosterEntry entryPrefab;
    [SerializeField] private Transform content;
    [SerializeField] private CrewCommandController commandController;

    private readonly Dictionary<CrewUnit, CrewRosterEntry> entries =
        new Dictionary<CrewUnit, CrewRosterEntry>();

    private RectTransform panelRect;
    private ScrollRect scrollRect;

    private void Awake()
    {
        EnsureView();

        if (commandController == null)
        {
            commandController = FindAnyObjectByType<CrewCommandController>();
        }
    }

    private void OnEnable()
    {
        CrewUnit.UnitRegistered += HandleUnitRegistered;
        CrewUnit.UnitUnregistered += HandleUnitUnregistered;
        SynchronizeExistingUnits();
    }

    private void OnDisable()
    {
        CrewUnit.UnitRegistered -= HandleUnitRegistered;
        CrewUnit.UnitUnregistered -= HandleUnitUnregistered;
    }

    private void SynchronizeExistingUnits()
    {
        List<CrewUnit> snapshot = new List<CrewUnit>(CrewUnit.ActiveUnits);

        foreach (CrewUnit unit in snapshot)
        {
            HandleUnitRegistered(unit);
        }

        List<CrewUnit> staleUnits = new List<CrewUnit>();

        foreach (KeyValuePair<CrewUnit, CrewRosterEntry> pair in entries)
        {
            if (pair.Key == null || !snapshot.Contains(pair.Key) || !pair.Key.IsPlayerOwned)
            {
                staleUnits.Add(pair.Key);
            }
        }

        foreach (CrewUnit staleUnit in staleUnits)
        {
            RemoveEntry(staleUnit);
        }
    }

    private void HandleUnitRegistered(CrewUnit unit)
    {
        if (unit == null || !unit.IsPlayerOwned || entries.ContainsKey(unit))
        {
            return;
        }

        if (entryPrefab == null || content == null || commandController == null)
        {
            Debug.LogError(
                "PlayerCrewPanel: assign Entry Prefab, Content and Command Controller in the Inspector.",
                this
            );
            return;
        }

        CrewRosterEntry entry = Instantiate(entryPrefab, content);
        entry.name = $"CrewEntry_{unit.name}";
        entry.Initialize(unit, commandController);
        entries.Add(unit, entry);
        RefreshPanelSize();
    }

    private void HandleUnitUnregistered(CrewUnit unit)
    {
        RemoveEntry(unit);
    }

    private void RemoveEntry(CrewUnit unit)
    {
        if (!entries.TryGetValue(unit, out CrewRosterEntry entry))
        {
            return;
        }

        entries.Remove(unit);

        if (entry != null)
        {
            Destroy(entry.gameObject);
        }

        RefreshPanelSize();
    }

    private void EnsureView()
    {
        if (content != null)
        {
            return;
        }

        panelRect = (RectTransform)transform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(16f, -120f);
        panelRect.sizeDelta = new Vector2(300f, 760f);

        Image background = CreateImage("Background", transform,
            new Color(0.025f, 0.035f, 0.055f, 0.88f));
        Stretch(background.rectTransform, 0f);
        background.raycastTarget = false;

        Image scrollImage = CreateImage("ScrollView", transform, Color.clear);
        Stretch(scrollImage.rectTransform, 10f);
        scrollRect = scrollImage.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        Image viewportImage = CreateImage("Viewport", scrollImage.transform,
            new Color(1f, 1f, 1f, 0.01f));
        Stretch(viewportImage.rectTransform, 0f);
        viewportImage.gameObject.AddComponent<RectMask2D>();

        RectTransform contentRect = CreateRect("Content", viewportImage.transform);
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportImage.rectTransform;
        scrollRect.content = contentRect;
        content = contentRect;
        RefreshPanelSize();
    }

    private void RefreshPanelSize()
    {
        if (panelRect == null)
        {
            panelRect = (RectTransform)transform;
        }

        int visibleCount = Mathf.Min(entries.Count, MaxVisibleEntries);
        float entriesHeight = visibleCount > 0
            ? visibleCount * EntryHeight + (visibleCount - 1) * EntrySpacing
            : 0f;

        panelRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            entriesHeight + (visibleCount > 0 ? VerticalPadding : 0f)
        );

        if (scrollRect != null)
        {
            scrollRect.vertical = entries.Count > MaxVisibleEntries;
            scrollRect.verticalNormalizedPosition = 1f;
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

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
