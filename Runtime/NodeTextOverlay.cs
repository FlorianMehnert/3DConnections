using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MonoBehaviour to add to a node game-object, which then renders text on the node
/// </summary>
public class CubeTextOverlay : MonoBehaviour
{
    // Prefab for the text overlay
    public GameObject textPrefab;
    
    // Reference to the text component
    private Text _overlayText;
    
    // Reference to the canvas for the overlay
    private Canvas _overlayCanvas;

    private void Start()
    {
        // Create the canvas for overlay texts
        CreateOverlayCanvas();
        
        // Create text overlay for this cube
        CreateTextOverlay();
    }

    private void CreateOverlayCanvas()
    {
        // Check if overlay canvas already exists
        _overlayCanvas = FindFirstObjectByType<Canvas>();

        if (_overlayCanvas != null) return;
        // Create a new canvas if none exists
        GameObject canvasObject = new GameObject("OverlayCanvas");
        _overlayCanvas = canvasObject.AddComponent<Canvas>();
            
        // Configure canvas for world space rendering
        _overlayCanvas.renderMode = RenderMode.WorldSpace;
            
        // Add canvas scaler for proper scaling
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
            
        // Add graphic raycaster
        canvasObject.AddComponent<GraphicRaycaster>();
            
        // Set the canvas to the OverlayScene layer
        canvasObject.layer = LayerMask.NameToLayer("OverlayScene");
    }

    private void CreateTextOverlay()
    {
        // If no prefab is assigned, create a default text object
        if (textPrefab == null)
        {
            var textObject = new GameObject("CubeText");
            var textComponent = textObject.AddComponent<Text>();
            
            // Configure text defaults
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = 1;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.white;
            
            textPrefab = textObject;
        }

        // Instantiate the text prefab as a child of the canvas
        GameObject textInstance = Instantiate(textPrefab, _overlayCanvas.transform);
        
        // Get the Text component
        _overlayText = textInstance.GetComponent<Text>();
        
        // Set text content (you can modify this as needed)
        _overlayText.text = gameObject.name;
        
        // Position the text relative to the cube
        textInstance.transform.localPosition = new Vector3(0, 1f, 0);
    }

    void Update()
    {
        // Optional: Update text position to follow the cube
        if (_overlayText != null)
        {
            _overlayText.transform.position = transform.position + Vector3.up;
        }
    }

    void OnDestroy()
    {
        // Clean up the text overlay when the cube is destroyed
        if (_overlayText != null)
        {
            Destroy(_overlayText.gameObject);
        }
    }
}