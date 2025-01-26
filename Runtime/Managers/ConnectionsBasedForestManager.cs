using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ConnectionsBasedForestManager
{
    private float _minNodeDistance = 2f; // Minimum distance between nodes
    private float _startingRadius = 3f; // Initial radius for first level
    private float _radiusIncrement = 4f;
    private float _rootSpacing = 10f; // Space between root trees

    /// <summary>
    /// Creates a Tree structure using existing connections structure containing connections between the node objects of the overlay layer
    /// </summary>
    /// <param name="connections">Connections between GameObjects</param>
    /// <returns></returns>
    public static List<TreeNode> BuildForest(List<NodeConnection> connections)
    {
        Dictionary<GameObject, TreeNode> nodeMap = new();
        if (connections == null || connections.Count == 0)
            return new List<TreeNode>();

        // Create TreeNode objects for all GameObjects
        foreach (var connection in connections)
        {
            if (!nodeMap.ContainsKey(connection.startNode))
                nodeMap[connection.startNode] = new TreeNode { GameObject = connection.startNode };
            if (!nodeMap.ContainsKey(connection.endNode))
                nodeMap[connection.endNode] = new TreeNode { GameObject = connection.endNode };
        }

        // Establish connections
        foreach (var connection in connections)
        {
            var parentNode = nodeMap[connection.startNode];
            var childNode = nodeMap[connection.endNode];

            if (!parentNode.Children.Contains(childNode))
                parentNode.Children.Add(childNode);
            if (!childNode.Parents.Contains(parentNode))
                childNode.Parents.Add(parentNode);
        }

        // Find root nodes (nodes with no parents or nodes part of cycles)
        return FindRootNodes(nodeMap);
    }

    /// <summary>
    /// Layout multiple TreeNode roots in a circle
    /// </summary>
    /// <param name="roots">List of root TreeNodes</param>
    public void LayoutForest(List<TreeNode> roots)
    {
        if (roots == null || roots.Count == 0) return;
        if (roots.Count == 1)
        {
            LayoutSingleTree(roots[0], Vector3.zero);
            return;
        }

        // For multiple roots, arrange them in a circle
        var rootRadius = (roots.Count * _rootSpacing) / (2 * Mathf.PI);
        for (var i = 0; i < roots.Count; i++)
        {
            var angle = (360f * i) / roots.Count;
            var x = rootRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
            var y = rootRadius * Mathf.Sin(angle * Mathf.Deg2Rad);
            var rootPosition = new Vector3(x, y, -1);

            LayoutSingleTree(roots[i], rootPosition);
        }
    }

    private void LayoutSingleTree(TreeNode root, Vector3 position)
    {
        root.GameObject.transform.position = position;

        var levelCounts = new Dictionary<int, int>();
        CalculateLevelCounts(root, 0, levelCounts, new HashSet<TreeNode>());
        LayoutChildren(root, 0, 0, 360, levelCounts, new HashSet<TreeNode>());
    }

    private static void CalculateLevelCounts(TreeNode node, int level, Dictionary<int, int> levelCounts, HashSet<TreeNode> visited)
    {
        if (node == null || !visited.Add(node)) return;

        levelCounts.TryAdd(level, 0);
        levelCounts[level]++;

        foreach (var child in node.Children)
        {
            CalculateLevelCounts(child, level + 1, levelCounts, visited);
        }
    }

    // Modified to handle cycles
    private void LayoutChildren(TreeNode node, int level, float startAngle, float angleRange,
        Dictionary<int, int> levelCounts, HashSet<TreeNode> visited)
    {
        if (node == null || node.Children.Count == 0 || !visited.Add(node)) return;

        var radius = _startingRadius + (level * _radiusIncrement);
        var childCount = node.Children.Count;
        var angleStep = angleRange / childCount;

        // Calculate minimum angle based on the minimum distance between nodes at this radius
        var minAngleForLevel = CalculateMinimumAngle(radius);

        // Ensure we're using an angle step that won't place nodes too close together
        angleStep = Mathf.Max(angleStep, minAngleForLevel);

        // Calculate the total angle needed for all children
        var totalAngleNeeded = angleStep * childCount;

        // Adjust the starting angle to center the children within the available angle range
        var adjustedStartAngle = startAngle + ((angleRange - totalAngleNeeded) / 2f);

        for (var i = 0; i < childCount; i++)
        {
            var child = node.Children[i];
            // Calculate the absolute angle for this child
            var childAngle = adjustedStartAngle + (i * angleStep);

            // Convert angle to radians for the trigonometric calculations
            var angleRadians = childAngle * Mathf.Deg2Rad;

            // Calculate position using polar coordinates
            var x = radius * Mathf.Cos(angleRadians);
            var y = radius * Mathf.Sin(angleRadians);

            // Position relative to the parent node's position
            var nodePosition = node.GameObject.transform.position;
            var childPosition = nodePosition + new Vector3(x, y, 0);
            child.GameObject.transform.position = childPosition;

            // Recursively layout this child's children, using the same angle range as calculated for even spacing
            LayoutChildren(child, level + 1, childAngle - (angleStep / 2f), angleStep, levelCounts, visited);
        }
    }

    // Previous helper methods remain the same
    private float CalculateMinimumAngle(float radius) => (_minNodeDistance / radius) * (180f / Mathf.PI);

    public void SetLayoutParameters(float minDistance, float startRadius, float radiusInc, float rootSpacing)
    {
        _minNodeDistance = minDistance;
        _startingRadius = startRadius;
        _radiusIncrement = radiusInc;
        _rootSpacing = rootSpacing;
    }

    private static void FindCycleRoots(TreeNode node, HashSet<TreeNode> visited, HashSet<TreeNode> inStack, List<TreeNode> cycleRoots)
    {
        if (inStack.Contains(node))
        {
            if (!cycleRoots.Contains(node))
                cycleRoots.Add(node);
            return;
        }

        if (!visited.Add(node))
            return;

        inStack.Add(node);

        foreach (var parent in node.Parents)
        {
            FindCycleRoots(parent, visited, inStack, cycleRoots);
        }

        inStack.Remove(node);
    }


    private static List<TreeNode> FindRootNodes(Dictionary<GameObject, TreeNode> nodeMap)
    {
        var rootNodes = new List<TreeNode>();
        var visited = new HashSet<TreeNode>();
        var inStack = new HashSet<TreeNode>(); // For cycle detection

        // First, add all nodes with no parents
        foreach (var node in nodeMap.Values.Where(node => node.Parents.Count == 0))
        {
            rootNodes.Add(node);
            visited.Add(node);
        }

        // Then find cycle entry points if they exist
        foreach (var node in nodeMap.Values.Where(node => !visited.Contains(node)))
        {
            FindCycleRoots(node, visited, inStack, rootNodes);
        }

        // If no roots found at all, pick the nodes with the most outgoing connections
        if (rootNodes.Count != 0) return rootNodes;
        var maxChildren = nodeMap.Values.Max(n => n.Children.Count);
        rootNodes.AddRange(nodeMap.Values.Where(n => n.Children.Count == maxChildren));

        return rootNodes;
    }

    /// <summary>
    /// Flattens all nodes to the Z=0 plane while maintaining their X and Y positions
    /// </summary>
    /// <param name="roots">List of root nodes to process</param>
    public void FlattenToZPlane(List<TreeNode> roots)
    {
        if (roots == null || roots.Count == 0) return;

        var processedNodes = new HashSet<TreeNode>();
        foreach (var root in roots)
        {
            FlattenNodeAndChildren(root, processedNodes);
        }
    }

    private static void FlattenNodeAndChildren(TreeNode node, HashSet<TreeNode> processedNodes)
    {
        if (node == null || !processedNodes.Add(node)) return;

        // Keep X and Y, set Z to 0
        var currentPos = node.GameObject.transform.position;
        node.GameObject.transform.localPosition = new Vector3(currentPos.x, currentPos.y, 0f);

        // Process all children
        foreach (var child in node.Children)
        {
            FlattenNodeAndChildren(child, processedNodes);
        }
    }
}