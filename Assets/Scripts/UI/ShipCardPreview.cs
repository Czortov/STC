using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ShipCardPreview : MonoBehaviour
{
    private const float HullPixelsPerCell = 256f;
    private const string PreviewRootName = "GeneratedPreview";

    private RectTransform previewRect;
    private RectTransform generatedRoot;

    private void Awake()
    {
        Image background = GetComponent<Image>();

        if (background != null)
        {
            background.color = Color.clear;
            background.raycastTarget = false;
        }

        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }
    }

    public void Show(
        ShipHullDefinition hull,
        ShipLayoutDefinition layout)
    {
        Clear();

        if (hull == null || layout == null)
        {
            return;
        }

        if (!ShipBlueprintBuilder.TryCreateData(
                hull,
                layout,
                out ShipBlueprintData data,
                out List<string> errors))
        {
            foreach (string error in errors)
            {
                Debug.LogError($"Не удалось создать превью корабля: {error}", this);
            }

            return;
        }

        previewRect = transform as RectTransform;

        if (previewRect == null)
        {
            return;
        }

        GameObject rootObject = new GameObject(
            PreviewRootName,
            typeof(RectTransform)
        );

        generatedRoot = rootObject.GetComponent<RectTransform>();
        generatedRoot.SetParent(previewRect, false);
        generatedRoot.anchorMin = Vector2.zero;
        generatedRoot.anchorMax = Vector2.one;
        generatedRoot.offsetMin = Vector2.zero;
        generatedRoot.offsetMax = Vector2.zero;

        float cellSize = CalculateCellSize(data, hull);
        System.Random random = new System.Random(hull.VisualSeed);

        CreateHull(hull, data, cellSize);
        CreateCells(hull, data, cellSize, random);
    }

    public void Clear()
    {
        Transform existing = generatedRoot;

        if (existing == null)
        {
            existing = transform.Find(PreviewRootName);
        }

        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        generatedRoot = null;
    }

    private float CalculateCellSize(
        ShipBlueprintData data,
        ShipHullDefinition hull)
    {
        Rect rect = previewRect.rect;
        float availableWidth = Mathf.Max(1f, rect.width);
        float availableHeight = Mathf.Max(1f, rect.height);

        float hullWidthInCells = data.Width + 2f;
        float hullHeightInCells = data.Height +
            Mathf.Max(0, hull.HullBottomPaddingInCells);

        if (hull.HullSprite != null)
        {
            hullWidthInCells = Mathf.Max(
                hullWidthInCells,
                hull.HullSprite.rect.width / HullPixelsPerCell
            );

            hullHeightInCells = Mathf.Max(
                hullHeightInCells,
                hull.HullSprite.rect.height / HullPixelsPerCell
            );
        }

        // Cover the entire preview area. RectMask2D trims only the small
        // excess on the dimension that does not fit the preview aspect.
        return Mathf.Max(
            availableWidth / hullWidthInCells,
            availableHeight / hullHeightInCells
        );
    }

    private void CreateHull(
        ShipHullDefinition hull,
        ShipBlueprintData data,
        float cellSize)
    {
        if (hull.HullSprite == null)
        {
            return;
        }

        Image image = CreateImage("Hull", hull.HullSprite, Color.white);
        RectTransform rect = image.rectTransform;
        Sprite sprite = hull.HullSprite;
        float pixelsToUnits = cellSize / HullPixelsPerCell;

        rect.sizeDelta = sprite.rect.size * pixelsToUnits;
        rect.pivot = new Vector2(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height
        );

        float matrixLeft = -data.Width * cellSize * 0.5f;
        float matrixBottom = -data.Height * cellSize * 0.5f;
        float hullLeft = matrixLeft - cellSize;
        float hullBottom = matrixBottom -
            Mathf.Max(0, hull.HullBottomPaddingInCells) * cellSize;

        rect.anchoredPosition = new Vector2(
            hullLeft + sprite.pivot.x * pixelsToUnits,
            hullBottom + sprite.pivot.y * pixelsToUnits
        );
    }

    private void CreateCells(
        ShipHullDefinition hull,
        ShipBlueprintData data,
        float cellSize,
        System.Random random)
    {
        for (int y = 0; y < data.Height; y++)
        {
            IReadOnlyList<Sprite> interiorSprites =
                hull.GetInteriorWallSprites(random.Next(2));

            for (int x = 0; x < data.Width; x++)
            {
                ShipCellBlueprint cell = data.Cells[x, y];

                if (cell == null || !cell.Exists)
                {
                    continue;
                }

                Vector2 position = new Vector2(
                    (x - (data.Width - 1) * 0.5f) * cellSize,
                    ((data.Height - 1) * 0.5f - y) * cellSize
                );

                Sprite wallSprite = cell.IsInterior
                    ? PickSprite(interiorSprites, random)
                    : PickSprite(hull.ExteriorWallSprites, random);

                Color fallbackColor = cell.IsInterior
                    ? new Color(0.52f, 0.43f, 0.31f, 1f)
                    : new Color(0.36f, 0.42f, 0.45f, 1f);

                Image wall = CreateImage(
                    $"Cell_{x}_{y}",
                    wallSprite,
                    wallSprite != null ? Color.white : fallbackColor
                );

                wall.rectTransform.sizeDelta = Vector2.one * cellSize;
                wall.rectTransform.anchoredPosition = position;

                if (cell.HullType.IsLadder() && hull.LadderSprite != null)
                {
                    Image ladder = CreateImage(
                        $"Ladder_{x}_{y}",
                        hull.LadderSprite,
                        Color.white
                    );

                    ladder.rectTransform.sizeDelta = Vector2.one * cellSize;
                    ladder.rectTransform.anchoredPosition = position;
                }

                CreateModule(hull, cell, position, cellSize, random);
            }
        }
    }

    private void CreateModule(
        ShipHullDefinition hull,
        ShipCellBlueprint cell,
        Vector2 position,
        float cellSize,
        System.Random random)
    {
        if (cell.ModuleType == ShipModuleType.None ||
            !hull.TryGetModuleVisual(cell.ModuleType, out ShipModuleVisualEntry visual) ||
            !visual.TryGetSprite(random, out Sprite sprite))
        {
            return;
        }

        Image module = CreateImage(
            $"Module_{cell.Coordinates.x}_{cell.Coordinates.y}",
            sprite,
            Color.white
        );

        module.rectTransform.sizeDelta = visual.SizeInCells * cellSize;
        module.rectTransform.anchoredPosition =
            position + visual.OffsetInCells * cellSize;
    }

    private Image CreateImage(string objectName, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.SetParent(generatedRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

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

        List<Sprite> validSprites = new List<Sprite>();

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] != null)
            {
                validSprites.Add(sprites[i]);
            }
        }

        return validSprites.Count == 0
            ? null
            : validSprites[random.Next(validSprites.Count)];
    }
}
