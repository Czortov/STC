using System.Collections.Generic;
using UnityEngine;

public class ShipRoom : MonoBehaviour
{
    [SerializeField] private string displayName = "Отсек";

    [Tooltip("Узлы, принадлежащие этому отсеку.")]
    [SerializeField] private List<ShipNode> nodes =
        new List<ShipNode>();

    public string DisplayName => displayName;
    public IReadOnlyList<ShipNode> Nodes => nodes;

    private void Awake()
    {
        RefreshNodes();
    }

    public ShipNode GetPreferredDestination(GameObject unit)
    {
        ShipNode firstFreeRegularNode = null;

        foreach (ShipNode node in nodes)
        {
            if (node == null || !node.CanBeDestinationFor(unit))
            {
                continue;
            }

            // Свободный пульт всегда имеет приоритет.
            if (node.NodeType == ShipNodeType.ControlPoint)
            {
                return node;
            }

            if (firstFreeRegularNode == null)
            {
                firstFreeRegularNode = node;
            }
        }

        return firstFreeRegularNode;
    }

    public bool HasFreeDestination(GameObject unit)
    {
        return GetPreferredDestination(unit) != null;
    }

    [ContextMenu("Refresh Nodes")]
    private void RefreshNodes()
    {
        nodes.Clear();

        nodes.AddRange(
            GetComponentsInChildren<ShipNode>(true)
        );
    }
}