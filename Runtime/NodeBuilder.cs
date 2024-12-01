using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Runtime
{
    public class NodeBuilder : MonoBehaviour
    {
        private readonly List<Node> _nodes = new();
        private readonly Dictionary<string, Node> _scripts = new();

        [Header("Node Configuration")] [SerializeField]
        private float nodeWidth = 2.0f;

        [SerializeField] private float nodeHeight = 1.0f;
        [SerializeField] private Color nodeColor = Color.white;

        [Header("Spawn Settings")] [SerializeField]
        private Vector2 initialSpawnPosition = new(0, 0);

        [SerializeField] private float verticalSpacing = 1.5f;
        [SerializeField] private string targetSceneName = "SecondDisplayScene"; // Scene to spawn nodes in

        private int _nodeCounter;

        /// <summary>
        /// Spawn a single node on the display
        /// </summary>
        /// <param name="spawnPosition"></param>
        /// <param name="nodeExtend"></param>
        /// <param name="display">display in which the node will be visible where <b>0</b> is display 1, and <b>1</b> is display 2</param>
        private void SpawnTestNodeOnSecondDisplay(Vector3 spawnPosition, Vector3 nodeExtend, int display = 1)
        {
            // Get the second scene
            var overlayedScene = SceneHandler.GetOverlayedScene();

            // create GOs in the overlay scene
            if (overlayedScene == null) return;
            SceneManager.SetActiveScene((Scene)overlayedScene);

            // 0. Get overlayed camera for second display
            var secondDisplayCamera = Camera.allCameras.FirstOrDefault(cam => cam.targetDisplay == display);

            if (secondDisplayCamera)
            {
                // 1. Create Placeholder Node
                var nodeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

                // set name allowing to differentiate between them 
                nodeObject.name = $"TestNode_{_nodeCounter}";
                nodeObject.transform.localScale = nodeExtend;

                nodeObject.transform.position = secondDisplayCamera.transform.position 
                                                + secondDisplayCamera.transform.forward * 5f 
                                                + spawnPosition;

                // required to only visible in display2
                nodeObject.layer = LayerMask.NameToLayer("OverlayScene");

                // remove BoxCollider and add BoxCollider2D
                var boxCollider = nodeObject.GetComponent<BoxCollider>();
                if (boxCollider)
                {
                    DestroyImmediate(boxCollider);
                }

                // Add to a node list for later use
                _nodes.Add(new Node(
                    "TestNode_" + _nodeCounter,
                    spawnPosition.x,
                    spawnPosition.y,
                    nodeExtend.x,
                    nodeExtend.y
                ));

                // Increment node counter - remove later
                _nodeCounter++;

                // TODO: check if this can be done with fewer steps
                ConfigureNode(nodeObject);

                // allow dragging nodes
                nodeObject.AddComponent<DragHandler>();
            }
            else
            {
                Debug.Log("did not find second camera");
            }
        }

        private void InitialSpawnNodes(int display)
        {
            var nodes = _nodes.ToArray();
            foreach (var node in nodes)
            {
                SpawnTestNodeOnSecondDisplay(new Vector3(node.X, node.Y, 0), new Vector3(node.Height, node.Width, 1f), display);
            }
        }
        
        /// <summary>
        /// Create references between nodes
        /// </summary>
        /// <param name="reference"></param>
        /// <param name="sourceNode"></param>
        private void CreateReference(KeyValuePair<string, ClassReferences> reference, Node sourceNode)
        {
            foreach (var className in reference.Value.References.Select(referencedScript =>
                         referencedScript.Contains(".")
                             ? referencedScript[
                                 (referencedScript.LastIndexOf(".", StringComparison.Ordinal) + 1)..]
                             : referencedScript))
            {
                if (_scripts.TryGetValue(className, out var targetNode))
                {
                    CreateEdge(sourceNode, targetNode);
                }
            }
        }

        private void CreateEdge(Node sourceNode, Node targetNode)
        {
        }

        /// <summary>
        /// Populates _nodes using scripts found in the project
        /// </summary>
        private void FindScriptNodes(string path)
        {
            Dictionary<string, ClassReferences> allReferences;
            {
                allReferences = ClassParser.GetAllClassReferencesParallel(path);
            }

            foreach (var (scriptName, _) in allReferences)
            {
                var node = new Node (scriptName, 0, 0, nodeWidth, nodeHeight );
                _scripts[scriptName] = node;
                _nodes.Add(node);
            }

            Parallel.ForEach(allReferences, reference =>
            {
                var sourceScriptName = reference.Key;
                if (!_scripts.TryGetValue(sourceScriptName, out var sourceNode)) return;

                CreateReference(reference, sourceNode);
            });
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
            FindScriptNodes("/home/florian/gamedev");
            InitialSpawnNodes(1);
        }
    }
}