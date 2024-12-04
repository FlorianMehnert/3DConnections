using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Runtime
{
    public class NodeBuilder : MonoBehaviour
    {
        private readonly Dictionary<string, Node> _scripts = new();

        [Header("Node Configuration")] [SerializeField]
        private float nodeWidth = 2.0f;

        [SerializeField] private float nodeHeight = 1.0f;
        [SerializeField] private Color nodeColor = Color.white;

        [SerializeField] private NodeConnectionManager connectionManager;

        private int _nodeCounter;

        private void Start()
        {
            connectionManager = GetComponent<NodeConnectionManager>();
        }


        /// <summary>
        /// Spawn a single node on the display
        /// <b>Requires</b> a second camera to be active with an existing and enabled overlayedScene :)
        /// </summary>
        /// <param name="spawnPosition">Position of the node in worldSpace: Please invoke for this one <see cref="CalculateNodePosition"/></param>
        /// <param name="nodeExtend">Node dimension</param>
        private GameObject SpawnTestNodeOnSecondDisplay(Vector3 spawnPosition, Vector3 nodeExtend)
        {
            var nodeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // set name allowing to differentiate between them 
            nodeObject.transform.position = spawnPosition;
            nodeObject.transform.localScale = nodeExtend;

            nodeObject.name = $"TestNode_{_nodeCounter}";
            nodeObject.layer = LayerMask.NameToLayer("OverlayScene");
            nodeObject.AddComponent<CubeTextOverlay>();
            RemoveAndReplaceCollider(nodeObject); // TODO: improve on this - unity always creates cube primitives using a collider attached
            ConfigureNode(nodeObject);
            nodeObject.AddComponent<DragHandler>();
            return nodeObject;
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
        private static Vector3 CalculateNodePosition(Vector3 secondDisplayCameraPosition, Vector3 spawnPosition)
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
            Dictionary<string, ClassReferences> allReferences;
            {
                allReferences = ClassParser.GetAllClassReferencesParallel(path);
            }

            foreach (var (scriptName, _) in allReferences)
            {
                var node = new Node(scriptName, 0, 0, nodeWidth, nodeHeight);
                _scripts[scriptName] = node;
                nodes.Add(node);
            }
            
            // Create references
            
            return nodes;
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

        public void Execute(int x = 20, int y = 60)
        {
            if (!GUI.Button(new Rect(x, y, 150, 30), "Other Scene Additive")) return;

            var secondCamera = SceneHandler.GetOverlayCamera(1);
            var overlayedScene = SceneHandler.GetOverlayedScene();
            if (overlayedScene != null) SceneManager.SetActiveScene((Scene)overlayedScene);

            var pos1 = CalculateNodePosition(secondCamera.transform.position, new Vector3(0, 0, 0));
            var pos2 = CalculateNodePosition(secondCamera.transform.position, new Vector3(8, 8, 0));
            var pos3 = CalculateNodePosition(secondCamera.transform.position, new Vector3(-8, 4, 0));

            var node1 = SpawnTestNodeOnSecondDisplay(pos1, new Vector3(nodeWidth, nodeHeight, 1f));
            var node2 = SpawnTestNodeOnSecondDisplay(pos2, new Vector3(nodeWidth, nodeHeight, 1f));
            var node3 = SpawnTestNodeOnSecondDisplay(pos3, new Vector3(nodeWidth, nodeHeight, 1f));

            connectionManager.AddConnection(node1, node2, Color.red, 0.2f);
            connectionManager.AddConnection(node1, node3, Color.green, 0.2f);
            connectionManager.AddConnection(node2, node3, Color.blue, 0.2f);
        }
    }
}