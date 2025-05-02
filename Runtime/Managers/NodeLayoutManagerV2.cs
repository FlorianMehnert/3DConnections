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
    public static void GridLayout(List<NodeV1> nodes, float targetAspectRatio = 1.0f, float minimumPadding = 5f)
    {
        if (nodes == null || nodes.Count == 0) return;

        // Determine layout grid dimensions
        var nodeCount = nodes.Count;
        var gridColumns = Mathf.CeilToInt(Mathf.Sqrt(nodeCount));

        // Normalize node sizes to a fixed aspect ratio while preserving total area
        var normalizedNodes = nodes.Select(node =>
        {
            // Calculate new dimensions maintaining the target aspect ratio
            var newWidth = Mathf.Sqrt(node.Width * node.Height * targetAspectRatio);
            var newHeight = newWidth / targetAspectRatio;

            return new GameObjectNodeV1(node.Name, 0, 0, newWidth, newHeight, null);
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
    public static void Layout(LayoutParameters layoutParameters, NodeGraphScriptableObject nodeGraph)
    {
        if (!NodeConnectionManager.Instance) return;
        var forestManager = new ConnectionsBasedForestManager();
        List<TreeNode> rootNodes;
        switch (layoutParameters.layoutType)
        {
            case (int)LayoutType.Grid:
            {
                rootNodes = ConnectionsBasedForestManager.BuildForest(NodeConnectionManager.Instance.conSo.connections);
                forestManager.SetLayoutParameters(
                    layoutParameters
                );
                rootNodes = LevelBoxAvoidanceLayout(rootNodes[0].GameObject, nodeGraph);
                break;
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

    private static List<TreeNode> LevelBoxAvoidanceLayout(GameObject rootNode, NodeGraphScriptableObject nodeGraph)
    {
        // Create TreeNodes for all GameObjects
        List<TreeNode> treeNodes = new();
        Dictionary<GameObject, TreeNode> gameObjectToTreeNode = new();

        foreach (var obj in nodeGraph.AllNodes)
        {
            var node = new TreeNode { GameObject = obj };
            treeNodes.Add(node);
            gameObjectToTreeNode[obj] = node;
        }

        // Build connections using LocalNodeConnections
        foreach (var obj in nodeGraph.AllNodes)
        {
            var currentNode = gameObjectToTreeNode[obj];
            var connections = obj.GetComponent<LocalNodeConnections>();

            // Add children based on outDirection connections
            foreach (var childNode in connections.outConnections.Select(childObj => gameObjectToTreeNode[childObj]))
            {
                currentNode.Children.Add(childNode);
                childNode.Parents.Add(currentNode);
            }
        }

        // Initialize dictionary to track levels
        Dictionary<TreeNode, int> nodeLevels = new();
        foreach (var node in treeNodes)
        {
            nodeLevels[node] = int.MaxValue;
        }

        // Set root level
        var rootTreeNode = gameObjectToTreeNode[rootNode];
        nodeLevels[rootTreeNode] = 0;

        for (var currentLevel = 0; currentLevel < 10; currentLevel++)
        {
            var currentLevelNodes = treeNodes.Where(n => nodeLevels[n] == currentLevel).ToList();
            var level = currentLevel;
            foreach (var child in from node in currentLevelNodes from child in node.Children where nodeLevels[child] > level + 1 select child)
            {
                nodeLevels[child] = currentLevel + 1;
            }

            // Layout current level and next level without overlap
            LayoutLevels(treeNodes, nodeLevels, currentLevel);
        }
        return treeNodes;
    }

    private static void LayoutLevels(List<TreeNode> allNodes, Dictionary<TreeNode, int> nodeLevels, int currentLevel)
    {
        var currentLevelNodes = allNodes.Where(n => nodeLevels[n] == currentLevel).ToList();
        var nextLevelNodes = allNodes.Where(n => nodeLevels[n] == currentLevel + 1).ToList();

        if (!currentLevelNodes.Any() || !nextLevelNodes.Any())
            return;

        var padding = 20f; // Adjust this value based on your needs

        float xOffset = 10;
        foreach (var node in currentLevelNodes)
        {
            var bounds = node.GameObject.GetComponent<Renderer>().bounds;
            var newPosition = new Vector3(xOffset, currentLevel * -1, 0);
            node.GameObject.transform.position = newPosition;
            xOffset += bounds.size.x + padding;
        }

        xOffset = 0;
        foreach (var node in nextLevelNodes)
        {
            var bounds = node.GameObject.GetComponent<Renderer>().bounds;
            Vector3 newPosition = new Vector3(xOffset, (currentLevel + 1) * -50f, 0);
            node.GameObject.transform.position = newPosition;
            xOffset += bounds.size.x + padding;
        }
        CenterNodes(currentLevelNodes);
        CenterNodes(nextLevelNodes);
    }

    private static void CenterNodes(List<TreeNode> nodes)
    {
        if (!nodes.Any()) return;

        const float padding = 20f;

        var totalWidth = nodes.Select(node => node.GameObject.GetComponent<Renderer>().bounds).Select(bounds => bounds.size.x + padding).Sum();

        totalWidth -= padding; // Remove extra padding from last node

        var centerOffset = -totalWidth / 2f;

        // Adjust positions
        foreach (var node in nodes)
        {
            var bounds = node.GameObject.GetComponent<Renderer>().bounds;
            var currentPos = node.GameObject.transform.position;
            node.GameObject.transform.position = new Vector3(
                currentPos.x + centerOffset,
                currentPos.y,
                currentPos.z
            );
            centerOffset += bounds.size.x + padding;
        }
    }
}