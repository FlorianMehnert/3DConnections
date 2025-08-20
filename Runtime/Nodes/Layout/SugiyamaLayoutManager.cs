namespace _3DConnections.Runtime.Nodes.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using ScriptableObjects;

    /// <summary>
    /// Implementation of the Sugiyama algorithm for hierarchical graph layout
    /// Consists of 4 main phases: cycle removal, layer assignment, crossing reduction, and coordinate assignment
    /// </summary>
    public class SugiyamaLayoutManager
    {
        private float _layerSpacing = 8f;
        private float _nodeSpacing = 6f;
        private int _maxIterations = 50;
        private bool _minimizeCrossings = true;

        private class SugiyamaNode
        {
            public TreeNode OriginalNode { get; set; }
            public int Layer { get; set; }
            public float Position { get; set; }
            public List<SugiyamaNode> Incoming { get; set; } = new List<SugiyamaNode>();
            public List<SugiyamaNode> Outgoing { get; set; } = new List<SugiyamaNode>();
            public bool IsDummy { get; set; } = false;
            public int OriginalX { get; set; }
        }

        /// <summary>
        /// Applies Sugiyama hierarchical layout to the node forest
        /// </summary>
        /// <param name="roots">Root nodes to layout</param>
        public void LayoutHierarchy(List<TreeNode> roots)
        {
            if (roots == null || roots.Count == 0) return;

            // Convert TreeNode structure to SugiyamaNode structure
            var sugiyamaNodes = ConvertToSugiyamaNodes(roots);
            if (sugiyamaNodes.Count == 0) return;

            try
            {
                // Phase 1: Cycle Removal (if needed)
                RemoveCycles(sugiyamaNodes);

                // Phase 2: Layer Assignment
                AssignLayers(sugiyamaNodes);

                // Phase 3: Insert dummy nodes for long edges
                var layeredNodes = InsertDummyNodes(sugiyamaNodes);

                // Phase 4: Crossing Reduction
                if (_minimizeCrossings)
                {
                    ReduceCrossings(layeredNodes);
                }

                // Phase 5: Coordinate Assignment
                AssignCoordinates(layeredNodes);

                // Apply positions to original GameObjects
                ApplyPositions(sugiyamaNodes);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in Sugiyama layout: {e.Message}");
            }
        }

        private List<SugiyamaNode> ConvertToSugiyamaNodes(List<TreeNode> roots)
        {
            var nodeMap = new Dictionary<TreeNode, SugiyamaNode>();
            var allNodes = new List<SugiyamaNode>();
            var visited = new HashSet<TreeNode>();

            // First pass: Create all SugiyamaNodes
            foreach (var root in roots)
            {
                CreateSugiyamaNodesRecursive(root, nodeMap, allNodes, visited);
            }

            // Second pass: Establish connections
            visited.Clear();
            foreach (var root in roots)
            {
                EstablishConnectionsRecursive(root, nodeMap, visited);
            }

            return allNodes;
        }

        private void CreateSugiyamaNodesRecursive(TreeNode node, Dictionary<TreeNode, SugiyamaNode> nodeMap, 
            List<SugiyamaNode> allNodes, HashSet<TreeNode> visited)
        {
            if (node == null || !visited.Add(node)) return;

            var sugiyamaNode = new SugiyamaNode { OriginalNode = node };
            nodeMap[node] = sugiyamaNode;
            allNodes.Add(sugiyamaNode);

            foreach (var child in node.Children)
            {
                CreateSugiyamaNodesRecursive(child, nodeMap, allNodes, visited);
            }
        }

        private void EstablishConnectionsRecursive(TreeNode node, Dictionary<TreeNode, SugiyamaNode> nodeMap, 
            HashSet<TreeNode> visited)
        {
            if (node == null || !visited.Add(node)) return;

            var sugiyamaNode = nodeMap[node];

            foreach (var child in node.Children)
            {
                if (nodeMap.ContainsKey(child))
                {
                    var childSugiyama = nodeMap[child];
                    sugiyamaNode.Outgoing.Add(childSugiyama);
                    childSugiyama.Incoming.Add(sugiyamaNode);
                }
                EstablishConnectionsRecursive(child, nodeMap, visited);
            }
        }

        private void RemoveCycles(List<SugiyamaNode> nodes)
        {
            // Simple cycle removal using DFS
            var visited = new HashSet<SugiyamaNode>();
            var recursionStack = new HashSet<SugiyamaNode>();
            var edgesToReverse = new List<(SugiyamaNode, SugiyamaNode)>();

            foreach (var node in nodes)
            {
                if (!visited.Contains(node))
                {
                    FindCycles(node, visited, recursionStack, edgesToReverse);
                }
            }

            // Reverse the edges that create cycles
            foreach (var (from, to) in edgesToReverse)
            {
                from.Outgoing.Remove(to);
                to.Incoming.Remove(from);
                to.Outgoing.Add(from);
                from.Incoming.Add(to);
            }
        }

        private bool FindCycles(SugiyamaNode node, HashSet<SugiyamaNode> visited, 
            HashSet<SugiyamaNode> recursionStack, List<(SugiyamaNode, SugiyamaNode)> edgesToReverse)
        {
            visited.Add(node);
            recursionStack.Add(node);

            foreach (var neighbor in node.Outgoing.ToList())
            {
                if (!visited.Contains(neighbor))
                {
                    if (FindCycles(neighbor, visited, recursionStack, edgesToReverse))
                        return true;
                }
                else if (recursionStack.Contains(neighbor))
                {
                    edgesToReverse.Add((node, neighbor));
                }
            }

            recursionStack.Remove(node);
            return false;
        }

        private void AssignLayers(List<SugiyamaNode> nodes)
        {
            // Find nodes with no incoming edges (sources)
            var sources = nodes.Where(n => n.Incoming.Count == 0).ToList();
            var queue = new Queue<SugiyamaNode>(sources);
            var incomingCount = nodes.ToDictionary(n => n, n => n.Incoming.Count);

            // Initialize layers
            foreach (var node in nodes)
            {
                node.Layer = 0;
            }

            // Topological sort with layer assignment
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var neighbor in current.Outgoing)
                {
                    neighbor.Layer = Mathf.Max(neighbor.Layer, current.Layer + 1);
                    incomingCount[neighbor]--;

                    if (incomingCount[neighbor] == 0)
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        private Dictionary<int, List<SugiyamaNode>> InsertDummyNodes(List<SugiyamaNode> nodes)
        {
            var layeredNodes = new Dictionary<int, List<SugiyamaNode>>();
            var allNodes = new List<SugiyamaNode>(nodes);

            // Group nodes by layer
            foreach (var node in nodes)
            {
                if (!layeredNodes.ContainsKey(node.Layer))
                    layeredNodes[node.Layer] = new List<SugiyamaNode>();
                layeredNodes[node.Layer].Add(node);
            }

            // Insert dummy nodes for edges that span multiple layers
            var edgesToProcess = new List<(SugiyamaNode from, SugiyamaNode to)>();
            
            foreach (var node in nodes)
            {
                foreach (var target in node.Outgoing.ToList())
                {
                    if (target.Layer - node.Layer > 1)
                    {
                        edgesToProcess.Add((node, target));
                    }
                }
            }

            foreach (var (from, to) in edgesToProcess)
            {
                // Remove original edge
                from.Outgoing.Remove(to);
                to.Incoming.Remove(from);

                // Insert dummy nodes
                var prev = from;
                for (int layer = from.Layer + 1; layer < to.Layer; layer++)
                {
                    var dummy = new SugiyamaNode
                    {
                        IsDummy = true,
                        Layer = layer,
                        OriginalNode = null
                    };

                    prev.Outgoing.Add(dummy);
                    dummy.Incoming.Add(prev);

                    if (!layeredNodes.ContainsKey(layer))
                        layeredNodes[layer] = new List<SugiyamaNode>();
                    layeredNodes[layer].Add(dummy);

                    allNodes.Add(dummy);
                    prev = dummy;
                }

                // Connect last dummy to target
                prev.Outgoing.Add(to);
                to.Incoming.Add(prev);
            }

            return layeredNodes;
        }

        private void ReduceCrossings(Dictionary<int, List<SugiyamaNode>> layeredNodes)
        {
            var layers = layeredNodes.Keys.OrderBy(k => k).ToList();
            
            for (int iteration = 0; iteration < _maxIterations; iteration++)
            {
                var improved = false;

                // Sweep down
                for (int i = 1; i < layers.Count; i++)
                {
                    if (OptimizeLayer(layeredNodes[layers[i]], true))
                        improved = true;
                }

                // Sweep up
                for (int i = layers.Count - 2; i >= 0; i--)
                {
                    if (OptimizeLayer(layeredNodes[layers[i]], false))
                        improved = true;
                }

                if (!improved) break;
            }
        }

        private bool OptimizeLayer(List<SugiyamaNode> layer, bool useIncoming)
        {
            var originalOrder = layer.ToList();
            
            // Calculate barycenter for each node
            var barycenters = new Dictionary<SugiyamaNode, float>();
            
            foreach (var node in layer)
            {
                var connections = useIncoming ? node.Incoming : node.Outgoing;
                if (connections.Count > 0)
                {
                    barycenters[node] = connections.Average(n => n.Position);
                }
                else
                {
                    barycenters[node] = node.Position;
                }
            }

            // Sort by barycenter
            layer.Sort((a, b) => barycenters[a].CompareTo(barycenters[b]));

            // Update positions
            for (int i = 0; i < layer.Count; i++)
            {
                layer[i].Position = i;
            }

            // Check if order changed
            return !layer.SequenceEqual(originalOrder);
        }

        private void AssignCoordinates(Dictionary<int, List<SugiyamaNode>> layeredNodes)
        {
            var maxLayer = layeredNodes.Keys.Max();
            var layerHeight = maxLayer * _layerSpacing * 0.5f;

            foreach (var kvp in layeredNodes)
            {
                var layer = kvp.Key;
                var nodes = kvp.Value;

                var y = layerHeight - (layer * _layerSpacing);
                var totalWidth = (nodes.Count - 1) * _nodeSpacing;
                var startX = -totalWidth * 0.5f;

                for (int i = 0; i < nodes.Count; i++)
                {
                    var x = startX + (i * _nodeSpacing);
                    nodes[i].Position = x;
                    
                    if (!nodes[i].IsDummy && nodes[i].OriginalNode?.GameObject != null)
                    {
                        nodes[i].OriginalNode.GameObject.transform.position = new Vector3(x, y, 0f);
                    }
                }
            }
        }

        private void ApplyPositions(List<SugiyamaNode> nodes)
        {
            foreach (var node in nodes.Where(n => !n.IsDummy && n.OriginalNode?.GameObject != null))
            {
                var currentPos = node.OriginalNode.GameObject.transform.position;
                node.OriginalNode.GameObject.transform.position = new Vector3(currentPos.x, currentPos.y, 0f);
            }
        }

        public void SetLayoutParameters(LayoutParameters parameters)
        {
            _layerSpacing = parameters.levelSpacing > 0 ? parameters.levelSpacing : _layerSpacing;
            _nodeSpacing = parameters.nodeSpacing > 0 ? parameters.nodeSpacing : _nodeSpacing;
        }

        public void SetSugiyamaParameters(float layerSpacing, float nodeSpacing, int maxIterations = 50, bool minimizeCrossings = true)
        {
            _layerSpacing = layerSpacing;
            _nodeSpacing = nodeSpacing;
            _maxIterations = maxIterations;
            _minimizeCrossings = minimizeCrossings;
        }
    }
}