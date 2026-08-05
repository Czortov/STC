using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShipAmmoLoadout : MonoBehaviour
{
    [Header("Default Ammo")]

    [Tooltip(
        "Этот боеприпас будет выбран в пушках при начале боя.")]
    [SerializeField]
    private CannonAmmoDefinition defaultAmmo;

    [Header("Available Ammo")]

    [Tooltip(
        "Типы боеприпасов, доступные каждой пушке корабля.")]
    [SerializeField]
    private List<CannonAmmoDefinition> availableAmmo =
        new List<CannonAmmoDefinition>();

    public CannonAmmoDefinition DefaultAmmo =>
        defaultAmmo;

    public IReadOnlyList<CannonAmmoDefinition> AvailableAmmo =>
        availableAmmo;

    public bool Contains(
        CannonAmmoDefinition ammo)
    {
        return ammo != null &&
               (
                   ammo == defaultAmmo ||
                   availableAmmo.Contains(ammo)
               );
    }

    private void OnValidate()
    {
        if (availableAmmo == null)
        {
            availableAmmo =
                new List<CannonAmmoDefinition>();
        }

        HashSet<CannonAmmoDefinition> uniqueAmmo =
            new HashSet<CannonAmmoDefinition>();

        for (int i = availableAmmo.Count - 1;
             i >= 0;
             i--)
        {
            CannonAmmoDefinition ammo =
                availableAmmo[i];

            if (ammo == null ||
                !uniqueAmmo.Add(ammo))
            {
                availableAmmo.RemoveAt(i);
            }
        }
    }
}