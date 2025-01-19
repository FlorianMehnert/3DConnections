using _3DConnections.Runtime.ScriptableObjects;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace _3DConnections.Runtime.Managers
{
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
        [SerializeField] private NodeGraphScriptableObject nodeGraph;
        [SerializeField] private GameObject parentObject;

        [SerializeField] private OverlaySceneScriptableObject overlay;

        private void Start()
        {
            _cam = overlay.GetCameraOfScene();

            // Calculate world dimensions based on current orthographic size
            CalculateWorldDimensions();
        }

        private void Update()
        {
            // Recalculate world dimensions if zoom changes
            CalculateWorldDimensions();

            HandleZoom();
            HandlePan();

            if (Input.GetKeyDown(KeyCode.F) && nodeGraph.currentlySelectedGameObject)
            {
                CenterOnTarget(nodeGraph.currentlySelectedGameObject);
            }
            else if (Input.GetKeyDown(KeyCode.F) && !nodeGraph.currentlySelectedGameObject)
            {
                CenterOnTarget(nodeGraph.currentlySelectedGameObject, true);
            }
            else if (Input.GetKeyDown(KeyCode.G) && parentObject)
            {
                AdjustCameraToViewChildren();
            }
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

            // Calculate zoom speed dynamically based on the current zoom level
            var dynamicZoomSpeed = zoomSpeed * (_cam.orthographicSize / 10f);
            _cam.orthographicSize -= scroll * dynamicZoomSpeed;
        }

        private void HandlePan()
        {
            if (Input.GetMouseButtonDown(2)) // Middle mouse button pressed
            {
                _lastMousePosition = Input.mousePosition;
            }

            if (!Input.GetMouseButton(2)) return; // Middle mouse button held down

            var delta = Input.mousePosition - _lastMousePosition;

            // Calculate pan based on screen width/height and world dimensions
            var horizontalWorldMovement = (delta.x / _screenWidth) * _worldWidth;
            var verticalWorldMovement = (delta.y / _screenHeight) * _worldHeight;

            var move = new Vector3(-horizontalWorldMovement, -verticalWorldMovement, 0);

            _cam.transform.position += move;

            _lastMousePosition = Input.mousePosition;
        }

        private void CenterOnTarget(GameObject targetObject, bool useEditorSelection = false)
        {
#if UNITY_EDITOR
            switch (useEditorSelection)
            {
                case true when Selection.activeGameObject:
                    targetObject = Selection.activeTransform.gameObject;
                    break;
                case true when !Selection.activeTransform.gameObject:
                    return;
            }
#else
            if (!targetObject) return;
#endif
            var lineRenderer = targetObject.GetComponent<LineRenderer>();
            if (lineRenderer && lineRenderer.positionCount == 2) // connections aka lineRenderers should be focussed on using their bounds
            {
                var highlight = !lineRenderer.GetComponent<HighlightConnection>() ? lineRenderer.AddComponent<HighlightConnection>() : lineRenderer.GetComponent<HighlightConnection>();
                highlight.Highlight(Color.red, 2f);

                var bounds = new Bounds(lineRenderer.transform.position, Vector3.zero);
                bounds.Encapsulate(lineRenderer.bounds);
                var center = bounds.center;
                _cam.transform.position = new Vector3(center.x, center.y, _cam.transform.position.z);
                var size = Mathf.Max(bounds.extents.x, bounds.extents.y);
                _cam.orthographicSize = size * padding;
            }
            else // normal gameObject
            {
                var targetPosition = targetObject.transform.position;

                // Keep camera's z position (depth) and only update x and y
                var newPosition = new Vector3(
                    targetPosition.x,
                    targetPosition.y,
                    _cam.transform.position.z
                );
                _cam.orthographicSize = 3;
                _cam.transform.position = newPosition;
            }
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
    }
}