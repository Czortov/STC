using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class ShipModuleVisualEntry
{
    [SerializeField] private ShipModuleType moduleType;
    [SerializeField] private Sprite[] sprites;
    [SerializeField] private Vector2 sizeInCells = Vector2.one;
    [SerializeField] private Vector2 offsetInCells;
    [Tooltip(
        "Use an order above 40 to render the module in front of crew. " +
        "Bunks are the exception and should remain behind crew.")]
    [SerializeField] private int sortingOrder = 50;

    public ShipModuleType ModuleType => moduleType;
    public Vector2 SizeInCells => new Vector2(
        Mathf.Max(0.01f, sizeInCells.x),
        Mathf.Max(0.01f, sizeInCells.y)
    );
    public Vector2 OffsetInCells => offsetInCells;
    public int SortingOrder => sortingOrder;
    public bool UsesNativeCellTransform =>
        Mathf.Approximately(sizeInCells.x, 1f) &&
        Mathf.Approximately(sizeInCells.y, 1f) &&
        Mathf.Approximately(offsetInCells.x, 0f) &&
        Mathf.Approximately(offsetInCells.y, 0f);

    public bool TryGetSprite(
        System.Random random,
        out Sprite selectedSprite)
    {
        selectedSprite = null;

        if (sprites == null || random == null)
        {
            return false;
        }

        int validSpriteCount = 0;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                validSpriteCount++;
            }
        }

        if (validSpriteCount == 0)
        {
            return false;
        }

        int selectedIndex = random.Next(validSpriteCount);

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                selectedSprite = sprites[i];
                return true;
            }

            selectedIndex--;
        }

        return false;
    }
}

[CreateAssetMenu(
    fileName = "NewShipHull",
    menuName = "Pirates/Ships/Hull Definition")]
public sealed class ShipHullDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private ShipType shipType = ShipType.Brig;
    [SerializeField] private string hullName = "Brig";

    [Tooltip(
        "0 - empty, 1 - interior room, 2 - exterior room, " +
        "3 - interior ladder, 4 - exterior ladder.")]
    [TextArea(4, 12)]
    [SerializeField] private string hullMatrix =
        "240000000\n" +
        "132222422\n" +
        "111111310\n" +
        "001111300";

    [Header("Visuals")]
    [SerializeField] private Sprite hullSprite;
    [SerializeField] private Sprite[] interiorWallGroupOneSprites;
    [SerializeField] private Sprite[] interiorWallGroupTwoSprites;
    [SerializeField] private Sprite[] exteriorWallSprites;
    [SerializeField] private ShipModuleVisualEntry[] moduleVisuals;
    [SerializeField] private Sprite ladderSprite;
    [SerializeField] private int ladderSortingOrder = -5;

    [Tooltip(
        "Guaranteed distance between the hull bottom and the lowest " +
        "matrix row. Hull extends one cell beyond each horizontal " +
        "side of the matrix.")]
    [Min(0)]
    [SerializeField] private int hullBottomPaddingInCells = 1;

    [SerializeField] private int hullSortingOrder = -20;
    [SerializeField] private int wallSortingOrder = -10;
    [SerializeField] private int visualSeed = 12345;

    private IReadOnlyList<ShipLayoutDefinition> availableLayouts =
        System.Array.Empty<ShipLayoutDefinition>();

    private Dictionary<ShipModuleType, ShipModuleVisualEntry>
        moduleVisualLookup;

    public ShipType ShipType => shipType;
    public string HullName => hullName;
    public string HullMatrix => hullMatrix;
    public Sprite HullSprite => hullSprite;
    public IReadOnlyList<Sprite> InteriorWallGroupOneSprites =>
        interiorWallGroupOneSprites;
    public IReadOnlyList<Sprite> InteriorWallGroupTwoSprites =>
        interiorWallGroupTwoSprites;
    public IReadOnlyList<Sprite> ExteriorWallSprites =>
        exteriorWallSprites;
    public IReadOnlyList<ShipModuleVisualEntry> ModuleVisuals =>
        moduleVisuals;
    public Sprite LadderSprite => ladderSprite;
    public int LadderSortingOrder => ladderSortingOrder;
    public int HullBottomPaddingInCells => hullBottomPaddingInCells;
    public int HullSortingOrder => hullSortingOrder;
    public int WallSortingOrder => wallSortingOrder;
    public int VisualSeed => visualSeed;

    public IReadOnlyList<ShipLayoutDefinition> AvailableLayouts =>
        availableLayouts;

    public void ConfigureAvailableLayouts(
        IReadOnlyList<ShipLayoutDefinition> layouts)
    {
        availableLayouts =
            layouts ?? System.Array.Empty<ShipLayoutDefinition>();
    }

    public IReadOnlyList<Sprite> GetInteriorWallSprites(int groupIndex)
    {
        return groupIndex == 0
            ? interiorWallGroupOneSprites
            : interiorWallGroupTwoSprites;
    }

    public bool TryGetModuleVisual(
        ShipModuleType moduleType,
        out ShipModuleVisualEntry visual)
    {
        EnsureModuleVisualLookup();
        return moduleVisualLookup.TryGetValue(moduleType, out visual);
    }

    private void OnEnable()
    {
        moduleVisualLookup = null;
    }

    private void OnValidate()
    {
        moduleVisualLookup = null;
    }

    private void EnsureModuleVisualLookup()
    {
        if (moduleVisualLookup != null)
        {
            return;
        }

        moduleVisualLookup =
            new Dictionary<ShipModuleType, ShipModuleVisualEntry>();

        if (moduleVisuals == null)
        {
            return;
        }

        foreach (ShipModuleVisualEntry visual in moduleVisuals)
        {
            if (visual == null ||
                visual.ModuleType == ShipModuleType.None)
            {
                continue;
            }

            if (moduleVisualLookup.ContainsKey(visual.ModuleType))
            {
                Debug.LogWarning(
                    $"Hull '{hullName}' contains more than one module " +
                    $"visual for {visual.ModuleType}. The first one is used.",
                    this
                );

                continue;
            }

            moduleVisualLookup.Add(visual.ModuleType, visual);
        }
    }

    public bool SupportsLayout(ShipLayoutDefinition layout)
    {
        if (layout == null || availableLayouts == null)
        {
            return false;
        }

        for (int i = 0; i < availableLayouts.Count; i++)
        {
            if (availableLayouts[i] == layout)
            {
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Validate Available Layouts")]
    private void ValidateAvailableLayouts()
    {
        if (availableLayouts.Count == 0)
        {
            Debug.LogWarning(
                $"Hull '{hullName}' has no available layouts.",
                this
            );

            return;
        }

        HashSet<ShipLayoutDefinition> checkedLayouts =
            new HashSet<ShipLayoutDefinition>();

        foreach (ShipLayoutDefinition layout in availableLayouts)
        {
            if (layout == null)
            {
                Debug.LogError(
                    $"Hull '{hullName}' contains an empty layout reference.",
                    this
                );

                continue;
            }

            if (!checkedLayouts.Add(layout))
            {
                Debug.LogWarning(
                    $"Layout '{layout.LayoutName}' is assigned to hull " +
                    $"'{hullName}' more than once.",
                    this
                );

                continue;
            }

            if (ShipBlueprintBuilder.TryCreateData(
                    this,
                    layout,
                    out ShipBlueprintData data,
                    out List<string> errors))
            {
                Debug.Log(
                    $"Hull '{hullName}' is compatible with layout " +
                    $"'{layout.LayoutName}'. Rooms: {data.Rooms.Count}.",
                    this
                );

                continue;
            }

            foreach (string error in errors)
            {
                Debug.LogError(
                    $"{hullName} + {layout.LayoutName}: {error}",
                    this
                );
            }
        }
    }
}
