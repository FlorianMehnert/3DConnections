namespace _3DConnections.Runtime.Nodes.Layout
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using ScriptableObjects;

    /// <summary>
    /// FM³ (Fast Multipole Multilevel Method) implementation for graph layout
    /// Optimized for directed graphs with cycles
    /// </summary>
    public class FM3LayoutManager
    {
        // FM³ specific parameters
        private readonly FM3Parameters _params = new FM3Parameters();
        
        // Multipole expansion order
        private const int MultipoleOrder = 4;
        
        // Direction bias for directed graphs
        private const float DirectionBias = 0.3f;
        
        private class FM3Parameters
        {
            // Force parameters (FM³ defaults)
            public float RepulsiveForceConstant { get; set; } = 100f;
            public float AttractiveForceConstant { get; set; } = 0.1f;
            public float IdealEdgeLength { get; set; } = 10f;
            
            // Multilevel parameters
            public int MaxLevels { get; set; } = 10;
            public int MinGraphSize { get; set; } = 20;
            public float CoarseningFactor { get; set; } = 0.5f;
            
            // Iteration parameters per level
            public int InitialIterations { get; set; } = 300;
            public int RefinementIterations { get; set; } = 50;
            
            // Temperature and cooling
            public float InitialTemperature { get; set; } = 10f;
            public float FinalTemperature { get; set; } = 0.01f;
            public float CoolingFactor { get; set; } = 0.95f;
            
            // Adaptive parameters
            public bool UseBarnesHut { get; set; } = true;
            public float BarnesHutTheta { get; set; } = 0.5f;
            
            // Directed graph specific
            public bool IsDirected { get; set; } = true;
            public float DirectionalForce { get; set; } = 2f;
            public float CycleHandlingStrength { get; set; } = 0.5f;
        }
        
        private class FM3Node
        {
            public TreeNode OriginalNode { get; set; }
            public Vector2 Position { get; set; }
            public Vector2 Force { get; set; }
            public Vector2 OldForce { get; set; }
            public float Mass { get; set; } = 1f;
            public float Radius { get; set; } = 1f;
            
            // Multilevel properties
            public List<FM3Node> Children { get; set; } = new List<FM3Node>();
            public FM3Node Parent { get; set; }
            public int Level { get; set; }
            
            // Directed graph properties
            public List<FM3Node> IncomingNeighbors { get; set; } = new List<FM3Node>();
            public List<FM3Node> OutgoingNeighbors { get; set; } = new List<FM3Node>();
            public int TopologicalOrder { get; set; } = -1;
            public bool InCycle { get; set; } = false;
            
            // Multipole coefficients for fast force calculation
            public Vector2 MultipoleCenter { get; set; }
            public float TotalCharge { get; set; }
            public Vector2[] MultipoleCoefficients { get; set; }
        }
        
        private class FM3Edge
        {
            public FM3Node Source { get; set; }
            public FM3Node Target { get; set; }
            public float Weight { get; set; } = 1f;
            public bool IsBackEdge { get; set; } = false; // For cycle detection
        }
        
        private class QuadTreeNode
        {
            public Vector2 Center { get; set; }
            public float Size { get; set; }
            public List<FM3Node> Nodes { get; set; } = new List<FM3Node>();
            public QuadTreeNode[] Children { get; set; }
            public Vector2 CenterOfMass { get; set; }
            public float TotalMass { get; set; }
            
            public bool IsLeaf => Children == null || Children.Length == 0;
        }
        
        // ReSharper disable once InconsistentNaming
        public void LayoutFM3(List<TreeNode> roots)
        {
            if (roots == null || roots.Count == 0) return;
            
            try
            {
                // Step 1: Build FM³ graph structure
                var (nodes, edges) = BuildFM3Graph(roots);
                if (nodes.Count == 0) return;
                
                // Step 2: Detect cycles and compute topological properties
                if (_params.IsDirected)
                {
                    DetectCyclesAndComputeOrder(nodes, edges);
                }
                
                // Step 3: Create multilevel hierarchy
                var hierarchy = CreateMultilevelHierarchy(nodes, edges);
                
                // Step 4: Initial placement at coarsest level
                InitialPlacement(hierarchy[hierarchy.Count - 1].Item1);
                
                // Step 5: Multilevel layout with FM³ force calculation
                for (int level = hierarchy.Count - 1; level >= 0; level--)
                {
                    var (levelNodes, levelEdges) = hierarchy[level];
                    
                    // Use more iterations at coarsest level
                    int iterations = level == hierarchy.Count - 1 
                        ? _params.InitialIterations 
                        : _params.RefinementIterations;
                    
                    // FM³ layout with fast multipole method
                    LayoutWithFM3(levelNodes, levelEdges, iterations, level);
                    
                    // Interpolate to next level
                    if (level > 0)
                    {
                        InterpolatePositions(hierarchy[level], hierarchy[level - 1]);
                    }
                }
                
                // Step 6: Post-processing for directed graphs
                if (_params.IsDirected)
                {
                    PostProcessDirectedLayout(nodes, edges);
                }
                
                // Step 7: Apply final positions
                ApplyFinalPositions(nodes);
            }
            catch (Exception e)
            {
                Debug.LogError($"FM³ Layout Error: {e.Message}\n{e.StackTrace}");
            }
        }
        
        private (List<FM3Node>, List<FM3Edge>) BuildFM3Graph(List<TreeNode> roots)
        {
            var nodeMap = new Dictionary<TreeNode, FM3Node>();
            var nodes = new List<FM3Node>();
            var edges = new List<FM3Edge>();
            var visited = new HashSet<TreeNode>();
            
            // Build nodes
            void ProcessNode(TreeNode treeNode)
            {
                if (treeNode == null || !visited.Add(treeNode)) return;
                
                var fm3Node = new FM3Node
                {
                    OriginalNode = treeNode,
                    Position = Vector2.zero,
                    Level = 0
                };
                
                nodeMap[treeNode] = fm3Node;
                nodes.Add(fm3Node);
                
                foreach (var child in treeNode.Children)
                {
                    ProcessNode(child);
                }
            }
            
            foreach (var root in roots)
            {
                ProcessNode(root);
            }
            
            // Build edges with directionality
            foreach (var node in nodeMap.Values)
            {
                foreach (var child in node.OriginalNode.Children)
                {
                    if (nodeMap.ContainsKey(child))
                    {
                        var targetNode = nodeMap[child];
                        var edge = new FM3Edge
                        {
                            Source = node,
                            Target = targetNode
                        };
                        
                        edges.Add(edge);
                        
                        // Set up directed neighbors
                        node.OutgoingNeighbors.Add(targetNode);
                        targetNode.IncomingNeighbors.Add(node);
                    }
                }
            }
            
            return (nodes, edges);
        }
        
        private void DetectCyclesAndComputeOrder(List<FM3Node> nodes, List<FM3Edge> edges)
        {
            // DFS-based cycle detection and topological sort attempt
            var visited = new HashSet<FM3Node>();
            var recursionStack = new HashSet<FM3Node>();
            var topoOrder = new List<FM3Node>();
            var cycleNodes = new HashSet<FM3Node>();
            
            void DFS(FM3Node node)
            {
                visited.Add(node);
                recursionStack.Add(node);
                
                foreach (var neighbor in node.OutgoingNeighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        DFS(neighbor);
                    }
                    else if (recursionStack.Contains(neighbor))
                    {
                        // Found a cycle - mark all nodes in the cycle
                        node.InCycle = true;
                        neighbor.InCycle = true;
                        cycleNodes.Add(node);
                        cycleNodes.Add(neighbor);
                        
                        // Mark edge as back edge
                        var backEdge = edges.FirstOrDefault(e => 
                            e.Source == node && e.Target == neighbor);
                        if (backEdge != null)
                            backEdge.IsBackEdge = true;
                    }
                }
                
                recursionStack.Remove(node);
                topoOrder.Insert(0, node);
            }
            
            // Run DFS from all unvisited nodes
            foreach (var node in nodes)
            {
                if (!visited.Contains(node))
                {
                    DFS(node);
                }
            }
            
            // Assign topological order (approximation for nodes not in cycles)
            for (int i = 0; i < topoOrder.Count; i++)
            {
                if (!topoOrder[i].InCycle)
                {
                    topoOrder[i].TopologicalOrder = i;
                }
            }
        }
        
        private List<(List<FM3Node>, List<FM3Edge>)> CreateMultilevelHierarchy(
            List<FM3Node> nodes, List<FM3Edge> edges)
        {
            var hierarchy = new List<(List<FM3Node>, List<FM3Edge>)>();
            hierarchy.Add((nodes, edges));
            
            var currentNodes = nodes;
            var currentEdges = edges;
            int level = 1;
            
            while (currentNodes.Count > _params.MinGraphSize && level < _params.MaxLevels)
            {
                var (coarsenedNodes, coarsenedEdges) = CoarsenGraph(currentNodes, currentEdges, level);
                
                if (coarsenedNodes.Count >= currentNodes.Count * 0.95f)
                    break; // Cannot coarsen effectively
                
                hierarchy.Add((coarsenedNodes, coarsenedEdges));
                currentNodes = coarsenedNodes;
                currentEdges = coarsenedEdges;
                level++;
            }
            
            return hierarchy;
        }
        
        private (List<FM3Node>, List<FM3Edge>) CoarsenGraph(
            List<FM3Node> nodes, List<FM3Edge> edges, int level)
        {
            var coarsenedNodes = new List<FM3Node>();
            var nodeMapping = new Dictionary<FM3Node, FM3Node>();
            var matched = new HashSet<FM3Node>();
            
            // Solar System model: match nodes with strongest connections
            var edgeWeights = new Dictionary<(FM3Node, FM3Node), float>();
            foreach (var edge in edges)
            {
                var key = (edge.Source, edge.Target);
                edgeWeights[key] = edge.Weight;
            }
            
            // Sort edges by weight for matching
            var sortedEdges = edges.OrderByDescending(e => e.Weight).ToList();
            
            foreach (var edge in sortedEdges)
            {
                if (matched.Contains(edge.Source) || matched.Contains(edge.Target))
                    continue;
                
                // Create coarsened node (solar system)
                var coarsenedNode = new FM3Node
                {
                    Position = (edge.Source.Position + edge.Target.Position) * 0.5f,
                    Mass = edge.Source.Mass + edge.Target.Mass,
                    Level = level,
                    Children = new List<FM3Node> { edge.Source, edge.Target }
                };
                
                edge.Source.Parent = coarsenedNode;
                edge.Target.Parent = coarsenedNode;
                
                // Preserve cycle information
                coarsenedNode.InCycle = edge.Source.InCycle || edge.Target.InCycle;
                
                coarsenedNodes.Add(coarsenedNode);
                nodeMapping[edge.Source] = coarsenedNode;
                nodeMapping[edge.Target] = coarsenedNode;
                matched.Add(edge.Source);
                matched.Add(edge.Target);
            }
            
            // Handle unmatched nodes (suns)
            foreach (var node in nodes)
            {
                if (!matched.Contains(node))
                {
                    var coarsenedNode = new FM3Node
                    {
                        Position = node.Position,
                        Mass = node.Mass,
                        Level = level,
                        Children = new List<FM3Node> { node },
                        InCycle = node.InCycle
                    };
                    
                    node.Parent = coarsenedNode;
                    coarsenedNodes.Add(coarsenedNode);
                    nodeMapping[node] = coarsenedNode;
                }
            }
            
            // Create coarsened edges
            var coarsenedEdgeMap = new Dictionary<(FM3Node, FM3Node), FM3Edge>();
            
            foreach (var edge in edges)
            {
                var sourceParent = nodeMapping[edge.Source];
                var targetParent = nodeMapping[edge.Target];
                
                if (sourceParent != targetParent)
                {
                    var key = (sourceParent, targetParent);
                    
                    if (coarsenedEdgeMap.ContainsKey(key))
                    {
                        coarsenedEdgeMap[key].Weight += edge.Weight;
                    }
                    else
                    {
                        var coarsenedEdge = new FM3Edge
                        {
                            Source = sourceParent,
                            Target = targetParent,
                            Weight = edge.Weight,
                            IsBackEdge = edge.IsBackEdge
                        };
                        
                        coarsenedEdgeMap[key] = coarsenedEdge;
                        
                        // Update neighbors
                        sourceParent.OutgoingNeighbors.Add(targetParent);
                        targetParent.IncomingNeighbors.Add(sourceParent);
                    }
                }
            }
            
            return (coarsenedNodes, coarsenedEdgeMap.Values.ToList());
        }
        
        private void InitialPlacement(List<FM3Node> nodes)
        {
            // Use spectral layout or grid placement for initial positions
            if (_params.IsDirected && nodes.Any(n => n.TopologicalOrder >= 0))
            {
                // Layer-based initial placement for directed graphs
                var layers = new Dictionary<int, List<FM3Node>>();
                int maxLayer = 0;
                
                foreach (var node in nodes)
                {
                    int layer = node.TopologicalOrder >= 0 
                        ? node.TopologicalOrder / Math.Max(1, nodes.Count / 10)
                        : 0;
                    
                    if (!layers.ContainsKey(layer))
                        layers[layer] = new List<FM3Node>();
                    
                    layers[layer].Add(node);
                    maxLayer = Math.Max(maxLayer, layer);
                }
                
                // Position nodes by layers
                float layerHeight = _params.IdealEdgeLength * 3f;
                
                foreach (var kvp in layers)
                {
                    var layerNodes = kvp.Value;
                    float layerWidth = layerNodes.Count * _params.IdealEdgeLength;
                    
                    for (int i = 0; i < layerNodes.Count; i++)
                    {
                        layerNodes[i].Position = new Vector2(
                            (i - layerNodes.Count * 0.5f) * _params.IdealEdgeLength,
                            -kvp.Key * layerHeight
                        );
                        
                        // Add small random perturbation
                        layerNodes[i].Position += new Vector2(
                            UnityEngine.Random.Range(-0.1f, 0.1f),
                            UnityEngine.Random.Range(-0.1f, 0.1f)
                        );
                    }
                }
                
                // Special handling for cycle nodes - arrange in circular pattern
                var cycleNodes = nodes.Where(n => n.InCycle).ToList();
                if (cycleNodes.Count > 2)
                {
                    float radius = cycleNodes.Count * _params.IdealEdgeLength / (2f * Mathf.PI);
                    for (int i = 0; i < cycleNodes.Count; i++)
                    {
                        float angle = i * 2f * Mathf.PI / cycleNodes.Count;
                        cycleNodes[i].Position = new Vector2(
                            radius * Mathf.Cos(angle),
                            radius * Mathf.Sin(angle)
                        );
                    }
                }
            }
            else
            {
                // Grid-based placement for undirected or fallback
                int gridSize = Mathf.CeilToInt(Mathf.Sqrt(nodes.Count));
                float spacing = _params.IdealEdgeLength * 2f;
                
                for (int i = 0; i < nodes.Count; i++)
                {
                    int row = i / gridSize;
                    int col = i % gridSize;
                    
                    nodes[i].Position = new Vector2(
                        (col - gridSize * 0.5f) * spacing,
                        (row - gridSize * 0.5f) * spacing
                    );
                    
                    // Add random perturbation
                    nodes[i].Position += new Vector2(
                        UnityEngine.Random.Range(-spacing * 0.1f, spacing * 0.1f),
                        UnityEngine.Random.Range(-spacing * 0.1f, spacing * 0.1f)
                    );
                }
            }
        }
        
        private void LayoutWithFM3(List<FM3Node> nodes, List<FM3Edge> edges, int iterations, int level)
        {
            float temperature = _params.InitialTemperature * Mathf.Pow(0.8f, level);
            float coolingRate = _params.CoolingFactor;
            
            for (int iter = 0; iter < iterations; iter++)
            {
                // Build QuadTree for efficient force calculation
                QuadTreeNode quadTree = null;
                if (_params.UseBarnesHut && nodes.Count > 100)
                {
                    quadTree = BuildQuadTree(nodes);
                }
                
                // Reset forces
                foreach (var node in nodes)
                {
                    node.OldForce = node.Force;
                    node.Force = Vector2.zero;
                }
                
                // Calculate repulsive forces
                if (quadTree != null)
                {
                    // Use Barnes-Hut approximation
                    foreach (var node in nodes)
                    {
                        CalculateBarnesHutForce(node, quadTree);
                    }
                }
                else
                {
                    // Direct N² calculation for small graphs
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        for (int j = i + 1; j < nodes.Count; j++)
                        {
                            CalculateRepulsiveForce(nodes[i], nodes[j]);
                        }
                    }
                }
                
                // Calculate attractive forces
                foreach (var edge in edges)
                {
                    CalculateAttractiveForce(edge);
                    
                    // Add directional bias for directed edges
                    if (_params.IsDirected && !edge.IsBackEdge)
                    {
                        ApplyDirectionalConstraint(edge);
                    }
                }
                
                // Apply cycle constraints
                if (_params.IsDirected)
                {
                    ApplyCycleConstraints(nodes, edges);
                }
                
                // Update positions with adaptive step size
                float maxDisplacement = 0f;
                foreach (var node in nodes)
                {
                    // Adaptive force scaling
                    float forceMag = node.Force.magnitude;
                    if (forceMag > temperature)
                    {
                        node.Force = node.Force.normalized * temperature;
                    }
                    
                    // Apply force with mass consideration
                    var displacement = node.Force / node.Mass;
                    
                    // Oscillation detection and damping
                    if (Vector2.Dot(node.Force, node.OldForce) < 0)
                    {
                        displacement *= 0.5f; // Dampen oscillations
                    }
                    
                    node.Position += displacement;
                    maxDisplacement = Mathf.Max(maxDisplacement, displacement.magnitude);
                }
                
                // Cool down temperature
                temperature *= coolingRate;
                
                // Early termination if converged
                if (maxDisplacement < 0.01f * _params.IdealEdgeLength)
                {
                    break;
                }
            }
        }
        
        private QuadTreeNode BuildQuadTree(List<FM3Node> nodes)
        {
            // Find bounds
            var minX = nodes.Min(n => n.Position.x);
            var maxX = nodes.Max(n => n.Position.x);
            var minY = nodes.Min(n => n.Position.y);
            var maxY = nodes.Max(n => n.Position.y);
            
            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            var size = Mathf.Max(maxX - minX, maxY - minY) * 1.1f;
            
            var root = new QuadTreeNode
            {
                Center = center,
                Size = size
            };
            
            foreach (var node in nodes)
            {
                InsertIntoQuadTree(root, node);
            }
            
            // Calculate centers of mass
            CalculateCenterOfMass(root);
            
            return root;
        }
        
        private void InsertIntoQuadTree(QuadTreeNode quadNode, FM3Node node)
        {
            if (quadNode.Nodes.Count == 0 && quadNode.Children == null)
            {
                quadNode.Nodes.Add(node);
                return;
            }
            
            if (quadNode.Children == null)
            {
                // Split the node
                quadNode.Children = new QuadTreeNode[4];
                var halfSize = quadNode.Size * 0.5f;
                var quarterSize = halfSize * 0.5f;
                
                // Create four children
                quadNode.Children[0] = new QuadTreeNode
                {
                    Center = quadNode.Center + new Vector2(-quarterSize, quarterSize),
                    Size = halfSize
                };
                quadNode.Children[1] = new QuadTreeNode
                {
                    Center = quadNode.Center + new Vector2(quarterSize, quarterSize),
                    Size = halfSize
                };
                quadNode.Children[2] = new QuadTreeNode
                {
                    Center = quadNode.Center + new Vector2(-quarterSize, -quarterSize),
                    Size = halfSize
                };
                quadNode.Children[3] = new QuadTreeNode
                {
                    Center = quadNode.Center + new Vector2(quarterSize, -quarterSize),
                    Size = halfSize
                };
                
                // Re-insert existing nodes
                var existingNodes = new List<FM3Node>(quadNode.Nodes);
                quadNode.Nodes.Clear();
                
                foreach (var existingNode in existingNodes)
                {
                    int childIndex = GetQuadrant(quadNode, existingNode.Position);
                    InsertIntoQuadTree(quadNode.Children[childIndex], existingNode);
                }
            }
            
            // Insert new node into appropriate child
            int quadrant = GetQuadrant(quadNode, node.Position);
            InsertIntoQuadTree(quadNode.Children[quadrant], node);
        }
        
        private int GetQuadrant(QuadTreeNode quadNode, Vector2 position)
        {
            bool right = position.x >= quadNode.Center.x;
            bool top = position.y >= quadNode.Center.y;
            
            if (top)
                return right ? 1 : 0;
            else
                return right ? 3 : 2;
        }
        
        private void CalculateCenterOfMass(QuadTreeNode quadNode)
        {
            if (quadNode.IsLeaf)
            {
                if (quadNode.Nodes.Count > 0)
                {
                    Vector2 centerOfMass = Vector2.zero;
                    float totalMass = 0f;
                    
                    foreach (var node in quadNode.Nodes)
                    {
                        centerOfMass += node.Position * node.Mass;
                        totalMass += node.Mass;
                    }
                    
                    quadNode.CenterOfMass = centerOfMass / totalMass;
                    quadNode.TotalMass = totalMass;
                }
            }
            else
            {
                Vector2 centerOfMass = Vector2.zero;
                float totalMass = 0f;
                
                foreach (var child in quadNode.Children)
                {
                    if (child != null)
                    {
                        CalculateCenterOfMass(child);
                        if (child.TotalMass > 0)
                        {
                            centerOfMass += child.CenterOfMass * child.TotalMass;
                            totalMass += child.TotalMass;
                        }
                    }
                }
                
                if (totalMass > 0)
                {
                    quadNode.CenterOfMass = centerOfMass / totalMass;
                    quadNode.TotalMass = totalMass;
                }
            }
        }
        
        private void CalculateBarnesHutForce(FM3Node node, QuadTreeNode quadNode)
        {
            if (quadNode == null || quadNode.TotalMass == 0) return;
            
            if (quadNode.IsLeaf)
            {
                foreach (var otherNode in quadNode.Nodes)
                {
                    if (otherNode != node)
                    {
                        CalculateRepulsiveForce(node, otherNode);
                    }
                }
            }
            else
            {
                var distance = Vector2.Distance(node.Position, quadNode.CenterOfMass);
                
                if (quadNode.Size / distance < _params.BarnesHutTheta)
                {
                    // Treat as single body
                    var delta = node.Position - quadNode.CenterOfMass;
                    if (delta.magnitude < 0.01f)
                    {
                        delta = new Vector2(
                            UnityEngine.Random.Range(-0.01f, 0.01f),
                            UnityEngine.Random.Range(-0.01f, 0.01f)
                        );
                    }
                    
                    var force = _params.RepulsiveForceConstant * node.Mass * quadNode.TotalMass 
                        / (delta.magnitude * delta.magnitude);
                    node.Force += delta.normalized * force;
                }
                else
                {
                    // Recurse into children
                    foreach (var child in quadNode.Children)
                    {
                        CalculateBarnesHutForce(node, child);
                    }
                }
            }
        }
        
        private void CalculateRepulsiveForce(FM3Node nodeA, FM3Node nodeB)
        {
            var delta = nodeA.Position - nodeB.Position;
            var distance = delta.magnitude;
            
            if (distance < 0.01f)
            {
                delta = new Vector2(
                    UnityEngine.Random.Range(-0.01f, 0.01f),
                    UnityEngine.Random.Range(-0.01f, 0.01f)
                );
                distance = delta.magnitude;
            }
            
            // FM³ repulsive force formula
            var force = _params.RepulsiveForceConstant * nodeA.Mass * nodeB.Mass 
                / (distance * distance);
            
            var forceVector = delta.normalized * force;
            nodeA.Force += forceVector;
            nodeB.Force -= forceVector;
        }
        
        private void CalculateAttractiveForce(FM3Edge edge)
        {
            var delta = edge.Target.Position - edge.Source.Position;
            var distance = delta.magnitude;
            
            if (distance < 0.01f) return;
            
            // FM³ attractive force formula
            var idealLength = _params.IdealEdgeLength * Mathf.Sqrt(edge.Weight);
            var force = _params.AttractiveForceConstant * Mathf.Log(distance / idealLength) * edge.Weight;
            
            var forceVector = delta.normalized * force;
            edge.Source.Force += forceVector;
            edge.Target.Force -= forceVector;
        }
        
        private void ApplyDirectionalConstraint(FM3Edge edge)
        {
            // Apply downward force to maintain direction (source above target)
            var verticalDiff = edge.Source.Position.y - edge.Target.Position.y;
            
            if (verticalDiff < _params.IdealEdgeLength * 0.5f)
            {
                var force = _params.DirectionalForce * (1f - verticalDiff / _params.IdealEdgeLength);
                edge.Source.Force += Vector2.up * force;
                edge.Target.Force += Vector2.down * force;
            }
            
            // Horizontal alignment tendency
            var horizontalDiff = Mathf.Abs(edge.Source.Position.x - edge.Target.Position.x);
            if (horizontalDiff > _params.IdealEdgeLength * 2f)
            {
                var horizontalForce = _params.DirectionalForce * 0.3f;
                if (edge.Source.Position.x > edge.Target.Position.x)
                {
                    edge.Source.Force += Vector2.left * horizontalForce;
                    edge.Target.Force += Vector2.right * horizontalForce;
                }
                else
                {
                    edge.Source.Force += Vector2.right * horizontalForce;
                    edge.Target.Force += Vector2.left * horizontalForce;
                }
            }
        }
        
        private void ApplyCycleConstraints(List<FM3Node> nodes, List<FM3Edge> edges)
        {
            // Group nodes in cycles
            var cycleGroups = new List<List<FM3Node>>();
            var processedNodes = new HashSet<FM3Node>();
            
            foreach (var node in nodes.Where(n => n.InCycle && !processedNodes.Contains(n)))
            {
                var cycleGroup = new List<FM3Node>();
                var queue = new Queue<FM3Node>();
                queue.Enqueue(node);
                
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (processedNodes.Contains(current)) continue;
                    
                    processedNodes.Add(current);
                    cycleGroup.Add(current);
                    
                    foreach (var neighbor in current.OutgoingNeighbors.Concat(current.IncomingNeighbors))
                    {
                        if (neighbor.InCycle && !processedNodes.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                
                if (cycleGroup.Count > 2)
                {
                    cycleGroups.Add(cycleGroup);
                }
            }
            
            // Apply circular arrangement force to cycle groups
            foreach (var cycleGroup in cycleGroups)
            {
                var center = Vector2.zero;
                foreach (var node in cycleGroup)
                {
                    center += node.Position;
                }
                center /= cycleGroup.Count;
                
                var radius = cycleGroup.Count * _params.IdealEdgeLength / (2f * Mathf.PI);
                
                for (int i = 0; i < cycleGroup.Count; i++)
                {
                    var idealAngle = i * 2f * Mathf.PI / cycleGroup.Count;
                    var idealPosition = center + new Vector2(
                        radius * Mathf.Cos(idealAngle),
                        radius * Mathf.Sin(idealAngle)
                    );
                    
                    var delta = idealPosition - cycleGroup[i].Position;
                    cycleGroup[i].Force += delta * _params.CycleHandlingStrength;
                }
            }
        }
        
        private void InterpolatePositions(
            (List<FM3Node>, List<FM3Edge>) coarseLevel, 
            (List<FM3Node>, List<FM3Edge>) fineLevel)
        {
            foreach (var fineNode in fineLevel.Item1)
            {
                if (fineNode.Parent != null)
                {
                    // Inherit position from parent with small random offset
                    var parentPos = fineNode.Parent.Position;
                    var offset = new Vector2(
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-1f, 1f)
                    ) * _params.IdealEdgeLength * 0.1f;
                    
                    fineNode.Position = parentPos + offset;
                    
                    // Special handling for directed graphs
                    if (_params.IsDirected && fineNode.Parent.Children.Count > 1)
                    {
                        // Arrange children vertically if they have order
                        var siblings = fineNode.Parent.Children
                            .OrderBy(n => n.TopologicalOrder)
                            .ToList();
                        
                        var index = siblings.IndexOf(fineNode);
                        if (index >= 0)
                        {
                            var verticalOffset = (index - siblings.Count * 0.5f) 
                                * _params.IdealEdgeLength * 0.3f;
                            fineNode.Position += Vector2.up * verticalOffset;
                        }
                    }
                }
            }
        }
        
        private void PostProcessDirectedLayout(List<FM3Node> nodes, List<FM3Edge> edges)
        {
            // Sugiyama-style layer assignment for non-cycle nodes
            var nonCycleNodes = nodes.Where(n => !n.InCycle).ToList();
            
            if (nonCycleNodes.Count > 0)
            {
                // Compute layers using longest path
                var layers = new Dictionary<FM3Node, int>();
                var maxLayer = 0;
                
                foreach (var node in nonCycleNodes.OrderBy(n => n.TopologicalOrder))
                {
                    var maxIncomingLayer = -1;
                    foreach (var incoming in node.IncomingNeighbors)
                    {
                        if (layers.ContainsKey(incoming))
                        {
                            maxIncomingLayer = Math.Max(maxIncomingLayer, layers[incoming]);
                        }
                    }
                    
                    layers[node] = maxIncomingLayer + 1;
                    maxLayer = Math.Max(maxLayer, layers[node]);
                }
                
                // Adjust Y positions based on layers
                var layerGroups = layers.GroupBy(kvp => kvp.Value)
                    .OrderBy(g => g.Key)
                    .ToList();
                
                float layerSpacing = _params.IdealEdgeLength * 2.5f;
                
                foreach (var layerGroup in layerGroups)
                {
                    var layerNodes = layerGroup.Select(kvp => kvp.Key).ToList();
                    var targetY = -layerGroup.Key * layerSpacing;
                    
                    // Sort nodes in layer by X position
                    layerNodes = layerNodes.OrderBy(n => n.Position.x).ToList();
                    
                    // Adjust positions
                    for (int i = 0; i < layerNodes.Count; i++)
                    {
                        var node = layerNodes[i];
                        var targetX = (i - layerNodes.Count * 0.5f) * _params.IdealEdgeLength * 1.5f;
                        
                        // Smoothly adjust position
                        node.Position = Vector2.Lerp(
                            node.Position,
                            new Vector2(targetX, targetY),
                            0.5f
                        );
                    }
                }
            }
            
            // Edge straightening
            foreach (var edge in edges.Where(e => !e.IsBackEdge))
            {
                var horizontalDiff = Mathf.Abs(edge.Source.Position.x - edge.Target.Position.x);
                if (horizontalDiff < _params.IdealEdgeLength * 0.5f)
                {
                    // Align vertically
                    var avgX = (edge.Source.Position.x + edge.Target.Position.x) * 0.5f;
                    edge.Source.Position = new Vector2(avgX, edge.Source.Position.y);
                    edge.Target.Position = new Vector2(avgX, edge.Target.Position.y);
                }
            }
        }
        
        private void ApplyFinalPositions(List<FM3Node> nodes)
        {
            if (nodes.Count == 0) return;
            
            // Calculate bounds
            var minX = nodes.Min(n => n.Position.x);
            var maxX = nodes.Max(n => n.Position.x);
            var minY = nodes.Min(n => n.Position.y);
            var maxY = nodes.Max(n => n.Position.y);
            
            var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            var scale = 1f;
            
            // Scale if needed
            var width = maxX - minX;
            var height = maxY - minY;
            var maxDimension = Mathf.Max(width, height);
            
            if (maxDimension > 100f)
            {
                scale = 100f / maxDimension;
            }
            
            // Apply positions to GameObjects
            foreach (var node in nodes)
            {
                if (node.OriginalNode?.GameObject != null)
                {
                    var finalPosition = (node.Position - center) * scale;
                    node.OriginalNode.GameObject.transform.position = new Vector3(
                        finalPosition.x,
                        finalPosition.y,
                        0f
                    );
                }
            }
        }
        
        public void SetLayoutParameters(
            LayoutParameters parameters)
        {
            _params.RepulsiveForceConstant = parameters.repulsion;
            _params.AttractiveForceConstant = parameters.attractionStrength;
            _params.IdealEdgeLength = parameters.idealEdgeLength;
            _params.IsDirected = parameters.isDirected;
            _params.DirectionalForce = parameters.directionalForce;
            _params.CycleHandlingStrength = parameters.cycleHandlingStrength;
            _params.UseBarnesHut = parameters.useBarnesHut;
        }
    }
}
