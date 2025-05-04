using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MermaidExporter : MonoBehaviour
{
    
#if UNITY_EDITOR
    [ContextMenu("Export To Mermaid")]
    public void ExportViaContextMenu()
    {
        const string defaultName = "nodegraph.md";
        var path = EditorUtility.SaveFilePanel("Export Node Graph to Mermaid", "", defaultName, "md");
        if (string.IsNullOrEmpty(path)) return;
        ExportToMermaid(path);
        Debug.Log($"Node graph exported to: {path}");
    }
#endif
    private static void ExportToMermaid(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph LR");

        var seenConnections = new HashSet<string>();

        foreach (var edge in from connection in NodeConnectionManager.Instance.conSo.connections where connection.startNode != null && connection.endNode != null let startName = connection.startNode.name let endName = connection.endNode.name let label = string.IsNullOrEmpty(connection.connectionType) 
                     ? "" 
                     : $" |{connection.connectionType}|" select $"{Sanitize(startName)} -->{label} {Sanitize(endName)}" into edge where seenConnections.Add(edge) select edge)
        {
            sb.AppendLine(edge);
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static string Sanitize(string name)
    {
        // Make a safe ID (letters, numbers, underscores only)
        var safe = name;
        safe = safe.Replace(" ", "_")
            .Replace("-", "_")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(":", "_")
            .Replace(".", "_")
            .Replace("/", "_")
            .Replace("\\", "_");
        return safe;
    }
}