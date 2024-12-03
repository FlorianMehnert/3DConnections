using UnityEngine;
using System.Collections.Generic;

public class NodeConnectionManager : MonoBehaviour
{
    // Represents a connection between two nodes
    [System.Serializable]
    public class NodeConnection
    {
        public GameObject startNode;
        public GameObject endNode;
        public Color connectionColor = Color.white;
        public float lineWidth = 0.1f;
        
        // Optional: Custom data for the connection
        public object Metadata;
    }

    // List of all connections
    public List<NodeConnection> connections = new();

    // Line renderer prefab for drawing connections
    public GameObject lineRendererPrefab;

    // Add a new connection between nodes
    public void AddConnection(GameObject startNode, GameObject endNode, Color? color = null, float lineWidth = 0.1f)
    {
        var newConnection = new NodeConnection
        {
            startNode = startNode,
            endNode = endNode,
            connectionColor = color ?? Color.white,
            lineWidth = lineWidth
        };
        
        connections.Add(newConnection);
        DrawConnection(newConnection);
    }

    // Draw a specific connection
    private void DrawConnection(NodeConnection connection)
    {
        // Instantiate line renderer
        var lineObj = Instantiate(lineRendererPrefab);
        var lineRenderer = lineObj.GetComponent<LineRenderer>();

        // Configure line renderer
        lineRenderer.startColor = connection.connectionColor;
        lineRenderer.endColor = connection.connectionColor;
        lineRenderer.startWidth = connection.lineWidth;
        lineRenderer.endWidth = connection.lineWidth;

        // Set line positions
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, connection.startNode.transform.position);
        lineRenderer.SetPosition(1, connection.endNode.transform.position);
    }

    // Update connections if nodes move
    private void Update()
    {
        UpdateConnectionPositions();
    }

    // Refresh line renderer positions
    private void UpdateConnectionPositions()
    {
        foreach (var connection in connections)
        {
            var lineRenderer = connection.startNode.GetComponentInChildren<LineRenderer>();
            if (!lineRenderer) continue;
            lineRenderer.SetPosition(0, connection.startNode.transform.position);
            lineRenderer.SetPosition(1, connection.endNode.transform.position);
        }
    }

    // Remove a specific connection
    public void RemoveConnection(GameObject startNode, GameObject endNode)
    {
        connections.RemoveAll(conn => 
            conn.startNode == startNode && conn.endNode == endNode);
    }

    // Clear all connections
    public void ClearConnections()
    {
        connections.Clear();
    }
}