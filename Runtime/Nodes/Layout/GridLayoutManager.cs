namespace _3DConnections.Runtime.Nodes.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using ScriptableObjects;

    public class GridLayoutManager
    {
        private float _cellSize = 5f;
        private Vector2 _gridSpacing = new Vector2(1f, 1f);
        private bool _centerGrid = true;
        private GridArrangement _arrangement = GridArrangement.SquareOptimal;

        public enum GridArrangement
        {
            SquareOptimal,    // Try to make a square-ish grid
            Horizontal,       // Arrange in rows (wider than tall)
            Vertical,         // Arrange in columns (taller than wide)
            SingleRow,        // All nodes in one row
            SingleColumn      // All nodes in one column
        }

        /// <summary>
        /// Arranges nodes in a grid layout
        /// </summary>
        /// <param name="roots">Root nodes to layout</param>
        public void LayoutGrid(List<TreeNode> roots)
        {
            if (roots == null || roots.Count == 0) return;

            // Collect all nodes from the forest
            var allNodes = CollectAllNodes(roots);
            if (allNodes.Count == 0) return;

            // Calculate grid dimensions
            var gridDimensions = CalculateGridDimensions(allNodes.Count);
            var rows = gridDimensions.x;
            var cols = gridDimensions.y;

            // Calculate starting position if centering is enabled
            var startPos = Vector3.zero;
            if (_centerGrid)
            {
                var totalWidth = (cols - 1) * (_cellSize + _gridSpacing.x);
                var totalHeight = (rows - 1) * (_cellSize + _gridSpacing.y);
                startPos = new Vector3(-totalWidth * 0.5f, totalHeight * 0.5f, 0f);
            }

            // Position nodes in grid
            for (int i = 0; i < allNodes.Count; i++)
            {
                var row = i / cols;
                var col = i % cols;

                var x = startPos.x + col * (_cellSize + _gridSpacing.x);
                var y = startPos.y - row * (_cellSize + _gridSpacing.y);

                allNodes[i].GameObject.transform.position = new Vector3(x, y, 0f);
            }
        }

        /// <summary>
        /// Arranges nodes in a hierarchical grid where each level forms its own row
        /// </summary>
        /// <param name="roots">Root nodes to layout</param>
        public void LayoutHierarchicalGrid(List<TreeNode> roots)
        {
            if (roots == null || roots.Count == 0) return;

            var levelNodes = new Dictionary<int, List<TreeNode>>();
            var visited = new HashSet<TreeNode>();

            // Group nodes by level
            foreach (var root in roots)
            {
                AssignLevels(root, 0, levelNodes, visited);
            }

            if (levelNodes.Count == 0) return;

            var maxLevel = levelNodes.Keys.Max();
            var startY = maxLevel * (_cellSize + _gridSpacing.y) * 0.5f;

            // Layout each level as a row
            foreach (var kvp in levelNodes)
            {
                var level = kvp.Key;
                var nodes = kvp.Value;
                
                var totalWidth = (nodes.Count - 1) * (_cellSize + _gridSpacing.x);
                var startX = -totalWidth * 0.5f;
                var y = startY - level * (_cellSize + _gridSpacing.y);

                for (int i = 0; i < nodes.Count; i++)
                {
                    var x = startX + i * (_cellSize + _gridSpacing.x);
                    nodes[i].GameObject.transform.position = new Vector3(x, y, 0f);
                }
            }
        }

        private void AssignLevels(TreeNode node, int level, Dictionary<int, List<TreeNode>> levelNodes, HashSet<TreeNode> visited)
        {
            if (node == null || !visited.Add(node)) return;

            if (!levelNodes.ContainsKey(level))
                levelNodes[level] = new List<TreeNode>();

            levelNodes[level].Add(node);

            foreach (var child in node.Children)
            {
                AssignLevels(child, level + 1, levelNodes, visited);
            }
        }

        private Vector2Int CalculateGridDimensions(int nodeCount)
        {
            switch (_arrangement)
            {
                case GridArrangement.SingleRow:
                    return new Vector2Int(1, nodeCount);
                
                case GridArrangement.SingleColumn:
                    return new Vector2Int(nodeCount, 1);
                
                case GridArrangement.Horizontal:
                    var hCols = Mathf.CeilToInt(Mathf.Sqrt(nodeCount * 1.5f));
                    var hRows = Mathf.CeilToInt((float)nodeCount / hCols);
                    return new Vector2Int(hRows, hCols);
                
                case GridArrangement.Vertical:
                    var vRows = Mathf.CeilToInt(Mathf.Sqrt(nodeCount * 1.5f));
                    var vCols = Mathf.CeilToInt((float)nodeCount / vRows);
                    return new Vector2Int(vRows, vCols);
                
                case GridArrangement.SquareOptimal:
                default:
                    var sqrtCount = Mathf.CeilToInt(Mathf.Sqrt(nodeCount));
                    var rows = sqrtCount;
                    var cols = Mathf.CeilToInt((float)nodeCount / rows);
                    return new Vector2Int(rows, cols);
            }
        }

        private List<TreeNode> CollectAllNodes(List<TreeNode> roots)
        {
            var allNodes = new List<TreeNode>();
            var visited = new HashSet<TreeNode>();

            foreach (var root in roots)
            {
                CollectNodesRecursive(root, allNodes, visited);
            }

            return allNodes;
        }

        private void CollectNodesRecursive(TreeNode node, List<TreeNode> allNodes, HashSet<TreeNode> visited)
        {
            if (node == null || !visited.Add(node)) return;

            allNodes.Add(node);

            foreach (var child in node.Children)
            {
                CollectNodesRecursive(child, allNodes, visited);
            }
        }

        public void SetLayoutParameters(LayoutParameters parameters)
        {
            _cellSize = parameters.nodeSpacing > 0 ? parameters.nodeSpacing : _cellSize;
            _gridSpacing = new Vector2(parameters.minDistance, parameters.minDistance);
            
            // Map layout type to grid arrangement if needed
            // This could be extended based on additional parameters
        }

        public void SetGridParameters(float cellSize, Vector2 spacing, GridArrangement arrangement, bool centerGrid = true)
        {
            _cellSize = cellSize;
            _gridSpacing = spacing;
            _arrangement = arrangement;
            _centerGrid = centerGrid;
        }
    }
}