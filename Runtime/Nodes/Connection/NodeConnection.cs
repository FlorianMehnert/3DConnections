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
    public bool dashed;
    
    public CodeReference codeReference;

    public void ApplyConnection()
    {
        if (!lineRenderer) return;
        var color = new Color(connectionColor.r, connectionColor.g, connectionColor.b, 0.5f);
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        // TODO: fix this
        if (dashed)
        {
            var dashedMat = Resources.Load<Material>($"dashedLine.mat");
            if (dashedMat)
            {
                lineRenderer.material = dashedMat;
                lineRenderer.textureMode = LineTextureMode.Tile;
                lineRenderer.material.mainTextureScale = new Vector2(1f / lineWidth, 1f);
            }
            else
            {
                Debug.LogWarning("DashedLineMaterial not found in Resources. Dashed line will not render as dashed.");
            }
        }
        else
        {
            var defaultMat = Resources.Load<Material>("Default-Line");
            if (defaultMat)
                lineRenderer.material = defaultMat;
        }
    }

    public void DisableConnection()
    {
        lineRenderer.enabled = false;
    } 
}

[System.Serializable]
public class CodeReference
{
    public string sourceFile;
    public int lineNumber;
    public string methodName;
    public string className;
    
    public bool HasReference => !string.IsNullOrEmpty(sourceFile) && lineNumber > 0;
}