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
                kvp.Value.SetHighlighted(false, null);
            }

            ClearSelection();

            if (m_FocusMode && m_FocusedNode != null)
            {
                // Focus mode: show only focused node and transitively connected nodes
                var relatedNodes = new HashSet<GameObjectGraphNode>();
                TraverseRelatedNodes(m_FocusedNode, relatedNodes);

                // Apply hierarchy depth filter relative to focused node in focus mode
                if (m_MaxHierarchyDepth >= 0)
                {
                    var filteredNodes = new HashSet<GameObjectGraphNode>();
                    foreach (var node in relatedNodes)
                    {
                        int depthFromFocused = GetDepthFromFocusedNode(node, m_FocusedNode);
                        if (depthFromFocused <= m_MaxHierarchyDepth)
                        {
                            filteredNodes.Add(node);
                        }
                    }

                    relatedNodes = filteredNodes;
                }

                // Hide all nodes first
                foreach (var (gameObject, node) in m_GameObjectNodes)
                {
                    node.style.display = DisplayStyle.None;
                }

                foreach (var assetNode in m_AssetNodes.Values)
                {
                    assetNode.style.display = DisplayStyle.None;
                }

                // Show only filtered related nodes and add to visible collection
                foreach (var node in relatedNodes)
                {
                    node.style.display = DisplayStyle.Flex;
                    m_VisibleNodes.Add(node); // Important: Add to visible nodes collection
                }

                // Show related asset nodes (only for visible nodes)
                var relatedAssets = new HashSet<AssetReferenceNode>();
                foreach (var node in relatedNodes)
                {
                    FindConnectedAssetNodes(node, relatedAssets);
                }

                foreach (var assetNode in relatedAssets)
                {
                    assetNode.style.display = DisplayStyle.Flex;
                    m_VisibleNodes.Add(assetNode); // Important: Add to visible nodes collection
                }

                // Highlight the focused node
                m_FocusedNode.SetHighlighted(true, Color.blueViolet);

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

                foreach (var node in relatedNodes)
                {
                    // Apply search filter and highlight matches
                    var matchesSearch = !string.IsNullOrEmpty(m_SearchFilter) &&
                                        node.name.ToLower().Contains(m_SearchFilter);

                    if (visible)
                    {
                        if (matchesSearch)
                        {
                            node.SetHighlighted(true, Color.red);
                        }
                        else
                        {
                            node.SetHighlighted(false, null);
                        }
                    }
                    else
                    {
                        node.style.display = DisplayStyle.None;
                    }
                }
            }
            else
            {
                // Normal mode: apply search and depth filters from root
                foreach (var (gameObject, node) in m_GameObjectNodes)
                {
                    bool visible = true;

                    // Apply hierarchy depth filter from root in normal mode
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
                            node.SetHighlighted(true, Color.red);
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


        private int GetDepthFromFocusedNode(GameObjectGraphNode targetNode, GameObjectGraphNode focusedNode)
        {
            if (targetNode == focusedNode) return 0;

            // Use BFS to find the shortest path depth from focused node to target node
            var queue = new Queue<(GameObjectGraphNode node, int depth)>();
            var visited = new HashSet<GameObjectGraphNode>();

            queue.Enqueue((focusedNode, 0));
            visited.Add(focusedNode);

            while (queue.Count > 0)
            {
                var (currentNode, currentDepth) = queue.Dequeue();

                // Check all outgoing connections from current node
                foreach (var edge in graphElements.OfType<Edge>())
                {
                    if (edge.output?.node == currentNode && edge.input?.node is GameObjectGraphNode connectedNode)
                    {
                        if (connectedNode == targetNode)
                        {
                            return currentDepth + 1;
                        }

                        if (!visited.Contains(connectedNode))
                        {
                            visited.Add(connectedNode);
                            queue.Enqueue((connectedNode, currentDepth + 1));
                        }
                    }
                }
            }

            // If no path found, return a high value to exclude it
            return int.MaxValue;
        }


        private void TraverseRelatedNodes(GameObjectGraphNode startNode, HashSet<GameObjectGraphNode> visited)
        {
            if (!visited.Add(startNode)) return;

            // Only traverse through outgoing edges (where startNode is the output/source)
            foreach (var edge in graphElements.OfType<Edge>())
            {
                GameObjectGraphNode connectedNode = null;

                // Only check if this edge has startNode as the OUTPUT (source) node
                if (edge.output?.node == startNode && edge.input?.node is GameObjectGraphNode inputNode)
                {
                    connectedNode = inputNode;
                }

                // Recursively traverse only outgoing connected nodes
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
                // Only find asset nodes that this GameObject node connects TO (outgoing connections)
                if (edge.output?.node == gameObjectNode && edge.input?.node is AssetReferenceNode assetNode)
                {
                    assetNodes.Add(assetNode);
                }
            }
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

            UpdateAllEdgeRouting();
            ApplyFilters();
        }

        public void ExpandAllNodes()
        {
            foreach (var node in m_GameObjectNodes.Values)
            {
                node.SetExpanded(true);
            }

            UpdateAllEdgeRouting();
            ApplyFilters();
        }

        private void UpdateAllEdgeRouting()
        {
            foreach (var node in m_GameObjectNodes.Values)
            {
                UpdateEdgeRoutingForNode(node);
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

            switch (referencedObject)
            {
                // Check if it's a GameObject or Component reference
                case GameObject targetGameObject:
                {
                    if (m_GameObjectNodes.TryGetValue(targetGameObject, out var gameObjectNode))
                    {
                        targetNode = gameObjectNode;
                        targetPort = gameObjectNode.ReferenceInputPort;
                    }

                    break;
                }
                case Component targetComponent:
                {
                    if (m_GameObjectNodes.TryGetValue(targetComponent.gameObject, out var gameObjectNode))
                    {
                        targetNode = gameObjectNode;
                        targetPort = gameObjectNode.ReferenceInputPort;
                    }

                    break;
                }
                // Check if it's an asset reference
                default:
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

                    break;
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
                bool shouldUpdateEdge = false;
                bool shouldShow = true;

                // Check if this edge is connected to the toggled node
                if (edge.output?.node == node || edge.input?.node == node)
                {
                    shouldUpdateEdge = true;

                    // Always show edges connected to collapsed ports when node is collapsed
                    if (!node.IsExpanded)
                    {
                        bool isCollapsedPortEdge = (edge.output == node.CollapsedReferenceOutputPort) ||
                                                   (edge.input == node.CollapsedReferenceInputPort);
                        shouldShow = isCollapsedPortEdge;
                    }
                    else
                    {
                        // When expanded, show individual component port edges
                        bool isComponentPortEdge = (node.GetComponentElementFromPort(edge.output) != null) ||
                                                   (node.GetComponentElementFromPort(edge.input) != null);
                        shouldShow = isComponentPortEdge ||
                                     edge.output == node.HierarchyOutputPort ||
                                     edge.input == node.HierarchyInputPort ||
                                     edge.input == node.ReferenceInputPort;
                    }
                }

                if (shouldUpdateEdge)
                {
                    edge.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
                }
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

            if (m_FocusMode && m_FocusedNode != null)
            {
                // In focus mode: only layout visible nodes
                ApplyFocusedSugiyamaLayout();
            }
            else
            {
                // Normal mode: layout all nodes
                m_SugiyamaLayout.ApplyLayout(m_GameObjectNodes, m_AssetNodes);
            }
        }

        private void ApplyFocusedSugiyamaLayout()
        {
            // Get only visible GameObject nodes
            var visibleGameObjectNodes = new Dictionary<GameObject, GameObjectGraphNode>();
            foreach (var node in m_VisibleNodes.OfType<GameObjectGraphNode>())
            {
                visibleGameObjectNodes[node.GameObject] = node;
            }

            // Get only visible Asset nodes
            var visibleAssetNodes = new Dictionary<Object, AssetReferenceNode>();
            foreach (var node in m_VisibleNodes.OfType<AssetReferenceNode>())
            {
                visibleAssetNodes[node.Asset] = node;
            }

            // Apply layout only to visible nodes
            if (visibleGameObjectNodes.Count > 0 || visibleAssetNodes.Count > 0)
            {
                m_SugiyamaLayout.ApplyLayout(visibleGameObjectNodes, visibleAssetNodes);
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

        public void UpdateEdgeRoutingForNode(GameObjectGraphNode node)
        {
            var edgesToUpdate = new List<Edge>();

            // Find all edges connected to this node's component ports
            foreach (var edge in graphElements.OfType<Edge>())
            {
                bool isComponentEdge = false;

                // Check if edge connects to/from component ports of this node
                if (edge.output?.node == node && node.GetComponentElementFromPort(edge.output) != null)
                {
                    isComponentEdge = true;
                }
                else if (edge.input?.node == node && node.GetComponentElementFromPort(edge.input) != null)
                {
                    isComponentEdge = true;
                }

                if (isComponentEdge)
                {
                    edgesToUpdate.Add(edge);
                }
            }

            // Reroute edges based on expansion state
            foreach (var edge in edgesToUpdate)
            {
                RerouteEdgeForNodeState(edge, node);
            }
        }

        private void RerouteEdgeForNodeState(Edge edge, GameObjectGraphNode node)
        {
            Port newOutputPort = edge.output;
            Port newInputPort = edge.input;

            // Handle output port rerouting
            if (edge.output?.node == node)
            {
                if (node.IsExpanded)
                {
                    // Node is expanded - edge should connect to specific component port
                    // The original component port should already be stored or can be determined
                    // from the edge's userData or by finding the appropriate component port
                }
                else
                {
                    // Node is collapsed - reroute to collapsed output port
                    newOutputPort = node.CollapsedReferenceOutputPort;
                }
            }

            // Handle input port rerouting  
            if (edge.input?.node == node)
            {
                if (node.IsExpanded)
                {
                    // Node is expanded - edge should connect to specific component port or main reference port
                    // Determine appropriate target port based on connection type
                }
                else
                {
                    // Node is collapsed - reroute to collapsed input port
                    newInputPort = node.CollapsedReferenceInputPort;
                }
            }

            // Only recreate edge if ports actually changed
            if (newOutputPort != edge.output || newInputPort != edge.input)
            {
                // Store edge data before removing
                var edgeData = new EdgeConnectionData
                {
                    OriginalOutputPort = edge.output,
                    OriginalInputPort = edge.input,
                    OutputNode = edge.output?.node,
                    InputNode = edge.input?.node
                };

                // Remove old edge
                RemoveElement(edge);

                // Create new edge with rerouted ports
                var newEdge = new Edge
                {
                    output = newOutputPort,
                    input = newInputPort
                };

                // Preserve edge styling
                newEdge.AddToClassList(edge.GetClasses().FirstOrDefault() ?? "reference-edge");

                // Store connection data for future rerouting
                newEdge.userData = edgeData;

                AddElement(newEdge);
            }
        }

        // Helper class to store original connection information
        private class EdgeConnectionData
        {
            public Port OriginalOutputPort;
            public Port OriginalInputPort;
            public Node OutputNode;
            public Node InputNode;
        }
    }
}