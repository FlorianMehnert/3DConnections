namespace _3DConnections.Runtime.Nodes.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using ScriptableObjects;

    /// <summary>
    /// Fast Multi-scale Method for drawing large graphs
    /// Uses a hierarchical coarsening approach followed by force-directed placement
    /// </summary>
    public class MultiscaleLayoutManager
    {
        private float _repulsiveForce = 1000f;
        private float _attractiveForce = 0.1f;
        private float _damping = 0.9f;
        private float _timeStep = 0.1f;
        private int _maxIterationsPerLevel = 100;
        private int _coarseningThreshold = 50;
        private float _coolingRate = 0.95f;
        private float _minTemperature = 0.01f;

        private class MultiscaleNode
        {
            public TreeNode OriginalNode { get; set; }
            public Vector2 Position { get; set; }
            public Vector2 Velocity { get; set; }
            public Vector2 Force { get; set; }
            public float Mass { get; set; } = 1f;
            public List<MultiscaleNode> Connections { get; set; } = new List<MultiscaleNode>();
            public List<TreeNode> RepresentedNodes { get; set; } = new List<TreeNode>();
            public bool IsCoarsened { get; set; } = false;
        }

        private class GraphLevel
        {
            public List<MultiscaleNode> Nodes { get; set; } = new List<MultiscaleNode>();
            public List<(MultiscaleNode, MultiscaleNode)> Edges { get; set; } = new List<(MultiscaleNode, MultiscaleNode)>();
        }

        /// <summary>
        /// Applies multiscale force-directed layout for large graphs
        /// </summary>
        /// <param name="roots">Root nodes to layout</param>
        public void LayoutMultiscale(List<TreeNode> roots)
        {
            if (roots == null || roots.Count == 0) return;

            try
            {
                // Convert to multiscale nodes
                var allNodes = CollectAllNodes(roots);
                if (allNodes.Count == 0) return;

                var multiscaleNodes = ConvertToMultiscaleNodes(allNodes);
                
                // Create hierarchy of coarsened graphs
                var graphLevels = CreateMultiscaleHierarchy(multiscaleNodes);
                
                // Layout from coarsest to finest level
                for (int level = graphLevels.Count - 1; level >= 0; level--)
                {
                    LayoutLevel(graphLevels[level], level == graphLevels.Count - 1);
                    
                    // Interpolate positions to next level
                    if (level > 0)
                    {
                        InterpolateToNextLevel(graphLevels[level], graphLevels[level - 1]);
                    }
                }

                // Apply final positions
                ApplyPositions(graphLevels[0].Nodes);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in Multiscale layout: {e.Message}");
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

        private List<MultiscaleNode> ConvertToMultiscaleNodes(List<TreeNode> treeNodes)
        {
            var nodeMap = new Dictionary<TreeNode, MultiscaleNode>();
            var multiscaleNodes = new List<MultiscaleNode>();

            // Create multiscale nodes
            foreach (var treeNode in treeNodes)
            {
                var msNode = new MultiscaleNode
                {
                    OriginalNode = treeNode,
                    Position = new Vector2(
                        UnityEngine.Random.Range(-10f, 10f),
                        UnityEngine.Random.Range(-10f, 10f)
                    ),
                    Velocity = Vector2.zero,
                    Force = Vector2.zero
                };
                msNode.RepresentedNodes.Add(treeNode);
                
                nodeMap[treeNode] = msNode;
                multiscaleNodes.Add(msNode);
            }

            // Establish connections
            foreach (var treeNode in treeNodes)
            {
                var msNode = nodeMap[treeNode];
                foreach (var child in treeNode.Children)
                {
                    if (nodeMap.ContainsKey(child))
                    {
                        var childMsNode = nodeMap[child];
                        if (!msNode.Connections.Contains(childMsNode))
                        {
                            msNode.Connections.Add(childMsNode);
                            childMsNode.Connections.Add(msNode);
                        }
                    }
                }
            }

            return multiscaleNodes;
        }

        private List<GraphLevel> CreateMultiscaleHierarchy(List<MultiscaleNode> nodes)
        {
            var levels = new List<GraphLevel>();
            var currentLevel = new GraphLevel { Nodes = new List<MultiscaleNode>(nodes) };
            
            // Create edges list
            var edgeSet = new HashSet<(MultiscaleNode, MultiscaleNode)>();
            foreach (var node in nodes)
            {
                foreach (var connection in node.Connections)
                {
                    var edge = node.GetHashCode() < connection.GetHashCode() 
                        ? (node, connection) 
                        : (connection, node);
                    edgeSet.Add(edge);
                }
            }
            currentLevel.Edges = edgeSet.ToList();
            
            levels.Add(currentLevel);

            // Create coarsened levels
            while (currentLevel.Nodes.Count > _coarseningThreshold)
            {
                var coarsenedLevel = CoarsenGraph(currentLevel);
                if (coarsenedLevel.Nodes.Count >= currentLevel.Nodes.Count)
                    break; // Cannot coarsen further
                
                levels.Add(coarsenedLevel);
                currentLevel = coarsenedLevel;
            }

            return levels;
        }

        private GraphLevel CoarsenGraph(GraphLevel level)
        {
            var coarsenedLevel = new GraphLevel();
            var processed = new HashSet<MultiscaleNode>();
            var nodeMapping = new Dictionary<MultiscaleNode, MultiscaleNode>();

            // Match nodes for coarsening (simple greedy matching)
            foreach (var node in level.Nodes)
            {
                if (processed.Contains(node)) continue;

                var coarsenedNode = new MultiscaleNode
                {
                    Position = node.Position,
                    Velocity = Vector2.zero,
                    Force = Vector2.zero,
                    Mass = node.Mass,
                    IsCoarsened = true
                };
                
                coarsenedNode.RepresentedNodes.AddRange(node.RepresentedNodes);
                processed.Add(node);
                nodeMapping[node] = coarsenedNode;

                // Find best neighbor to merge with
                MultiscaleNode bestNeighbor = null;
                float bestWeight = 0f;

                foreach (var neighbor in node.Connections)
                {
                    if (!processed.Contains(neighbor))
                    {
                        float weight = 1f / (1f + Vector2.Distance(node.Position, neighbor.Position));
                        if (weight > bestWeight)
                        {
                            bestWeight = weight;
                            bestNeighbor = neighbor;
                        }
                    }
                }

                // Merge with best neighbor if found
                if (bestNeighbor != null)
                {
                    coarsenedNode.Position = (node.Position + bestNeighbor.Position) * 0.5f;
                    coarsenedNode.Mass += bestNeighbor.Mass;
                    coarsenedNode.RepresentedNodes.AddRange(bestNeighbor.RepresentedNodes);
                    processed.Add(bestNeighbor);
                    nodeMapping[bestNeighbor] = coarsenedNode;
                }

                coarsenedLevel.Nodes.Add(coarsenedNode);
            }

            // Create coarsened edges
            var edgeSet = new HashSet<(MultiscaleNode, MultiscaleNode)>();
            foreach (var (nodeA, nodeB) in level.Edges)
            {
                var coarsenedA = nodeMapping.ContainsKey(nodeA) ? nodeMapping[nodeA] : null;
                var coarsenedB = nodeMapping.ContainsKey(nodeB) ? nodeMapping[nodeB] : null;

                if (coarsenedA != null && coarsenedB != null && coarsenedA != coarsenedB)
                {
                    var edge = coarsenedA.GetHashCode() < coarsenedB.GetHashCode() 
                        ? (coarsenedA, coarsenedB) 
                        : (coarsenedB, coarsenedA);
                    edgeSet.Add(edge);
                }
            }
            coarsenedLevel.Edges = edgeSet.ToList();

            // Update connections in coarsened nodes
            foreach (var node in coarsenedLevel.Nodes)
            {
                node.Connections.Clear();
            }

            foreach (var (nodeA, nodeB) in coarsenedLevel.Edges)
            {
                nodeA.Connections.Add(nodeB);
                nodeB.Connections.Add(nodeA);
            }

            return coarsenedLevel;
        }

        private void LayoutLevel(GraphLevel level, bool isCoarsest)
        {
            float temperature = isCoarsest ? 10f : 1f;
            int iterations = isCoarsest ? _maxIterationsPerLevel * 2 : _maxIterationsPerLevel;

            for (int iter = 0; iter < iterations; iter++)
            {
                // Calculate forces
                foreach (var node in level.Nodes)
                {
                    node.Force = Vector2.zero;
                }

                // Repulsive forces (all pairs)
                for (int i = 0; i < level.Nodes.Count; i++)
                {
                    for (int j = i + 1; j < level.Nodes.Count; j++)
                    {
                        ApplyRepulsiveForce(level.Nodes[i], level.Nodes[j]);
                    }
                }

                // Attractive forces (connected pairs)
                foreach (var (nodeA, nodeB) in level.Edges)
                {
                    ApplyAttractiveForce(nodeA, nodeB);
                }

                // Apply forces and update positions
                foreach (var node in level.Nodes)
                {
                    // Limit force magnitude
                    var forceMagnitude = node.Force.magnitude;
                    if (forceMagnitude > temperature)
                    {
                        node.Force = node.Force.normalized * temperature;
                    }

                    // Update velocity and position
                    node.Velocity = (node.Velocity + node.Force * _timeStep / node.Mass) * _damping;
                    node.Position += node.Velocity * _timeStep;
                }

                // Cool down temperature
                temperature *= _coolingRate;
                if (temperature < _minTemperature)
                    temperature = _minTemperature;

                // Early termination if system is stable
                var totalKineticEnergy = level.Nodes.Sum(n => n.Velocity.sqrMagnitude * n.Mass);
                if (totalKineticEnergy < 0.001f)
                    break;
            }
        }

        private void ApplyRepulsiveForce(MultiscaleNode nodeA, MultiscaleNode nodeB)
        {
            var delta = nodeA.Position - nodeB.Position;
            var distance = delta.magnitude;
            
            if (distance < 0.1f)
            {
                // Add small random displacement to avoid division by zero
                delta = new Vector2(UnityEngine.Random.Range(-0.1f, 0.1f), UnityEngine.Random.Range(-0.1f, 0.1f));
                distance = delta.magnitude;
            }

            var force = (_repulsiveForce * nodeA.Mass * nodeB.Mass) / (distance * distance);
            var forceVector = delta.normalized * force;

            nodeA.Force += forceVector;
            nodeB.Force -= forceVector;
        }

        private void ApplyAttractiveForce(MultiscaleNode nodeA, MultiscaleNode nodeB)
        {
            var delta = nodeB.Position - nodeA.Position;
            var distance = delta.magnitude;
            
            var idealDistance = Mathf.Sqrt(nodeA.Mass * nodeB.Mass) * 2f;
            var force = _attractiveForce * (distance - idealDistance);
            var forceVector = delta.normalized * force;

            nodeA.Force += forceVector;
            nodeB.Force -= forceVector;
        }

        private void InterpolateToNextLevel(GraphLevel coarseLevel, GraphLevel fineLevel)
        {
            foreach (var fineNode in fineLevel.Nodes)
            {
                if (fineNode.IsCoarsened) continue;

                // Find the coarse node that represents this fine node
                MultiscaleNode representingCoarseNode = null;
                foreach (var coarseNode in coarseLevel.Nodes)
                {
                    if (coarseNode.RepresentedNodes.Contains(fineNode.OriginalNode))
                    {
                        representingCoarseNode = coarseNode;
                        break;
                    }
                }

                if (representingCoarseNode != null)
                {
                    // Add small random offset to avoid overlapping
                    var offset = new Vector2(
                        UnityEngine.Random.Range(-0.5f, 0.5f),
                        UnityEngine.Random.Range(-0.5f, 0.5f)
                    );
                    fineNode.Position = representingCoarseNode.Position + offset;
                    fineNode.Velocity = representingCoarseNode.Velocity * 0.1f;
                }
            }
        }

        private void ApplyPositions(List<MultiscaleNode> nodes)
        {
            // Find bounds for normalization
            if (nodes.Count == 0) return;

            var minX = nodes.Min(n => n.Position.x);
            var maxX = nodes.Max(n => n.Position.x);
            var minY = nodes.Min(n => n.Position.y);
            var maxY = nodes.Max(n => n.Position.y);

            var centerX = (minX + maxX) * 0.5f;
            var centerY = (minY + maxY) * 0.5f;

            // Apply positions to GameObjects
            foreach (var node in nodes)
            {
                if (node.OriginalNode?.GameObject != null)
                {
                    var normalizedPos = new Vector3(
                        node.Position.x - centerX,
                        node.Position.y - centerY,
                        0f
                    );
                    node.OriginalNode.GameObject.transform.position = normalizedPos;
                }
            }
        }

        public void SetLayoutParameters(LayoutParameters parameters)
        {
            if (parameters.nodeSpacing > 0)
                _attractiveForce = 1f / parameters.nodeSpacing;
            
            if (parameters.minDistance > 0)
                _repulsiveForce = parameters.minDistance * parameters.minDistance * 100f;
        }

        public void SetMultiscaleParameters(float repulsiveForce, float attractiveForce, float damping = 0.9f, 
            float timeStep = 0.1f, int maxIterationsPerLevel = 100, int coarseningThreshold = 50, 
            float coolingRate = 0.95f)
        {
            _repulsiveForce = repulsiveForce;
            _attractiveForce = attractiveForce;
            _damping = damping;
            _timeStep = timeStep;
            _maxIterationsPerLevel = maxIterationsPerLevel;
            _coarseningThreshold = coarseningThreshold;
            _coolingRate = coolingRate;
        }

        /// <summary>
        /// Alternative layout method for very large graphs using spatial hashing for optimization
        /// </summary>
        /// <param name="roots">Root nodes to layout</param>
        public void LayoutLargeGraph(List<TreeNode> roots)
        {
            if (roots == null || roots.Count == 0) return;

            var allNodes = CollectAllNodes(roots);
            if (allNodes.Count < 1000) // Use regular multiscale for smaller graphs
            {
                LayoutMultiscale(roots);
                return;
            }

            // For very large graphs, use a simplified approach with spatial hashing
            var multiscaleNodes = ConvertToMultiscaleNodes(allNodes);
            var gridSize = Mathf.CeilToInt(Mathf.Sqrt(multiscaleNodes.Count));
            var cellSize = 50f / gridSize;

            // Initial random placement in grid
            for (int i = 0; i < multiscaleNodes.Count; i++)
            {
                var row = i / gridSize;
                var col = i % gridSize;
                multiscaleNodes[i].Position = new Vector2(
                    (col - gridSize * 0.5f) * cellSize + UnityEngine.Random.Range(-cellSize * 0.2f, cellSize * 0.2f),
                    (row - gridSize * 0.5f) * cellSize + UnityEngine.Random.Range(-cellSize * 0.2f, cellSize * 0.2f)
                );
            }

            // Apply limited force iterations
            for (int iter = 0; iter < 50; iter++)
            {
                foreach (var node in multiscaleNodes)
                {
                    node.Force = Vector2.zero;
                    
                    // Only apply forces to nearby nodes and connections
                    foreach (var connection in node.Connections)
                    {
                        ApplyAttractiveForce(node, connection);
                    }
                }

                foreach (var node in multiscaleNodes)
                {
                    node.Velocity = (node.Velocity + node.Force * _timeStep) * _damping;
                    node.Position += node.Velocity * _timeStep;
                }
            }

            ApplyPositions(multiscaleNodes);
        }
    }
}