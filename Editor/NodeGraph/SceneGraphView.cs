namespace _3DConnections.Editor.NodeGraph
{
    using UnityEngine;
    using UnityEditor;
    using UnityEngine.UIElements;
    using UnityEditor.Experimental.GraphView;
    using System.Collections.Generic;

    public class SceneGraphView : GraphView
    {
        private Dictionary<GameObject, GameObjectGraphNode> m_GameObjectNodes = new();
        private Dictionary<Object, AssetReferenceNode> m_AssetNodes = new();
        private Dictionary<string, Edge> m_ReferenceEdges = new();
        private SugiyamaLayout m_SugiyamaLayout = new();
        private int m_MaxHierarchyDepth = -1; // -1 means show all
        
        public SceneGraphView()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();
            
            // Load styles
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/SceneGraphView.uss");
            if (styleSheet != null)
                styleSheets.Add(styleSheet);
                
            // Handle connection events
            graphViewChanged += OnGraphViewChanged;
        }
        
        public void SetHierarchyDepthFilter(int maxDepth)
        {
            m_MaxHierarchyDepth = maxDepth;
            ApplyDepthFilter();
        }

        private void ApplyDepthFilter()
        {
            foreach (var kvp in m_GameObjectNodes)
            {
                var gameObject = kvp.Key;
                var node = kvp.Value;
                int depth = GetGameObjectHierarchyDepth(gameObject);
                
                bool shouldShow = m_MaxHierarchyDepth == -1 || depth <= m_MaxHierarchyDepth;
                node.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        
        public void RefreshGraph()
        {
            ClearGraph();
            TraverseScene();
            CreateAssetNodes();
            CreateConnections();
            ApplySugiyamaLayout(); // Use Sugiyama layout instead of simple layout
            ApplyDepthFilter();
        }
        
        private void CreateAssetNodes()
        {
            var referencedAssets = new HashSet<Object>();
            
            // Collect all referenced assets from components
            foreach (var gameObjectNode in m_GameObjectNodes.Values)
            {
                var components = gameObjectNode.GameObject.GetComponents<Component>();
                
                foreach (var component in components)
                {
                    if (component == null) continue;
                    
                    var serializedObject = new SerializedObject(component);
                    var property = serializedObject.GetIterator();
                    
                    if (property.NextVisible(true))
                    {
                        do
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference && 
                                property.objectReferenceValue != null)
                            {
                                var referencedObject = property.objectReferenceValue;
                                
                                // Only create asset nodes for non-scene objects
                                if (ShouldCreateAssetNode(referencedObject))
                                {
                                    referencedAssets.Add(referencedObject);
                                }
                            }
                        }
                        while (property.NextVisible(false));
                    }
                }
            }
            
            // Create asset nodes (with deduplication check)
            foreach (var asset in referencedAssets)
            {
                // Check if asset node already exists (deduplication)
                if (!m_AssetNodes.ContainsKey(asset))
                {
                    var assetNode = new AssetReferenceNode(asset, this);
                    m_AssetNodes[asset] = assetNode;
                    AddElement(assetNode);
                }
            }
        }
        
        private bool ShouldCreateAssetNode(Object obj)
        {
            if (obj == null) return false;
            
            // Don't create nodes for GameObjects or Components (they're already in the scene)
            if (obj is GameObject || obj is Component) return false;
            
            // Check if it's an asset (has an asset path)
            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath)) return false;
            
            // Include common asset types
            return obj is ScriptableObject ||
                   obj is Sprite ||
                   obj is Texture ||
                   obj is Material ||
                   obj is Mesh ||
                   obj is AudioClip ||
                   obj is AnimationClip ||
                   PrefabUtility.IsPartOfPrefabAsset(obj);
        }
        
        private int GetGameObjectHierarchyDepth(GameObject gameObject)
        {
            int depth = 0;
            Transform current = gameObject.transform;
            
            while (current.parent != null)
            {
                depth++;
                current = current.parent;
            }
            
            return depth;
        }
        
        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            if (graphViewChange.edgesToCreate != null)
            {
                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    HandleConnectionCreated(edge);
                }
            }
            
            return graphViewChange;
        }
        
        private void HandleConnectionCreated(Edge edge)
        {
            // Handle reference connections
            if (edge.output.userData is ReferencePortData outputData)
            {
                Object targetObject = null;
                
                if (edge.input.node is GameObjectGraphNode targetGameObjectNode)
                {
                    targetObject = GetConnectionTarget(targetGameObjectNode, edge.input);
                }
                else if (edge.input.node is AssetReferenceNode targetAssetNode)
                {
                    targetObject = targetAssetNode.Asset;
                }
                
                if (targetObject != null)
                {
                    // Update the component property
                    var sourceComponentElement = (edge.output.node as GameObjectGraphNode)?.GetComponentElement(outputData.Component);
                    sourceComponentElement?.UpdatePropertyValue(outputData.PropertyPath, targetObject);
                    
                    // Store the edge for tracking
                    var edgeKey = $"{outputData.Component.GetInstanceID()}_{outputData.PropertyPath}";
                    m_ReferenceEdges[edgeKey] = edge;
                }
            }
        }
        
        private Object GetConnectionTarget(GameObjectGraphNode targetNode, Port inputPort)
        {
            if (inputPort == targetNode.ReferenceInputPort)
            {
                return targetNode.GameObject;
            }
            return targetNode.GameObject;
        }
        
        public void OnReferenceChanged(GameObjectGraphNode sourceNode, Component component, string propertyPath)
        {
            // Remove old connection
            var edgeKey = $"{component.GetInstanceID()}_{propertyPath}";
            if (m_ReferenceEdges.TryGetValue(edgeKey, out var oldEdge))
            {
                RemoveElement(oldEdge);
                m_ReferenceEdges.Remove(edgeKey);
            }
            
            // Create new connection if property has a value
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(propertyPath);
            if (property?.objectReferenceValue != null)
            {
                CreateReferenceConnectionForProperty(sourceNode, component, property);
            }
        }
        
        
        public void ClearGraph()
        {
            DeleteElements(graphElements.ToList());
            m_GameObjectNodes.Clear();
            m_AssetNodes.Clear();
            m_ReferenceEdges.Clear();
        }
        
        public void CollapseAllNodes()
        {
            foreach (var node in m_GameObjectNodes.Values)
            {
                node.SetExpanded(false);
            }
        }
        
        public void ExpandAllNodes()
        {
            foreach (var node in m_GameObjectNodes.Values)
            {
                node.SetExpanded(true);
            }
        }
        
        private void TraverseScene()
        {
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            
            foreach (var rootObj in rootObjects)
            {
                TraverseGameObject(rootObj);
            }
        }
        
        private void TraverseGameObject(GameObject gameObject)
        {
            CreateGameObjectNode(gameObject);
            
            // Traverse children
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                TraverseGameObject(gameObject.transform.GetChild(i).gameObject);
            }
        }
        
        private void CreateGameObjectNode(GameObject gameObject)
        {
            var node = new GameObjectGraphNode(gameObject, this);
            m_GameObjectNodes[gameObject] = node;
            AddElement(node);
        }
        
        private void CreateConnections()
        {
            foreach (var kvp in m_GameObjectNodes)
            {
                var gameObject = kvp.Key;
                var node = kvp.Value;
                
                // Create hierarchy connections (parent-child)
                CreateHierarchyConnections(gameObject, node);
                
                // Create component reference connections
                CreateComponentConnections(gameObject, node);
            }
        }
        
        private void CreateHierarchyConnections(GameObject gameObject, GameObjectGraphNode node)
        {
            if (gameObject.transform.parent != null)
            {
                var parentGameObject = gameObject.transform.parent.gameObject;
                if (m_GameObjectNodes.TryGetValue(parentGameObject, out var parentNode))
                {
                    var edge = new Edge
                    {
                        output = parentNode.HierarchyOutputPort,
                        input = node.HierarchyInputPort
                    };
                    edge.AddToClassList("hierarchy-edge");
                    AddElement(edge);
                }
            }
        }
        
        private void CreateComponentConnections(GameObject gameObject, GameObjectGraphNode node)
        {
            var components = gameObject.GetComponents<Component>();
            
            foreach (var component in components)
            {
                if (component == null) continue;
                
                var serializedObject = new SerializedObject(component);
                var property = serializedObject.GetIterator();
                
                if (property.NextVisible(true))
                {
                    do
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference && 
                            property.objectReferenceValue != null)
                        {
                            CreateReferenceConnectionForProperty(node, component, property);
                        }
                    }
                    while (property.NextVisible(false));
                }
            }
        }
        
         private void CreateReferenceConnectionForProperty(GameObjectGraphNode sourceNode, Component component, SerializedProperty property)
        {
            var referencedObject = property.objectReferenceValue;
            Node targetNode = null;
            Port targetPort = null;
            
            // Check if it's a GameObject or Component reference
            if (referencedObject is GameObject targetGameObject)
            {
                if (m_GameObjectNodes.TryGetValue(targetGameObject, out var gameObjectNode))
                {
                    targetNode = gameObjectNode;
                    targetPort = gameObjectNode.ReferenceInputPort;
                }
            }
            else if (referencedObject is Component targetComponent)
            {
                if (m_GameObjectNodes.TryGetValue(targetComponent.gameObject, out var gameObjectNode))
                {
                    targetNode = gameObjectNode;
                    targetPort = gameObjectNode.ReferenceInputPort;
                }
            }
            // Check if it's an asset reference
            else if (m_AssetNodes.TryGetValue(referencedObject, out var assetNode))
            {
                targetNode = assetNode;
                targetPort = assetNode.ReferenceInputPort;
            }
            
            if (targetNode != null && targetPort != null)
            {
                var outputPort = sourceNode.GetReferenceOutputPort(component, property.propertyPath);
                if (outputPort != null)
                {
                    var edge = new Edge
                    {
                        output = outputPort,
                        input = targetPort
                    };
                    
                    // Use different edge styles for different connection types
                    if (targetNode is AssetReferenceNode)
                        edge.AddToClassList("asset-reference-edge");
                    else
                        edge.AddToClassList("reference-edge");
                    
                    AddElement(edge);
                    
                    // Store for tracking
                    var edgeKey = $"{component.GetInstanceID()}_{property.propertyPath}";
                    m_ReferenceEdges[edgeKey] = edge;
                }
            }
        }
        
        public void ApplySugiyamaLayout()
        {
            if (m_GameObjectNodes.Count == 0 && m_AssetNodes.Count == 0) return;

            // Update node sizes first (if available)
            m_SugiyamaLayout.UpdateNodeSizes(m_GameObjectNodes, m_AssetNodes);
            
            // Apply the Sugiyama layout algorithm with both GameObject and Asset nodes
            m_SugiyamaLayout.ApplyLayout(m_GameObjectNodes, m_AssetNodes);
        }
        
        private void LayoutNodes()
        {
            // Layout GameObjects using hierarchy
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            float xOffset = 100f;
            float yOffset = 100f;
            
            int rootIndex = 0;
            foreach (var rootObj in rootObjects)
            {
                LayoutNodeHierarchy(rootObj, xOffset, yOffset + rootIndex * 200f, 0);
                rootIndex++;
            }
            
            // Layout asset nodes to the right
            LayoutAssetNodes();
        }
        
        private void LayoutAssetNodes()
        {
            float assetStartX = 1500f; // Position asset nodes to the right
            float assetY = 100f;
            float assetSpacing = 150f;
            
            int assetIndex = 0;
            foreach (var assetNode in m_AssetNodes.Values)
            {
                assetNode.SetPosition(new Rect(assetStartX, assetY + assetIndex * assetSpacing, 0, 0));
                assetIndex++;
            }
        }
        
        
        
        private void LayoutNodeHierarchy(GameObject gameObject, float x, float y, int depth)
        {
            if (m_GameObjectNodes.TryGetValue(gameObject, out var node))
            {
                node.SetPosition(new Rect(x + depth * 250f, y, 0, 0));
                
                // Layout children
                float childY = y;
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    var child = gameObject.transform.GetChild(i).gameObject;
                    LayoutNodeHierarchy(child, x, childY, depth + 1);
                    childY += 150f;
                }
            }
        }
        
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            
            ports.ForEach((port) =>
            {
                if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
                {
                    // Additional compatibility checks for reference ports
                    if (startPort.userData is ReferencePortData || port.userData is ReferencePortData)
                    {
                        // Allow connections between reference output ports and any input port
                        if ((startPort.userData is ReferencePortData && port.direction == Direction.Input) ||
                            (port.userData is ReferencePortData && startPort.direction == Direction.Input))
                        {
                            compatiblePorts.Add(port);
                        }
                    }
                    else
                    {
                        compatiblePorts.Add(port);
                    }
                }
            });
            
            return compatiblePorts;
        }
    }
}
