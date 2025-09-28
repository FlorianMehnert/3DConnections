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
        private readonly Dictionary<GameObject, GameObjectGraphNode> m_GameObjectNodes = new();
        private readonly Dictionary<Object, AssetReferenceNode> m_AssetNodes = new();
        private readonly Dictionary<string, Edge> m_ReferenceEdges = new();
        private readonly SugiyamaLayout m_SugiyamaLayout = new();
        private int m_MaxHierarchyDepth = -1; // -1 means show all
        private string m_SearchFilter = "";
        private bool m_FocusMode;
        private GameObjectGraphNode m_FocusedNode;
        private readonly HashSet<Node> m_VisibleNodes = new();
        private HashSet<Edge> m_VisibleEdges = new();
        private GridBackground grid;
        private Button m_ExitFocusButton;
        private readonly Dictionary<string, ReferenceEdgeData> m_ReferenceEdgeData = new();

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
            switch (evt.keyCode)
            {
                case KeyCode.F when selection.Count == 1:
                {
                    if (selection.First() is GameObjectGraphNode node)
                    {
                        FocusOnNode(node);
                        evt.StopPropagation();
                    }

                    break;
                }
                case KeyCode.Escape when m_FocusMode:
                    ClearFocusMode();
                    evt.StopPropagation();
                    break;
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
                m_ExitFocusButton.style.backgroundColor = new Color(1f, 0.8f, 0f, 0.9f);
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
                foreach (var (_, node) in m_GameObjectNodes)
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
                    var inputVisible = edge.input?.node != null && m_VisibleNodes.Contains(edge.input.node);
                    var outputVisible = edge.output?.node != null && m_VisibleNodes.Contains(edge.output.node);

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
                    var visible = true;

                    // Apply hierarchy depth filter from root in normal mode
                    int depth = GetGameObjectHierarchyDepth(gameObject);
                    if (m_MaxHierarchyDepth >= 0)
                    {
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
                    var inputVisible = edge.input?.node != null && m_VisibleNodes.Contains(edge.input.node);
                    var outputVisible = edge.output?.node != null && m_VisibleNodes.Contains(edge.output.node);

                    // Also check if the ports themselves are visible
                    var inputPortVisible = edge.input?.style.display != DisplayStyle.None;
                    var outputPortVisible = edge.output?.style.display != DisplayStyle.None;

                    if (inputVisible && outputVisible && inputPortVisible && outputPortVisible)
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
                    if (edge.output?.node != currentNode ||
                        edge.input?.node is not GameObjectGraphNode connectedNode) continue;
                    if (connectedNode == targetNode)
                    {
                        return currentDepth + 1;
                    }

                    if (!visited.Add(connectedNode)) continue;
                    queue.Enqueue((connectedNode, currentDepth + 1));
                }
            }

            // If no path found, return a high value to exclude it
            return int.MaxValue;
        }


        private void TraverseRelatedNodes(GameObjectGraphNode startNode, HashSet<GameObjectGraphNode> visited)
        {
            if (!visited.Add(startNode)) return;

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
            foreach (var property in from gameObjectNode in m_GameObjectNodes.Values
                     select gameObjectNode.GameObject.GetComponents<Component>()
                     into components
                     from component in components
                     where component != null
                     select new SerializedObject(component)
                     into serializedObject
                     select serializedObject.GetIterator()
                     into property
                     where property.NextVisible(true)
                     select property)
            {
                do
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue == null) continue;
                    var referencedObject = property.objectReferenceValue;

                    // Only create asset nodes for non-scene objects
                    if (!ShouldCreateAssetNode(referencedObject)) continue;
                    // For sprites from sprite sheets, use the texture instead
                    if (referencedObject is Sprite sprite && sprite.texture != null)
                    {
                        referencedAssets.Add(sprite.texture);
                    }
                    else
                    {
                        referencedAssets.Add(referencedObject);
                    }
                } while (property.NextVisible(false));
            }

            // Create asset nodes (with deduplication)
            foreach (var asset in referencedAssets)
            {
                if (m_AssetNodes.ContainsKey(asset)) continue;
                var assetNode = new AssetReferenceNode(asset, this);
                m_AssetNodes[asset] = assetNode;
                AddElement(assetNode);
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
            var depth = 0;
            var current = gameObject.transform;

            while (current.parent != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
        {
            // Handle edge creation
            if (graphViewChange.edgesToCreate != null)
            {
                foreach (var edge in graphViewChange.edgesToCreate)
                {
                    // Check if this is a hierarchy edge
                    if (IsHierarchyEdge(edge))
                    {
                        HandleHierarchyConnectionCreated(edge);
                    }
                    else
                    {
                        HandleConnectionCreated(edge);
                    }
                }
            }

            // Handle edge removal
            if (graphViewChange.elementsToRemove != null)
            {
                foreach (var element in graphViewChange.elementsToRemove)
                {
                    if (element is Edge edge && IsHierarchyEdge(edge))
                    {
                        HandleHierarchyConnectionRemoved(edge);
                    }
                }
            }

            return graphViewChange;
        }

        private bool IsHierarchyEdge(Edge edge)
        {
            // Check if the ports are hierarchy ports
            return edge.output?.node is GameObjectGraphNode outputNode &&
                   edge.input?.node is GameObjectGraphNode inputNode &&
                   edge.output == outputNode.HierarchyOutputPort &&
                   edge.input == inputNode.HierarchyInputPort;
        }

        private void HandleHierarchyConnectionCreated(Edge edge)
        {
            if (edge.output?.node is not GameObjectGraphNode parentNode || 
                edge.input?.node is not GameObjectGraphNode childNode)
                return;

            var parentGameObject = parentNode.GameObject;
            var childGameObject = childNode.GameObject;
    
            // Show confirmation dialog
            if (!EditorUtility.DisplayDialog("Change Hierarchy", 
                    $"Set '{childGameObject.name}' as child of '{parentGameObject.name}'?", 
                    "Yes", "Cancel"))
            {
                // User cancelled - remove the edge
                schedule.Execute(() => {
                    edge.output?.Disconnect(edge);
                    edge.input?.Disconnect(edge);
                    RemoveElement(edge);
                });
                return;
            }

            // Prevent circular dependencies
            if (IsCircularDependency(parentGameObject, childGameObject))
            {
                EditorUtility.DisplayDialog("Invalid Operation",
                    "Cannot create this hierarchy connection as it would create a circular dependency.", "OK");

                // Remove the edge
                schedule.Execute(() =>
                {
                    edge.output?.Disconnect(edge);
                    edge.input?.Disconnect(edge);
                    RemoveElement(edge);
                });
                return;
            }

            // Record undo operation
            Undo.RecordObject(childGameObject.transform, $"Set Parent to {parentGameObject.name}");

            // Update the actual transform hierarchy
            childGameObject.transform.SetParent(parentGameObject.transform);

            // Mark the scene as dirty
            EditorUtility.SetDirty(childGameObject);

            // Add hierarchy edge styling
            edge.AddToClassList("hierarchy-edge");
        }

        private void HandleHierarchyConnectionRemoved(Edge edge)
        {
            if (edge.input?.node is not GameObjectGraphNode childNode)
                return;

            var childGameObject = childNode.GameObject;

            // Record undo operation
            Undo.RecordObject(childGameObject.transform, "Remove Parent");

            // Remove from parent (make it a root object)
            childGameObject.transform.SetParent(null);

            // Mark the scene as dirty
            EditorUtility.SetDirty(childGameObject);
        }

        private bool IsCircularDependency(GameObject potentialParent, GameObject potentialChild)
        {
            // Check if potentialParent is already a child of potentialChild
            Transform current = potentialParent.transform;

            while (current != null)
            {
                if (current.gameObject == potentialChild)
                    return true;
                current = current.parent;
            }

            return false;
        }


        private void HandleConnectionCreated(Edge edge)
        {
            // Handle reference connections
            if (edge.output.userData is not ReferencePortData outputData) return;

            var targetObject = edge.input.node switch
            {
                GameObjectGraphNode targetGameObjectNode => GetConnectionTarget(targetGameObjectNode),
                AssetReferenceNode targetAssetNode => targetAssetNode.Asset,
                _ => null
            };

            if (targetObject == null) return;
            // Update the component property
            var sourceComponentElement =
                (edge.output.node as GameObjectGraphNode)?.GetComponentElement(outputData.Component);
            sourceComponentElement?.UpdatePropertyValue(outputData.PropertyPath, targetObject);

            // Store the edge for tracking
            var edgeKey = $"{outputData.Component.GetInstanceID()}_{outputData.PropertyPath}";
            m_ReferenceEdges[edgeKey] = edge;
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
            foreach (var (gameObject, node) in m_GameObjectNodes)
            {
                CreateHierarchyConnections(gameObject, node);
                CreateComponentConnections(gameObject, node);
            }
        }

        private void CreateHierarchyConnections(GameObject gameObject, GameObjectGraphNode node)
        {
            if (gameObject.transform.parent == null) return;
            var parentGameObject = gameObject.transform.parent.gameObject;
            if (!m_GameObjectNodes.TryGetValue(parentGameObject, out var parentNode)) return;
            var edge = new Edge
            {
                output = parentNode.HierarchyOutputPort,
                input = node.HierarchyInputPort
            };

            edge.output.Connect(edge);
            edge.input.Connect(edge);

            edge.AddToClassList("hierarchy-edge");
            AddElement(edge);
        }

        private void CreateComponentConnections(GameObject gameObject, GameObjectGraphNode node)
        {
            var components = gameObject.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component == null) continue;

                var serializedObject = new SerializedObject(component);
                var property = serializedObject.GetIterator();

                if (!property.NextVisible(true)) continue;
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

        private void CreateReferenceConnectionForProperty(GameObjectGraphNode sourceNode, Component component,
            SerializedProperty property)
        {
            var referencedObject = property.objectReferenceValue;
            Node targetNode = null;
            Port targetPort = null;

            switch (referencedObject)
            {
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
                default:
                {
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

            if (targetNode == null || targetPort == null) return;
            var componentElement = sourceNode.GetComponentElement(component);
            var componentPort = sourceNode.GetReferenceOutputPort(component, property.propertyPath);

            if (componentPort == null) return;
            // Determine which port to use based on component element expansion state
            Port activeOutputPort;
            if (componentElement is { IsExpanded: false })
            {
                // Component is collapsed - use the collapsed port
                activeOutputPort = sourceNode.CollapsedReferenceOutputPort;
            }
            else if (componentPort.style.display == DisplayStyle.None)
            {
                // Port is hidden - use the collapsed port
                activeOutputPort = sourceNode.CollapsedReferenceOutputPort;
            }
            else
            {
                // Component is expanded - use the component's specific port
                activeOutputPort = componentPort;
            }

            var edge = new Edge
            {
                output = activeOutputPort,
                input = targetPort
            };

            // Properly connect the edge to the ports
            edge.output.Connect(edge);
            edge.input.Connect(edge);

            // Apply styling
            edge.AddToClassList(targetNode is AssetReferenceNode ? "asset-reference-edge" : "reference-edge");

            AddElement(edge);

            // Store edge data for tracking
            var edgeKey = $"{component.GetInstanceID()}_{property.propertyPath}";
            m_ReferenceEdgeData[edgeKey] = new ReferenceEdgeData
            {
                SourceComponent = component,
                PropertyPath = property.propertyPath,
                OriginalOutputPort = componentPort, // Always store the original port
                TargetNode = targetNode,
                TargetPort = targetPort,
                ComponentElement = componentElement
            };
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
                if (startPort == port || startPort.node == port.node || startPort.direction == port.direction)
                    return;

                // Special handling for hierarchy ports
                if (startPort.node is GameObjectGraphNode startGameObjectNode &&
                    port.node is GameObjectGraphNode targetGameObjectNode)
                {
                    // Check if these are hierarchy ports
                    bool isStartHierarchyPort = startPort == startGameObjectNode.HierarchyOutputPort ||
                                                startPort == startGameObjectNode.HierarchyInputPort;
                    bool isTargetHierarchyPort = port == targetGameObjectNode.HierarchyOutputPort ||
                                                 port == targetGameObjectNode.HierarchyInputPort;

                    if (isStartHierarchyPort && isTargetHierarchyPort)
                    {
                        // Only allow hierarchy connections between GameObjects
                        // Check for single capacity on input ports
                        if (port.capacity == Port.Capacity.Single && port.connected)
                        {
                            // For hierarchy input ports, we might want to allow replacement
                            // Disconnect the existing connection first
                            var existingEdge = port.connections.FirstOrDefault();
                            if (existingEdge != null)
                            {
                                schedule.Execute(() =>
                                {
                                    existingEdge.output?.Disconnect(existingEdge);
                                    existingEdge.input?.Disconnect(existingEdge);
                                    RemoveElement(existingEdge);
                                });
                            }
                        }

                        compatiblePorts.Add(port);
                        return;
                    }
                }

                // Check if target port already has a connection (for single capacity ports)
                if (port.capacity == Port.Capacity.Single && port.connected)
                {
                    return;
                }

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
                else if (!(startPort.node is GameObjectGraphNode) || !(port.node is GameObjectGraphNode))
                {
                    // Allow connections for non-GameObject nodes (like AssetReferenceNode)
                    compatiblePorts.Add(port);
                }
            });

            return compatiblePorts;
        }


        public void UpdateEdgeRoutingForNode(GameObjectGraphNode node)
        {
            var components = node.GameObject.GetComponents<Component>();

            foreach (var component in components)
            {
                var componentElement = node.GetComponentElement(component);
                if (componentElement != null)
                {
                    UpdateComponentEdgeRouting(node, componentElement, node.IsExpanded && componentElement.IsExpanded);
                }
            }
        }


        public void UpdateComponentEdgeRouting(GameObjectGraphNode node, ComponentElement componentElement,
            bool isExpanded)
        {
            var componentEdgeData = m_ReferenceEdgeData
                .Where(kvp => kvp.Value.ComponentElement == componentElement)
                .ToList();

            foreach (var kvp in componentEdgeData)
            {
                var edgeData = kvp.Value;
                var componentPort = edgeData.OriginalOutputPort;

                // Find and properly disconnect the current edge
                var currentEdge = graphElements.OfType<Edge>().FirstOrDefault(edge =>
                    edge.input == edgeData.TargetPort &&
                    (edge.output == componentPort || edge.output == node.CollapsedReferenceOutputPort));

                if (currentEdge != null)
                {
                    // Properly disconnect from ports before removing
                    currentEdge.output?.Disconnect(currentEdge);
                    currentEdge.input?.Disconnect(currentEdge);
                    RemoveElement(currentEdge);
                }

                // Create new edge with appropriate output port
                var newOutputPort = isExpanded ? componentPort : node.CollapsedReferenceOutputPort;

                var newEdge = new Edge
                {
                    output = newOutputPort,
                    input = edgeData.TargetPort
                };

                // Properly connect the new edge
                newEdge.output.Connect(newEdge);
                newEdge.input.Connect(newEdge);

                newEdge.AddToClassList(edgeData.TargetNode is AssetReferenceNode
                    ? "asset-reference-edge"
                    : "reference-edge");

                AddElement(newEdge);
            }
        }


        // Helper class to store original connection information
        public class ReferenceEdgeData
        {
            public Component SourceComponent;
            public string PropertyPath;
            public Port OriginalOutputPort;
            public Node TargetNode;
            public Port TargetPort;
            public ComponentElement ComponentElement;
        }
    }
}