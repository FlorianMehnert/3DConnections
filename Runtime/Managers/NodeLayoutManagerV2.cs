using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class NodeLayoutManagerV2 : MonoBehaviour
{
    /// <summary>
    /// Compact layout algorithm with fixed aspect ratio for all nodes
    /// </summary>
    /// <param name="nodes">List of nodes to be positioned</param>
    /// <param name="targetAspectRatio">Desired aspect ratio (width/height)</param>
    /// <param name="minimumPadding">Minimum spacing between nodes</param>
    public static void GridLayout(List<Node> nodes, float targetAspectRatio = 1.0f, float minimumPadding = 5f)
    {
        if (nodes == null || nodes.Count == 0) return;

        // Determine layout grid dimensions
        var nodeCount = nodes.Count;
        var gridColumns = Mathf.CeilToInt(Mathf.Sqrt(nodeCount));
        var gridRows = Mathf.CeilToInt((float)nodeCount / gridColumns);

        // Normalize node sizes to a fixed aspect ratio while preserving total area
        var normalizedNodes = nodes.Select(node =>
        {
            // Calculate new dimensions maintaining the target aspect ratio
            var newWidth = Mathf.Sqrt(node.Width * node.Height * targetAspectRatio);
            var newHeight = newWidth / targetAspectRatio;

            return new GameObjectNode(node.Name, 0, 0, newWidth, newHeight, null);
        }).ToList();

        // Determine max node dimensions in the grid
        var maxNodeWidth = normalizedNodes.Max(n => n.Width);
        var maxNodeHeight = normalizedNodes.Max(n => n.Height);

        // Parallel processing for positioning
        Parallel.For(0, normalizedNodes.Count, i =>
        {
            // Calculate grid position
            var row = i / gridColumns;
            var col = i % gridColumns;

            // Position calculation with padding
            var x = col * (maxNodeWidth + minimumPadding);
            var y = row * (maxNodeHeight + minimumPadding);

            // Update node position and size
            var originalNode = nodes[i];

            // Preserve original scaling while positioning
            originalNode.X = x + (maxNodeWidth - originalNode.Width) / 2;
            originalNode.Y = y + (maxNodeHeight - originalNode.Height) / 2;
        });
    }


    /// <summary>
    /// Requires existing connections in <see cref="NodeConnectionManager"/> to layout nodes as forest in a circular arrangement
    /// </summary>
    public static void LayoutForest(LayoutParameters layoutParameters)
    {
        if (!NodeConnectionManager.Instance) return;
        var forestManager = new ConnectionsBasedForestManager();
        var rootNodes = new List<TreeNode>();
        switch (layoutParameters.layoutType)
        {
            case (int)LayoutType.Grid:
            {
                return;
            }
            case (int)LayoutType.Radial:
            {
                rootNodes = ConnectionsBasedForestManager.BuildForest(NodeConnectionManager.Instance.conSo.connections);
                forestManager.SetLayoutParameters(
                    layoutParameters
                );
                forestManager.LayoutForest(rootNodes);
                break;
            }
            case (int)LayoutType.Tree:
            {
                rootNodes = HierarchicalLayout(layoutParameters);
                break;
            }
            default:
                Debug.Log("Unknown layout type");
                return;
        }
        
        forestManager.FlattenToZPlane(rootNodes);
    }

    private static List<TreeNode> HierarchicalLayout(LayoutParameters layoutParameters)
    {
        var rootNodes = ConnectionsBasedForestManager.BuildForest(
            NodeConnectionManager.Instance.conSo.connections);
    
        var hierarchicalLayout = new HierarchicalTreeLayout();
        hierarchicalLayout.SetLayoutParameters(
            layoutParameters.levelSpacing,
            layoutParameters.nodeSpacing,
            layoutParameters.subtreeSpacing
        );
    
        hierarchicalLayout.LayoutTree(rootNodes);
        return rootNodes;
    }
}