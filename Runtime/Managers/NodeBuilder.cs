using System;
using System.Collections.Generic;
using System.Linq;
using _3DConnections.Runtime.ScriptableObjects;
using Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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
        private GameObject _nodeGraph;
        [SerializeField] private NodeGraphScriptableObject nodeGraphScriptableObject;

        [SerializeField] private ToAnalyzeSceneScriptableObject toAnalyzeSceneScriptableObject;
        [SerializeField] private OverlaySceneScriptableObject overlay;


        private void Start()
        {
            _connectionManager = GetComponent<NodeConnectionManager>();
            _nodeGraph = overlay.GetNodeGraph();
        }


        /// <summary>
        /// Spawn a single node on the display
        /// <b>Requires</b> a second camera to be active with an existing and enabled overlayedScene :)
        /// </summary>
        /// <param name="spawnPosition">Position of the node in worldSpace: Please invoke for this one <see cref="GetNodePositionRelativeToCamera"/></param>
        /// <param name="nodeExtend">Node dimension</param>
        private GameObject SpawnTestNodeOnSecondDisplay(Vector3 spawnPosition, Vector3 nodeExtend, string nodeName = "Node")
        {
            // spawn node prefab as child of this element
            if (!_nodeGraph)
            {
                _nodeGraph = overlay.GetNodeGraph();
                if (!_nodeGraph)
                {
                    Debug.Log("In SpawnTestNodeOnSecondDisplay node graph game object was not found");
                }
            }

            var nodeObject = Instantiate(nodePrefab, _nodeGraph.transform);

            // set name allowing to differentiate between them 
            nodeObject.transform.position = spawnPosition;
            nodeObject.transform.localScale = nodeExtend;
            nodeObject.name = nodeName;
            nodeObject.layer = LayerMask.NameToLayer("OverlayScene");
            // nodeObject.AddComponent<NodeTextOverlay>();
            ConfigureNode(nodeObject);
            return nodeObject;
        }

        /// <summary>
        /// Takes a node and executes <see cref="SpawnTestNodeOnSecondDisplay"/>
        /// </summary>
        /// <param name="node"></param>
        private GameObject SpawnCubeNodeUsingNodeObject(Node node)
        {
            return !overlay.GetCameraOfScene() ? null : SpawnTestNodeOnSecondDisplay(GetNodePositionRelativeToCamera(overlay.GetCameraOfScene().transform.position, new Vector3(node.X, node.Y, 0)), new Vector3(nodeWidth, nodeHeight, 1f), node.name);
        }

        /// <summary>
        /// Based on the camera position, determine the node position so nodes will be spawned in front of the camera
        /// </summary>
        /// <param name="secondDisplayCameraPosition">Vector3 that should be the transform position of the camera</param>
        /// <param name="spawnPosition">Spawn Position in the camera frame</param>
        /// <returns></returns>
        private static Vector3 GetNodePositionRelativeToCamera(Vector3 secondDisplayCameraPosition, Vector3 spawnPosition)
        {
            return secondDisplayCameraPosition
                   + Vector3.forward * 5f
                   + spawnPosition;
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
                var node = new Node(scriptName);
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
                nodesByScriptName.TryAdd(node.name, node);
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
                    _connectionManager.AddConnection(sourceNode.relatedGameObject, targetNode.relatedGameObject, Color.HSVToRGB(0, 0.5f, .9f));
                }
            }
        }


        /// <summary>
        /// Set node color, 
        /// </summary>
        /// <param name="nodeObject"></param>
        private void ConfigureNode(GameObject nodeObject)
        {
            var componentRenderer = nodeObject.GetComponent<Renderer>();
            if (componentRenderer)
            {
                componentRenderer.material.color = nodeColorsScriptableObject.nodeDefaultColor;
            }

            // Set up text component
            var textComponent = nodeObject.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent)
            {
                textComponent.text = $"Node {_nodeCounter}";
            }
        }

        internal void Clear()
        {
            // clear node objects
            _nodeGraph = overlay.GetNodeGraph();
            if (_nodeGraph)
            {
                foreach (Transform child in _nodeGraph.transform)
                {
                    if (child && child.parent)
                    {
                        Destroy(child.gameObject);
                    }
                }
            }
            nodeGraphScriptableObject.Clear();

            // clear connections
            _connectionManager.ClearConnections();

            // clear text
            var textManager = GetComponent<NodeTextOverlay>();
            if (textManager)
            {
                textManager.ClearText();
            }
        }

        private Transform[] GetSceneRootObjects()
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
                var rootNode = new Node(rootTransform);
                rootNodes.Add(rootNode);

                nodeGraphScriptableObject.Add(rootNode);
                if (!toExploreNodes.Contains(rootNode))
                {
                    toExploreNodes.Add(rootNode);
                }

                while (toExploreNodes.Count > 0)
                {
                    var currentNodes = toExploreNodes.ToList();
                    foreach (var node in currentNodes)
                    {
                        if (node.relatedGameObject.transform.childCount > 0)
                        {
                            var children = new List<Node>();
                            foreach (Transform child in node.relatedGameObject.transform)
                            {
                                // only create new Node object if this not already present
                                Node childrenNode;
                                if (nodeGraphScriptableObject.Contains(child.gameObject))
                                {
                                    childrenNode = nodeGraphScriptableObject.GetRelations()[child.gameObject];
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
                                childrenNode = new Node(child.gameObject.name)
                                {
                                    relatedGameObject = child.gameObject
                                };
                                nodeGraphScriptableObject.Add(childrenNode);
                                children.Add(childrenNode);
                                rootNode.relatedGameObject = child.gameObject;
                                nodeGraphScriptableObject.Replace(rootNode);
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
                currentNodeObject = SpawnCubeNodeUsingNodeObject(node);
                _spawnedNodes[node] = currentNodeObject;
            }

            if (node.Children is not { Count: > 0 }) return;

            foreach (var child in node.Children)
            {
                if (!_spawnedNodes.TryGetValue(child, out var childObject))
                {
                    childObject = SpawnCubeNodeUsingNodeObject(child);
                    _spawnedNodes[child] = childObject;
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
            return new Node("ROOT")
            {
                Color = nodeColorsScriptableObject.nodeRootColor,
                Children = rootNodes
            };
        }

        internal void DrawGrid(string[] paths = null)
        {
            var overlayedScene = SceneHandler.GetOverlayedScene();
            if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);
            _secondCamera = overlay.GetCameraOfScene();
            if (!_secondCamera)
                return ;

            if (paths!.Length > 0)
            {
                var scriptNodes = FindScriptNodes(paths[0], out var allReferences);
                NodeLayoutManagerV2.GridLayout(scriptNodes);
                    
                foreach (var scriptNode in scriptNodes)
                {
                    var nodeGameObject = SpawnCubeNodeUsingNodeObject(scriptNode);
                    scriptNode.relatedGameObject = nodeGameObject;
                    nodeGraphScriptableObject.Add(scriptNode);
                }

                var connections = CalculateNodeConnections(scriptNodes, allReferences);
                DrawNodeConnections(connections);
            }
            else
            {
                Debug.Log("Path is empty");
            }
        }

        internal void DrawTree()
        {
            Debug.Log("Starting to draw tree for scene " + toAnalyzeSceneScriptableObject.scene.Name);
            
            Clear();
            var overlayedScene = SceneHandler.GetOverlayedScene();
            if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);
            _secondCamera = overlay.GetCameraOfScene();
            // Find Root nodes in scene
            var rootTransforms = GetSceneRootObjects();
            var rootNodes = ConstructNodesWithChildrenUsingTransforms(rootTransforms);
            SpawnTreeFromNode(DefineRootNode(rootNodes));
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}