using Runtime;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minOrthographicSize = 2f;
    public float maxOrthographicSize = 20f;

    [Header("Pan Settings")]
    public float basePanSpeed = 0.5f;

    private Camera _cam;
    private Vector3 _lastMousePosition;

    private void Start()
    {
        _cam = SceneHandler.GetOverlayCamera(1);
    }

    private void Update()
    {
        HandleZoom();
        HandlePan();
    }

    private void HandleZoom()
    {
        var scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;
        _cam.orthographicSize -= scroll * zoomSpeed;
        _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, minOrthographicSize, maxOrthographicSize);
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(2)) // Middle mouse button pressed
        {
            _lastMousePosition = Input.mousePosition;
        }

        if (!Input.GetMouseButton(2)) return; // Middle mouse button held down
        var delta = Input.mousePosition - _lastMousePosition;

        // Adjust pan speed based on zoom level
        var adjustedPanSpeed = basePanSpeed * _cam.orthographicSize;

        var move = new Vector3(-delta.x * adjustedPanSpeed, -delta.y * adjustedPanSpeed, 0);

        _cam.transform.position += _cam.ScreenToWorldPoint(move) - _cam.ScreenToWorldPoint(Vector3.zero);
        _lastMousePosition = Input.mousePosition;
    }
}