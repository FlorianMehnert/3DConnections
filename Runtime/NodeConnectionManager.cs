using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _3DConnections.Runtime
{
    /// <summary>
    /// Manager that handles all the connections in the node graph
    /// </summary>
    public class NodeConnectionManager : MonoBehaviour
    {
        [System.Serializable]
        public class NodeConnection
        {
            public GameObject startNode;
            public GameObject endNode;
            public LineRenderer lineRenderer;
            public Color connectionColor = new(1,255,50);
            public float lineWidth = 0.1f;
        }

        public List<NodeConnection> connections = new();
        public GameObject lineRendererPrefab;

        public void AddConnection(GameObject startNode, GameObject endNode, Color? color = null, float lineWidth = 0.1f)
        {
            // Create line renderer
            var lineObj = Instantiate(lineRendererPrefab, transform);
            var lineRenderer = lineObj.GetComponent<LineRenderer>();

            var newConnection = new NodeConnection
            {
                startNode = startNode,
                endNode = endNode,
                lineRenderer = lineRenderer,
                connectionColor = color ?? Color.white,
                lineWidth = lineWidth
            };
        
            // Configure line renderer
            lineRenderer.startColor = newConnection.connectionColor;
            lineRenderer.endColor = newConnection.connectionColor;
            lineRenderer.startWidth = newConnection.lineWidth;
            lineRenderer.endWidth = newConnection.lineWidth;
            lineRenderer.positionCount = 2;

            connections.Add(newConnection);
        }

        private void Update()
        {
            UpdateConnectionPositions();
        }

        private void UpdateConnectionPositions()
        {
            foreach (var connection in connections.Where(connection => connection.startNode && connection.endNode && connection.lineRenderer))
            {
                connection.lineRenderer.SetPosition(0, connection.startNode.transform.position);
                connection.lineRenderer.SetPosition(1, connection.endNode.transform.position);
            }
        }

        public void RemoveConnection(GameObject startNode, GameObject endNode)
        {
            for (var i = connections.Count - 1; i >= 0; i--)
            {
                var conn = connections[i];
                if (conn.startNode != startNode || conn.endNode != endNode) continue;
                // Destroy line renderer
                if (conn.lineRenderer != null)
                    Destroy(conn.lineRenderer.gameObject);
                
                // Remove from the list
                connections.RemoveAt(i);
            }
        }

        public void ClearConnections()
        {
            foreach (var conn in connections.Where(conn => conn.lineRenderer != null))
            {
                Destroy(conn.lineRenderer.gameObject);
            }

            connections.Clear();
        }
    }
}