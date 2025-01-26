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
        var bounds = lineRenderer.bounds;
        var center = bounds.center;
        overlayCamera.transform.position = new Vector3(center.x, center.y, overlayCamera.transform.position.z);
        var size = Mathf.Max(bounds.extents.x, bounds.extents.y);
        overlayCamera.orthographicSize = size * 1.1f;
        Debug.Log($"Focused OverlayCamera on {selected.name}");
        var highlight = !lineRenderer.GetComponent<HighlightConnection>()
            ? lineRenderer.gameObject.AddComponent<HighlightConnection>()
            : lineRenderer.GetComponent<HighlightConnection>();
        highlight.Highlight(Color.red, 1f, (() => { Destroy(highlight); }));
    }

    [MenuItem("GameObject/3DConnections/Focus Overlay Camera #&%f", true)]
    private static bool ValidateFocusOverlayCamera()
    {
        return Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<LineRenderer>() != null;
    }
}