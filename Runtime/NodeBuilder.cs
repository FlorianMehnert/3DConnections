using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.VisualScripting;

namespace _3DConnections.Runtime
{
    /// <summary>
    /// Manager class responsible for the construction of the node-graph
    /// </summary>
    public class NodeBuilder : MonoBehaviour
    {
        private readonly Dictionary<Node, GameObject> _relatedGameObjects = new();
        private Dictionary<string, ClassReferences> _allReferences = new();

        [Header("Node Configuration")] [SerializeField]
        private float nodeWidth = 2.0f;

        [SerializeField] private float nodeHeight = 1.0f;
        [SerializeField] private NodeColorsScriptableObject nodeColorsScriptableObject;

        private NodeConnectionManager _connectionManager;
        [SerializeField] private GameObject nodePrefab;

        private int _nodeCounter;
        private Camera _secondCamera;
        private GameObject _nodeGraph;

        private List<Node> _nodes = new();
        private readonly List<GameObject> _rootGameObjects = new();
        private readonly List<Node> _rootNodes = new();
        
        private const float HorizontalSpacing = 3f; // Space between nodes horizontally
        private const float VerticalSpacing = 2f;   // Space between levels vertically


        private void Start()
        {
            _connectionManager = GetComponent<NodeConnectionManager>();
            _nodeGraph = SceneHandler.GetNodeGraph("New Scene");
        }


        /// <summary>
        /// Spawn a single node on the display
        /// <b>Requires</b> a second camera to be active with an existing and enabled overlayedScene :)
        /// </summary>
        /// <param name="spawnPosition">Position of the node in worldSpace: Please invoke for this one <see cref="GetNodePositionRelativeToCamera"/></param>
        /// <param name="nodeExtend">Node dimension</param>
        private GameObject SpawnTestNodeOnSecondDisplay(Vector3 spawnPosition, Vector3 nodeExtend, string nodeName="Node")
        {
            // spawn node prefab as child of this element
            if (!_nodeGraph)
            {
                _nodeGraph = SceneHandler.GetNodeGraph("NewScene");
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
            return !_secondCamera ? null : SpawnTestNodeOnSecondDisplay(GetNodePositionRelativeToCamera(_secondCamera.transform.position, new Vector3(node.X, node.Y, 0)), new Vector3(nodeWidth, nodeHeight, 1f), node.name);
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
        /// <returns>List of nodes that were created for scripts in the given path</returns>
        private List<Node> FindScriptNodes(string path)
        {
            List<Node> nodes = new();
            _allReferences = ClassParser.GetAllClassReferencesParallel(path);

            foreach (var (scriptName, _) in _allReferences)
            {
                var node = new Node(scriptName, 0, 0, nodeWidth, nodeHeight);
                nodes.Add(node);
            }

            return nodes;
        }

        private static Dictionary<Node, HashSet<Node>> CalculateNodeConnections(
            List<Node> nodes,
            Dictionary<string, ClassReferences> allReferences)
        {
            // Create a lookup dictionary to quickly find nodes by their script name
            var nodesByScriptName = nodes
                .ToDictionary(
                    node => node.name,
                    node => node
                );

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
            Dictionary<Node, HashSet<Node>> nodeConnections,
            Dictionary<Node, GameObject> nodeToGameObjectMap)
        {
            foreach (var (sourceNode, connectedNodes) in nodeConnections)
            {
                // Skip if we can't find the source GameObject
                if (!nodeToGameObjectMap.TryGetValue(sourceNode, out var sourceGameObject))
                    continue;

                foreach (var targetNode in connectedNodes)
                {
                    // Skip if we can't find the target GameObject
                    if (!nodeToGameObjectMap.TryGetValue(targetNode, out GameObject targetGameObject))
                        continue;

                    // Draw connection between the two GameObjects
                    _connectionManager.AddConnection(sourceGameObject, targetGameObject, Color.HSVToRGB(0, 0.5f, .9f));
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

        private void Clear()
        {
            // clear node objects
            _nodeGraph = SceneHandler.GetNodeGraph("NewScene");
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

            _relatedGameObjects.Clear();
            _nodes.Clear();
            _rootGameObjects.Clear();
            _rootNodes.Clear();

            // clear connections
            _connectionManager.ClearConnections();

            // clear text
            var textManager = GetComponent<NodeTextOverlay>();
            if (textManager)
            {
                textManager.ClearText();
            }
        }

        /// <summary>
        /// Builds a parent-child tree for a given scene
        /// </summary>
        private void BuildTree()
        {
            var toExploreNodes = new List<Node>();
            var rootTransforms = SceneHandler.GetSceneRootObjects("SampleScene");
            foreach (var rootTransform in rootTransforms)
            {
                _rootGameObjects.Add(rootTransform.gameObject);
                var newNode = new Node(rootTransform.name)
                {
                    relatedGameObject = rootTransform.gameObject
                };
                _rootNodes.Add(newNode);

                _nodes.Add(newNode);
                _relatedGameObjects[newNode] = rootTransform.gameObject;
                if (!toExploreNodes.Contains(newNode))
                {
                    toExploreNodes.Add(newNode);
                }

                while (toExploreNodes.Count > 0)
                {
                    var currentNodes = toExploreNodes.ToList();
                    foreach (var node in currentNodes)
                    {
                        if (node.relatedGameObject.transform.childCount > 0)
                        {
                            foreach (Transform child in node.relatedGameObject.transform)
                            {
                                // only create new Node object if this not already present
                                if (!_relatedGameObjects.ContainsValue(child.gameObject))
                                {
                                    var childrenNode = new Node(child.gameObject.name)
                                    {
                                        relatedGameObject = child.gameObject
                                    };
                                    if (!_nodes.Contains(childrenNode))
                                    {
                                        _nodes.Add(newNode);
                                    }

                                    _relatedGameObjects[newNode] = child.gameObject;
                                    if (!toExploreNodes.Contains(childrenNode))
                                    {
                                        toExploreNodes.Add(childrenNode);
                                    }

                                    node.Children.Add(childrenNode);
                                }
                            }
                        }

                        toExploreNodes.Remove(node);
                    }
                }
            }
        }

        private void SpawnTreeFromNode(Node rootNode)
        {
            TreeLayout.LayoutTree(rootNode);
            SpawnNodesRecursive(rootNode);
            TreeLayout.LayoutTree(rootNode);
        }

        private void SpawnNodesRecursive(Node node)
        {
            // Spawn current node
            Debug.Log("spawned " + node.name + " at " + node.X + " " + node.Y);
            var currentNodeObject = SpawnCubeNodeUsingNodeObject(node);
            // Process children
            if (node.Children is not { Count: > 0 }) return;
            foreach (var child in node.Children)
            {
                // Spawn child and create connection
                var childObject = SpawnCubeNodeUsingNodeObject(child);
                _connectionManager.AddConnection(currentNodeObject, childObject, Color.HSVToRGB(0, 0.5f, 0.9f));
                    
                // Recursively process child's children
                SpawnNodesRecursive(child);
            }
        }

        /// <summary>
        /// Since a scene does have multiple root objects, define a single entry root node. Required for Tree node spawning
        /// </summary>
        /// <returns></returns>
        private Node DefineRootNode()
        {
            
            return new Node("ROOT")
            {
                Color = nodeColorsScriptableObject.nodeRootColor,
                Children = _rootNodes
            };
        }

        public void Execute(int x = 20, int y = 60, string[] paths = null)
            {
                if (GUI.Button(new Rect(x, y, 150, 30), "Other Scene Additive"))
                {
                    var overlayedScene = SceneHandler.GetOverlayedScene();
                    if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);
                    _secondCamera = SceneHandler.GetCameraOfScene("NewScene");
                    if (!_secondCamera)
                    {
                        Debug.Log("The second camera is null please load the second Scene");
                        return;
                    }

                    if (paths!.Length == 0)
                    {
                        // var pos1 = GetNodePositionRelativeToCamera(_secondCamera.transform.position, new Vector3(0, 0, 0));
                        // var pos2 = GetNodePositionRelativeToCamera(_secondCamera.transform.position, new Vector3(8, 8, 0));
                        // var pos3 = GetNodePositionRelativeToCamera(_secondCamera.transform.position, new Vector3(-8, 4, 0));
                        //
                        // var node1 = SpawnTestNodeOnSecondDisplay(pos1, new Vector3(nodeWidth, nodeHeight, 1f));
                        // var node2 = SpawnTestNodeOnSecondDisplay(pos2, new Vector3(nodeWidth, nodeHeight, 1f));
                        // var node3 = SpawnTestNodeOnSecondDisplay(pos3, new Vector3(nodeWidth, nodeHeight, 1f));
                        //
                        // _connectionManager.AddConnection(node1, node2, Color.red, 0.2f);
                        // _connectionManager.AddConnection(node1, node3, Color.green, 0.2f);
                        // _connectionManager.AddConnection(node2, node3, Color.blue, 0.2f);

                        var connections = ParentChildConnections.CalculateNodeConnections();

                        foreach (var nodeConnection in connections)
                        {
                            var currentNodeGameObject = SpawnCubeNodeUsingNodeObject(nodeConnection.Key);
                            _relatedGameObjects.Add(nodeConnection.Key, currentNodeGameObject);
                            foreach (var childNode in from childNode in nodeConnection.Value let currentNode = nodeConnection.Value select childNode)
                            {
                                GameObject childGameObject;
                                if (!_relatedGameObjects.TryGetValue(childNode, out var node))
                                {
                                    childGameObject = SpawnCubeNodeUsingNodeObject(nodeConnection.Key);
                                    _relatedGameObjects.Add(childNode, childGameObject);
                                }
                                else
                                {
                                    childGameObject = node;
                                }

                                _connectionManager.AddConnection(currentNodeGameObject, childGameObject, Color.HSVToRGB(0, 0.5f, .9f));
                            }
                        }
                    }
                    else
                    {
                        Debug.Log(paths);
                        var scriptNodes = FindScriptNodes(paths[0]);
                        NodeLayoutManagerV2.CompactFixedAspectRatioLayout(scriptNodes);

                        // Spawn a node for each node in scriptPaths
                        foreach (var scriptNode in scriptNodes)
                        {
                            _relatedGameObjects.Add(scriptNode, SpawnCubeNodeUsingNodeObject(scriptNode));
                        }

                        var connections = CalculateNodeConnections(scriptNodes, _allReferences);
                        DrawNodeConnections(connections, _relatedGameObjects);
                    }
                }
                else if (GUI.Button(new Rect(x, y + 30, 150, 30), "Clear Nodes"))
                {
                    Clear();
                }
                else if (GUI.Button(new Rect(x, y + 60, 150, 30), "Tree Layout"))
                {
                    BuildTree();
                    //PrintTree();
                    SpawnTreeFromNode(DefineRootNode());
                }
            }

            private void OnDestroy()
            {
                Clear();
            }
        }
    }