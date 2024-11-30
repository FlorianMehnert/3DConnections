using System.Collections.Generic;
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
        [SerializeField] private string targetSceneName = "NewScene"; // Scene to spawn nodes in

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


        private Vector2 GetNextSpawnPosition()
        {
            // Calculate spawn position with vertical offset
            return initialSpawnPosition + Vector2.up * (verticalSpacing * _nodeCounter++);
        }

        private void ConfigureNode(GameObject nodeObject)
        {
            // Set up node visual components
            RectTransform rectTransform = nodeObject.GetComponent<RectTransform>();
            if (rectTransform)
            {
                rectTransform.sizeDelta = new Vector2(nodeWidth, nodeHeight);
            }

            // Set up text component
            TextMeshProUGUI textComponent = nodeObject.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent)
            {
                textComponent.text = $"Node {_nodeCounter}";
            }

            // Optional: Configure node color
            var nodeImage = nodeObject.GetComponent<Image>();
            if (nodeImage)
            {
                nodeImage.color = nodeColor;
            }
        }

        public void Execute(int x = 20, int y = 60)
        {
            if (!GUI.Button(new Rect(x, y, 150, 30), "Other Scene Additive")) return;
            SpawnNode();
        }
    }
}