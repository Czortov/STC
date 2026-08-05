using System.Collections.Generic;
using UnityEngine;

public enum ShipNodeType
{
    Regular,
    ControlPoint
}

public class ShipNode : MonoBehaviour
{
    [SerializeField] private ShipNodeType nodeType = ShipNodeType.Regular;

    [Tooltip("Узлы, к которым можно напрямую перейти из этой точки.")]
    [SerializeField] private List<ShipNode> neighbours =
        new List<ShipNode>();

    private GameObject occupant;
    private GameObject reservedBy;

    public ShipNodeType NodeType => nodeType;
    public IReadOnlyList<ShipNode> Neighbours => neighbours;

    public GameObject Occupant => occupant;
    public GameObject ReservedBy => reservedBy;

    public bool IsOccupied => occupant != null;
    public bool IsReserved => reservedBy != null;

    public ShipRoom Room { get; private set; }

    private void Awake()
    {
        Room = GetComponentInParent<ShipRoom>();
    }

    public bool CanBeDestinationFor(GameObject unit)
    {
        if (unit == null)
        {
            return false;
        }

        bool occupiedByAnother =
            occupant != null && occupant != unit;

        bool reservedByAnother =
            reservedBy != null && reservedBy != unit;

        return !occupiedByAnother && !reservedByAnother;
    }

    public bool TryReserve(GameObject unit)
    {
        if (!CanBeDestinationFor(unit))
        {
            return false;
        }

        reservedBy = unit;
        return true;
    }

    public void ReleaseReservation(GameObject unit)
    {
        if (reservedBy == unit)
        {
            reservedBy = null;
        }
    }

    public bool TryOccupy(GameObject unit)
    {
        if (!CanBeDestinationFor(unit))
        {
            return false;
        }

        occupant = unit;

        if (reservedBy == unit)
        {
            reservedBy = null;
        }

        return true;
    }

    public void Leave(GameObject unit)
    {
        if (occupant == unit)
        {
            occupant = null;
        }
    }

    private void OnValidate()
    {
        neighbours.RemoveAll(node => node == null || node == this);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = nodeType == ShipNodeType.ControlPoint
            ? Color.red
            : Color.cyan;

        Gizmos.DrawSphere(transform.position, 0.12f);

        Gizmos.color = Color.blue;

        foreach (ShipNode neighbour in neighbours)
        {
            if (neighbour == null)
            {
                continue;
            }

            /*
             * Связь может быть нарисована повторно, если оба узла
             * добавлены в списки соседей друг друга. Для Gizmos это
             * безвредно и лучше, чем использовать устаревший
             * GetInstanceID() только ради сортировки.
             */
            Gizmos.DrawLine(
                transform.position,
                neighbour.transform.position
            );
        }
    }
}