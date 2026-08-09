using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShipEditorPreview : MonoBehaviour
{
    private const float HullPixelsPerCell = 256f;
    private const string GeneratedRootName = "GeneratedShipEditorPreview";

    private ShipEditorController owner;
    private RectTransform previewRect;
    private RectTransform generatedRoot;
    private ShipHullDefinition hull;
    private ShipBlueprintData data;

    public void Initialize(ShipEditorController controller)
    {
        owner = controller;
        previewRect = transform as RectTransform;

        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    public void Show(
        ShipHullDefinition hullDefinition,
        ShipBlueprintData blueprint)
    {
        Clear();
        hull = hullDefinition;
        data = blueprint;

        if (hull == null || data == null || previewRect == null)
        {
            return;
        }

        GameObject root = new GameObject(
            GeneratedRootName,
            typeof(RectTransform)
        );
        generatedRoot = root.GetComponent<RectTransform>();
        generatedRoot.SetParent(previewRect, false);
        generatedRoot.anchorMin = Vector2.zero;
        generatedRoot.anchorMax = Vector2.one;
        generatedRoot.offsetMin = Vector2.zero;
        generatedRoot.offsetMax = Vector2.zero;

        float cellSize = CalculateCellSize();
        System.Random random = new System.Random(hull.VisualSeed);
        CreateHull(cellSize);
        CreateCells(cellSize, random);
    }

    public void Clear()
    {
        Transform existing = generatedRoot;

        if (existing == null)
        {
            existing = transform.Find(GeneratedRootName);
        }

        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        generatedRoot = null;
    }

    public void PlaceSelectedModule(ShipEditorCell cell)
    {
        if (cell == null || !cell.IsEditable ||
            owner.SelectedModule == ShipModuleType.None)
        {
            return;
        }

        owner.SetModule(cell.X, cell.Y, owner.SelectedModule);
    }

    public void RemoveModule(ShipEditorCell cell)
    {
        if (cell == null || !cell.IsEditable ||
            cell.ModuleType == ShipModuleType.None)
        {
            return;
        }

        owner.SetModule(cell.X, cell.Y, ShipModuleType.None);
    }

    private float CalculateCellSize()
    {
        float padding = Mathf.Max(16f, owner.PreviewPadding);
        float availableWidth = Mathf.Max(1f, previewRect.rect.width - padding * 2f);
        float availableHeight = Mathf.Max(1f, previewRect.rect.height - padding * 2f);
        float widthInCells = data.Width + 2f;
        float heightInCells = data.Height +
            Mathf.Max(0, hull.HullBottomPaddingInCells);

        if (hull.HullSprite != null)
        {
            widthInCells = Mathf.Max(
                widthInCells,
                hull.HullSprite.rect.width / HullPixelsPerCell
            );
            heightInCells = Mathf.Max(
                heightInCells,
                hull.HullSprite.rect.height / HullPixelsPerCell
            );
        }

        return Mathf.Min(
            availableWidth / widthInCells,
            availableHeight / heightInCells
        );
    }

    private void CreateHull(float cellSize)
    {
        if (hull.HullSprite == null)
        {
            return;
        }

        Image image = CreateImage("Hull", generatedRoot, hull.HullSprite, Color.white);
        RectTransform rect = image.rectTransform;
        Sprite sprite = hull.HullSprite;
        float scale = cellSize / HullPixelsPerCell;
        rect.sizeDelta = sprite.rect.size * scale;
        rect.pivot = new Vector2(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height
        );

        float matrixLeft = -data.Width * cellSize * 0.5f;
        float matrixBottom = -data.Height * cellSize * 0.5f;
        rect.anchoredPosition = new Vector2(
            matrixLeft - cellSize + sprite.pivot.x * scale,
            matrixBottom - hull.HullBottomPaddingInCells * cellSize +
            sprite.pivot.y * scale
        );
    }

    private void CreateCells(float cellSize, System.Random random)
    {
        for (int y = 0; y < data.Height; y++)
        {
            IReadOnlyList<Sprite> interiors =
                hull.GetInteriorWallSprites(random.Next(2));

            for (int x = 0; x < data.Width; x++)
            {
                ShipCellBlueprint blueprint = data.Cells[x, y];

                if (blueprint == null || !blueprint.Exists)
                {
                    continue;
                }

                Vector2 position = new Vector2(
                    (x - (data.Width - 1) * 0.5f) * cellSize,
                    ((data.Height - 1) * 0.5f - y) * cellSize
                );
                GameObject cellObject = new GameObject(
                    $"Cell_{x}_{y}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(ShipEditorCell)
                );
                RectTransform cellRect = cellObject.GetComponent<RectTransform>();
                cellRect.SetParent(generatedRoot, false);
                cellRect.anchorMin = cellRect.anchorMax = new Vector2(0.5f, 0.5f);
                cellRect.sizeDelta = Vector2.one * cellSize;
                cellRect.anchoredPosition = position;

                Sprite wallSprite = blueprint.IsInterior
                    ? PickSprite(interiors, random)
                    : PickSprite(hull.ExteriorWallSprites, random);
                Image wall = cellObject.GetComponent<Image>();
                wall.sprite = wallSprite;
                wall.color = wallSprite != null
                    ? Color.white
                    : blueprint.IsInterior
                        ? new Color(0.52f, 0.43f, 0.31f, 1f)
                        : new Color(0.36f, 0.42f, 0.45f, 1f);
                wall.raycastTarget = true;

                if (blueprint.IsLadder && hull.LadderSprite != null)
                {
                    CreateChildImage(
                        "Ladder",
                        cellRect,
                        hull.LadderSprite,
                        Color.white,
                        Vector2.one * cellSize,
                        Vector2.zero
                    );
                }

                CreateModule(blueprint, cellRect, cellSize, random);
                Image overlay = CreateChildImage(
                    "HoverOverlay",
                    cellRect,
                    null,
                    Color.clear,
                    Vector2.one * cellSize,
                    Vector2.zero
                );
                overlay.raycastTarget = false;
                overlay.enabled = false;
                cellObject.GetComponent<ShipEditorCell>().Initialize(
                    this,
                    x,
                    y,
                    blueprint.HullType,
                    blueprint.ModuleType,
                    overlay
                );
            }
        }
    }

    private void CreateModule(
        ShipCellBlueprint cell,
        RectTransform parent,
        float cellSize,
        System.Random random)
    {
        if (cell.ModuleType == ShipModuleType.None ||
            !hull.TryGetModuleVisual(cell.ModuleType, out ShipModuleVisualEntry visual) ||
            !visual.TryGetSprite(random, out Sprite sprite))
        {
            return;
        }

        CreateChildImage(
            "Module",
            parent,
            sprite,
            Color.white,
            visual.SizeInCells * cellSize,
            visual.OffsetInCells * cellSize
        );
    }

    private static Image CreateChildImage(
        string objectName,
        RectTransform parent,
        Sprite sprite,
        Color color,
        Vector2 size,
        Vector2 position)
    {
        Image image = CreateImage(objectName, parent, sprite, color);
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = position;
        return image;
    }

    private static Image CreateImage(
        string objectName,
        RectTransform parent,
        Sprite sprite,
        Color color)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    private static Sprite PickSprite(
        IReadOnlyList<Sprite> sprites,
        System.Random random)
    {
        if (sprites == null)
        {
            return null;
        }

        List<Sprite> valid = new List<Sprite>();

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] != null)
            {
                valid.Add(sprites[i]);
            }
        }

        return valid.Count == 0 ? null : valid[random.Next(valid.Count)];
    }
}
