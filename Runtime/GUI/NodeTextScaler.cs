using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

public class NodeTextScaler : ModularSettingsUser
{
    
    [RegisterModularBoolSetting("Enable Text Scaling", "Allow scaling text based on mouse proximity", "NodeTextScaler", false)]
    [Tooltip("Allow scaling text based on mouse proximity")]
    [SerializeField] private bool enableTextScaling;
    
    [Header("Scaling Settings")]
    [Tooltip("Maximum radius to detect nodes around the mouse cursor")]
    [RegisterModularFloatSetting("Influence radius of the mouse", "The larger the further away you can move the mouse to still scale the text", "NodeTextScaler", 50.0f, 0f, 100.0f)]
    public float detectionRadius = 50.0f;
    
    [Tooltip("Minimum font size (at edge of radius)")]
    [RegisterModularFloatSetting("minimum font size", "size of the font if the scaler is active and nothing is nearby", "NodeTextScaler", 12.0f, 0f, 100.0f)]
    public float minFontSize = 12.0f;
    
    [Tooltip("Maximum font size (at mouse position)")]
    [RegisterModularFloatSetting("maximum font size", "size of the font if this scaler is active and the mouse is nearby", "NodeTextScaler", 100.0f, 1.0f, 100.0f)]
    public float maxFontSize = 100.0f;
    
    [Tooltip("How smooth the scaling transition should be")]
    [RegisterModularFloatSetting("Smooth time", "Time the text will take to change its size", "NodeTextScaler", 0.1f, 0f, 1.0f)]
    public float smoothTime = 0.1f;
    
    [Header("References")]
    [Tooltip("Reference to the NodeGraph containing all nodes")]
    public NodeGraphScriptableObject nodeGraph;

    public Camera mainCamera;
    public MenuState menuState;

      
    
    private readonly Dictionary<TMP_Text, float> _velocities = new();
    private readonly Dictionary<TMP_Text, float> _originalFontSizes = new();

    private void Start()
    {
        mainCamera = mainCamera ? mainCamera : GetComponent<Camera>();
        InitializeOriginalFontSizes();
    }

    private void Awake()
    {
        RegisterModularSettings();
    }

    private void InitializeOriginalFontSizes()
    {
        if (!nodeGraph || nodeGraph.AllNodes == null)
            return;

        foreach (var textComponent in from node in nodeGraph.AllNodes 
                 where node
                 select node.GetComponentInChildren<TMP_Text>() 
                 into textComponent 
                 where textComponent
                 select textComponent)
        {
            _originalFontSizes[textComponent] = textComponent.fontSize;
            _velocities[textComponent] = 0f;
        }
    }

    private void Update()
    {
        if (menuState && !menuState.menuOpen && enableTextScaling)
        {
            ScaleTextBasedOnMouseProximity();
        }
    }

    private void ScaleTextBasedOnMouseProximity()
    {
        if (!nodeGraph || nodeGraph.AllNodes == null || !mainCamera)
            return;

        var mousePosition = Input.mousePosition;
        mousePosition.z = -mainCamera.transform.position.z; // Set the distance from the camera
        var mouseWorldPos = mainCamera.ScreenToWorldPoint(mousePosition);
        
        foreach (var node in nodeGraph.AllNodes)
        {
            if (!node) continue;
            
            // Using our new approach for all text components
            var targetSize = CalculateTargetSize(node, mouseWorldPos);
            ChangeNodeTextSize(node, targetSize);
        }
    }

    private float CalculateTargetSize(GameObject node, Vector3 mouseWorldPos)
    {
        var textComponent = node.GetComponentInChildren<TMP_Text>();
        if (!textComponent) return minFontSize;
            
        // Make sure we have the original font size recorded
        if (!_originalFontSizes.ContainsKey(textComponent))
        {
            _originalFontSizes[textComponent] = textComponent.fontSize;
            _velocities[textComponent] = 0f;
        }
            
        // Calculate distance to mouse
        var distance = Vector2.Distance(
            new Vector2(mouseWorldPos.x, mouseWorldPos.y),
            new Vector2(node.transform.position.x, node.transform.position.y)
        );
            
        // Determine target size based on distance
        if (distance <= detectionRadius)
        {
            // Normalize distance (0 = at cursor, 1 = at radius edge)
            float normalizedDistance = Mathf.Clamp01(distance / detectionRadius);
                
            // Inverse lerp for size (closer = larger)
            return Mathf.Lerp(maxFontSize, minFontSize, normalizedDistance);
        }
        
        // Outside radius, use minimum size
        return minFontSize;
    }

    private void ChangeNodeTextSize(GameObject node, float targetSize)
    {
        var textComponent = node.GetComponentInChildren<TMP_Text>();
        if (!textComponent) return;
        var currentSize = textComponent.fontSize;
        _velocities.TryAdd(textComponent, 0f);
        var velocity = _velocities[textComponent];
        var newSize = Mathf.SmoothDamp(currentSize, targetSize, ref velocity, smoothTime);
        _velocities[textComponent] = velocity;
        nodeGraph.ChangeTextSize(node, newSize);
    }
    

    private void OnDisable()
    {
        ResetAllTextSizes();
    }
    
    private void OnDestroy()
    {
        ResetAllTextSizes();
    }
    
    private void ResetAllTextSizes()
    {
        if (!nodeGraph || nodeGraph.AllNodes == null)
            return;
            
        foreach (var node in nodeGraph.AllNodes)
        {
            if (!node) continue;
            var textComponent = node.GetComponentInChildren<TMP_Text>();
            if (textComponent && _originalFontSizes.TryGetValue(textComponent, out var originalSize))
            {
                nodeGraph.ChangeTextSize(node, originalSize);
            }
        }
    }
}