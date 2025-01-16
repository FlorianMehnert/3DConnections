using System.Collections.Generic;
using System.Linq;
using _3DConnections.Runtime.ScriptableObjects;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime.Managers
{
    /// <summary>
    /// Singleton Manager that handles all the connections in the node graph. Singleton because connections are only important for the overlay scene
    /// </summary>
    public class NodeConnectionManager : MonoBehaviour
    {
        

        private static NodeConnectionManager _instance;

        public static NodeConnectionManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindFirstObjectByType<NodeConnectionManager>();
                if (_instance != null) return _instance;
                var singletonObject = new GameObject("NodeConnectionManager");
                _instance = singletonObject.AddComponent<NodeConnectionManager>();
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"Multiple {nameof(NodeConnectionManager)} instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        
        private void OnApplicationQuit()
        {
            _instance = null;
            
        }

        

        private void OnDestroy()
        {
            // Only clear the static instance if we're the current instance
            if (_instance == this)
            {
                _instance = null;
            }
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
                connectionColor = color ?? Color.black,
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