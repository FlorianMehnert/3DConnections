using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _3DConnections.Runtime.ScriptableObjects;
using Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        [SerializeField] internal Color parentChildConnection = new(0.5f, 0.5f, 1f); // Light Blue
        [SerializeField] internal Color componentConnection = new(0.5f, 1f, 0.5f); // Light Green
        [SerializeField] internal Color referenceConnection = new(1f, 0f, 0.5f); // Light Yellow


        private void Start()
        {
            _connectionManager = GetComponent<NodeConnectionManager>();
            _parentNode = overlay.GetNodeGraph();
        }


        /// <summary>
        /// Spawn a new game object for the given node, sets its relatedGameObject and add to the nodegraph 
        /// <b>Requires</b> a second camera to be active with an existing and enabled overlayedScene :)
        /// </summary>
        /// <param name="node"></param>
        /// <param name="color">Color of the node</param>
        private void SpawnNodeOnOverlay(Node node, Color color)
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
            if (node == null) return;

            var nodeObject = Instantiate(nodePrefab, _parentNode.transform);
            nodeObject.transform.localPosition = new Vector3(node.X, node.Y, 0);
            nodeObject.transform.localScale = new Vector3(node.Width, node.Height, 1f);
            nodeObject.name = node.Name;
            nodeObject.layer = LayerMask.NameToLayer("OverlayScene");
            node.RelatedGameObject = nodeObject.gameObject;
            var componentRenderer = nodeObject.GetComponent<Renderer>();
            if (componentRenderer)
                componentRenderer.material.color = color;
            if (nodegraph.Add(node)) return;
            if (nodegraph.ReplaceRelatedGo(node))
                Debug.Log("no successful Add nor successful Replace in nodegraph with node" + node);
            else
                Debug.Log("replaced ");if (nodegraph.Add(node)) return;
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

            if (toAnalyzeSceneScriptableObject.reference == null)
            {
                Debug.Log("scene of to analyze scene config is empty");
                return Array.Empty<Transform>();
            }

            if (!toAnalyzeSceneScriptableObject.reference.scene.IsValid())
            {
                Debug.Log("selected scene is invalid probably because it is not loaded");
                return Array.Empty<Transform>();
            }

            var scene = toAnalyzeSceneScriptableObject.reference.scene;
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

                            node.SetChildren(children);
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
                SpawnNodeOnOverlay(node, gameobjectColor);
                _spawnedNodes[node] = node.RelatedGameObject;
            }

            if (node.GetChildren() is not { Count: > 0 }) return;

            foreach (var child in node.GetChildren())
            {
                if (!_spawnedNodes.TryGetValue(child, out var childObject))
                {
                    SpawnNodeOnOverlay(child, gameobjectColor);
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
        private static Node DefineRootNode(List<Node> rootNodes)
        {
            var root = new GameObjectNode("ROOT", null);
            foreach (var node in rootNodes)
            {
                root.AddChild(node);
            }    
            return root;
        }

        internal void DrawGrid(string[] paths = null)
        {
            var overlayedScene = SceneHandler.GetOverlayedScene();
            if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);

            if (paths!.Length > 0)
            {
                var scriptNodes = FindScriptNodes(paths[0], out var allReferences);
                NodeLayoutManagerV2.GridLayout(scriptNodes);

                foreach (var scriptNode in scriptNodes)
                {
                    SpawnNodeOnOverlay(scriptNode, gameobjectColor);
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
            Debug.Log("Starting to draw tree for scene " + toAnalyzeSceneScriptableObject.reference.Name);

            Clear();
            var overlayedScene = SceneHandler.GetOverlayedScene();
            if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);

            // Find Root nodes in the scene
            var rootTransforms = GetSceneRootTransforms();

            // Populating Children
            var rootNodes = ConstructNodesWithChildrenUsingTransforms(rootTransforms);
            SpawnTreeFromNode(DefineRootNode(rootNodes));
        }

        private void ProcessUnityObject(Object reference, ComponentNode componentNode)
        {
            if (nodegraph.Contains(reference)) return;
            var referenceNode = reference switch
            {
                ScriptableObject so => ScriptableObjectNode.GetOrCreateNode(so, nodegraph),
                GameObject go => GameObjectNode.GetOrCreateNode(go, nodegraph),
                Component co => ComponentNode.GetOrCreateNode(co, nodegraph),
                _ => null
            };
            if (referenceNode == null) return;
            SpawnNodeOnOverlay(referenceNode, gameobjectColor);
            _connectionManager.AddConnection(componentNode.RelatedGameObject, referenceNode.RelatedGameObject, referenceConnection);
        }

        /// <summary>
        /// Wrapper function for <see cref="ProcessUnityObject"/> called by <see cref="AnalyzeComponents"/> 
        /// </summary>
        /// <param name="obj">A serialized field reference within the current component</param>
        /// <param name="componentNode">node that represents the component to which this reference will be attached to</param>
        private void AnalyzeSerializedFields(Object obj, ComponentNode componentNode)
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
                        ProcessUnityObject(unityObj, componentNode);
                        break;
                    // Handle arrays and lists of Unity Objects
                    case IEnumerable<Object> objectList:
                    {
                        foreach (var listItem in objectList)
                        {
                            if (listItem != null)
                            {
                                ProcessUnityObject(listItem, componentNode);
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
                                    ProcessUnityObject(item, componentNode);
                                }
                            }
                        }

                        break;
                    }
                }
            }
        }

        private void AnalyzeComponents(GameObjectNode gameObjectNode)
        {
            var components = gameObjectNode.GameObject.GetComponents(typeof(Component));
            
            foreach (var component in components)
            {
                Debug.Log("component name: " + component.GetType().Name);
                var node = ComponentNode.GetOrCreateNode(component, nodegraph);
                SpawnNodeOnOverlay(node, componentColor);
                _connectionManager.AddConnection(gameObjectNode.RelatedGameObject, node.RelatedGameObject, color: componentConnection);
                
                // Analyze serialized fields
                AnalyzeSerializedFields(component, node);
                
            }
        }

        public void AnalyzeScene()
        {
            // 1. Clean and setup overlay parameters
            Clear();
            SceneManager.SetActiveScene(overlay.overlayScene.scene);

            // 2a. Collect all GameObjects in the scene
            var serializedScene = SceneSerializer.SerializeSceneHierarchy(toAnalyzeSceneScriptableObject.reference.scene);
            foreach (var gameObjectNode in serializedScene.Select(go => new GameObjectNode(go.name, go)))
            {
                SpawnNodeOnOverlay(gameObjectNode, gameobjectColor);
                AnalyzeComponents(gameObjectNode);
            }

            // 2b. Create Transform Hierarchy from GameObjectNodes
            var rootNode = nodegraph.GetRootNode(toAnalyzeSceneScriptableObject.reference.scene.GetRootGameObjects());
            SpawnNodeOnOverlay(rootNode, gameobjectColor);
            nodegraph.FillChildrenForGameObjectNodes();
            // TreeLayout.LayoutTree(rootNode);
            // RadialLayout.LayoutChildrenRadially(rootNode, 0);

            // 3. Add parent-child gamenode connections TODO: draw all the connections
            foreach (var node in rootNode.GetChildren())
            {
                foreach (var child in node.GetChildren())
                {
                    _connectionManager.AddConnection(node.RelatedGameObject, child.RelatedGameObject, parentChildConnection);
                }
            }

            var rootNodes = ConnectionsBasedForestManager.BuildForest(_connectionManager.connections);
            var forestManager = new ConnectionsBasedForestManager();
            forestManager.SetLayoutParameters(
                minDistance: 2f,    // Minimum distance between nodes
                startRadius: 3f,    // Initial radius for first level
                radiusInc: 4f,      // Radius increase per level
                rootSpacing: 10f    // Space between root trees
            );
            forestManager.LayoutForest(rootNodes);
            forestManager.FlattenToZPlane(rootNodes);

            // 4. Finally, move all nodes where they belong
            // nodegraph.ApplyNodePositions();
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}