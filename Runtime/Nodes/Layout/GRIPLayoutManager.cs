namespace _3DConnections.Runtime.Nodes.Layout
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using ScriptableObjects;
    
    /// <summary>
    /// GRIP (Graph drawing with Intelligent Placement) layout algorithm implementation
    /// Based on force-directed principles with multi-level optimization
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class GRIPLayoutManager
    {
        private readonly LayoutParameters _layoutParameters;
        public GRIPLayoutManager(LayoutParameters layoutParameters)
        {
            _layoutParameters = layoutParameters;
        }
        
        // ReSharper disable once InconsistentNaming
        private class GRIPNode
        {
            public GameObject GameObject { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Force { get; set; }
            public List<GRIPNode> Neighbors { get; set; } = new List<GRIPNode>();
            public GRIPNode CoarseParent { get; set; }
            public List<GRIPNode> FineChildren { get; set; } = new List<GRIPNode>();
            public float Mass { get; set; } = 1.0f;
        }
        
        /// <summary>
        /// Apply GRIP layout to the given connections
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public void ApplyGRIPLayout(List<NodeConnection> connections)
        {
            if (connections == null || connections.Count == 0) return;
            
            // Build the graph structure
            var nodeMap = BuildGraphStructure(connections);
            if (nodeMap.Count == 0) return;
            
            // Create a hierarchy of coarsened graphs
            var graphHierarchy = CreateCoarsenedHierarchy(nodeMap);
            
            // Layout from coarsest to finest level
            for (int level = graphHierarchy.Count - 1; level >= 0; level--)
            {
                var currentLevel = graphHierarchy[level];
                
                if (level == graphHierarchy.Count - 1)
                {
                    // Initialize positions for the coarsest level
                    InitializePositions(currentLevel);
                }
                else
                {
                    // Interpolate positions from a coarser level
                    InterpolatePositions(currentLevel);
                }
                
                // Apply force-directed layout at this level
                ApplyForceDirectedLayout(currentLevel, _layoutParameters.iterations / (level + 1));
            }
            
            // Apply final positions to GameObjects
            ApplyPositionsToGameObjects(graphHierarchy[0]);
        }
        
        private Dictionary<GameObject, GRIPNode> BuildGraphStructure(List<NodeConnection> connections)
        {
            var nodeMap = new Dictionary<GameObject, GRIPNode>();
            
            // Create nodes
            foreach (var connection in connections)
            {
                if (!nodeMap.ContainsKey(connection.startNode))
                    nodeMap[connection.startNode] = new GRIPNode { GameObject = connection.startNode };
                if (!nodeMap.ContainsKey(connection.endNode))
                    nodeMap[connection.endNode] = new GRIPNode { GameObject = connection.endNode };
            }
            
            // Create edges
            foreach (var connection in connections)
            {
                var startNode = nodeMap[connection.startNode];
                var endNode = nodeMap[connection.endNode];
                
                if (!startNode.Neighbors.Contains(endNode))
                    startNode.Neighbors.Add(endNode);
                if (!endNode.Neighbors.Contains(startNode))
                    endNode.Neighbors.Add(startNode);
            }
            
            return nodeMap;
        }
        
        private List<Dictionary<GameObject, GRIPNode>> CreateCoarsenedHierarchy(Dictionary<GameObject, GRIPNode> originalGraph)
        {
            var hierarchy = new List<Dictionary<GameObject, GRIPNode>> { originalGraph };
            var currentLevel = originalGraph;
            
            for (int i = 0; i < _layoutParameters.coarseningLevels; i++)
            {
                var coarsenedLevel = CoarsenGraph(currentLevel);
                if (coarsenedLevel.Count >= currentLevel.Count * _layoutParameters.coarseningRatio)
                {
                    hierarchy.Add(coarsenedLevel);
                    currentLevel = coarsenedLevel;
                }
                else
                {
                    break; // Stop if coarsening is not effective
                }
            }
            
            return hierarchy;
        }
        
        private Dictionary<GameObject, GRIPNode> CoarsenGraph(Dictionary<GameObject, GRIPNode> graph)
        {
            var coarsenedGraph = new Dictionary<GameObject, GRIPNode>();
            var visited = new HashSet<GRIPNode>();
            
            foreach (var kvp in graph)
            {
                var node = kvp.Value;
                if (visited.Contains(node)) continue;
                
                // Find the best neighbor to merge with
                GRIPNode bestNeighbor = null;
                float bestScore = float.MinValue;
                
                foreach (var neighbor in node.Neighbors)
                {
                    if (visited.Contains(neighbor)) continue;
                    
                    // Score based on edge weight and node similarity
                    float score = 1.0f / (neighbor.Neighbors.Count + 1);
                    if (!(score > bestScore)) continue;
                    bestScore = score;
                    bestNeighbor = neighbor;
                }
                
                // Create coarse node
                var coarseNode = new GRIPNode
                {
                    GameObject = node.GameObject, // Use one of the original GameObjects as representative
                    Mass = node.Mass
                };
                
                coarseNode.FineChildren.Add(node);
                node.CoarseParent = coarseNode;
                visited.Add(node);
                
                if (bestNeighbor != null && !visited.Contains(bestNeighbor))
                {
                    coarseNode.FineChildren.Add(bestNeighbor);
                    bestNeighbor.CoarseParent = coarseNode;
                    coarseNode.Mass += bestNeighbor.Mass;
                    visited.Add(bestNeighbor);
                }
                
                coarsenedGraph[coarseNode.GameObject] = coarseNode;
            }
            
            // Build coarse edges
            foreach (var kvp in coarsenedGraph)
            {
                var coarseNode = kvp.Value;
                var neighborSet = new HashSet<GRIPNode>();
                
                foreach (var fineChild in coarseNode.FineChildren)
                {
                    foreach (var fineNeighbor in fineChild.Neighbors)
                    {
                        if (fineNeighbor.CoarseParent != null && 
                            fineNeighbor.CoarseParent != coarseNode)
                        {
                            neighborSet.Add(fineNeighbor.CoarseParent);
                        }
                    }
                }
                
                coarseNode.Neighbors = neighborSet.ToList();
            }
            
            return coarsenedGraph;
        }
        
        private void InitializePositions(Dictionary<GameObject, GRIPNode> graph)
        {
            var nodes = graph.Values.ToList();
            var radius = Mathf.Sqrt(nodes.Count) * _layoutParameters.idealEdgeLength;
            
            for (int i = 0; i < nodes.Count; i++)
            {
                float angle = 2 * Mathf.PI * i / nodes.Count;
                nodes[i].Position = new Vector3(
                    radius * Mathf.Cos(angle),
                    radius * Mathf.Sin(angle),
                    0
                );
            }
        }
        
        private void InterpolatePositions(Dictionary<GameObject, GRIPNode> fineLevel)
        {
            foreach (var fineNode in fineLevel.Values)
            {
                if (fineNode.CoarseParent == null) continue;
                // Add a small random offset to avoid overlapping
                var offset = Random.insideUnitSphere * 0.5f;
                offset.z = 0;
                fineNode.Position = fineNode.CoarseParent.Position + offset;
            }
        }
        
        private void ApplyForceDirectedLayout(Dictionary<GameObject, GRIPNode> graph, int iterations)
        {
            var nodes = graph.Values.ToList();
            float temperature = _layoutParameters.initialTemperature;
            
            for (int iter = 0; iter < iterations; iter++)
            {
                // Calculate forces
                foreach (var node in nodes)
                {
                    node.Force = Vector3.zero;
                    
                    // Repulsive forces
                    foreach (var other in nodes)
                    {
                        if (node == other) continue;
                        
                        Vector3 delta = node.Position - other.Position;
                        float distance = delta.magnitude;

                        if (!(distance > 0) || !(distance < 50f)) continue; // Cutoff for performance
                        float repulsion = (_layoutParameters.repulsion * node.Mass * other.Mass) / (distance * distance);
                        node.Force += delta.normalized * repulsion;
                    }
                    
                    // Attractive forces
                    foreach (var neighbor in node.Neighbors)
                    {
                        Vector3 delta = neighbor.Position - node.Position;
                        float distance = delta.magnitude;

                        if (!(distance > 0)) continue;
                        float attraction = _layoutParameters.attractionStrength * Mathf.Log(distance / _layoutParameters.idealEdgeLength);
                        node.Force += delta.normalized * attraction;
                    }
                }
                
                // Update positions
                foreach (var node in nodes)
                {
                    var displacement = node.Force.normalized * Mathf.Min(temperature, node.Force.magnitude);
                    node.Position += displacement;
                    
                    // Keep nodes on the z=0 plane
                    node.Position = new Vector3(node.Position.x, node.Position.y, 0);
                }
                
                // Cool down
                temperature *= _layoutParameters.coolingFactor;
            }
        }
        
        private void ApplyPositionsToGameObjects(Dictionary<GameObject, GRIPNode> graph)
        {
            foreach (var kvp in graph)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.transform.position = kvp.Value.Position;
                }
            }
        }
        
    }
}
