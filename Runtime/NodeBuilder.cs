using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Runtime
{
    /// <summary>
    /// Manager class responsible for the construction of the node-graph
    /// </summary>
    public class NodeBuilder : MonoBehaviour
    {
        private readonly Dictionary<Node, GameObject> _nodes = new();
        private Dictionary<string, ClassReferences> _allReferences = new();

        [Header("Node Configuration")] [SerializeField]
        private float nodeWidth = 2.0f;

        [SerializeField] private float nodeHeight = 1.0f;
        [SerializeField] private Color nodeColor = Color.white;

        private NodeConnectionManager connectionManager;
        [SerializeField] private GameObject nodePrefab;

        private int _nodeCounter;
        private Camera _secondCamera;

        private void Start()
        {
            connectionManager = GetComponent<NodeConnectionManager>();
        }


        /// <summary>
        /// Spawn a single node on the display
        /// <b>Requires</b> a second camera to be active with an existing and enabled overlayedScene :)
        /// </summary>
        /// <param name="spawnPosition">Position of the node in worldSpace: Please invoke for this one <see cref="GetNodePositionRelativeToCamera"/></param>
        /// <param name="nodeExtend">Node dimension</param>
        private GameObject SpawnTestNodeOnSecondDisplay(Vector3 spawnPosition, Vector3 nodeExtend)
        {
            // spawn node prefab as child of this element
            var nodeObject = Instantiate(nodePrefab, transform);
            
            // set name allowing to differentiate between them 
            nodeObject.transform.position = spawnPosition;
            nodeObject.transform.localScale = nodeExtend;
            nodeObject.name = $"Node";
            nodeObject.layer = LayerMask.NameToLayer("OverlayScene");
            nodeObject.AddComponent<CubeTextOverlay>();
            RemoveAndReplaceCollider(nodeObject); // TODO: improve on this - unity always creates cube primitives using a collider attached
            ConfigureNode(nodeObject);
            return nodeObject;
        }

        /// <summary>
        /// Takes a node and executes <see cref="SpawnTestNodeOnSecondDisplay"/>
        /// </summary>
        /// <param name="node"></param>
        private GameObject SpawnCubeNodeUsingNodeObject(Node node)
        {
            return SpawnTestNodeOnSecondDisplay(GetNodePositionRelativeToCamera(_secondCamera.transform.position, new Vector3(node.X, node.Y, 0)), new Vector3(nodeWidth, nodeHeight, 1f));
        }

        private static void RemoveAndReplaceCollider(GameObject nodeObject)
        {
            // Remove 3D box collider
            var boxCollider = nodeObject.GetComponent<BoxCollider>();
            if (boxCollider)
            {
                DestroyImmediate(boxCollider);
            }

            nodeObject.AddComponent<BoxCollider2D>();
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
                    node => node.Name, 
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
                    connectionManager.AddConnection(sourceGameObject, targetGameObject, Color.HSVToRGB(0, 0.5f, .9f ));
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
                componentRenderer.material.color = nodeColor;
            }

            // Set up text component
            var textComponent = nodeObject.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent)
            {
                textComponent.text = $"Node {_nodeCounter}";
            }
        }

        public void Execute(int x = 20, int y = 60, string[] paths = null)
        {
            if (!GUI.Button(new Rect(x, y, 150, 30), "Other Scene Additive")) return;

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
                var pos1 = GetNodePositionRelativeToCamera(_secondCamera.transform.position, new Vector3(0, 0, 0));
                var pos2 = GetNodePositionRelativeToCamera(_secondCamera.transform.position, new Vector3(8, 8, 0));
                var pos3 = GetNodePositionRelativeToCamera(_secondCamera.transform.position, new Vector3(-8, 4, 0));

                var node1 = SpawnTestNodeOnSecondDisplay(pos1, new Vector3(nodeWidth, nodeHeight, 1f));
                var node2 = SpawnTestNodeOnSecondDisplay(pos2, new Vector3(nodeWidth, nodeHeight, 1f));
                var node3 = SpawnTestNodeOnSecondDisplay(pos3, new Vector3(nodeWidth, nodeHeight, 1f));

                connectionManager.AddConnection(node1, node2, Color.red, 0.2f);
                connectionManager.AddConnection(node1, node3, Color.green, 0.2f);
                connectionManager.AddConnection(node2, node3, Color.blue, 0.2f);
            }
            else
            {
                Debug.Log(paths);
                var scriptNodes = FindScriptNodes(paths[0]);
                NodeLayoutManagerV2.CompactFixedAspectRatioLayout(scriptNodes);
                
                // Spawn a node for each node in scriptPaths
                foreach (var scriptNode in scriptNodes)
                {
                    _nodes.Add(scriptNode, SpawnCubeNodeUsingNodeObject(scriptNode));
                }

                var connections = CalculateNodeConnections(scriptNodes, _allReferences);
                DrawNodeConnections(connections, _nodes);
            }
        }
    }
}