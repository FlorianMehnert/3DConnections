namespace _3DConnections.Runtime.Managers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
#if UNITY_EDITOR
    using UnityEditor;
#endif
    using UnityEngine;
    using UnityEngine.InputSystem;
    
    using ScriptableObjectInventory;
    using Nodes;
    using Utils;

    /// <summary>
    /// Manager Class to move the orthographic camera using middle mouse drag and zoom in/out using mouse wheel
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        public float zoomSpeed = 10f;

        public Camera cam;
        private Vector3 _lastMousePosition;
        private float _screenWidth;
        private float _screenHeight;
        private float _worldWidth;
        private float _worldHeight;
        private GraphLODManager _lodManager;

        [SerializeField] private float padding = 1.1f; // Extra space when centering on selection

        [SerializeField] private GameObject parentObject;
        private Vector2 _moveAmountGamepad;
        private float _zoomGamepad;

        [Header("Wide Screenshot specific")] [SerializeField]

        // wide screenshot parameters
        public int width = 2000;

        public string filePath = "Assets/screenshot.png";

        private void Start()
        {
            AddLayerToCamera("OverlayScene");

            // Find or create LOD manager
            _lodManager = FindFirstObjectByType<GraphLODManager>();
            if (!_lodManager)
            {
                _lodManager = gameObject.AddComponent<GraphLODManager>();
            }

            // Calculate world dimensions based on current orthographic size
            CalculateWorldDimensions();
        }

        private void OnEnable()
        {
            cam = GetComponent<Camera>();
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
            if (!cameraComponent)
            {
                Debug.Log("Please only attach the CameraController to a Camera");
                return;
            }

            cameraComponent.cullingMask |= layerToAdd;
        }

        private void Update()
        {
            if (!ScriptableObjectInventory.Instance.menuState ||
                ScriptableObjectInventory.Instance.menuState.menuOpen) return;

            if (Input.GetKeyDown(KeyCode.G) && parentObject)
            {
                AdjustCameraToViewChildren();
                return;
            }

            if (!ScriptableObjectInventory.Instance.menuState) return;
            HandleZoom();
            HandlePan();

            var movement = new Vector3(_moveAmountGamepad.x, _moveAmountGamepad.y, 0) *
                           (5 * Time.deltaTime * cam.orthographicSize);
            cam.transform.position += movement;
            cam.orthographicSize += _zoomGamepad * cam.orthographicSize;
            cam.orthographicSize = Mathf.Abs(cam.orthographicSize);
        }

        private void CalculateWorldDimensions()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            // Calculate world width and height based on camera's orthographic size and aspect ratio
            var aspectRatio = (float)Screen.width / Screen.height;
            _worldWidth = cam.orthographicSize * 2f * aspectRatio;
            _worldHeight = cam.orthographicSize * 2f;
        }

        private void HandleZoom()
        {
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll == 0f) return;
            CalculateWorldDimensions();
            // Calculate zoom speed dynamically based on the current zoom level
            var dynamicZoomSpeed = zoomSpeed * (cam.orthographicSize / 10f);
            cam.orthographicSize -= scroll * dynamicZoomSpeed;
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
            var horizontalWorldMovement = delta.x / _screenWidth * _worldWidth;
            var verticalWorldMovement = delta.y / _screenHeight * _worldHeight;

            var move = new Vector3(-horizontalWorldMovement, -verticalWorldMovement, 0);

            cam.transform.position += move;

            _lastMousePosition = Input.mousePosition;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            if (ScriptableObjectInventory.Instance.menuState.menuOpen) return;
            _moveAmountGamepad = context.ReadValue<Vector2>();
        }

        private void CenterOnTarget(GameObject targetObject, bool useEditorSelection = false)
        {
            if (!targetObject &&
                ScriptableObjectInventory.Instance.graph.currentlySelectedBounds.size == Vector3.zero) return;
#if UNITY_EDITOR
            switch (useEditorSelection)
            {
                case true when Selection.activeGameObject:
                    targetObject = Selection.activeTransform.gameObject;
                    break;

                // center on bounds of orange highlighted nodes
                case true when ScriptableObjectInventory.Instance.graph.currentlySelectedBounds.size != Vector3.zero:
                    break;

                // when no selection bounds nor an editor selection is available
                case true when !Selection.activeTransform || !Selection.activeTransform.gameObject:
                    return;
            }
#else
            if (!targetObject) return;
#endif
            LineRenderer lineRenderer = null;
            if (targetObject)
                lineRenderer = targetObject.GetComponent<LineRenderer>();
            if (lineRenderer &&
                lineRenderer.positionCount ==
                2) // connections aka lineRenderers should be focussed on using their bounds
            {
                var highlight = !lineRenderer.GetComponent<HighlightConnection>()
                    ? lineRenderer.gameObject.AddComponent<HighlightConnection>()
                    : lineRenderer.GetComponent<HighlightConnection>();
                highlight.Highlight(Color.red, 2f);

                var bounds = lineRenderer.bounds;
                if (ScriptableObjectInventory.Instance.graph.currentlySelectedBounds.size != Vector3.zero)
                {
                    bounds.Encapsulate(ScriptableObjectInventory.Instance.graph.currentlySelectedBounds);
                }

                SetCameraToBounds(bounds);
            }
            else if (targetObject && targetObject.GetComponent<Collider2D>())
            {
                var bounds = ScriptableObjectInventory.Instance.graph.currentlySelectedBounds;
                if (ScriptableObjectInventory.Instance.graph.currentlySelectedBounds.size == Vector3.zero) return;
                bounds.Encapsulate(ScriptableObjectInventory.Instance.graph.currentlySelectedBounds);
                SetCameraToBounds(bounds);
            }
            else if (ScriptableObjectInventory.Instance.graph.currentlySelectedBounds.size != Vector3.zero)
            {
                SetCameraToBounds(ScriptableObjectInventory.Instance.graph.currentlySelectedBounds);
            }
            else // catch gameObjects without collider2D
            {
                var targetPosition = targetObject.transform.position;
                var newPosition = new Vector3(
                    targetPosition.x,
                    targetPosition.y,
                    cam.transform.position.z
                );
                cam.orthographicSize = 3;
                cam.transform.position = newPosition;
            }
        }

        private void SetCameraToBounds(Bounds bounds)
        {
            var center = bounds.center;
            cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
            var size = Mathf.Max(bounds.extents.x, bounds.extents.y);
            cam.orthographicSize = size * padding;
        }

        private void AdjustCameraToViewChildren()
        {
            if (!cam || !parentObject)
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
            cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);

            // Adjust orthographic size
            var size = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y);
            if (size == 0) size = 1;
            cam.orthographicSize = size * padding;
        }
        
        public static void AdjustCameraToViewObjects(Camera cam, IEnumerable<GameObject> targets, float padding = 1.1f)
        {
            if (!cam)
            {
                Debug.LogWarning("Camera is not assigned.");
                return;
            }

            var combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
            var hasBounds = false;

            foreach (var obj in targets)
            {
                if (!obj) continue;

                // Prefer renderer bounds
                var renderer = obj.GetComponentInChildren<Renderer>();
                if (renderer)
                {
                    if (hasBounds)
                        combinedBounds.Encapsulate(renderer.bounds);
                    else
                    {
                        combinedBounds = renderer.bounds;
                        hasBounds = true;
                    }
                    continue;
                }

                // Fallback: collider bounds
                var collider = obj.GetComponentInChildren<Collider>();
                if (!collider) continue;
                if (hasBounds)
                    combinedBounds.Encapsulate(collider.bounds);
                else
                {
                    combinedBounds = collider.bounds;
                    hasBounds = true;
                }
            }

            if (!hasBounds)
            {
                Debug.LogWarning("No valid objects with bounds found.");
                return;
            }

            var center = combinedBounds.center;

            if (cam.orthographic)
            {
                // Move orthographic camera to center
                cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);

                var size = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y);
                cam.orthographicSize = size * padding;
            }
            else
            {
                // For perspective cameras: move & adjust distance
                cam.transform.LookAt(center);

                var boundsSize = combinedBounds.extents.magnitude;
                var fov = cam.fieldOfView * Mathf.Deg2Rad;
                var distance = boundsSize / Mathf.Sin(fov / 2f);

                cam.transform.position = center - cam.transform.forward * distance * padding;
            }
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

#if UNITY_EDITOR
        [ContextMenu("Capture Node Graph Screenshot")]
        public void Capture()
        {
            if (ScriptableObjectInventory.Instance.graph.AllNodes == null ||
                ScriptableObjectInventory.Instance.graph.AllNodes.Count == 0 || !cam)
            {
                Debug.LogError("Missing camera or nodes");
                return;
            }

            var positions = ScriptableObjectInventory.Instance.graph.AllNodes.Select(go => go.transform.position)
                .ToArray();
            var min = positions.Aggregate(Vector3.Min);
            var max = positions.Aggregate(Vector3.Max);
            var center = (min + max) * 0.5f;
            var size = max - min;

            var cameraHeight = size.y * 1.1f;
            var cameraWidth = size.x * 1.1f;
            var outputHeight = Mathf.RoundToInt(width * (cameraHeight / cameraWidth));

            // --- Set up RenderTexture ---
            var rt = new RenderTexture(width, outputHeight, 24, RenderTextureFormat.DefaultHDR)
            {
                antiAliasing = 1
            };
            rt.Create();

            var camGo = new GameObject("TempCaptureCamera");
            var tempCam = camGo.AddComponent<Camera>();
            tempCam.cullingMask = ~(1 << LayerMask.NameToLayer("OverlayScene"));
            tempCam.CopyFrom(cam); // Copy all settings from your main cam
            tempCam.orthographic = true;
            tempCam.orthographicSize = cameraHeight / 2f;
            tempCam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
            tempCam.targetTexture = rt;

            tempCam.Render();

            RenderTexture.active = rt;
            var image = new Texture2D(width, outputHeight, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, outputHeight), 0, 0);
            image.Apply();

            File.WriteAllBytes(filePath, image.EncodeToPNG());
            Debug.Log("Saved screenshot to: " + filePath);

            tempCam.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            DestroyImmediate(camGo);
            AssetDatabase.Refresh();
        }

#endif
    }
}