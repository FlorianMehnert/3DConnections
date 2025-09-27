using System.Linq;

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
        private string m_SearchFilter = "";
        private bool m_FocusMode = false;
        private GameObjectGraphNode m_FocusedNode = null;
        private HashSet<Node> m_VisibleNodes = new HashSet<Node>();
        private HashSet<Edge> m_VisibleEdges = new HashSet<Edge>();
        private GridBackground grid;
        private Button m_ExitFocusButton;

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

            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void SetHierarchyDepthFilter(int maxDepth)
        {
            m_MaxHierarchyDepth = maxDepth;
            ApplyFilters();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.F && selection.Count == 1)
            {
                var node = selection.First() as GameObjectGraphNode;
                if (node != null)
                {
                    FocusOnNode(node);
                    evt.StopPropagation();
                }
            }
            else if (evt.keyCode == KeyCode.Escape && m_FocusMode)
            {
                ClearFocusMode();
                evt.StopPropagation();
            }
        }

        public void SetSearchFilter(string searchText)
        {
            m_SearchFilter = searchText?.ToLower() ?? "";
            ApplyFilters();
        }

        public void FocusOnNode(GameObjectGraphNode node)
        {
            m_FocusMode = true;
            m_FocusedNode = node;
            ShowExitFocusButton();
            ApplyFilters();
        }

        public void ClearFocusMode()
        {
            m_FocusMode = false;
            m_FocusedNode = null;
            HideExitFocusButton();
            ApplyFilters();
        }

        private void ShowExitFocusButton()
        {
            if (m_ExitFocusButton == null)
            {
                m_ExitFocusButton = new Button(ClearFocusMode)
                {
                    text = "Exit Focus Mode (Esc)"
                };
                m_ExitFocusButton.AddToClassList("focus-exit-button");
                m_ExitFocusButton.style.position = Position.Absolute;
                m_ExitFocusButton.style.top = 10;
                m_ExitFocusButton.style.right = 10;
                m_ExitFocusButton.style.backgroundColor = new Color(1f, 0.8f, 0f, 0.9f); // Gold background
                m_ExitFocusButton.style.color = Color.black;
                m_ExitFocusButton.style.paddingLeft = 10;
                m_ExitFocusButton.style.paddingRight = 10;
                m_ExitFocusButton.style.paddingTop = 5;
                m_ExitFocusButton.style.paddingBottom = 5;
                Add(m_ExitFocusButton);
            }

            m_ExitFocusButton.visible = true;
        }

        private void HideExitFocusButton()
        {
            if (m_ExitFocusButton != null)
                m_ExitFocusButton.visible = false;
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

            if (m_FocusMode && m_FocusedNode != null)
            {
                // Focus mode: show only focused node and transitively connected nodes
                var relatedNodes = new HashSet<GameObjectGraphNode>();
                TraverseRelatedNodes(m_FocusedNode, relatedNodes);

                foreach (var (gameObject, node) in m_GameObjectNodes)
                {
                    node.style.display = DisplayStyle.None;
                }

                foreach (var assetNode in m_AssetNodes.Values)
                {
                    assetNode.style.display = DisplayStyle.None;
                }

                foreach (var node in relatedNodes)
                {
                    node.style.display = DisplayStyle.Flex;
                    m_VisibleNodes.Add(node);
                }

                // Show related asset nodes
                var relatedAssets = new HashSet<AssetReferenceNode>();
                foreach (var node in relatedNodes)
                {
                    FindConnectedAssetNodes(node, relatedAssets);
                }

                foreach (var assetNode in relatedAssets)
                {
                    assetNode.style.display = DisplayStyle.Flex;
                    m_VisibleNodes.Add(assetNode);
                }

                // Highlight the focused node
                m_FocusedNode.SetHighlighted(true);

                // Show only edges between visible nodes
                foreach (var edge in graphElements.OfType<Edge>())
                {
                    bool inputVisible = edge.input?.node != null && m_VisibleNodes.Contains(edge.input.node);
                    bool outputVisible = edge.output?.node != null && m_VisibleNodes.Contains(edge.output.node);

                    if (inputVisible && outputVisible)
                    {
                        edge.style.display = DisplayStyle.Flex;
                        m_VisibleEdges.Add(edge);
                    }
                    else
                    {
                        edge.style.display = DisplayStyle.None;
                    }
                }
            }
            else
            {
                // Normal mode: apply search and depth filters
                foreach (var (gameObject, node) in m_GameObjectNodes)
                {
                    bool visible = true;

                    // Apply hierarchy depth filter
                    if (m_MaxHierarchyDepth >= 0)
                    {
                        int depth = GetGameObjectHierarchyDepth(gameObject);
                        if (depth > m_MaxHierarchyDepth)
                            visible = false;
                    }

                    // Apply search filter and highlight matches
                    var matchesSearch = !string.IsNullOrEmpty(m_SearchFilter) &&
                                        gameObject.name.ToLower().Contains(m_SearchFilter);

                    if (visible)
                    {
                        m_VisibleNodes.Add(node);
                        node.style.display = DisplayStyle.Flex;

                        if (matchesSearch)
                        {
                            node.SetHighlighted(true);
                        }
                    }
                    else
                    {
                        node.style.display = DisplayStyle.None;
                    }
                }

                // Show all asset nodes in normal mode
                foreach (var assetNode in m_AssetNodes.Values)
                {
                    assetNode.style.display = DisplayStyle.Flex;
                    m_VisibleNodes.Add(assetNode);
                }

                // Show all edges between visible nodes
                foreach (var edge in graphElements.OfType<Edge>())
                {
                    bool inputVisible = edge.input?.node != null && m_VisibleNodes.Contains(edge.input.node);
                    bool outputVisible = edge.output?.node != null && m_VisibleNodes.Contains(edge.output.node);

                    if (inputVisible && outputVisible && ArePortsVisible(edge.input, edge.output))
                    {
                        edge.style.display = DisplayStyle.Flex;
                        m_VisibleEdges.Add(edge);
                    }
                    else
                    {
                        edge.style.display = DisplayStyle.None;
                    }
                }
            }
        }

        private void TraverseRelatedNodes(GameObjectGraphNode startNode, HashSet<GameObjectGraphNode> visited)
        {
            if (!visited.Add(startNode)) return;

            // Traverse through all connected edges
            foreach (var edge in graphElements.OfType<Edge>())
            {
                GameObjectGraphNode connectedNode = null;

                // Check if this edge connects to our current node
                if (edge.output?.node == startNode && edge.input?.node is GameObjectGraphNode inputNode)
                {
                    connectedNode = inputNode;
                }
                else if (edge.input?.node == startNode && edge.output?.node is GameObjectGraphNode outputNode)
                {
                    connectedNode = outputNode;
                }

                // Recursively traverse connected nodes
                if (connectedNode != null)
                {
                    TraverseRelatedNodes(connectedNode, visited);
                }
            }
        }

        private void FindConnectedAssetNodes(GameObjectGraphNode gameObjectNode, HashSet<AssetReferenceNode> assetNodes)
        {
            foreach (var edge in graphElements.OfType<Edge>())
            {
                if (edge.output?.node == gameObjectNode && edge.input?.node is AssetReferenceNode assetReferenceNode)
                {
                    assetNodes.Add(assetReferenceNode);
                }
                else if (edge.input?.node == gameObjectNode && edge.output?.node is AssetReferenceNode assetNode)
                {
                    assetNodes.Add(assetNode);
                }
            }
        }

        public void SetFocusMode(GameObjectGraphNode focusNode)
        {
            m_FocusMode = focusNode != null;
            m_FocusedNode = focusNode;
            ApplyFilters();
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

            UpdateAllEdgeVisibility();
            ApplyFilters();
        }

        public void ExpandAllNodes()
        {
            foreach (var node in m_GameObjectNodes.Values)
            {
                node.SetExpanded(true);
            }

            UpdateAllEdgeVisibility();
            ApplyFilters();
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

                CreateHierarchyConnections(gameObject, node);
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

        public void UpdateEdgeVisibilityForNode(GameObjectGraphNode node)
        {
            foreach (var edge in graphElements.OfType<Edge>())
            {
                // Check if the edge is connected to any of the node's component reference output ports
                bool isConnectedToCollapsedPort = false;

                // Check output port
                if (edge.output != null && edge.output.node == node)
                {
                    // If the node is collapsed and this is a component reference output port, hide the edge
                    if (!node.IsExpanded && node.GetComponentElementFromPort(edge.output) != null)
                    {
                        isConnectedToCollapsedPort = true;
                    }
                }

                // Check input port (for reference input port, you may want to hide those edges too)
                if (edge.input != null && edge.input.node == node)
                {
                    // If the node is collapsed and this is a component reference input port, hide the edge
                    if (!node.IsExpanded && node.GetComponentElementFromPort(edge.input) != null)
                    {
                        isConnectedToCollapsedPort = true;
                    }
                }

                edge.style.display = isConnectedToCollapsedPort ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public void UpdateAllEdgeVisibility()
        {
            foreach (var edge in graphElements.OfType<Edge>())
            {
                bool shouldShow = true;

                // Check if either end is a component reference port on a collapsed node
                if (edge.output?.node is GameObjectGraphNode outputNode)
                {
                    if (!outputNode.IsExpanded && outputNode.GetComponentElementFromPort(edge.output) != null)
                        shouldShow = false;
                }

                if (edge.input?.node is GameObjectGraphNode inputNode)
                {
                    if (!inputNode.IsExpanded && inputNode.GetComponentElementFromPort(edge.input) != null)
                        shouldShow = false;
                }

                edge.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
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