using UnityEngine;

public enum ShipTeam
{
    Player,
    Enemy
}

public sealed class ShipIdentity : MonoBehaviour
{
    [SerializeField] private ShipTeam team;

    [SerializeField] private string shipName = "Ship";

    public ShipTeam Team => team;
    public string ShipName => shipName;
}