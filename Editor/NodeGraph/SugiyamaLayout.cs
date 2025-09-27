namespace _3DConnections.Editor.NodeGraph
{
    using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;
    using System;

    public class SugiyamaLayout
    {
        private const float DEFAULT_NODE_WIDTH = 200f;
        private const float DEFAULT_NODE_HEIGHT = 100f;
        private const float ASSET_NODE_WIDTH = 180f;
        private const float ASSET_NODE_HEIGHT = 120f;
        private const float LAYER_SPACING = 300f;
        private const float NODE_SPACING = 50f;
        private const int MAX_ITERATIONS = 100;

        public class LayoutNode
        {
            public GameObjectGraphNode GameObjectNode;
            public AssetReferenceNode AssetNode;
            public GameObject GameObject;
            public UnityEngine.Object Asset;
            public int Layer;
            public float X;
            public float Y;
            public float Width;
            public float Height;
            public List<LayoutNode> Children = new List<LayoutNode>();
            public List<LayoutNode> Parents = new List<LayoutNode>();
            public List<LayoutNode> ReferencedBy = new List<LayoutNode>(); // For asset references
            public int Order; // Position within layer
            public bool IsDummy; // For long edges
            public bool IsAsset => AssetNode != null;

            public LayoutNode(GameObjectGraphNode graphNode, GameObject gameObject)
            {
                GameObjectNode = graphNode;
                GameObject = gameObject;
                Width = DEFAULT_NODE_WIDTH;
                Height = DEFAULT_NODE_HEIGHT;
            }

            public LayoutNode(AssetReferenceNode assetNode, UnityEngine.Object asset)
            {
                AssetNode = assetNode;
                Asset = asset;
                Width = ASSET_NODE_WIDTH;
                Height = ASSET_NODE_HEIGHT;
            }

            // Dummy node constructor for edge splitting
            public LayoutNode()
            {
                IsDummy = true;
                Width = 10f;
                Height = 10f;
            }
        }

        private Dictionary<GameObject, LayoutNode> m_GameObjectLayoutNodes = new Dictionary<GameObject, LayoutNode>();
        private Dictionary<UnityEngine.Object, LayoutNode> m_AssetLayoutNodes = new Dictionary<UnityEngine.Object, LayoutNode>();
        private List<List<LayoutNode>> m_Layers = new List<List<LayoutNode>>();

        public void ApplyLayout(Dictionary<GameObject, GameObjectGraphNode> gameObjectNodes, Dictionary<UnityEngine.Object, AssetReferenceNode> assetNodes)
        {
            if (gameObjectNodes.Count == 0 && assetNodes.Count == 0) return;

            // Clear previous layout data
            m_GameObjectLayoutNodes.Clear();
            m_AssetLayoutNodes.Clear();
            m_Layers.Clear();

            // Step 1: Create layout nodes and build relationships (only for provided nodes)
            CreateLayoutNodes(gameObjectNodes, assetNodes);

            // Step 2: Assign layers with improved asset placement
            AssignLayers();

            // Step 3: Add dummy nodes for long edges
            AddDummyNodes();

            // Step 4: Minimize crossings (layer by layer sweep)
            MinimizeCrossings();

            // Step 5: Assign X coordinates with better asset distribution
            AssignXCoordinates();

            // Step 6: Apply positions to actual nodes
            ApplyPositions();
        }


        private void CreateLayoutNodes(Dictionary<GameObject, GameObjectGraphNode> gameObjectNodes, Dictionary<UnityEngine.Object, AssetReferenceNode> assetNodes)
        {
            m_GameObjectLayoutNodes.Clear();
            m_AssetLayoutNodes.Clear();

            // Create GameObject layout nodes
            foreach (var kvp in gameObjectNodes)
            {
                var layoutNode = new LayoutNode(kvp.Value, kvp.Key);
                m_GameObjectLayoutNodes[kvp.Key] = layoutNode;
            }

            // Create Asset layout nodes
            foreach (var kvp in assetNodes)
            {
                var layoutNode = new LayoutNode(kvp.Value, kvp.Key);
                m_AssetLayoutNodes[kvp.Key] = layoutNode;
            }

            // Build GameObject parent-child relationships
            foreach (var kvp in m_GameObjectLayoutNodes)
            {
                var gameObject = kvp.Key;
                var layoutNode = kvp.Value;

                // Add children
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i).gameObject;
                    if (m_GameObjectLayoutNodes.TryGetValue(child, out var childLayoutNode))
                    {
                        layoutNode.Children.Add(childLayoutNode);
                        childLayoutNode.Parents.Add(layoutNode);
                    }
                }
            }

            // Build asset reference relationships
            BuildAssetReferenceRelationships(gameObjectNodes);
        }

        private void BuildAssetReferenceRelationships(Dictionary<GameObject, GameObjectGraphNode> gameObjectNodes)
        {
            foreach (var gameObjectKvp in gameObjectNodes)
            {
                var gameObject = gameObjectKvp.Key;
        
                // Only process if we have a layout node for this GameObject
                if (!m_GameObjectLayoutNodes.TryGetValue(gameObject, out var gameObjectLayoutNode))
                    continue;
            
                var components = gameObject.GetComponents<Component>();

                foreach (var component in components)
                {
                    if (component == null) continue;

                    var serializedObject = new UnityEditor.SerializedObject(component);
                    var property = serializedObject.GetIterator();

                    if (property.NextVisible(true))
                    {
                        do
                        {
                            if (property.propertyType == UnityEditor.SerializedPropertyType.ObjectReference &&
                                property.objectReferenceValue != null)
                            {
                                var referencedObject = property.objectReferenceValue;

                                // Handle sprite texture references (for sprite sheet consolidation)
                                if (referencedObject is Sprite sprite && sprite.texture != null)
                                {
                                    referencedObject = sprite.texture;
                                }

                                // Only create relationships if both nodes are in our current layout set
                                if (m_AssetLayoutNodes.TryGetValue(referencedObject, out var assetLayoutNode))
                                {
                                    // GameObject references Asset
                                    assetLayoutNode.ReferencedBy.Add(gameObjectLayoutNode);
                                }
                            }
                        }
                        while (property.NextVisible(false));
                    }
                }
            }
        }


        private void AssignLayers()
        {
            m_Layers.Clear();

            // Step 1: Assign layers to GameObject hierarchy
            var gameObjectRootNodes = m_GameObjectLayoutNodes.Values.Where(n => n.Parents.Count == 0).ToList();

            if (gameObjectRootNodes.Count == 0 && m_GameObjectLayoutNodes.Count > 0)
            {
                // Handle cyclic case - pick arbitrary root
                gameObjectRootNodes.Add(m_GameObjectLayoutNodes.Values.First());
            }

            // Assign layers using BFS from GameObject roots
            var visited = new HashSet<LayoutNode>();
            var queue = new Queue<(LayoutNode node, int layer)>();

            foreach (var root in gameObjectRootNodes)
            {
                queue.Enqueue((root, 0));
            }

            int maxGameObjectLayer = -1;
            while (queue.Count > 0)
            {
                var (node, layer) = queue.Dequeue();

                if (visited.Contains(node)) continue;
                visited.Add(node);

                node.Layer = layer;
                maxGameObjectLayer = Math.Max(maxGameObjectLayer, layer);

                // Ensure we have enough layers
                while (m_Layers.Count <= layer)
                {
                    m_Layers.Add(new List<LayoutNode>());
                }

                m_Layers[layer].Add(node);

                // Add children to next layer
                foreach (var child in node.Children)
                {
                    if (!visited.Contains(child))
                    {
                        queue.Enqueue((child, layer + 1));
                    }
                }
            }

            // Step 2: Smart asset placement based on reference patterns
            PlaceAssetsIntelligently(maxGameObjectLayer);

            // Handle any remaining unvisited GameObject nodes
            foreach (var node in m_GameObjectLayoutNodes.Values.Where(n => !visited.Contains(n)))
            {
                node.Layer = maxGameObjectLayer + 1;
                if (m_Layers.Count <= node.Layer)
                {
                    m_Layers.Add(new List<LayoutNode>());
                }
                m_Layers[node.Layer].Add(node);
            }
        }

        private void PlaceAssetsIntelligently(int maxGameObjectLayer)
        {
            // Group assets by their reference patterns
            var assetGroups = new Dictionary<int, List<LayoutNode>>();
            
            foreach (var assetNode in m_AssetLayoutNodes.Values)
            {
                if (assetNode.ReferencedBy.Count == 0)
                {
                    // Unreferenced assets go to the far right
                    int unreferencedLayer = maxGameObjectLayer + 3;
                    if (!assetGroups.ContainsKey(unreferencedLayer))
                        assetGroups[unreferencedLayer] = new List<LayoutNode>();
                    assetGroups[unreferencedLayer].Add(assetNode);
                    continue;
                }

                // Calculate the average layer of referencing GameObjects
                float avgReferencerLayer = (float)assetNode.ReferencedBy.Average(r => r.Layer);
                
                // Place asset near the average layer of its references
                int targetLayer;
                if (assetNode.ReferencedBy.Count == 1)
                {
                    // Single reference: place right next to the referencer
                    targetLayer = assetNode.ReferencedBy[0].Layer;
                }
                else if (assetNode.ReferencedBy.All(r => r.Layer == assetNode.ReferencedBy[0].Layer))
                {
                    // All references from same layer: place next to that layer
                    targetLayer = assetNode.ReferencedBy[0].Layer;
                }
                else
                {
                    // Multiple references from different layers: place at average + offset
                    targetLayer = Mathf.RoundToInt(avgReferencerLayer) + maxGameObjectLayer + 1;
                }

                // Ensure we don't overlap with GameObject layers
                targetLayer = Math.Max(targetLayer, maxGameObjectLayer + 1);

                if (!assetGroups.ContainsKey(targetLayer))
                    assetGroups[targetLayer] = new List<LayoutNode>();
                assetGroups[targetLayer].Add(assetNode);
            }

            // Add asset groups to layers
            foreach (var kvp in assetGroups)
            {
                int layerIndex = kvp.Key;
                var assets = kvp.Value;

                // Ensure we have enough layers
                while (m_Layers.Count <= layerIndex)
                {
                    m_Layers.Add(new List<LayoutNode>());
                }

                // Sort assets by reference count (most referenced first), then by name
                var sortedAssets = assets
                    .OrderByDescending(a => a.ReferencedBy.Count)
                    .ThenBy(a => a.Asset?.name ?? "Unknown")
                    .ToList();

                foreach (var asset in sortedAssets)
                {
                    asset.Layer = layerIndex;
                    m_Layers[layerIndex].Add(asset);
                }
            }
        }

        private void AddDummyNodes()
        {
            // For GameObject hierarchy edges that span multiple layers
            foreach (var layer in m_Layers.ToList()) // ToList to avoid modification during iteration
            {
                foreach (var node in layer.ToList())
                {
                    if (node.IsAsset || node.IsDummy) continue;

                    foreach (var child in node.Children.ToList())
                    {
                        int layerSpan = child.Layer - node.Layer;
                        if (layerSpan > 1)
                        {
                            // Remove direct connection
                            node.Children.Remove(child);
                            child.Parents.Remove(node);

                            // Add dummy nodes
                            var previousNode = node;
                            for (int i = 1; i < layerSpan; i++)
                            {
                                var dummyNode = new LayoutNode();
                                dummyNode.Layer = node.Layer + i;

                                // Connect previous to dummy
                                previousNode.Children.Add(dummyNode);
                                dummyNode.Parents.Add(previousNode);

                                // Add to appropriate layer
                                m_Layers[dummyNode.Layer].Add(dummyNode);

                                previousNode = dummyNode;
                            }

                            // Connect last dummy to original child
                            previousNode.Children.Add(child);
                            child.Parents.Add(previousNode);
                        }
                    }
                }
            }
        }

        private void MinimizeCrossings()
        {
            // Iterative layer-by-layer sweep to minimize crossings
            for (int iteration = 0; iteration < MAX_ITERATIONS; iteration++)
            {
                bool improved = false;

                // Forward sweep (left to right)
                for (int layerIndex = 1; layerIndex < m_Layers.Count; layerIndex++)
                {
                    if (OptimizeLayerOrder(layerIndex, true))
                        improved = true;
                }

                // Backward sweep (right to left)
                for (int layerIndex = m_Layers.Count - 2; layerIndex >= 0; layerIndex--)
                {
                    if (OptimizeLayerOrder(layerIndex, false))
                        improved = true;
                }

                if (!improved) break;
            }

            // Assign final order indices
            for (int layerIndex = 0; layerIndex < m_Layers.Count; layerIndex++)
            {
                for (int i = 0; i < m_Layers[layerIndex].Count; i++)
                {
                    m_Layers[layerIndex][i].Order = i;
                }
            }
        }

        private bool OptimizeLayerOrder(int layerIndex, bool useLeftLayer)
        {
            var layer = m_Layers[layerIndex];
            if (layer.Count <= 1) return false;

            // Calculate barycenter for each node in current layer
            var nodeBaryCenters = new List<(LayoutNode node, float barycenter)>();

            foreach (var node in layer)
            {
                List<LayoutNode> connections;
                
                if (useLeftLayer)
                {
                    connections = node.Parents.Concat(node.ReferencedBy).ToList();
                }
                else
                {
                    connections = node.Children.ToList();
                }

                if (connections.Count == 0)
                {
                    nodeBaryCenters.Add((node, node.Order));
                }
                else
                {
                    float sum = connections.Sum(c => c.Order);
                    float barycenter = sum / connections.Count;
                    nodeBaryCenters.Add((node, barycenter));
                }
            }

            // Sort by barycenter, but keep assets at the bottom of their layers
            var originalOrder = layer.ToList();
            var gameObjectNodes = nodeBaryCenters.Where(x => !x.node.IsAsset).OrderBy(x => x.barycenter).Select(x => x.node);
            var assetNodes = nodeBaryCenters.Where(x => x.node.IsAsset).OrderBy(x => x.barycenter).Select(x => x.node);
            var sortedNodes = gameObjectNodes.Concat(assetNodes).ToList();

            // Check if order changed
            bool changed = false;
            for (int i = 0; i < originalOrder.Count; i++)
            {
                if (originalOrder[i] != sortedNodes[i])
                {
                    changed = true;
                    break;
                }
            }

            if (changed)
            {
                m_Layers[layerIndex] = sortedNodes;
                // Update order indices
                for (int i = 0; i < sortedNodes.Count; i++)
                {
                    sortedNodes[i].Order = i;
                }
            }

            return changed;
        }

        private void AssignXCoordinates()
        {
            // Assign coordinates for each layer with improved asset spacing
            for (int layerIndex = 0; layerIndex < m_Layers.Count; layerIndex++)
            {
                var layer = m_Layers[layerIndex];
                if (layer.Count == 0) continue;

                // Separate GameObjects and Assets for better positioning
                var gameObjects = layer.Where(n => !n.IsAsset).ToList();
                var assets = layer.Where(n => n.IsAsset).ToList();

                float currentY = layerIndex * LAYER_SPACING;
                
                // Position GameObjects first
                if (gameObjects.Count > 0)
                {
                    PositionNodesInLayer(gameObjects, 0, currentY);
                }

                // Position Assets below GameObjects with some spacing
                if (assets.Count > 0)
                {
                    float assetYOffset = gameObjects.Count > 0 ? 150f : 0f;
                    PositionNodesInLayer(assets, 0, currentY + assetYOffset);
                }
            }
        }

        private void PositionNodesInLayer(List<LayoutNode> nodes, float baseX, float y)
        {
            if (nodes.Count == 0) return;

            float totalWidth = nodes.Sum(n => n.Width) + (nodes.Count - 1) * NODE_SPACING;
            float startX = baseX - totalWidth / 2f;

            float currentX = startX;
            foreach (var node in nodes)
            {
                node.X = currentX + node.Width / 2f;
                node.Y = y;
                currentX += node.Width + NODE_SPACING;
            }
        }

        private void ApplyPositions()
        {
            // Apply positions to GameObject nodes
            foreach (var layoutNode in m_GameObjectLayoutNodes.Values)
            {
                if (layoutNode.GameObjectNode != null)
                {
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (layoutNode.GameObjectNode != null)
                        {
                            var rect = layoutNode.GameObjectNode.GetPosition();
                            rect.x = layoutNode.X;
                            rect.y = layoutNode.Y;
                            layoutNode.GameObjectNode.SetPosition(rect);
                        }
                    };
                }
            }

            // Apply positions to Asset nodes
            foreach (var layoutNode in m_AssetLayoutNodes.Values)
            {
                if (layoutNode.AssetNode != null)
                {
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (layoutNode.AssetNode != null)
                        {
                            var rect = layoutNode.AssetNode.GetPosition();
                            rect.x = layoutNode.X;
                            rect.y = layoutNode.Y;
                            layoutNode.AssetNode.SetPosition(rect);
                        }
                    };
                }
            }
        }

        public void UpdateNodeSizes(Dictionary<GameObject, GameObjectGraphNode> gameObjectNodes, Dictionary<UnityEngine.Object, AssetReferenceNode> assetNodes)
        {
            // Update GameObject node sizes
            foreach (var kvp in gameObjectNodes)
            {
                if (m_GameObjectLayoutNodes.TryGetValue(kvp.Key, out var layoutNode))
                {
                    var rect = kvp.Value.GetPosition();
                    if (rect.width > 0 && rect.height > 0)
                    {
                        layoutNode.Width = rect.width;
                        layoutNode.Height = rect.height;
                    }
                }
            }

            // Update Asset node sizes
            foreach (var kvp in assetNodes)
            {
                if (m_AssetLayoutNodes.TryGetValue(kvp.Key, out var layoutNode))
                {
                    var rect = kvp.Value.GetPosition();
                    if (rect.width > 0 && rect.height > 0)
                    {
                        layoutNode.Width = rect.width;
                        layoutNode.Height = rect.height;
                    }
                }
            }
        }
    }
}