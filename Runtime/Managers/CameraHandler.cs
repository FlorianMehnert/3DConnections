using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime.Managers
{
    /// <summary>
    /// Manager Class to move the orthographic camera using middle mouse drag and zoom in/out using mouse wheel
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Zoom Settings")]
        public float zoomSpeed = 10f;

        private Camera _cam;
        private Vector3 _lastMousePosition;
        private float _screenWidth;
        private float _screenHeight;
        private float _worldWidth;
        private float _worldHeight;

        public CameraController(float basePanSpeed)
        {
        }

        private void Start()
        {
            _cam = SceneHandler.GetCameraOfScene();
        
            // Calculate world dimensions based on current orthographic size
            CalculateWorldDimensions();
        }

        private void Update()
        {
            // Recalculate world dimensions if zoom changes
            CalculateWorldDimensions();
        
            HandleZoom();
            HandlePan();
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
    }
}