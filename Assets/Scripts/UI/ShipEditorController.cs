using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class ShipEditorController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenuScene";
    private const string LoadoutFolder = "Assets/Data/Ships/Layout";

    [SerializeField] private ShipCatalogDefinition shipCatalog;
    [SerializeField] private ShipEditorModuleEntry moduleEntryPrefab;
    [SerializeField, Min(0f)] private float previewPadding = 42f;

    private readonly List<ShipHullDefinition> hulls = new List<ShipHullDefinition>();
    private readonly List<ShipLayoutDefinition> loadouts = new List<ShipLayoutDefinition>();
    private readonly List<ShipEditorModuleEntry> moduleEntries = new List<ShipEditorModuleEntry>();

    private TMP_Dropdown hullDropdown;
    private TMP_Dropdown loadoutDropdown;
    private TMP_InputField nameInput;
    private TMP_Text statusText;
    private Button saveButton;
    private Button duplicateButton;
    private Button deleteButton;
    private RectTransform moduleContent;
    private ShipEditorPreview preview;
    private EditorConfirmDialog confirmDialog;

    private ShipHullDefinition selectedHull;
    private ShipLayoutDefinition selectedAsset;
    private ShipLayoutDefinition workingDefinition;
    private string workingMatrix = string.Empty;
    private string savedMatrix = string.Empty;
    private string savedName = string.Empty;
    private bool suppressUiEvents;
    private bool isDirty;

    public ShipModuleType SelectedModule { get; private set; }
    public float PreviewPadding => previewPadding;

    protected virtual void Awake()
    {
        ResolveCatalog();
        BuildInterface();
        workingDefinition = ScriptableObject.CreateInstance<ShipLayoutDefinition>();
        workingDefinition.hideFlags = HideFlags.HideAndDontSave;
    }

    protected virtual void Start()
    {
        InitializeData();
    }

    protected virtual void OnDestroy()
    {
        if (workingDefinition != null)
        {
            Destroy(workingDefinition);
        }
    }

    public void BackToMainMenu()
    {
        RunAfterDiscardConfirmation(() => SceneManager.LoadScene(MainMenuSceneName));
    }

    public void SelectModule(ShipModuleType moduleType)
    {
        SelectedModule = SelectedModule == moduleType
            ? ShipModuleType.None
            : moduleType;

        foreach (ShipEditorModuleEntry entry in moduleEntries)
        {
            entry.SetSelected(entry.ModuleType == SelectedModule);
        }
    }

    public void SetModule(
        int x,
        int y,
        ShipModuleType moduleType)
    {
        if (selectedHull == null ||
            !ShipEditorLayoutUtility.TryGetHullCell(selectedHull.HullMatrix, x, y, out HullCellType hullType) ||
            !hullType.IsRoom())
        {
            return;
        }

        if (!ShipEditorLayoutUtility.TryReplaceModule(ref workingMatrix, x, y, moduleType))
        {
            return;
        }

        MarkDirty();
        RebuildPreview();
    }

    private void ResolveCatalog()
    {
#if UNITY_EDITOR
        if (shipCatalog == null)
        {
            shipCatalog = AssetDatabase.LoadAssetAtPath<ShipCatalogDefinition>(
                "Assets/Data/Ships/ShipCatalog.asset"
            );
        }

        if (moduleEntryPrefab == null)
        {
            moduleEntryPrefab = AssetDatabase.LoadAssetAtPath<ShipEditorModuleEntry>(
                "Assets/Prefabs/UI/ModulePaletteEntry.prefab"
            );
        }
#endif
    }

    private void InitializeData()
    {
        hulls.Clear();

        if (shipCatalog == null)
        {
            SetStatus("Каталог кораблей не назначен.", true);
            SetEditingEnabled(false);
            return;
        }

        shipCatalog.ApplyToHulls();

        foreach (ShipCatalogEntry entry in shipCatalog.Entries)
        {
            if (entry?.Hull != null && !hulls.Contains(entry.Hull))
            {
                hulls.Add(entry.Hull);
            }
        }

        suppressUiEvents = true;
        hullDropdown.ClearOptions();
        List<string> options = new List<string>();

        foreach (ShipHullDefinition hull in hulls)
        {
            options.Add(hull.HullName);
        }

        hullDropdown.AddOptions(options);
        hullDropdown.SetValueWithoutNotify(0);
        suppressUiEvents = false;

        if (hulls.Count == 0)
        {
            SetStatus("В каталоге нет корпусов.", true);
            SetEditingEnabled(false);
            return;
        }

        SelectHullImmediately(0);
    }

    private void OnHullChanged(int index)
    {
        if (suppressUiEvents || index < 0 || index >= hulls.Count)
        {
            return;
        }

        int previous = Mathf.Max(0, hulls.IndexOf(selectedHull));
        RunAfterDiscardConfirmation(
            () => SelectHullImmediately(index),
            () => hullDropdown.SetValueWithoutNotify(previous)
        );
    }

    private void SelectHullImmediately(int index)
    {
        if (index < 0 || index >= hulls.Count)
        {
            return;
        }

        selectedHull = hulls[index];
        hullDropdown.SetValueWithoutNotify(index);
        RefreshLoadouts();
        BuildModulePalette();

        if (loadouts.Count > 0)
        {
            SelectLoadoutImmediately(0);
        }
        else
        {
            StartNew("Новая комплектация", ShipEditorLayoutUtility.CreateEmptyMatrix(selectedHull.HullMatrix));
            SetDirty(false);
            SetStatus("Для этого корпуса пока нет комплектаций.", false);
        }
    }

    private void RefreshLoadouts()
    {
        loadouts.Clear();

        if (shipCatalog.TryGetLayouts(selectedHull, out IReadOnlyList<ShipLayoutDefinition> catalogLayouts))
        {
            for (int i = 0; i < catalogLayouts.Count; i++)
            {
                ShipLayoutDefinition layout = catalogLayouts[i];

                if (layout != null && layout.ShipType == selectedHull.ShipType)
                {
                    loadouts.Add(layout);
                }
            }
        }

        suppressUiEvents = true;
        loadoutDropdown.ClearOptions();
        List<string> options = new List<string>();

        foreach (ShipLayoutDefinition loadout in loadouts)
        {
            options.Add(loadout.DisplayName);
        }

        if (options.Count == 0)
        {
            options.Add("— нет комплектаций —");
        }

        loadoutDropdown.AddOptions(options);
        loadoutDropdown.SetValueWithoutNotify(0);
        loadoutDropdown.interactable = loadouts.Count > 0;
        suppressUiEvents = false;
    }

    private void OnLoadoutChanged(int index)
    {
        if (suppressUiEvents || index < 0 || index >= loadouts.Count)
        {
            return;
        }

        int previous = Mathf.Max(0, loadouts.IndexOf(selectedAsset));
        RunAfterDiscardConfirmation(
            () => SelectLoadoutImmediately(index),
            () => loadoutDropdown.SetValueWithoutNotify(previous)
        );
    }

    private void SelectLoadoutImmediately(int index)
    {
        if (index < 0 || index >= loadouts.Count)
        {
            return;
        }

        selectedAsset = loadouts[index];
        workingMatrix = selectedAsset.RoomMatrix;
        savedMatrix = workingMatrix;
        savedName = selectedAsset.DisplayName;
        suppressUiEvents = true;
        nameInput.SetTextWithoutNotify(savedName);
        loadoutDropdown.SetValueWithoutNotify(index);
        suppressUiEvents = false;
        SelectModule(SelectedModule);
        SelectedModule = ShipModuleType.None;
        UpdateModuleSelection();
        SetDirty(false);
        SetStatus(string.Empty, false);
        RebuildPreview();
    }

    private void OnNameChanged(string value)
    {
        if (!suppressUiEvents)
        {
            MarkDirty();
        }
    }

    private void NewLoadout()
    {
        RunAfterDiscardConfirmation(
            () => StartNew(
                "Новая комплектация",
                selectedAsset != null
                    ? ShipEditorLayoutUtility.ClearModules(selectedAsset.RoomMatrix)
                    : ShipEditorLayoutUtility.CreateEmptyMatrix(selectedHull.HullMatrix)
            )
        );
    }

    private void DuplicateLoadout()
    {
        if (selectedHull == null)
        {
            return;
        }

        RunAfterDiscardConfirmation(() =>
        {
            string sourceName = string.IsNullOrWhiteSpace(nameInput.text)
                ? "Новая комплектация"
                : nameInput.text.Trim();
            string sourceMatrix = string.IsNullOrWhiteSpace(workingMatrix)
                ? ShipEditorLayoutUtility.CreateEmptyMatrix(selectedHull.HullMatrix)
                : workingMatrix;
            StartNew(sourceName + " (копия)", sourceMatrix);
        });
    }

    private void StartNew(string displayName, string matrix)
    {
        selectedAsset = null;
        workingMatrix = matrix;
        savedMatrix = string.Empty;
        savedName = string.Empty;
        nameInput.SetTextWithoutNotify(displayName);
        loadoutDropdown.SetValueWithoutNotify(0);
        SelectedModule = ShipModuleType.None;
        UpdateModuleSelection();
        SetDirty(true);
        SetStatus("Новая комплектация будет создана при сохранении.", false);
        RebuildPreview();
    }

    private void SaveLoadout()
    {
#if UNITY_EDITOR
        if (selectedHull == null)
        {
            return;
        }

        string displayName = nameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            SetStatus("Введите непустое название комплектации.", true);
            nameInput.ActivateInputField();
            return;
        }

        workingDefinition.SetEditorData(
            displayName,
            selectedHull.ShipType,
            workingMatrix
        );

        if (!ShipBlueprintBuilder.TryCreateEditorData(
                selectedHull,
                workingDefinition,
                out _,
                out List<string> errors))
        {
            SetStatus(errors.Count > 0 ? errors[0] : "Некорректная матрица.", true);
            return;
        }

        if (selectedAsset == null)
        {
            Directory.CreateDirectory(LoadoutFolder);
            selectedAsset = ScriptableObject.CreateInstance<ShipLayoutDefinition>();
            selectedAsset.SetEditorData(displayName, selectedHull.ShipType, workingMatrix);
            string safeName = MakeSafeFileName(displayName);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{LoadoutFolder}/{safeName}.asset"
            );
            AssetDatabase.CreateAsset(selectedAsset, path);
            Undo.RecordObject(shipCatalog, "Add ship loadout");

            if (!shipCatalog.AddLayout(selectedHull, selectedAsset))
            {
                AssetDatabase.DeleteAsset(path);
                selectedAsset = null;
                SetStatus("Не удалось добавить комплектацию в каталог.", true);
                return;
            }

            EditorUtility.SetDirty(shipCatalog);
        }
        else
        {
            Undo.RecordObject(selectedAsset, "Edit ship loadout");
            selectedAsset.SetEditorData(displayName, selectedHull.ShipType, workingMatrix);
        }

        EditorUtility.SetDirty(selectedAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        savedName = displayName;
        savedMatrix = workingMatrix;
        RefreshLoadouts();
        int index = loadouts.IndexOf(selectedAsset);

        if (index >= 0)
        {
            loadoutDropdown.SetValueWithoutNotify(index);
        }

        SetDirty(false);
        SetStatus("Комплектация сохранена.", false);
#else
        SetStatus("Сохранение assets доступно только в Unity Editor.", true);
#endif
    }

    private void DeleteLoadout()
    {
#if UNITY_EDITOR
        if (selectedAsset == null)
        {
            SetStatus("Новая комплектация ещё не сохранена.", true);
            return;
        }

        ShipLayoutDefinition assetToDelete = selectedAsset;
        confirmDialog.Show(
            $"Удалить комплектацию «{assetToDelete.DisplayName}»? Отменить удаление после подтверждения нельзя.",
            () =>
            {
                string path = AssetDatabase.GetAssetPath(assetToDelete);
                Undo.RecordObject(shipCatalog, "Remove ship loadout");
                shipCatalog.RemoveLayout(assetToDelete);
                EditorUtility.SetDirty(shipCatalog);
                AssetDatabase.SaveAssets();

                if (!AssetDatabase.DeleteAsset(path))
                {
                    SetStatus("Unity не смог удалить выбранный asset.", true);
                    return;
                }

                AssetDatabase.Refresh();
                selectedAsset = null;
                RefreshLoadouts();

                if (loadouts.Count > 0)
                {
                    SelectLoadoutImmediately(0);
                }
                else
                {
                    StartNew("Новая комплектация", ShipEditorLayoutUtility.CreateEmptyMatrix(selectedHull.HullMatrix));
                    SetDirty(false);
                }

                SetStatus("Комплектация удалена.", false);
            }
        );
#endif
    }

    private void RebuildPreview()
    {
        if (selectedHull == null || workingDefinition == null)
        {
            preview.Clear();
            return;
        }

        workingDefinition.SetEditorData(
            string.IsNullOrWhiteSpace(nameInput.text) ? "Preview" : nameInput.text,
            selectedHull.ShipType,
            workingMatrix
        );
        Canvas.ForceUpdateCanvases();

        if (ShipBlueprintBuilder.TryCreateEditorData(
                selectedHull,
                workingDefinition,
                out ShipBlueprintData blueprint,
                out List<string> errors))
        {
            preview.Show(selectedHull, blueprint);
            return;
        }

        preview.Clear();
        SetStatus(errors.Count > 0 ? errors[0] : "Не удалось построить preview.", true);
    }

    private void BuildModulePalette()
    {
        foreach (Transform child in moduleContent)
        {
            Destroy(child.gameObject);
        }

        moduleEntries.Clear();
        SelectedModule = ShipModuleType.None;

        foreach (ShipModuleType moduleType in Enum.GetValues(typeof(ShipModuleType)))
        {
            if (moduleType == ShipModuleType.None)
            {
                continue;
            }

            Sprite icon = null;

            if (selectedHull.TryGetModuleVisual(moduleType, out ShipModuleVisualEntry visual))
            {
                visual.TryGetSprite(new System.Random(selectedHull.VisualSeed), out icon);
            }

            CreateModuleEntry(moduleType, icon, GetModuleName(moduleType));
        }
    }

    private void CreateModuleEntry(
        ShipModuleType moduleType,
        Sprite icon,
        string displayName)
    {
        GameObject entryObject;

        if (moduleEntryPrefab != null)
        {
            ShipEditorModuleEntry instance = Instantiate(
                moduleEntryPrefab,
                moduleContent
            );
            entryObject = instance.gameObject;
            entryObject.name = $"Module_{moduleType}";
        }
        else
        {
            entryObject = CreateUiObject(
                $"Module_{moduleType}",
                moduleContent,
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement),
                typeof(ShipEditorModuleEntry)
            );
        }
        Image background = entryObject.GetComponent<Image>();
        Button button = entryObject.GetComponent<Button>();
        button.targetGraphic = background;
        LayoutElement layout = entryObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 64f;
        layout.minHeight = 64f;
        Image iconImage = CreateImage(
            "Icon",
            entryObject.transform as RectTransform,
            new Vector2(8f, 8f),
            new Vector2(56f, 56f)
        );
        iconImage.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        iconImage.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        iconImage.rectTransform.pivot = new Vector2(0f, 0.5f);
        iconImage.preserveAspect = true;
        TMP_Text text = CreateText(
            "NameText",
            entryObject.transform as RectTransform,
            displayName,
            20f,
            TextAlignmentOptions.MidlineLeft
        );
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(72f, 4f);
        text.rectTransform.offsetMax = new Vector2(-8f, -4f);
        ShipEditorModuleEntry entry = entryObject.GetComponent<ShipEditorModuleEntry>();
        entry.Initialize(this, moduleType, icon, displayName, background, iconImage, text, button);
        moduleEntries.Add(entry);
    }

    private void UpdateModuleSelection()
    {
        foreach (ShipEditorModuleEntry entry in moduleEntries)
        {
            entry.SetSelected(entry.ModuleType == SelectedModule);
        }
    }

    private void MarkDirty()
    {
        string currentName = nameInput.text.Trim();
        bool dirty = selectedAsset == null ||
            currentName != savedName ||
            workingMatrix != savedMatrix;
        SetDirty(dirty);
    }

    private void SetDirty(bool dirty)
    {
        isDirty = dirty;
        saveButton.interactable = selectedHull != null && dirty;
        deleteButton.interactable = selectedAsset != null;
        duplicateButton.interactable = selectedHull != null;
    }

    private void RunAfterDiscardConfirmation(
        Action action,
        Action cancelled = null)
    {
        if (!isDirty)
        {
            action?.Invoke();
            return;
        }

        confirmDialog.Show(
            "Есть несохранённые изменения. Продолжить без сохранения?",
            action
        );

        if (cancelled != null)
        {
            // Dropdown already shows the requested value; restore it while
            // the modal is open. Confirmed action will set the new value.
            cancelled.Invoke();
        }
    }

    private void SetEditingEnabled(bool enabled)
    {
        hullDropdown.interactable = enabled;
        loadoutDropdown.interactable = enabled;
        nameInput.interactable = enabled;
        saveButton.interactable = enabled;
        duplicateButton.interactable = enabled;
        deleteButton.interactable = enabled;
    }

    private void SetStatus(string message, bool error)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message;
        statusText.color = error
            ? new Color(1f, 0.42f, 0.36f, 1f)
            : new Color(0.72f, 0.9f, 0.78f, 1f);
    }

    private void BuildInterface()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();

        if (canvas == null)
        {
            throw new InvalidOperationException("EditorScene must contain a Canvas.");
        }

        canvas.name = "EditorCanvas";
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();

        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        foreach (Transform child in canvas.transform)
        {
            child.gameObject.SetActive(false);
        }

        RectTransform root = CreatePanel(
            "ShipEditorUI",
            canvas.transform as RectTransform,
            new Color(0.055f, 0.075f, 0.105f, 0.96f)
        );
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        BuildTopBar(root);
        BuildModulePanel(root);
        BuildPreviewPanel(root);
        BuildActions(root);
        BuildConfirmDialog(root);
    }

    private void BuildTopBar(RectTransform root)
    {
        RectTransform top = CreatePanel("TopBar", root, new Color(0.1f, 0.14f, 0.19f, 0.98f));
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        top.pivot = new Vector2(0.5f, 1f);
        top.sizeDelta = new Vector2(0f, 94f);
        top.anchoredPosition = Vector2.zero;

        CreateLabelAt(top, "HullLabel", "Корпус", 18f, 18f, 68f, 54f);
        hullDropdown = CreateDropdown(top, "HullDropdown", 88f, 18f, 220f, 56f);
        hullDropdown.onValueChanged.AddListener(OnHullChanged);
        CreateLabelAt(top, "LoadoutLabel", "Комплектация", 330f, 18f, 130f, 54f);
        loadoutDropdown = CreateDropdown(top, "LoadoutDropdown", 468f, 18f, 260f, 56f);
        loadoutDropdown.onValueChanged.AddListener(OnLoadoutChanged);
        nameInput = CreateInput(top, "LoadoutNameInput", 746f, 18f, 370f, 56f);
        nameInput.onValueChanged.AddListener(OnNameChanged);
        saveButton = CreateButton(top, "SaveButton", "Сохранить", 1134f, 18f, 180f, 56f, SaveLoadout);
        CreateButton(top, "BackButton", "В меню", 1332f, 18f, 180f, 56f, BackToMainMenu);
    }

    private void BuildModulePanel(RectTransform root)
    {
        RectTransform panel = CreatePanel("ModulePanel", root, new Color(0.08f, 0.11f, 0.15f, 0.98f));
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(16f, -28f);
        panel.sizeDelta = new Vector2(300f, -142f);
        TMP_Text title = CreateText("Title", panel, "Модули", 26f, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(0f, 50f);

        ScrollRect scroll = CreateScrollView(panel, out moduleContent);
        RectTransform scrollRect = scroll.transform as RectTransform;
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(12f, 12f);
        scrollRect.offsetMax = new Vector2(-12f, -54f);
    }

    private void BuildPreviewPanel(RectTransform root)
    {
        RectTransform panel = CreatePanel("ShipPreviewPanel", root, new Color(0.035f, 0.055f, 0.08f, 0.97f));
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = new Vector2(332f, 104f);
        panel.offsetMax = new Vector2(-20f, -110f);
        RectTransform editorRoot = CreatePanel("ShipEditorRoot", panel, Color.clear);
        editorRoot.anchorMin = Vector2.zero;
        editorRoot.anchorMax = Vector2.one;
        editorRoot.offsetMin = new Vector2(12f, 12f);
        editorRoot.offsetMax = new Vector2(-12f, -12f);
        preview = editorRoot.gameObject.AddComponent<ShipEditorPreview>();
        preview.Initialize(this);
    }

    private void BuildActions(RectTransform root)
    {
        RectTransform actions = CreatePanel("LoadoutActions", root, new Color(0.1f, 0.14f, 0.19f, 0.98f));
        actions.anchorMin = new Vector2(0f, 0f);
        actions.anchorMax = new Vector2(1f, 0f);
        actions.pivot = new Vector2(0.5f, 0f);
        actions.sizeDelta = new Vector2(0f, 88f);
        actions.anchoredPosition = Vector2.zero;
        CreateButton(actions, "NewButton", "Новая", 332f, 16f, 180f, 54f, NewLoadout);
        duplicateButton = CreateButton(actions, "DuplicateButton", "Дублировать", 528f, 16f, 210f, 54f, DuplicateLoadout);
        deleteButton = CreateButton(actions, "DeleteButton", "Удалить", 754f, 16f, 180f, 54f, DeleteLoadout);
        statusText = CreateText("StatusText", actions, string.Empty, 18f, TextAlignmentOptions.MidlineLeft);
        statusText.rectTransform.anchorMin = new Vector2(0f, 0f);
        statusText.rectTransform.anchorMax = new Vector2(1f, 1f);
        statusText.rectTransform.offsetMin = new Vector2(956f, 10f);
        statusText.rectTransform.offsetMax = new Vector2(-20f, -10f);
    }

    private void BuildConfirmDialog(RectTransform root)
    {
        RectTransform overlay = CreatePanel("ConfirmDialog", root, new Color(0f, 0f, 0f, 0.72f));
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        RectTransform box = CreatePanel("DialogBox", overlay, new Color(0.11f, 0.15f, 0.2f, 1f));
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(650f, 260f);
        TMP_Text message = CreateText("Message", box, string.Empty, 24f, TextAlignmentOptions.Center);
        message.rectTransform.anchorMin = Vector2.zero;
        message.rectTransform.anchorMax = Vector2.one;
        message.rectTransform.offsetMin = new Vector2(36f, 90f);
        message.rectTransform.offsetMax = new Vector2(-36f, -30f);
        Button confirm = CreateButton(box, "ConfirmButton", "Продолжить", 116f, 26f, 190f, 56f, null);
        Button cancel = CreateButton(box, "CancelButton", "Отмена", 344f, 26f, 190f, 56f, null);
        confirmDialog = overlay.gameObject.AddComponent<EditorConfirmDialog>();
        confirmDialog.Initialize(message, confirm, cancel);
    }

    private static RectTransform CreatePanel(
        string name,
        RectTransform parent,
        Color color)
    {
        GameObject panel = CreateUiObject(name, parent, typeof(Image));
        Image image = panel.GetComponent<Image>();
        image.color = color;
        return panel.transform as RectTransform;
    }

    private static GameObject CreateUiObject(
        string name,
        RectTransform parent,
        params Type[] components)
    {
        List<Type> types = new List<Type> { typeof(RectTransform) };
        types.AddRange(components);
        GameObject result = new GameObject(name, types.ToArray());
        result.layer = 5;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static TMP_Text CreateText(
        string name,
        RectTransform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateLabelAt(
        RectTransform parent,
        string name,
        string value,
        float x,
        float y,
        float width,
        float height)
    {
        TMP_Text label = CreateText(name, parent, value, 18f, TextAlignmentOptions.MidlineLeft);
        SetBottomLeft(label.rectTransform, x, y, width, height);
    }

    private static Image CreateImage(
        string name,
        RectTransform parent,
        Vector2 position,
        Vector2 size)
    {
        GameObject imageObject = CreateUiObject(name, parent, typeof(CanvasRenderer), typeof(Image));
        Image image = imageObject.GetComponent<Image>();
        image.color = Color.white;
        SetBottomLeft(image.rectTransform, position.x, position.y, size.x, size.y);
        return image;
    }

    private static Button CreateButton(
        RectTransform parent,
        string name,
        string label,
        float x,
        float y,
        float width,
        float height,
        UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(Image), typeof(Button));
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.19f, 0.31f, 0.4f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        if (action != null)
        {
            button.onClick.AddListener(action);
        }

        SetBottomLeft(buttonObject.transform as RectTransform, x, y, width, height);
        TMP_Text text = CreateText("Text", buttonObject.transform as RectTransform, label, 20f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 8f);
        return button;
    }

    private static TMP_InputField CreateInput(
        RectTransform parent,
        string name,
        float x,
        float y,
        float width,
        float height)
    {
        GameObject inputObject = CreateUiObject(name, parent, typeof(Image), typeof(TMP_InputField));
        Image image = inputObject.GetComponent<Image>();
        image.color = new Color(0.04f, 0.07f, 0.1f, 1f);
        RectTransform rect = inputObject.transform as RectTransform;
        SetBottomLeft(rect, x, y, width, height);
        TMP_Text text = CreateText("Text", rect, string.Empty, 20f, TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform, 14f);
        TMP_Text placeholder = CreateText("Placeholder", rect, "Название комплектации", 20f, TextAlignmentOptions.MidlineLeft);
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        Stretch(placeholder.rectTransform, 14f);
        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textComponent = text as TextMeshProUGUI;
        input.placeholder = placeholder as Graphic;
        input.targetGraphic = image;
        return input;
    }

    private static TMP_Dropdown CreateDropdown(
        RectTransform parent,
        string name,
        float x,
        float y,
        float width,
        float height)
    {
        GameObject dropdownObject = CreateUiObject(name, parent, typeof(Image), typeof(TMP_Dropdown));
        Image background = dropdownObject.GetComponent<Image>();
        background.color = new Color(0.04f, 0.07f, 0.1f, 1f);
        RectTransform rect = dropdownObject.transform as RectTransform;
        SetBottomLeft(rect, x, y, width, height);
        TMP_Text caption = CreateText("Label", rect, string.Empty, 19f, TextAlignmentOptions.MidlineLeft);
        caption.rectTransform.anchorMin = Vector2.zero;
        caption.rectTransform.anchorMax = Vector2.one;
        caption.rectTransform.offsetMin = new Vector2(14f, 4f);
        caption.rectTransform.offsetMax = new Vector2(-38f, -4f);
        TMP_Text arrow = CreateText("Arrow", rect, "▼", 18f, TextAlignmentOptions.Center);
        arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
        arrow.rectTransform.anchorMax = Vector2.one;
        arrow.rectTransform.pivot = new Vector2(1f, 0.5f);
        arrow.rectTransform.sizeDelta = new Vector2(34f, 0f);

        RectTransform template = CreatePanel("Template", rect, new Color(0.06f, 0.09f, 0.13f, 1f));
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = new Vector2(0f, -2f);
        template.sizeDelta = new Vector2(0f, 220f);
        ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = CreatePanel("Viewport", template, Color.clear);
        Stretch(viewport, 2f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = CreatePanel("Content", viewport, Color.clear);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 36f);
        GameObject itemObject = CreateUiObject("Item", content, typeof(Toggle));
        RectTransform itemRect = itemObject.transform as RectTransform;
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 36f);
        Image itemBackground = CreateImage("Item Background", itemRect, Vector2.zero, Vector2.zero);
        Stretch(itemBackground.rectTransform, 0f);
        itemBackground.color = new Color(0.18f, 0.28f, 0.36f, 0.8f);
        TMP_Text itemLabel = CreateText("Item Label", itemRect, "Option", 18f, TextAlignmentOptions.MidlineLeft);
        Stretch(itemLabel.rectTransform, 12f);
        Toggle toggle = itemObject.GetComponent<Toggle>();
        toggle.targetGraphic = itemBackground;
        toggle.graphic = itemBackground;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        template.gameObject.SetActive(false);

        TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic = background;
        dropdown.template = template;
        dropdown.captionText = caption as TMP_Text;
        dropdown.itemText = itemLabel as TMP_Text;
        return dropdown;
    }

    private static ScrollRect CreateScrollView(
        RectTransform parent,
        out RectTransform content)
    {
        GameObject scrollObject = CreateUiObject("ScrollView", parent, typeof(ScrollRect));
        RectTransform viewport = CreatePanel("Viewport", scrollObject.transform as RectTransform, Color.clear);
        Stretch(viewport, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        content = CreatePanel("Content", viewport, Color.clear);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        return scroll;
    }

    private static void SetBottomLeft(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = -Vector2.one * inset;
    }

    private static string MakeSafeFileName(string value)
    {
        string result = value.Trim();

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(result) ? "NewShipLayout" : result;
    }

    private static string GetModuleName(ShipModuleType moduleType)
    {
        switch (moduleType)
        {
            case ShipModuleType.Rudder: return "Штурвал";
            case ShipModuleType.Supplies: return "Припасы";
            case ShipModuleType.Cannon: return "Пушка";
            case ShipModuleType.OpenDeck: return "Обычная палуба";
            case ShipModuleType.Sails: return "Паруса";
            case ShipModuleType.Bunks: return "Койки";
            default: return moduleType.ToString();
        }
    }
}
