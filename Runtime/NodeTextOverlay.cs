using System.Collections.Generic;
using Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace _3DConnections.Runtime
{
    /// <summary>
    /// MonoBehaviour to add to a node game-object, which then renders text on the node
    /// </summary>
    public class NodeTextOverlay : MonoBehaviour
    {
        // Prefab for the text overlay
        public GameObject textPrefab;
    
        // Reference to the text component
        private Text _overlayText;
    
        // Reference to the canvas for the overlay
        private Canvas _overlayCanvas;
    
        private readonly HashSet<GameObject> _textObjects = new();

        /// <summary>
        /// Spawn text as child of the canvas object in the overlay scene
        /// </summary>
        private void CreateTextOverlay(string goName = "CubeText", Transform position = null)
        {
            // If no prefab is assigned, create a default text object
            if (textPrefab == null)
            {
                var textObject = new GameObject(goName);
                var canvas = SceneHandler.GetCanvas("NewScene");
                textObject.transform.parent = canvas.transform;
                var textComponent = textObject.AddComponent<Text>();
            
                // Configure text defaults
                textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                textComponent.fontSize = 1;
                textComponent.alignment = TextAnchor.MiddleCenter;
                textComponent.color = Color.white;
            
                textPrefab = textObject;
            }

            // Instantiate the text prefab as a child of the canvas
            var textInstance = Instantiate(textPrefab, position);
        
            // Get the Text component
            _overlayText = textInstance.GetComponent<Text>();
        
            // Set text content (you can modify this as needed)
            _overlayText.text = gameObject.name;
        
            // Position the text relative to the cube
            textInstance.transform.localPosition = new Vector3(0, 1f, 0);
            _textObjects.Add(textInstance);
        }

        private void Update()
        {
            // Optional: Update text position to follow the cube
            // if (_overlayText)
            // {
            //     _overlayText.transform.position = transform.position + Vector3.up;
            // }
        }

        public void ClearText()
        {
            foreach (var textObject in _textObjects)
            {
                DestroyImmediate(textObject);
            }
            _textObjects.Clear();
        }

        private void OnDestroy()
        {
            // Clean up the text overlay when the cube is destroyed
            if (_overlayText != null)
            {
                ClearText();
            }
        }
    }
}