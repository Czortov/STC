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

#if UNITY_EDITOR
    public bool AddLayout(ShipLayoutDefinition layout)
    {
        if (layout == null || layouts.Contains(layout))
        {
            return false;
        }

        layouts.Add(layout);
        return true;
    }

    public bool RemoveLayout(ShipLayoutDefinition layout)
    {
        return layout != null && layouts.Remove(layout);
    }
#endif
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

#if UNITY_EDITOR
    public bool AddLayout(
        ShipHullDefinition hull,
        ShipLayoutDefinition layout)
    {
        if (hull == null || layout == null || entries == null)
        {
            return false;
        }

        foreach (ShipCatalogEntry entry in entries)
        {
            if (entry?.Hull == hull && entry.AddLayout(layout))
            {
                ApplyToHulls();
                return true;
            }
        }

        return false;
    }

    public bool RemoveLayout(ShipLayoutDefinition layout)
    {
        if (layout == null || entries == null)
        {
            return false;
        }

        bool removed = false;

        foreach (ShipCatalogEntry entry in entries)
        {
            if (entry != null)
            {
                removed |= entry.RemoveLayout(layout);
            }
        }

        if (removed)
        {
            ApplyToHulls();
        }

        return removed;
    }
#endif
}
