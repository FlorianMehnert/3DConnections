namespace _3DConnections.Editor.NodeGraph
{
    using UnityEngine;
    using UnityEditor;
    using UnityEngine.UIElements;
    using UnityEditor.Experimental.GraphView;
    using System.Collections.Generic;
    using System.Linq;

    public class SceneGraphView : GraphView
    {
        private Dictionary<GameObject, GameObjectGraphNode> m_GameObjectNodes = new();
        private Dictionary<Object, AssetReferenceNode> m_AssetNodes = new();
        private Dictionary<string, Edge> m_ReferenceEdges = new();
        private SugiyamaLayout m_SugiyamaLayout = new();
        private int m_MaxHierarchyDepth = -1; // -1 means show all
        private string m_SearchFilter = "";
        private bool m_FocusMode = false;
        private GameObjectGraphNode m_FocusedNode = null;
        private HashSet<Node> m_VisibleNodes = new HashSet<Node>();
        private HashSet<Edge> m_VisibleEdges = new HashSet<Edge>();
        private GridBackground grid;

        public SceneGraphView()
        {
            SetupZoom(0.00001f, 10f);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // Load styles
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/SceneGraphView.uss");
            if (styleSheet != null)
                styleSheets.Add(styleSheet);

            // Handle connection events
            graphViewChanged += OnGraphViewChanged;
        }

        private void OnSelectionChanged(List<ISelectable> selection)
        {
        }

        public void SetHierarchyDepthFilter(int maxDepth)
        {
            m_MaxHierarchyDepth = maxDepth;
            ApplyFilters();
        }

        public void SetSearchFilter(string searchText)
        {
            m_SearchFilter = searchText?.ToLower() ?? "";
            ApplyFilters();
        }

        public void SetFocusMode(GameObjectGraphNode focusNode)
        {
            m_FocusMode = focusNode != null;
            m_FocusedNode = focusNode;
            ApplyFilters();
        }

        public void ClearFocusMode()
        {
            SetFocusMode(null);
        }

        public void ApplyFilters()
        {
            m_VisibleNodes.Clear();
            m_VisibleEdges.Clear();

            // First pass: clear highlights
            foreach (var kvp in m_GameObjectNodes)
            {
                kvp.Value.SetHighlighted(false);
            }

            ClearSelection();

            // Second pass: apply visibility and highlight matches
            foreach (var (gameObject, node) in m_GameObjectNodes)
            {
                var matchesSearch = !string.IsNullOrEmpty(m_SearchFilter) &&
                                    gameObject.name.ToLower().Contains(m_SearchFilter);

                m_VisibleNodes.Add(node);
                node.style.display = DisplayStyle.Flex;

                if (matchesSearch)
                {
                    node.SetHighlighted(true);
                }
            }
        }


        private bool ShouldShowGameObjectNode(GameObject gameObject, GameObjectGraphNode node)
        {
            // Hierarchy depth filter
            if (m_MaxHierarchyDepth != -1)
            {
                int depth = GetGameObjectHierarchyDepth(gameObject);
                if (depth > m_MaxHierarchyDepth) return false;
            }

            // Search filter - REMOVED: Now all nodes stay visible during search
            // The search highlighting is handled separately in ApplyFilters()

            // Focus mode filter
            if (m_FocusMode && m_FocusedNode != null)
            {
                return IsNodeInFocusScope(node);
            }

            return true;
        }

        private bool ShouldShowAssetNode(Object asset)
        {
            // Search filter - REMOVED: Now all asset nodes stay visible during search
            // The search highlighting is handled separately in ApplyFilters()

            // Focus mode filter
            if (m_FocusMode && m_FocusedNode != null)
            {
                return IsAssetReferencedByFocusedNode(asset);
            }

            // Only show assets that are actually referenced by visible GameObjects
            return IsAssetReferencedByVisibleGameObjects(asset);
        }

        private bool IsNodeInFocusScope(GameObjectGraphNode node)
        {
            if (node == m_FocusedNode) return true;

            // Check if this node references assets that the focused node also references
            var focusedAssets = GetReferencedAssets(m_FocusedNode.GameObject);
            var nodeAssets = GetReferencedAssets(node.GameObject);

            return focusedAssets.Intersect(nodeAssets).Any();
        }

        private bool IsAssetReferencedByFocusedNode(Object asset)
        {
            if (m_FocusedNode == null) return false;
            return GetReferencedAssets(m_FocusedNode.GameObject).Contains(asset);
        }

        private bool IsAssetReferencedByVisibleGameObjects(Object asset)
        {
            foreach (var gameObjectNode in m_GameObjectNodes.Values)
            {
                if (m_VisibleNodes.Contains(gameObjectNode))
                {
                    if (GetReferencedAssets(gameObjectNode.GameObject).Contains(asset))
                        return true;
                }
            }

            return false;
        }

        private HashSet<Object> GetReferencedAssets(GameObject gameObject)
        {
            var referencedAssets = new HashSet<Object>();
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
                            var referencedObject = property.objectReferenceValue;
                            if (ShouldCreateAssetNode(referencedObject))
                            {
                                referencedAssets.Add(referencedObject);
                            }
                        }
                    } while (property.NextVisible(false));
                }
            }

            return referencedAssets;
        }

        private bool ArePortsVisible(Port inputPort, Port outputPort)
        {
            // Check if ports are visible based on node expansion state
            if (inputPort.node is GameObjectGraphNode inputGameObjectNode)
            {
                if (!inputGameObjectNode.IsExpanded && inputPort != inputGameObjectNode.HierarchyInputPort &&
                    inputPort != inputGameObjectNode.ReferenceInputPort)
                    return false;
            }

            if (outputPort.node is GameObjectGraphNode outputGameObjectNode)
            {
                if (!outputGameObjectNode.IsExpanded && outputPort != outputGameObjectNode.HierarchyOutputPort)
                    return false;
            }

            return true;
        }

        public void RefreshGraph()
        {
            ClearGraph();
            TraverseScene();
            CreateAssetNodes();
            CreateConnections();
            ApplySugiyamaLayout();
            ApplyFilters();
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
                                    // For sprites from sprite sheets, use the texture instead
                                    if (referencedObject is Sprite sprite && sprite.texture != null)
                                    {
                                        referencedAssets.Add(sprite.texture);
                                    }
                                    else
                                    {
                                        referencedAssets.Add(referencedObject);
                                    }
                                }
                            }
                        } while (property.NextVisible(false));
                    }
                }
            }

            // Create asset nodes (with deduplication)
            foreach (var asset in referencedAssets)
            {
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
                    targetObject = GetConnectionTarget(targetGameObjectNode);
                }
                else if (edge.input.node is AssetReferenceNode targetAssetNode)
                {
                    targetObject = targetAssetNode.Asset;
                }

                if (targetObject != null)
                {
                    // Update the component property
                    var sourceComponentElement =
                        (edge.output.node as GameObjectGraphNode)?.GetComponentElement(outputData.Component);
                    sourceComponentElement?.UpdatePropertyValue(outputData.PropertyPath, targetObject);

                    // Store the edge for tracking
                    var edgeKey = $"{outputData.Component.GetInstanceID()}_{outputData.PropertyPath}";
                    m_ReferenceEdges[edgeKey] = edge;
                }
            }
        }

        private Object GetConnectionTarget(GameObjectGraphNode targetNode)
        {
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
            m_VisibleNodes.Clear();
            m_VisibleEdges.Clear();
        }

        public void CollapseAllNodes()
        {
            foreach (var node in m_GameObjectNodes.Values)
            {
                node.SetExpanded(false);
            }

            ApplyFilters(); // Refresh edge visibility
        }

        public void ExpandAllNodes()
        {
            foreach (var node in m_GameObjectNodes.Values)
            {
                node.SetExpanded(true);
            }

            ApplyFilters(); // Refresh edge visibility
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
                    } while (property.NextVisible(false));
                }
            }
        }

        private void CreateReferenceConnectionForProperty(GameObjectGraphNode sourceNode, Component component,
            SerializedProperty property)
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
            else
            {
                // For sprites from sprite sheets, connect to the texture asset node
                Object assetToConnect = referencedObject;
                if (referencedObject is Sprite sprite && sprite.texture != null)
                {
                    assetToConnect = sprite.texture;
                }

                if (m_AssetNodes.TryGetValue(assetToConnect, out var assetNode))
                {
                    targetNode = assetNode;
                    targetPort = assetNode.ReferenceInputPort;
                }
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

            // Update node sizes first
            m_SugiyamaLayout.UpdateNodeSizes(m_GameObjectNodes, m_AssetNodes);

            // Apply the Sugiyama layout algorithm
            m_SugiyamaLayout.ApplyLayout(m_GameObjectNodes, m_AssetNodes);
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