using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _3DConnections.Runtime.ScriptableObjects;
using Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Object = UnityEngine.Object;

namespace _3DConnections.Runtime.Managers
{
    /// <summary>
    /// Manager class responsible for the construction of the node-graph
    /// </summary>
    public class NodeBuilder : MonoBehaviour
    {
        [Header("Node Configuration")] [SerializeField]
        private float nodeWidth = 2.0f;

        [SerializeField] private float nodeHeight = 1.0f;
        [SerializeField] private NodeColorsScriptableObject nodeColorsScriptableObject;

        private NodeConnectionManager _connectionManager;
        [SerializeField] private GameObject nodePrefab;

        private int _nodeCounter;
        private Camera _secondCamera;
        private GameObject _parentNode;
        [SerializeField] private NodeGraphScriptableObject nodegraph;

        [SerializeField] private ToAnalyzeSceneScriptableObject toAnalyzeSceneScriptableObject;
        [SerializeField] private OverlaySceneScriptableObject overlay;

        // new stuffs from relationanalyzer
        private HashSet<Object> processedObjects = new();

        [SerializeField] internal Color gameobjectColor = new(0.2f, 0.6f, 1f); // Blue
        [SerializeField] internal Color componentColor = new(0.4f, 0.8f, 0.4f); // Green
        [SerializeField] internal Color scriptableObjectColor = new(0.8f, 0.4f, 0.8f); // Purple
        [SerializeField] internal Color prefabColor = new(1f, 0.6f, 0.2f); // Orange
        [SerializeField] internal Color referenceColor = new(0.8f, 0.8f, 0.2f); // Yellow
        internal static Color ParentChildConnection = new(0.5f, 0.5f, 1f); // Light Blue
        internal static Color ComponentConnection = new(0.5f, 1f, 0.5f); // Light Green
        internal static Color ReferenceConnection = new(1f, 1f, 0.5f); // Light Yellow


        private void Start()
        {
            _connectionManager = GetComponent<NodeConnectionManager>();
            _parentNode = overlay.GetNodeGraph();
        }


        /// <summary>
        /// Spawn a new game object for the given node and add to the nodegraph 
        /// <b>Requires</b> a second camera to be active with an existing and enabled overlayedScene :)
        /// </summary>
        /// <param name="node"></param>
        private void SpawnNodeOnOverlay(Node node)
        {
            if (!overlay.GetCameraOfScene())
            {
                Debug.Log("no camera while trying to spawn a node in NodeBuilder");
                return;
            }

            if (!_parentNode)
            {
                _parentNode = overlay.GetNodeGraph();
                if (!_parentNode)
                {
                    Debug.Log("In SpawnTestNodeOnSecondDisplay node graph game object was not found");
                }
            }

            var nodeObject = Instantiate(nodePrefab, _parentNode.transform);
            nodeObject.transform.position = node.Position;
            nodeObject.transform.localScale = new Vector3(node.Width, node.Height, 1f);
            nodeObject.name = node.Name;
            nodeObject.layer = LayerMask.NameToLayer("OverlayScene");
            node.RelatedGameObject = nodeObject.gameObject;
            var componentRenderer = nodeObject.GetComponent<Renderer>();
            if (componentRenderer)
                componentRenderer.material.color = nodeColorsScriptableObject.nodeDefaultColor;
            // var textComponent = nodeObject.GetComponentInChildren<TextMeshProUGUI>();
            // if (textComponent)
            //     textComponent.text = $"Node {_nodeCounter}";
            if (nodegraph.Add(node)) return;
            if (nodegraph.ReplaceRelatedGo(node))
                Debug.Log("no successful Add nor successful Replace in nodegraph with node" + node);
            else
                Debug.Log("replaced ");
        }

        /// <summary>
        /// Create a list of nodes that represent scripts that correspond to all scripts in the given location
        /// </summary>
        /// <param name="path">Location in which to look for scripts to display</param>
        /// <param name="allReferences">Filled out string to string ClassReferences for the given path</param>
        /// <returns>List of nodes that were created for scripts in the given path</returns>
        private static List<Node> FindScriptNodes(string path, out Dictionary<string, ClassReferences> allReferences)
        {
            List<Node> nodes = new();
            allReferences = ClassParser.GetAllClassReferencesParallel(path);

            foreach (var (scriptName, _) in allReferences)
            {
                var node = new GameObjectNode(scriptName, null);
                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>
        /// Calculate Node Connections using InheritanceReferences, FieldReferences and MethodReferences
        /// </summary>
        /// <param name="nodes">List of nodes to analyze</param>
        /// <param name="allReferences">Lookup for all connections using string lookup</param>
        /// <returns></returns>
        private static Dictionary<Node, HashSet<Node>> CalculateNodeConnections(
            List<Node> nodes,
            Dictionary<string, ClassReferences> allReferences)
        {
            // Create a lookup dictionary to quickly find nodes by their script name
            var nodesByScriptName = new Dictionary<string, Node>();
            foreach (var node in nodes)
            {
                nodesByScriptName.TryAdd(node.Name, node);
            }

            // Dictionary to store node connections
            var nodeConnections = new Dictionary<Node, HashSet<Node>>();

            foreach (var (scriptName, classReferences) in allReferences)
            {
                // Skip if the current script doesn't have a corresponding node
                if (!nodesByScriptName.TryGetValue(scriptName, out var currentNode))
                    continue;

                // Ensure we have an entry for this node in the connection dictionary
                if (!nodeConnections.ContainsKey(currentNode))
                {
                    nodeConnections[currentNode] = new HashSet<Node>();
                }

                // Find connections to other nodes
                foreach (var referencedScript in classReferences.References)
                {
                    // Avoid self-references and ensure the referenced script exists as a node
                    if (referencedScript != scriptName &&
                        nodesByScriptName.TryGetValue(referencedScript, out var referencedNode))
                    {
                        nodeConnections[currentNode].Add(referencedNode);
                    }
                }
            }

            return nodeConnections;
        }

        private void DrawNodeConnections(
            Dictionary<Node, HashSet<Node>> nodeConnections)
        {
            foreach (var (sourceNode, connectedNodes) in nodeConnections)
            {
                foreach (var targetNode in connectedNodes)
                {
                    _connectionManager.AddConnection(sourceNode.RelatedGameObject, targetNode.RelatedGameObject, Color.HSVToRGB(0, 0.5f, .9f));
                }
            }
        }

        internal void Clear()
        {
            // clear node objects
            _parentNode = overlay.GetNodeGraph();
            if (_parentNode)
            {
                foreach (Transform child in _parentNode.transform)
                {
                    if (child && child.parent)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }

            nodegraph.Clear();
            processedObjects.Clear();

            // clear connections
            _connectionManager.ClearConnections();

            // clear text
            var textManager = GetComponent<NodeTextOverlay>();
            if (textManager)
            {
                textManager.ClearText();
            }
        }

        private Transform[] GetSceneRootTransforms()
        {
            if (toAnalyzeSceneScriptableObject == null)
            {
                Debug.Log("to analyze scene config is empty");
                return Array.Empty<Transform>();
            }

            if (toAnalyzeSceneScriptableObject.scene == null)
            {
                Debug.Log("scene of to analyze scene config is empty");
                return Array.Empty<Transform>();
            }

            if (!toAnalyzeSceneScriptableObject.scene.scene.IsValid())
            {
                Debug.Log("selected scene is invalid probably because it is not loaded");
                return Array.Empty<Transform>();
            }

            var scene = toAnalyzeSceneScriptableObject.scene.scene;
            return scene.GetRootGameObjects()
                .Select(go => go.transform)
                .ToArray();
        }

        /// <summary>
        /// Create Tree structure using given transforms
        /// </summary>
        private List<Node> ConstructNodesWithChildrenUsingTransforms(Transform[] rootTransforms)
        {
            // Newly created Node objects list
            var rootNodes = new List<Node>();

            // Kind of flags for "not yet analyzed node"
            var toExploreNodes = new List<Node>();

            foreach (var rootTransform in rootTransforms)
            {
                // Create new Node for each initially given Transform
                var rootNode = new GameObjectNode(rootTransform, null);
                rootNodes.Add(rootNode);

                nodegraph.Add(rootNode);
                if (!toExploreNodes.Contains(rootNode))
                {
                    toExploreNodes.Add(rootNode);
                }

                while (toExploreNodes.Count > 0)
                {
                    var currentNodes = toExploreNodes.ToList();
                    foreach (var node in currentNodes)
                    {
                        if (node.RelatedGameObject.transform.childCount > 0)
                        {
                            var children = new List<Node>();
                            foreach (Transform child in node.RelatedGameObject.transform)
                            {
                                // only create new Node object if this not already present
                                Node childrenNode;
                                if (nodegraph.Contains(child.gameObject))
                                {
                                    childrenNode = nodegraph.GetRelations()[child.gameObject];
                                    if (!children.Contains(childrenNode))
                                    {
                                        children.Add(childrenNode);
                                    }

                                    if (!toExploreNodes.Contains(childrenNode))
                                    {
                                        toExploreNodes.Add(childrenNode);
                                    }

                                    continue;
                                }

                                // TODO: go might be known here
                                childrenNode = new GameObjectNode(child.gameObject.name, null)
                                {
                                    RelatedGameObject = child.gameObject
                                };
                                nodegraph.Add(childrenNode);
                                children.Add(childrenNode);
                                rootNode.RelatedGameObject = child.gameObject;
                                nodegraph.ReplaceRelatedGo(rootNode);
                                if (!toExploreNodes.Contains(childrenNode))
                                {
                                    toExploreNodes.Add(childrenNode);
                                }
                            }

                            node.Children = children;
                        }

                        toExploreNodes.Remove(node);
                    }
                }
            }

            return rootNodes;
        }

        private void SpawnTreeFromNode(Node rootNode)
        {
            TreeLayout.LayoutTree(rootNode);
            SpawnNodesRecursive(rootNode);
            TreeLayout.LayoutTree(rootNode);
        }

        private readonly Dictionary<Node, GameObject> _spawnedNodes = new();


        /// <summary>
        /// Spawn the entire tree until leaf nodes. Also works for cyclic graphs
        /// </summary>
        /// <param name="node"></param>
        private void SpawnNodesRecursive(Node node)
        {
            if (!_spawnedNodes.TryGetValue(node, out var currentNodeObject))
            {
                SpawnNodeOnOverlay(node);
                _spawnedNodes[node] = node.RelatedGameObject;
            }

            if (node.Children is not { Count: > 0 }) return;

            foreach (var child in node.Children)
            {
                if (!_spawnedNodes.TryGetValue(child, out var childObject))
                {
                    SpawnNodeOnOverlay(child);
                    _spawnedNodes[child] = child.RelatedGameObject;
                }

                _connectionManager.AddConnection(currentNodeObject, childObject, Color.HSVToRGB(0, 0.5f, 0.9f));
                SpawnNodesRecursive(child);
            }
        }


        /// <summary>
        /// Since a scene does have multiple root objects, define a single entry root node. Required for Tree node spawning
        /// </summary>
        /// <returns></returns>
        private Node DefineRootNode(List<Node> rootNodes)
        {
            return new GameObjectNode("ROOT", null)
            {
                Children = rootNodes
            };
        }

        internal void DrawGrid(string[] paths = null)
        {
            var overlayedScene = SceneHandler.GetOverlayedScene();
            if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);
            _secondCamera = overlay.GetCameraOfScene();
            if (!_secondCamera)
                return;

            if (paths!.Length > 0)
            {
                var scriptNodes = FindScriptNodes(paths[0], out var allReferences);
                NodeLayoutManagerV2.GridLayout(scriptNodes);

                foreach (var scriptNode in scriptNodes)
                {
                    SpawnNodeOnOverlay(scriptNode);
                }

                var connections = CalculateNodeConnections(scriptNodes, allReferences);
                DrawNodeConnections(connections);
            }
            else
            {
                Debug.Log("Path is empty");
            }
        }

        // using Children attribute of nodes
        internal void DrawTree()
        {
            Debug.Log("Starting to draw tree for scene " + toAnalyzeSceneScriptableObject.scene.Name);

            Clear();
            var overlayedScene = SceneHandler.GetOverlayedScene();
            if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);
            _secondCamera = overlay.GetCameraOfScene();

            // Find Root nodes in the scene
            var rootTransforms = GetSceneRootTransforms();

            // Populating Children
            var rootNodes = ConstructNodesWithChildrenUsingTransforms(rootTransforms);
            SpawnTreeFromNode(DefineRootNode(rootNodes));
        }

        private void ProcessUnityObject(Object obj, Node parentNode)
        {
            if (nodegraph.Contains(obj)) return;
            Debug.Log("processing unity object");
            var referenceNode = obj switch
            {
                ScriptableObject so => ScriptableObjectNode.GetOrCreateNode(so, nodegraph),
                GameObject go => GameObjectNode.GetOrCreateNode(go, nodegraph),
                Component co => ComponentNode.GetOrCreateNode(co, nodegraph),
                _ => null
            };

            if (referenceNode == null) return;
            parentNode.Children.Add(referenceNode);
            // Recursively analyze the referenced object
            AnalyzeSerializedFields(obj, referenceNode);
        }

        private void AnalyzeSerializedFields(Object obj, Node parentNode)
        {
            if (obj == null || !processedObjects.Add(obj)) return;
            Debug.Log("analyzing serialized fields");
            var type = obj.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.IsDefined(typeof(SerializeField), true) || field.IsPublic);

            foreach (var field in fields)
            {
                var value = field.GetValue(obj);
                switch (value)
                {
                    case null:
                        continue;
                    // Handle Unity Object types
                    case Object unityObj:
                        ProcessUnityObject(unityObj, parentNode);
                        break;
                    // Handle arrays and lists of Unity Objects
                    case IEnumerable<Object> objectList:
                    {
                        foreach (var listItem in objectList)
                        {
                            if (listItem != null)
                            {
                                ProcessUnityObject(listItem, parentNode);
                            }
                        }

                        break;
                    }
                    default:
                    {
                        if (value.GetType().IsArray && value.GetType().GetElementType()!.IsSubclassOf(typeof(Object)))
                        {
                            var array = (Object[])value;
                            foreach (var item in array)
                            {
                                if (item != null)
                                {
                                    ProcessUnityObject(item, parentNode);
                                }
                            }
                        }

                        break;
                    }
                }
            }
        }

        private void AnalyzeComponents(GameObject go, Node parentNode)
        {
            var components = go.GetComponents<Component>();

            foreach (var component in components)
            {
                if (component == null) continue;
                var componentNode = ComponentNode.GetOrCreateNode(component, nodegraph);
                parentNode.Children.Add(componentNode);

                // Analyze serialized fields
                AnalyzeSerializedFields(component, componentNode);
            }
        }

        private Node CreateNodeHierarchy(Transform tf, Node parentNode)
        {
            // Create or get existing node for GameObject
            var node = GameObjectNode.GetOrCreateNode(tf.gameObject, nodegraph);

            parentNode?.Children.Add(node);

            // Process components
            AnalyzeComponents(tf.gameObject, node);

            // Process children
            foreach (Transform child in tf)
            {
                CreateNodeHierarchy(child, node);
            }

            // Position and spawn the node
            SpawnNodeOnOverlay(node);
            return node;
        }

        private void DrawNodeConnections(Node node)
        {
            if (node.Children == null || node.RelatedGameObject == null) return;

            foreach (var childNode in node.Children)
            {
                if (childNode.RelatedGameObject == null) continue;
                var connectionColor = node.GetConnectionColor(childNode);
                _connectionManager.AddConnection(node.RelatedGameObject, childNode.RelatedGameObject, connectionColor);
            }
        }

        public void AnalyzeScene()
        {
            // 1. Clean and setup overlay parameters
            Clear();
            var overlayedScene = SceneHandler.GetOverlayedScene();
            if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);
            _secondCamera = overlay.GetCameraOfScene();

            // 2a. Collect all GameObjects in the scene
            var serializedScene = SceneSerializer.SerializeSceneHierarchy(overlayedScene);
            foreach (var serializedGameObject in serializedScene.Select(go => new GameObjectNode(go.name, go)))
            {
                SpawnNodeOnOverlay(serializedGameObject);
            }

            // 2b. Create Transform Hierarchy from GameObjectNodes
            nodegraph.FillChildrenForGameObjectNodes();
            var rootNode = nodegraph.GetRootNode(toAnalyzeSceneScriptableObject.scene.scene.GetRootGameObjects());
            TreeLayout.LayoutTree(rootNode);

            // 3. Add parent-child gamenode connections
            foreach (var node in rootNode.Children)
            {
                foreach (var child in node.Children)
                {
                    _connectionManager.AddConnection(node.RelatedGameObject, child.RelatedGameObject, ParentChildConnection);
                }
            }

            // 4. Finally, move all nodes where they belong
            nodegraph.ApplyNodePositions();
        }

        private void OnDestroy()
        {
            Clear();
        }
    }

    public static class NodeExtensions
    {
        public static Color GetConnectionColor(this Node parentNode, Node childNode)
        {
            if (childNode.RelatedGameObject != null &&
                parentNode.RelatedGameObject != null &&
                childNode.RelatedGameObject.transform.parent == parentNode.RelatedGameObject.transform)
            {
                return NodeBuilder.ParentChildConnection;
            }

            if (childNode.RelatedGameObject != null &&
                childNode.RelatedGameObject.GetComponents<Component>().Any(c => c.gameObject == parentNode.RelatedGameObject))
            {
                return NodeBuilder.ComponentConnection;
            }

            return NodeBuilder.ReferenceConnection;
        }
    }
}