using System.Collections.Generic;
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

    [Header("Rudder effect")]

    [Tooltip(
        "Во сколько раз активный штурвал этого корабля продлевает " +
        "перезарядку пушек противоположного корабля.")]
    [Min(0.01f)]
    [SerializeField]
    private float activeRudderOpponentReloadDurationMultiplier = 1.5f;

    private readonly List<ShipRoomRuntime> rudderRooms =
        new List<ShipRoomRuntime>();

    public ShipTeam Team => team;
    public string ShipName => shipName;

    public float OpponentReloadDurationMultiplier
    {
        get
        {
            EnsureRudderRoomsCached();

            foreach (ShipRoomRuntime rudderRoom in rudderRooms)
            {
                if (rudderRoom != null && rudderRoom.IsOperational)
                {
                    return Mathf.Max(
                        0.01f,
                        activeRudderOpponentReloadDurationMultiplier
                    );
                }
            }

            return 1f;
        }
    }

    private void EnsureRudderRoomsCached()
    {
        rudderRooms.RemoveAll(room => room == null);

        if (rudderRooms.Count > 0)
        {
            return;
        }

        ShipRoomRuntime[] rooms =
            GetComponentsInChildren<ShipRoomRuntime>(true);

        foreach (ShipRoomRuntime room in rooms)
        {
            if (room != null &&
                room.SystemModule == ShipModuleType.Rudder)
            {
                rudderRooms.Add(room);
            }
        }
    }

    private void OnValidate()
    {
        activeRudderOpponentReloadDurationMultiplier =
            Mathf.Max(
                0.01f,
                activeRudderOpponentReloadDurationMultiplier
            );
    }
}
