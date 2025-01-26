using _3DConnections.Runtime;
using UnityEditor;
using UnityEngine;

public class FocusOnObject : Editor
{
    [MenuItem("GameObject/3DConnections/Focus Overlay Camera #&%f", false, 20)]
    private static void FocusOverlayCamera()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        var selected = Selection.activeGameObject;

        if (selected == null || selected.GetComponent<LineRenderer>() == null)
        {
            return;
        }

        var overlayCamera = GameObject.Find("OverlayCamera")?.GetComponent<Camera>();

        if (overlayCamera == null)
        {
            Debug.LogError("No camera named 'OverlayCamera' found in the scene.");
            return;
        }

        var lineRenderer = selected.GetComponent<LineRenderer>();
        var center = lineRenderer.bounds.center;
        overlayCamera.transform.position = new Vector3(center.x, center.y, overlayCamera.transform.position.z);
        var size = Mathf.Max(lineRenderer.bounds.extents.x, lineRenderer.bounds.extents.y);
        overlayCamera.orthographicSize = size * 1.1f;
        Debug.Log($"Focused OverlayCamera on {selected.name}");
        var coloredObject = selected.GetComponent<ColoredObject>();
        if (coloredObject == null) return;
        coloredObject.Highlight(Color.red, 1f);
    }

    [MenuItem("GameObject/3DConnections/Focus Overlay Camera #&%f", true)]
    private static bool ValidateFocusOverlayCamera()
    {
        return Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<LineRenderer>() != null;
    }
}