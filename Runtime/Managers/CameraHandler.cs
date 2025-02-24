using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manager Class to move the orthographic camera using middle mouse drag and zoom in/out using mouse wheel
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")] public float zoomSpeed = 10f;

    private Camera _cam;
    private Vector3 _lastMousePosition;
    private float _screenWidth;
    private float _screenHeight;
    private float _worldWidth;
    private float _worldHeight;
    [SerializeField] private float padding = 1.1f; // Extra space when centering on selection
    [SerializeField] public NodeGraphScriptableObject nodeGraph;
    [SerializeField] private GameObject parentObject;
    Vector2 moveAmountGamepad;
    float zoomGamepad;

    [SerializeField] private OverlaySceneScriptableObject overlay;
    [SerializeField] private MenuState menuState;

    private void Start()
    {
        _cam = overlay.GetCameraOfScene();
        AddLayerToCamera("OverlayScene");

        // Calculate world dimensions based on current orthographic size
        CalculateWorldDimensions();
    }

    private void AddLayerToCamera(string layerName)
    {
        var layerToAdd = LayerMask.GetMask(layerName);
        if (layerToAdd == 0)
        {
            Debug.LogWarning($"In AddLayerToCamera, layer '{layerName}' does not exist!");
            return;
        }

        var cameraComponent = gameObject.GetComponent<Camera>();
        if (cameraComponent == null)
        {
            Debug.Log("Please only attach the CameraController to a Camera");
            return;
        }

        cameraComponent.cullingMask |= layerToAdd;
    }

    private void Update()
    {
        if (!menuState || menuState.menuOpen) return;
        // Recalculate world dimensions if zoom changes
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F))
        {
            // Disable all nodes
            foreach (var node in nodeGraph.AllNodes)
            { 
                if (!node) continue; 
                var meshRenderer = node.GetComponent<MeshRenderer>();
                if (meshRenderer)
                    meshRenderer.enabled = false;
                foreach (Transform child in node.transform)
                {
                    child.gameObject.SetActive(false);
                }
            }
            
            // Disable all connections
            foreach (var lineRenderer in NodeConnectionManager.Instance.conSo.connections.Select(node => node.lineRenderer))
                lineRenderer.enabled = false;
            
            // reenable all nodes that are connected with deep
            nodeGraph.ReenableConnectedNodes(nodeGraph.currentlySelectedGameObject, 0);
            EnableOutgoingLines(nodeGraph.currentlySelectedGameObject);
            return;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            CenterOnTarget(nodeGraph.currentlySelectedGameObject, true);
            return;
        }

        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.G) && parentObject)
        {
            foreach (var node in nodeGraph.AllNodes)
            { 
                if (!node) continue; 
                var meshRenderer = node.GetComponent<MeshRenderer>();
                if (meshRenderer)
                    meshRenderer.enabled = true;
                foreach (Transform child in node.transform)
                {
                    child.gameObject.SetActive(true);
                }
            }
            foreach (var lineRenderer in NodeConnectionManager.Instance.conSo.connections.Select(node => node.lineRenderer))
                lineRenderer.enabled = true;
        }

        if (Input.GetKeyDown(KeyCode.G) && parentObject)
        {
            AdjustCameraToViewChildren();
            return;
        }

        if (!menuState) return;
        HandleZoom();
        HandlePan();
        
        var movement = new Vector3(moveAmountGamepad.x, moveAmountGamepad.y, 0) * (5 * Time.deltaTime * _cam.orthographicSize);
        _cam.transform.position += movement;
        _cam.orthographicSize += zoomGamepad  * _cam.orthographicSize;
        _cam.orthographicSize = Mathf.Abs(_cam.orthographicSize);
    }

    private void CalculateWorldDimensions()
    {
        _screenWidth = Screen.width;
        _screenHeight = Screen.height;
        // Calculate world width and height based on camera's orthographic size and aspect ratio
        var aspectRatio = (float)Screen.width / Screen.height;
        _worldWidth = _cam.orthographicSize * 2f * aspectRatio;
        _worldHeight = _cam.orthographicSize * 2f;
    }

    private void HandleZoom()
    {
        var scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;
        CalculateWorldDimensions();
        // Calculate zoom speed dynamically based on the current zoom level
        var dynamicZoomSpeed = zoomSpeed * (_cam.orthographicSize / 10f);
        _cam.orthographicSize -= scroll * dynamicZoomSpeed;
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1)) // Middle mouse button pressed
        {
            _lastMousePosition = Input.mousePosition;
        }

        if (!Input.GetMouseButton(2) && !Input.GetMouseButton(1)) return; // Middle mouse button held down

        var delta = Input.mousePosition - _lastMousePosition;

        // Calculate pan based on screen width/height and world dimensions
        var horizontalWorldMovement = (delta.x / _screenWidth) * _worldWidth;
        var verticalWorldMovement = (delta.y / _screenHeight) * _worldHeight;

        var move = new Vector3(-horizontalWorldMovement, -verticalWorldMovement, 0);

        _cam.transform.position += move;

        _lastMousePosition = Input.mousePosition;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (menuState.menuOpen) return;
        moveAmountGamepad = context.ReadValue<Vector2>();

    }
    
    public void OnZoom(InputAction.CallbackContext context)
    {
        if (menuState.menuOpen) return;
        var zoomVector = context.ReadValue<float>();
        zoomGamepad = -zoomVector * Time.deltaTime;            
        CalculateWorldDimensions();
    }

    private void CenterOnTarget(GameObject targetObject, bool useEditorSelection = false)
    {
        if (!targetObject && nodeGraph.currentlySelectedBounds.size == Vector3.zero) return;
#if UNITY_EDITOR
        switch (useEditorSelection)
        {
            case true when Selection.activeGameObject:
                targetObject = Selection.activeTransform.gameObject;
                break;

            // center on bounds of orange highlighted nodes
            case true when nodeGraph.currentlySelectedBounds.size != Vector3.zero:
                break;

            // when no selection bounds nor an editor selection is available
            case true when !Selection.activeTransform || !Selection.activeTransform.gameObject:
                return;
        }
#else
            if (!targetObject) return;
#endif
        LineRenderer lineRenderer = null;
        if (targetObject != null)
            lineRenderer = targetObject.GetComponent<LineRenderer>();
        if (lineRenderer &&
            lineRenderer.positionCount == 2) // connections aka lineRenderers should be focussed on using their bounds
        {
            var highlight = !lineRenderer.GetComponent<HighlightConnection>()
                ? lineRenderer.gameObject.AddComponent<HighlightConnection>()
                : lineRenderer.GetComponent<HighlightConnection>();
            highlight.Highlight(Color.red, 2f);

            var bounds = lineRenderer.bounds;
            if (nodeGraph.currentlySelectedBounds.size != Vector3.zero)
            {
                bounds.Encapsulate(nodeGraph.currentlySelectedBounds);
            }

            SetCameraToBounds(bounds);
        }
        else if (targetObject != null && targetObject.GetComponent<Collider2D>() != null)
        {
            var bounds = nodeGraph.currentlySelectedBounds;
            if (nodeGraph.currentlySelectedBounds.size == Vector3.zero) return;
            bounds.Encapsulate(nodeGraph.currentlySelectedBounds);
            SetCameraToBounds(bounds);
        }
        else if (nodeGraph.currentlySelectedBounds.size != Vector3.zero)
        {
            SetCameraToBounds(nodeGraph.currentlySelectedBounds);
        }
        else // catch gameObjects without collider2D
        {
            var targetPosition = targetObject.transform.position;
            var newPosition = new Vector3(
                targetPosition.x,
                targetPosition.y,
                _cam.transform.position.z
            );
            _cam.orthographicSize = 3;
            _cam.transform.position = newPosition;
        }
    }

    private void SetCameraToBounds(Bounds bounds)
    {
        var center = bounds.center;
        _cam.transform.position = new Vector3(center.x, center.y, _cam.transform.position.z);
        var size = Mathf.Max(bounds.extents.x, bounds.extents.y);
        _cam.orthographicSize = size * padding;
    }

    private void AdjustCameraToViewChildren()
    {
        if (!_cam || !parentObject)
        {
            Debug.LogWarning("Camera or Parent Object is not assigned.");
            return;
        }

        // Calculate the combined bounds of all children
        var combinedBounds = new Bounds(parentObject.transform.position, Vector3.zero);
        var hasBounds = false;

        foreach (Transform child in parentObject.transform)
        {
            var childRenderer = child.GetComponent<Renderer>();
            if (!childRenderer) continue;
            if (hasBounds)
            {
                combinedBounds.Encapsulate(childRenderer.bounds);
            }
            else
            {
                combinedBounds = childRenderer.bounds;
                hasBounds = true;
            }
        }

        if (!hasBounds)
        {
            Debug.LogWarning("No child objects with renderers found under the parent.");
            return;
        }

        // Center the camera on the bounds
        var center = combinedBounds.center;
        _cam.transform.position = new Vector3(center.x, center.y, _cam.transform.position.z);

        // Adjust orthographic size
        var size = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y);
        _cam.orthographicSize = size * padding;
    }
    
    /// <summary>
    /// Used in Focus on Node to reenable Nodes that are in focus
    /// </summary>
    /// <param name="startNode"></param>
    /// <param name="maxDepth"></param>
    private void EnableOutgoingLines(GameObject startNode, int maxDepth = 5)
    {
        if (!startNode) return;
        Queue<(GameObject node, int depth)> queue = new();
        var visited = new HashSet<GameObject>();
        queue.Enqueue((startNode, 0));
        visited.Add(startNode);
        var connectionManager = NodeConnectionManager.Instance;
        while (queue.Count > 0)
        {
            var (currentNode, depth) = queue.Dequeue();
            if (depth >= maxDepth) continue;
            var nodeConnections = currentNode.GetComponent<LocalNodeConnections>();
            if (!nodeConnections) continue;
            foreach (var nextNode in nodeConnections.outConnections)
            {
                if (!nextNode || visited.Contains(nextNode)) continue;
                var connection = connectionManager.GetConnection(currentNode, nextNode);
                if (connection != null && connection.lineRenderer)
                    connection.lineRenderer.enabled = true;
                queue.Enqueue((nextNode, depth + 1));
                visited.Add(nextNode);
            }
        }
    }
}