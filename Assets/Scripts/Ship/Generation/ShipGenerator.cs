using System.Collections.Generic;
using UnityEngine;

public sealed class ShipGenerator : MonoBehaviour
{
    private const string GeneratedRootName = "GeneratedShip";
    private const float HullPixelsPerCell = 256f;

    [Header("Blueprint Source")]

    [Tooltip(
        "Включить после создания меню выбора корпуса и модификации.")]
    [SerializeField] private bool useRuntimeSelection;

    [Tooltip(
        "Корпус для проверки генератора прямо из Inspector.")]
    [SerializeField] private ShipHullDefinition previewHull;

    [Tooltip(
        "Модификация для проверки генератора прямо из Inspector.")]
    [SerializeField] private ShipLayoutDefinition previewLayout;

    [Header("Generation")]

    [SerializeField] private bool generateOnStart = true;

    [Min(0.1f)]
    [SerializeField] private float cellSize = 1f;

    [Min(1)]
    [SerializeField] private int defaultRoomHealth = 100;

    [SerializeField] private bool centerShip = true;

    [Header("Colors")]

    [SerializeField] private Color borderColor =
        new Color(0.08f, 0.08f, 0.08f, 1f);

    [SerializeField] private Color ladderColor =
        new Color(0.95f, 0.85f, 0.25f, 1f);

    [SerializeField] private Color[] roomColors =
    {
        new Color(0.25f, 0.55f, 0.85f, 1f),
        new Color(0.30f, 0.70f, 0.55f, 1f),
        new Color(0.75f, 0.45f, 0.30f, 1f),
        new Color(0.55f, 0.40f, 0.75f, 1f),
        new Color(0.70f, 0.65f, 0.30f, 1f),
        new Color(0.35f, 0.65f, 0.72f, 1f)
    };

    private readonly Dictionary<int, ShipRoomRuntime>
        generatedRooms =
            new Dictionary<int, ShipRoomRuntime>();

    private Transform generatedRoot;

    private static Texture2D runtimeSquareTexture;
    private static Sprite runtimeSquareSprite;

    public IReadOnlyDictionary<int, ShipRoomRuntime>
        GeneratedRooms => generatedRooms;

    public int LastVisualSeed { get; private set; }

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateShip();
        }
    }

    [ContextMenu("Generate Ship Preview")]
    public void GenerateShip()
    {
        if (!TryResolveDefinitions(
                out ShipHullDefinition hull,
                out ShipLayoutDefinition layout))
        {
            return;
        }

        if (!ShipBlueprintBuilder.TryCreateData(
                hull,
                layout,
                out ShipBlueprintData shipData,
                out List<string> errors))
        {
            foreach (string error in errors)
            {
                Debug.LogError(
                    $"Не удалось сгенерировать корабль: {error}",
                    this
                );
            }

            return;
        }

        ClearGeneratedShip();

        Sprite squareSprite = GetRuntimeSquareSprite();

        GameObject rootObject =
            new GameObject(GeneratedRootName);

        rootObject.transform.SetParent(transform, false);

        if (!Application.isPlaying)
        {
            rootObject.hideFlags =
                HideFlags.DontSaveInEditor;
        }

        generatedRoot = rootObject.transform;

        CreateHullVisual(shipData, hull);

        LastVisualSeed = hull.VisualSeed;
        System.Random visualRandom =
            new System.Random(LastVisualSeed);

        WarnAboutMissingVisuals(hull);

        Dictionary<int, ShipRoomBlueprint>
            roomBlueprints =
                CreateRoomObjects(shipData);

        CreateCellObjects(
            shipData,
            roomBlueprints,
            squareSprite,
            hull,
            visualRandom
        );

        ConfigureRoomColliders(
            shipData,
            roomBlueprints
        );

        Debug.Log(
            $"Корабль \"{shipData.Name}\" создан. " +
            $"Клеток матрицы: " +
            $"{shipData.Width}×{shipData.Height}. " +
            $"Отсеков: {shipData.Rooms.Count}.",
            this
        );
    }

    [ContextMenu("Clear Generated Ship")]
    public void ClearGeneratedShip()
    {
        generatedRooms.Clear();

        Transform existingRoot = generatedRoot;

        if (existingRoot == null)
        {
            existingRoot =
                transform.Find(GeneratedRootName);
        }

        if (existingRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existingRoot.gameObject);
        }
        else
        {
            DestroyImmediate(existingRoot.gameObject);
        }

        generatedRoot = null;
    }

    private bool TryResolveDefinitions(
        out ShipHullDefinition hull,
        out ShipLayoutDefinition layout)
    {
        if (useRuntimeSelection)
        {
            hull = ShipSelectionState.SelectedHull;
            layout = ShipSelectionState.SelectedLayout;

            if (hull == null)
            {
                Debug.LogError(
                    "ShipGenerator: игрок не выбрал корпус.",
                    this
                );

                return false;
            }

            if (layout == null)
            {
                Debug.LogError(
                    "ShipGenerator: игрок не выбрал модификацию.",
                    this
                );

                return false;
            }

            return true;
        }

        hull = previewHull;
        layout = previewLayout;

        if (hull == null)
        {
            Debug.LogError(
                "ShipGenerator: не назначен Preview Hull.",
                this
            );

            return false;
        }

        if (layout == null)
        {
            Debug.LogError(
                "ShipGenerator: не назначен Preview Layout.",
                this
            );

            return false;
        }

        return true;
    }

    private Dictionary<int, ShipRoomBlueprint>
        CreateRoomObjects(ShipBlueprintData shipData)
    {
        Dictionary<int, ShipRoomBlueprint>
            roomBlueprints =
                new Dictionary<int, ShipRoomBlueprint>();

        foreach (ShipRoomBlueprint roomBlueprint
                 in shipData.Rooms)
        {
            GameObject roomObject = new GameObject(
                $"Room_{roomBlueprint.Id:00}_" +
                $"Deck_{roomBlueprint.DeckIndex:00}"
            );

            roomObject.transform.SetParent(
                generatedRoot,
                false
            );

            ShipRoomRuntime roomRuntime =
                roomObject.AddComponent<ShipRoomRuntime>();

            roomRuntime.Initialize(
                roomBlueprint,
                defaultRoomHealth
            );

            generatedRooms.Add(
                roomBlueprint.Id,
                roomRuntime
            );

            roomBlueprints.Add(
                roomBlueprint.Id,
                roomBlueprint
            );
        }

        return roomBlueprints;
    }

    private void CreateHullVisual(
        ShipBlueprintData shipData,
        ShipHullDefinition hull)
    {
        if (hull.HullSprite == null)
        {
            Debug.LogWarning(
                $"ShipGenerator: hull sprite is not assigned for " +
                $"ship type {hull.ShipType}.",
                hull
            );

            return;
        }

        GameObject visualsObject = new GameObject("Visuals");
        visualsObject.transform.SetParent(generatedRoot, false);

        GameObject hullObject = new GameObject("HullSprite");
        hullObject.transform.SetParent(visualsObject.transform, false);

        SpriteRenderer renderer =
            hullObject.AddComponent<SpriteRenderer>();

        renderer.sprite = hull.HullSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = hull.HullSortingOrder;

        Sprite hullSprite = hull.HullSprite;
        float pixelsToWorld = cellSize / HullPixelsPerCell;
        float uniformScale =
            pixelsToWorld * hullSprite.pixelsPerUnit;

        Vector3 matrixCenter = GetMatrixCenterLocalPosition(
            shipData.Width,
            shipData.Height
        );

        float matrixLeft =
            matrixCenter.x - shipData.Width * cellSize * 0.5f;
        float matrixBottom =
            matrixCenter.y - shipData.Height * cellSize * 0.5f;

        float hullLeft = matrixLeft - cellSize;
        float hullBottom =
            matrixBottom -
            Mathf.Max(0, hull.HullBottomPaddingInCells) * cellSize;

        // The PNG is authored on a 256 px grid. Position its full rect by
        // the actual sprite pivot, without relying on bounds or assuming a
        // centered pivot. The PNG height is intentionally unrestricted.
        hullObject.transform.localPosition = new Vector3(
            hullLeft + hullSprite.pivot.x * pixelsToWorld,
            hullBottom + hullSprite.pivot.y * pixelsToWorld,
            0f
        );

        hullObject.transform.localScale = new Vector3(
            uniformScale,
            uniformScale,
            1f
        );

        float expectedWidthInPixels =
            (shipData.Width + 2) * HullPixelsPerCell;

        if (!Mathf.Approximately(
                hullSprite.rect.width,
                expectedWidthInPixels))
        {
            Debug.LogWarning(
                $"ShipGenerator: hull sprite '{hullSprite.name}' is " +
                $"{hullSprite.rect.width}px wide, but a " +
                $"{shipData.Width}-cell matrix requires " +
                $"{expectedWidthInPixels}px (matrix width plus one " +
                "cell on each side). The PNG is displayed at its " +
                "authored 256 px per cell scale.",
                hull
            );
        }
    }

    private Vector3 GetMatrixCenterLocalPosition(int width, int height)
    {
        if (centerShip)
        {
            return Vector3.zero;
        }

        return new Vector3(
            (width - 1) * cellSize * 0.5f,
            (height - 1) * cellSize * 0.5f,
            0f
        );
    }

    private void WarnAboutMissingVisuals(ShipHullDefinition hull)
    {
        if (!ContainsSprite(hull.InteriorWallGroupOneSprites))
        {
            Debug.LogWarning(
                $"ShipGenerator: {hull.ShipType} has no sprites in " +
                "interior wall group 1. The color fallback will be used.",
                hull
            );
        }

        if (!ContainsSprite(hull.InteriorWallGroupTwoSprites))
        {
            Debug.LogWarning(
                $"ShipGenerator: {hull.ShipType} has no sprites in " +
                "interior wall group 2. The color fallback will be used.",
                hull
            );
        }

        if (!ContainsSprite(hull.ExteriorWallSprites))
        {
            Debug.LogWarning(
                $"ShipGenerator: {hull.ShipType} has no exterior wall " +
                "sprites. Exterior cells will remain transparent.",
                hull
            );
        }

        if (hull.LadderSprite == null)
        {
            Debug.LogWarning(
                $"ShipGenerator: {hull.ShipType} has no ladder sprite. " +
                "The procedural ladder fallback will be used.",
                hull
            );
        }
    }

    private static bool ContainsSprite(IReadOnlyList<Sprite> sprites)
    {
        if (sprites == null)
        {
            return false;
        }

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Sprite GetWallSprite(
        IReadOnlyList<Sprite> sprites,
        System.Random random)
    {
        if (sprites == null || random == null)
        {
            return null;
        }

        int validSpriteCount = 0;

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] != null)
            {
                validSpriteCount++;
            }
        }

        if (validSpriteCount == 0)
        {
            return null;
        }

        int selectedIndex = random.Next(validSpriteCount);

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i] == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return sprites[i];
            }

            selectedIndex--;
        }

        return null;
    }

    private void CreateCellObjects(
        ShipBlueprintData shipData,
        Dictionary<int, ShipRoomBlueprint> roomBlueprints,
        Sprite squareSprite,
        ShipHullDefinition hull,
        System.Random visualRandom)
    {
        HashSet<ShipModuleType> missingModuleVisualWarnings =
            new HashSet<ShipModuleType>();

        for (int y = 0; y < shipData.Height; y++)
        {
            int interiorWallGroupIndex = visualRandom.Next(2);
            IReadOnlyList<Sprite> interiorWallSprites =
                hull.GetInteriorWallSprites(interiorWallGroupIndex);

            for (int x = 0; x < shipData.Width; x++)
            {
                ShipCellBlueprint cellBlueprint =
                    shipData.Cells[x, y];

                if (cellBlueprint == null ||
                    !cellBlueprint.Exists)
                {
                    continue;
                }

                if (!generatedRooms.TryGetValue(
                        cellBlueprint.RoomId,
                        out ShipRoomRuntime roomRuntime))
                {
                    Debug.LogError(
                        $"Для клетки X={x}, Y={y} " +
                        $"не найден отсек " +
                        $"{cellBlueprint.RoomId}.",
                        this
                    );

                    continue;
                }

                roomBlueprints.TryGetValue(
                    cellBlueprint.RoomId,
                    out ShipRoomBlueprint roomBlueprint
                );

                bool isControlPoint =
                    roomBlueprint != null &&
                    roomBlueprint.HasControlPoint &&
                    roomBlueprint.ControlPointCoordinates ==
                    cellBlueprint.Coordinates;

                GameObject cellObject = new GameObject(
                    GetCellObjectName(cellBlueprint)
                );

                cellObject.transform.SetParent(
                    roomRuntime.transform,
                    false
                );

                cellObject.transform.localPosition =
                    GetCellLocalPosition(
                        x,
                        y,
                        shipData.Width,
                        shipData.Height
                    );

                ShipCellView cellView =
                    cellObject.AddComponent<ShipCellView>();

                ShipModuleVisualEntry moduleVisual = null;
                Sprite moduleSprite = null;

                if (cellBlueprint.ModuleType != ShipModuleType.None &&
                    (!hull.TryGetModuleVisual(
                         cellBlueprint.ModuleType,
                         out moduleVisual) ||
                     !moduleVisual.TryGetSprite(
                         visualRandom,
                         out moduleSprite)))
                {
                    moduleVisual = null;

                    if (missingModuleVisualWarnings.Add(
                            cellBlueprint.ModuleType))
                    {
                        Debug.LogWarning(
                            $"ShipGenerator: {hull.ShipType} has no " +
                            $"sprite for module " +
                            $"{cellBlueprint.ModuleType}. " +
                            "The placeholder will be used.",
                            hull
                        );
                    }
                }

                cellView.Initialize(
                    cellBlueprint,
                    roomRuntime,
                    isControlPoint,
                    squareSprite,
                    GetWallSprite(
                        cellBlueprint.IsExterior
                            ? hull.ExteriorWallSprites
                            : interiorWallSprites,
                        visualRandom
                    ),
                    moduleVisual,
                    moduleSprite,
                    hull.LadderSprite,
                    hull.LadderSortingOrder,
                    cellSize,
                    GetRoomColor(cellBlueprint.RoomId),
                    borderColor,
                    GetModuleColor(cellBlueprint.ModuleType),
                    ladderColor,
                    hull.WallSortingOrder
                );

                roomRuntime.RegisterCell(cellView);
            }
        }
    }

    private void ConfigureRoomColliders(
        ShipBlueprintData shipData,
        Dictionary<int, ShipRoomBlueprint> roomBlueprints)
    {
        foreach (KeyValuePair<int, ShipRoomBlueprint>
                 pair in roomBlueprints)
        {
            if (!generatedRooms.TryGetValue(
                    pair.Key,
                    out ShipRoomRuntime roomRuntime))
            {
                continue;
            }

            ShipRoomBlueprint roomBlueprint = pair.Value;

            Vector2 min = new Vector2(
                float.MaxValue,
                float.MaxValue
            );

            Vector2 max = new Vector2(
                float.MinValue,
                float.MinValue
            );

            foreach (Vector2Int coordinates
                     in roomBlueprint.Cells)
            {
                Vector3 position =
                    GetCellLocalPosition(
                        coordinates.x,
                        coordinates.y,
                        shipData.Width,
                        shipData.Height
                    );

                min.x = Mathf.Min(min.x, position.x);
                min.y = Mathf.Min(min.y, position.y);

                max.x = Mathf.Max(max.x, position.x);
                max.y = Mathf.Max(max.y, position.y);
            }

            Vector2 center = (min + max) * 0.5f;

            Vector2 size =
                max - min +
                new Vector2(cellSize, cellSize);

            roomRuntime.ConfigureClickArea(
                center,
                size
            );
        }
    }

    private Vector3 GetCellLocalPosition(
        int x,
        int matrixY,
        int width,
        int height)
    {
        float startX = 0f;
        float startY = 0f;

        if (centerShip)
        {
            startX =
                -(width - 1) * cellSize * 0.5f;

            startY =
                -(height - 1) * cellSize * 0.5f;
        }

        // Первая строка матрицы — верхняя палуба.
        int invertedY = height - 1 - matrixY;

        return new Vector3(
            startX + x * cellSize,
            startY + invertedY * cellSize,
            0f
        );
    }

    private Color GetRoomColor(int roomId)
    {
        if (roomColors == null ||
            roomColors.Length == 0)
        {
            return Color.gray;
        }

        int index =
            Mathf.Abs(roomId) % roomColors.Length;

        return roomColors[index];
    }

    private static Color GetModuleColor(
        ShipModuleType moduleType)
    {
        switch (moduleType)
        {
            case ShipModuleType.Rudder:
                return new Color(0.85f, 0.20f, 0.20f, 1f);

            case ShipModuleType.Supplies:
                return new Color(0.70f, 0.45f, 0.15f, 1f);

            case ShipModuleType.Cannon:
                return new Color(0.15f, 0.15f, 0.15f, 1f);

            case ShipModuleType.OpenDeck:
                return new Color(0.80f, 0.75f, 0.55f, 1f);

            case ShipModuleType.Sails:
                return new Color(0.92f, 0.92f, 0.82f, 1f);

            case ShipModuleType.Bunks:
                return new Color(0.45f, 0.25f, 0.15f, 1f);

            default:
                return Color.clear;
        }
    }

    private static string GetCellObjectName(
        ShipCellBlueprint cell)
    {
        string name =
            $"Cell_{cell.Coordinates.x}_" +
            $"{cell.Coordinates.y}";

        if (cell.IsLadder)
        {
            return $"{name}_Ladder";
        }

        if (cell.ModuleType != ShipModuleType.None)
        {
            return $"{name}_{cell.ModuleType}";
        }

        return name;
    }

    private static Sprite GetRuntimeSquareSprite()
    {
        if (runtimeSquareSprite != null)
        {
            return runtimeSquareSprite;
        }

        runtimeSquareTexture = new Texture2D(
            1,
            1,
            TextureFormat.RGBA32,
            false
        );

        runtimeSquareTexture.name =
            "RuntimeShipSquareTexture";

        runtimeSquareTexture.SetPixel(
            0,
            0,
            Color.white
        );

        runtimeSquareTexture.Apply();
        runtimeSquareTexture.filterMode =
            FilterMode.Point;

        runtimeSquareTexture.hideFlags =
            HideFlags.HideAndDontSave;

        runtimeSquareSprite = Sprite.Create(
            runtimeSquareTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );

        runtimeSquareSprite.name =
            "RuntimeShipSquareSprite";

        runtimeSquareSprite.hideFlags =
            HideFlags.HideAndDontSave;

        return runtimeSquareSprite;
    }
}
