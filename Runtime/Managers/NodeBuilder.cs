using System.Collections.Generic;
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

        [SerializeField] private GameObject nodePrefab;

        private int _nodeCounter;
        
        // parent for all nodes to be spawned
        private GameObject _parentNode;
        
        // ScriptableObjects to keep track of Nodes
        [SerializeField] private NodeGraphScriptableObject nodegraph;
        [SerializeField] private ToAnalyzeSceneScriptableObject toAnalyzeSceneScriptableObject;
        [SerializeField] private OverlaySceneScriptableObject overlay;

        // new stuffs from relationanalyzer
        [SerializeField] internal Color gameobjectColor = new(0.2f, 0.6f, 1f); // Blue


        private void Start()
        {
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

            switch (node)
            {
                case null:
                    return;
                case GameObjectNode goNode when goNode.GameObject != null:
                {
                    if (nodegraph.ContainsGameObjectNodeByID(goNode.GameObject.GetInstanceID()) != null)
                    {
                        Debug.Log("goNode is already present");
                        return;
                    }

                    break;
                }
                case GameObjectNode goNode:
                    Debug.Log("trying to spawn a goNode without a game object with Name being: " + goNode.Name);
                    return;
            }

            var nodeObject = Instantiate(nodePrefab, _parentNode.transform);
            nodeObject.transform.localPosition = new Vector3(node.X, node.Y, 0);
            nodeObject.transform.localScale = new Vector3(node.Width, node.Height, 1f);
            nodeObject.name = node.Name;
            nodeObject.layer = LayerMask.NameToLayer("OverlayScene");
            
            // set nodeType
            var type = nodeObject.GetComponent<NodeType>();
            if (type != null)
            {
                type.nodeTypeName = node switch
                {
                    GameObjectNode => "GameObject",
                    ComponentNode => "Component",
                    ScriptableObjectNode => "ScriptableObject",
                    _ => type.nodeTypeName
                };
                type.reference = node switch
                {
                    GameObjectNode gameObjectNode => gameObjectNode.GameObject,
                    ComponentNode componentNode => componentNode.Component.gameObject,
                    ScriptableObjectNode => null,
                    _ => null
                };
            }
            
            node.RelatedGameObject = nodeObject.gameObject;
            var componentRenderer = nodeObject.GetComponent<Renderer>();
            if (componentRenderer)
                componentRenderer.material.color = color;
            if (nodegraph.Add(node)) return;
            if (nodegraph.ReplaceRelatedGo(node))
                Debug.Log("no successful Add nor successful Replace in nodegraph with node" + node);
            else
                Debug.Log("replaced ");
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

        private static void DrawNodeConnections(
            Dictionary<Node, HashSet<Node>> nodeConnections)
        {
            foreach (var (sourceNode, connectedNodes) in nodeConnections)
            {
                foreach (var targetNode in connectedNodes)
                {
                    NodeConnectionManager.Instance.AddConnection(sourceNode.RelatedGameObject, targetNode.RelatedGameObject, Color.HSVToRGB(0, 0.5f, .9f));
                }
            }
        }

        private void Clear()
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

            // clear connections
            NodeConnectionManager.Instance.ClearConnections();

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


        private void OnDestroy()
        {
            Clear();
        }
    }
}