using UnityEngine;


/// <summary>
/// Contains the connection gameObject in the form of the lineRenderer 
/// </summary>
[System.Serializable]
public class NodeConnection
{
    public GameObject startNode;
    public GameObject endNode;
    public LineRenderer lineRenderer;
    public Color connectionColor = new(1, 255, 50);
    public float lineWidth = 0.1f;
    public string connectionType;

    public void ApplyConnection()
    {
        var color = new Color(connectionColor.r, connectionColor.g, connectionColor.b, 0.5f);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
    }

    public void DisableConnection()
    {
        lineRenderer.enabled = false;
    } 
}