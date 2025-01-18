using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _3DConnections.Runtime.Managers
{
    /// <summary>
    /// Singleton Manager that handles all the connections in the node graph. Singleton because connections are only important for the overlay scene.
    /// </summary>
    public class NodeConnectionManager : MonoBehaviour
    {
        private static NodeConnectionManager _instance;

        public static NodeConnectionManager Instance
        {
            get
            {
                if (_isShuttingDown)
                {
                    return null;
                }

                if (_instance != null) return _instance;

                _instance = FindFirstObjectByType<NodeConnectionManager>();
                if (_instance != null) return _instance;

                var singletonObject = new GameObject("NodeConnectionManager");
                _instance = singletonObject.AddComponent<NodeConnectionManager>();
                return _instance;
            }
        }

        private static bool _isShuttingDown;

        public List<NodeConnection> connections = new();
        public GameObject lineRendererPrefab;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                ClearConnections();
                _instance = null;
            }
        }

        private void OnDisable()
        {
            if (_instance == this)
            {
                ClearConnections();
            }
        }

        public void AddConnection(GameObject startNode, GameObject endNode, Color? color = null, float lineWidth = 0.1f)
        {
            if (_isShuttingDown) return;

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
            foreach (var connection in connections.Where(connection => connection.lineRenderer != null))
            {
                Destroy(connection.lineRenderer.gameObject);
            }

            connections.Clear();
        }
    }
}
