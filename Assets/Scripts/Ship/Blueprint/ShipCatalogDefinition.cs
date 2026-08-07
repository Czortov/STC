using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class ShipCatalogEntry
{
    [SerializeField] private ShipHullDefinition hull;
    [SerializeField] private List<ShipLayoutDefinition> layouts =
        new List<ShipLayoutDefinition>();

    public ShipHullDefinition Hull => hull;
    public IReadOnlyList<ShipLayoutDefinition> Layouts => layouts;
}

[CreateAssetMenu(
    fileName = "ShipCatalog",
    menuName = "Pirates/Ships/Ship Catalog")]
public sealed class ShipCatalogDefinition : ScriptableObject
{
    [SerializeField] private List<ShipCatalogEntry> entries =
        new List<ShipCatalogEntry>();

    public IReadOnlyList<ShipCatalogEntry> Entries => entries;

    public void ApplyToHulls()
    {
        if (entries == null)
        {
            return;
        }

        foreach (ShipCatalogEntry entry in entries)
        {
            if (entry?.Hull != null)
            {
                entry.Hull.ConfigureAvailableLayouts(entry.Layouts);
            }
        }
    }

    public bool TryGetLayouts(
        ShipHullDefinition hull,
        out IReadOnlyList<ShipLayoutDefinition> layouts)
    {
        if (hull != null && entries != null)
        {
            foreach (ShipCatalogEntry entry in entries)
            {
                if (entry?.Hull == hull)
                {
                    layouts = entry.Layouts;
                    return true;
                }
            }
        }

        layouts = System.Array.Empty<ShipLayoutDefinition>();
        return false;
    }

    private void OnEnable()
    {
        ApplyToHulls();
    }

    private void OnValidate()
    {
        ApplyToHulls();
    }
}
