using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Runtime
{
    public class NodeBuilder : MonoBehaviour
    {
        private List<Node> _nodes = new();

        [Header("Node Configuration")] [SerializeField]
        private float nodeWidth = 2.0f;

        [SerializeField] private float nodeHeight = 1.0f;
        [SerializeField] private Color nodeColor = Color.white;

        [Header("Spawn Settings")] [SerializeField]
        private Vector2 initialSpawnPosition = new(0, 0);

        [SerializeField] private float verticalSpacing = 1.5f;
        [SerializeField] private string targetSceneName = "SecondDisplayScene"; // Scene to spawn nodes in

        private int _nodeCounter;

        private void SpawnNode()
        {
            var mainBody = new GameObject("New Sprite");
            var spriteRenderer = mainBody.AddComponent<SpriteRenderer>();
            var pos = GetNextSpawnPosition();
            mainBody.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y, 0), Quaternion.identity);

            var texture = Resources.Load<Texture2D>("Prefabs/Connector");
            var sprite = Sprite.Create(texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = nodeColor;

            // Add DragHandler to enable dragging
            mainBody.AddComponent<DragHandler>();

            _nodes.Add(new Node(initialSpawnPosition.x, initialSpawnPosition.y, nodeWidth, nodeHeight));
            _nodeCounter++;

            var targetScene = SceneManager.GetSceneByName(targetSceneName);

            if (!targetScene.isLoaded)
            {
                Debug.LogError($"Target scene '{targetSceneName}' is not loaded!");
                return;
            }

            SceneManager.MoveGameObjectToScene(mainBody, targetScene);

            ConfigureNode(mainBody);
        }

        /// <summary>
        /// Spawn a single node on the display
        /// </summary>
        /// <param name="spawnPosition"></param>
        /// <param name="nodeExtend"></param>
        /// <param name="display">display in which the node will be visible where <b>0</b> is display 1 and <b>1</b> is display 2</param>
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

        public void InitialSpawnNodes(List<Node> nodes, int display)
        {
            foreach (var node in nodes)
            {
                SpawnTestNodeOnSecondDisplay(new Vector3(node.X, node.Y, 0), new Vector3(node.Height, node.Width, 1f), display);
            }
        }


        private Vector2 GetNextSpawnPosition()
        {
            // Calculate spawn position with vertical offset
            return initialSpawnPosition + Vector2.up * (verticalSpacing * _nodeCounter++);
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
            SpawnTestNodeOnSecondDisplay(new Vector3(0, 0, 0), new Vector3(nodeWidth, nodeHeight, 1f));
            SpawnTestNodeOnSecondDisplay(new Vector3(1, 1.5f, 0), new Vector3(nodeWidth, nodeHeight, 1f));
            SpawnTestNodeOnSecondDisplay(new Vector3(3, 1.5f, 0), new Vector3(nodeWidth, nodeHeight, 1f));
            SpawnTestNodeOnSecondDisplay(new Vector3(3, 3, 0), new Vector3(nodeWidth, nodeHeight, 1f));
            SpawnTestNodeOnSecondDisplay(new Vector3(4.5f, 4, 0), new Vector3(nodeWidth, nodeHeight, 1f));
        }
    }
}