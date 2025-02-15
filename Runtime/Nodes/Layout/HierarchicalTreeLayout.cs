using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HierarchicalTreeLayout
{
    private float _levelSpacing = 5f;  // Vertical space between levels
    private float _nodeSpacing = 3f;   // Horizontal space between nodes
    private float _subtreeSpacing = 2f; // Extra space between subtrees
    
    private readonly Dictionary<TreeNode, float> _nodeWidths = new();
    private readonly Dictionary<TreeNode, float> _subtreeWidths = new();
    private readonly Dictionary<TreeNode, Vector2> _preliminaryPositions = new();
    private readonly HashSet<TreeNode> _processedNodes = new();

    public void LayoutTree(List<TreeNode> roots)
    {
        if (roots == null || roots.Count == 0) return;

        _nodeWidths.Clear();
        _subtreeWidths.Clear();
        _preliminaryPositions.Clear();
        _processedNodes.Clear();
        foreach (var root in roots)
            CalculateNodeWidths(root);

        float currentX = 0;
        foreach (var root in roots)
        {
            // First pass: Calculate preliminary positions
            CalculateInitialPositions(root, currentX, 0);
            currentX += _subtreeWidths[root] + _subtreeSpacing;
        }

        // Second pass: Handle cycles and adjust positions
        foreach (var root in roots)
        {
            _processedNodes.Clear();
            AdjustPositionsForCycles(root);
        }

        // Final pass: Apply positions to gameObjects
        foreach (var root in roots)
        {
            _processedNodes.Clear();
            ApplyFinalPositions(root);
        }
    }

    private void CalculateNodeWidths(TreeNode node)
    {
        if (node == null || _nodeWidths.ContainsKey(node)) return;

        // Get node width from GameObject (assuming it has a Renderer or RectTransform)
        var width = 1f;
        if (node.GameObject)
        {
            var renderer = node.GameObject.GetComponent<Renderer>();
            if (renderer)
                width = renderer.bounds.size.x;
            else
            {
                var rectTransform = node.GameObject.GetComponent<RectTransform>();
                if (rectTransform)
                {
                    width = rectTransform.rect.width;
                }
            }
        }

        _nodeWidths[node] = width;

        // Calculate subtree width
        var subtreeWidth = width;
        if (node.Children.Count > 0)
        {
            subtreeWidth = 0;
            foreach (var child in node.Children)
            {
                CalculateNodeWidths(child);
                subtreeWidth += _subtreeWidths.GetValueOrDefault(child, 0) + _nodeSpacing;
            }
            subtreeWidth -= _nodeSpacing; // Remove extra spacing after last child
        }

        _subtreeWidths[node] = Mathf.Max(width, subtreeWidth);
    }

    private void CalculateInitialPositions(TreeNode node, float x, float y)
    {
        if (node == null || _preliminaryPositions.ContainsKey(node)) return;

        _preliminaryPositions[node] = new Vector2(x, y);

        if (node.Children.Count == 0) return;

        // Calculate children positions
        var childrenTotalWidth = node.Children.Sum(child => _subtreeWidths.GetValueOrDefault(child, 0)) + 
                                 _nodeSpacing * (node.Children.Count - 1);
        var startX = x - childrenTotalWidth / 2f;

        foreach (var child in node.Children)
        {
            var childX = startX + _subtreeWidths[child] / 2f;
            CalculateInitialPositions(child, childX, y + _levelSpacing);
            startX += _subtreeWidths[child] + _nodeSpacing;
        }
    }

    private void AdjustPositionsForCycles(TreeNode node)
    {
        if (node == null || !_processedNodes.Add(node)) return;

        // Check for cycles by looking at parents
        foreach (var parent in node.Parents)
        {
            if (!_preliminaryPositions.TryGetValue(parent, out var parentPos)) continue;
            var currentPos = _preliminaryPositions[node];

            // If we find a cycle where the child is higher than or at the same level as the parent
            if (!(currentPos.y <= parentPos.y)) continue;
            // Move this node and its subtree down
            var visited = new HashSet<TreeNode>();
            ShiftSubtreeDown(node, parentPos.y + _levelSpacing - currentPos.y, visited);
        }

        foreach (var child in node.Children)
            AdjustPositionsForCycles(child);
    }

    private void ShiftSubtreeDown(TreeNode node, float deltaY, HashSet<TreeNode> visited)
    {
        if (node == null || !visited.Add(node)) return;

        if (!_preliminaryPositions.TryGetValue(node, out var position)) return;
        _preliminaryPositions[node] = new Vector2(position.x, position.y + deltaY);

        // Process children only if this node hasn't been visited
        foreach (var child in node.Children)
        {
            ShiftSubtreeDown(child, deltaY, visited);
        }
    }

    private void ApplyFinalPositions(TreeNode node)
    {
        if (node == null || !_processedNodes.Add(node)) return;

        if (_preliminaryPositions.TryGetValue(node, out var position))
        {
            node.GameObject.transform.position = new Vector3(position.x, position.y, 0);
        }

        foreach (var child in node.Children)
        {
            ApplyFinalPositions(child);
        }
    }

    public void SetLayoutParameters(float levelSpacing, float nodeSpacing, float subtreeSpacing)
    {
        _levelSpacing = levelSpacing;
        _nodeSpacing = nodeSpacing;
        _subtreeSpacing = subtreeSpacing;
    }
}