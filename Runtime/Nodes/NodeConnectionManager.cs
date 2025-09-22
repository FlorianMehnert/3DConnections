using _3DConnections.Runtime.Nodes.Connection;
using _3DConnections.Runtime.ScriptableObjects;

namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.Linq;
    using Unity.Collections;
    using Unity.Mathematics;
    using UnityEngine;
    using Color = UnityEngine.Color;
    using ScriptableObjectInventory;
    using Scene;
    using Nodes;

    /// <summary>
    /// Singleton Manager that handles all the connections in the node graph. Singleton because connections are only important for the overlay scene.
    /// </summary>
    public sealed class NodeConnectionManager : MonoBehaviour
    {
        private static NodeConnectionManager _instance;
        [SerializeField] private Transform rootEdgeTransform;

        private static bool _isShuttingDown;
        public GameObject lineRendererPrefab;

        public static NodeConnectionManager Instance
        {
            get
            {
                if (_isShuttingDown)
                {
                    return null;
                }

                if (_instance) return _instance;

                _instance = FindFirstObjectByType<NodeConnectionManager>();
                if (_instance) return _instance;

                var singletonObject = new GameObject("NodeConnectionManager");
                _instance = singletonObject.AddComponent<NodeConnectionManager>();
                return _instance;
            }
        }


        private void Awake()
        {
            _isShuttingDown = false;
            if (_instance && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            if (!rootEdgeTransform) return;
            var rootEdgeGameObject = GameObject.Find("ParentEdgesObject");
            rootEdgeTransform = rootEdgeGameObject.transform
                ? rootEdgeGameObject.transform
                : new GameObject("ParentEdgesObject").transform;
            ScriptableObjectInventory.Instance.conSo.connections.Clear();
            ScriptableObjectInventory.Instance.edgeRoot = rootEdgeTransform;
            ScriptableObjectInventory.Instance.clearEvent.onEventTriggered.AddListener(HandleEvent);
        }

        private void OnDisable()
        {
            if (ScriptableObjectInventory.Instance == null) return;
            ScriptableObjectInventory.Instance.conSo.connections.Clear();
            ScriptableObjectInventory.Instance.clearEvent.onEventTriggered.RemoveListener(HandleEvent);
        }

        /// <summary>
        /// Handle ClearEvent
        /// </summary>
        public void HandleEvent()
        {
            if (ScriptableObjectInventory.Instance == null || ScriptableObjectInventory.Instance.conSo == null) return;
            ClearConnections();
        }

        private void Update()
        {
            if (ScriptableObjectInventory.Instance.simConfig.SimulationType == SimulationType.Static) return;
            UpdateConnections();
        }

        public void UpdateConnections()
        {
            if (ScriptableObjectInventory.Instance.conSo.usingNativeArray)
            {
                UpdateConnectionPositionsNative();
            }
            else if (ScriptableObjectInventory.Instance.conSo.connections.Count > 0)
            {
                UpdateConnectionPositions();
            }
        }

        private void OnApplicationQuit()
        {
            _isShuttingDown = true;
        }

        private void OnDestroy()
        {
            if (ScriptableObjectInventory.Instance == null) return;
            _isShuttingDown = true;
            try
            {
                if (ScriptableObjectInventory.Instance.conSo.usingNativeArray &&
                    ScriptableObjectInventory.Instance.conSo.NativeConnections.IsCreated)
                {
                    ScriptableObjectInventory.Instance.conSo.NativeConnections.Dispose();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public NodeConnection AddConnection(
            GameObject startNode,
            GameObject endNode,
            Color? color = null,
            float lineWidth = 1f,
            float saturation = 1f,
            string connectionType = "parentChildConnection",
            bool dashed = false,
            CodeReference codeReference = null)
        {
            if (_isShuttingDown) return null;

            var lineObj = Instantiate(lineRendererPrefab, rootEdgeTransform);
            var lineRenderer = lineObj.GetComponent<LineRenderer>();
            lineRenderer.name = startNode.name + "-" + endNode.name;

            var type = lineObj.GetComponent<EdgeType>();
            if (type)
            {
                type.connectionType = connectionType;
                if (codeReference != null)
                {
                    type.codeReference = codeReference;
                }
            }

            if (!lineObj.GetComponent<Collider2D>())
            {
                var edgeCollider2D = lineObj.AddComponent<EdgeCollider2D>();
                UpdateEdgeCollider(edgeCollider2D, lineRenderer);
            }

            var knownColor = color ?? Color.white;
            Color.RGBToHSV(knownColor, out var h, out _, out var v);

            var coloredObject = lineObj.GetComponent<ColoredObject>();
            coloredObject.SetOriginalColor(knownColor);

            knownColor = Color.HSVToRGB(h, saturation, v);
            knownColor.a = .5f;

            var newConnection = new NodeConnection
            {
                startNode = startNode,
                endNode = endNode,
                lineRenderer = lineRenderer,
                connectionColor = knownColor,
                lineWidth = lineWidth,
                connectionType = connectionType,
                dashed = dashed,
                codeReference = codeReference
            };

            newConnection.ApplyConnection();
            lineRenderer.positionCount = 2;

            ScriptableObjectInventory.Instance.conSo.connections.Add(newConnection);
            return newConnection;
        }

        private void UpdateEdgeCollider(EdgeCollider2D edgeCollider2D, LineRenderer lineRenderer)
        {
            if (lineRenderer.positionCount < 2) return;

            Vector2[] points = new Vector2[lineRenderer.positionCount];
            for (int i = 0; i < lineRenderer.positionCount; i++)
            {
                points[i] = lineRenderer.transform.InverseTransformPoint(lineRenderer.GetPosition(i));
            }

            edgeCollider2D.points = points;
        }


        private void UpdateConnectionPositions()
        {
            var lodManager = FindFirstObjectByType<GraphLODManager>();

            foreach (var connection in ScriptableObjectInventory.Instance.conSo.connections.Where(connection =>
                         connection.startNode && connection.endNode && connection.lineRenderer))
            {
                // Check if either node is clustered
                GameObject startPos = connection.startNode;
                GameObject endPos = connection.endNode;

                if (lodManager != null && lodManager.enabled)
                {
                    var startCluster = lodManager.GetClusterForNode(connection.startNode);
                    var endCluster = lodManager.GetClusterForNode(connection.endNode);

                    if (startCluster != null) startPos = startCluster;
                    if (endCluster != null) endPos = endCluster;
                }

                connection.lineRenderer.SetPosition(0, startPos.transform.position);
                connection.lineRenderer.SetPosition(1, endPos.transform.position);

                // UPDATE THE EDGE COLLIDER TOO
                var edgeCollider = connection.lineRenderer.GetComponent<EdgeCollider2D>();
                if (edgeCollider != null)
                {
                    UpdateEdgeCollider(edgeCollider, connection.lineRenderer);
                }
            }
        }


        private void ClearConnections()
        {
            var conSo = ScriptableObjectInventory.Instance.conSo;
            if (conSo && conSo.usingNativeArray && conSo.NativeConnections.IsCreated)
            {
                conSo.NativeConnections.Dispose();
                conSo.usingNativeArray = false;
            }

            try
            {
                if (ScriptableObjectInventory.Instance == null) return;
                if (!ScriptableObjectInventory.Instance.conSo) return;
                foreach (var connection in conSo.connections.Where(connection => connection.lineRenderer))
                {
                    Destroy(connection.lineRenderer.gameObject);
                }

                conSo.connections.Clear();
                conSo.currentConnectionCount = 0;
            }
            catch (Exception)
            {
                Debug.Log("trying to access object in clear connections");
            }
        }

        public void AddSpringsToConnections()
        {
            foreach (var connection in ScriptableObjectInventory.Instance.conSo.connections)
            {
                SpringJoint2D[] existingSprings;
                try
                {
                    existingSprings = connection.startNode.GetComponents<SpringJoint2D>();
                }
                catch (Exception)
                {
                    continue;
                }


                // avoid duplicating spring joints
                var alreadyExists = false;
                SpringJoint2D springComponent = null;
                foreach (var existingSpring in existingSprings)
                {
                    if (existingSpring.connectedBody.gameObject != connection.endNode.gameObject) continue;
                    alreadyExists = true;
                    springComponent = existingSpring;
                    break;
                }

                var spring = (alreadyExists && springComponent) ? springComponent :
                    alreadyExists ? null : connection.startNode.AddComponent<SpringJoint2D>();
                if (!spring) return;
                spring.autoConfigureDistance = true;
                spring.connectedBody = connection.endNode.GetComponent<Rigidbody2D>();
                spring.dampingRatio = ScriptableObjectInventory.Instance.simConfig.damping * 2;
                spring.distance = ScriptableObjectInventory.Instance.simConfig.colliderRadius;
                spring.frequency = 0.05f;
                if (!spring.connectedBody) return;
                spring.connectedBody.freezeRotation = true;
            }
        }

        /// <summary>
        /// DO NOT CLEAR, MIGHT BE USEFUL LATER WHEN READDING ITS BUTTON
        /// </summary>
        public void UpdateSpringParameters()
        {
            foreach (var spring in ScriptableObjectInventory.Instance.conSo.connections
                         .Select(connection => connection.startNode.GetComponents<SpringJoint2D>())
                         .SelectMany(springComponents => springComponents))
            {
                spring.dampingRatio = ScriptableObjectInventory.Instance.simConfig.damping;
                spring.frequency = ScriptableObjectInventory.Instance.simConfig.Stiffness;
                spring.distance = ScriptableObjectInventory.Instance.simConfig.colliderRadius;
            }
        }

        public void ConvertToNativeArray()
        {
            if (ScriptableObjectInventory.Instance.conSo.usingNativeArray)
            {
                if (ScriptableObjectInventory.Instance.conSo.NativeConnections.IsCreated)
                {
                    ScriptableObjectInventory.Instance.conSo.NativeConnections.Dispose();
                }
            }

            ScriptableObjectInventory.Instance.conSo.currentConnectionCount =
                ScriptableObjectInventory.Instance.conSo.connections.Count;
            if (ScriptableObjectInventory.Instance.conSo.currentConnectionCount == 0) return;

            // Create a new native array with the exact size needed
            ScriptableObjectInventory.Instance.conSo.NativeConnections = new NativeArray<float3>(
                ScriptableObjectInventory.Instance.conSo.currentConnectionCount * 2, Allocator.Persistent);

            // Copy existing connections to the native array
            for (var i = 0; i < ScriptableObjectInventory.Instance.conSo.currentConnectionCount; i++)
            {
                if (!ScriptableObjectInventory.Instance.conSo.connections[i].startNode ||
                    !ScriptableObjectInventory.Instance.conSo.connections[i].endNode) continue;
                ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2] = ScriptableObjectInventory.Instance
                    .conSo.connections[i].startNode.transform.position;
                ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2 + 1] = ScriptableObjectInventory
                    .Instance.conSo.connections[i].endNode.transform.position;
            }

            ScriptableObjectInventory.Instance.conSo.usingNativeArray = true;
        }

        public void UseNativeArray()
        {
            ScriptableObjectInventory.Instance.conSo.usingNativeArray = true;
        }

        public void ResizeNativeArray()
        {
            if (!ScriptableObjectInventory.Instance.conSo.usingNativeArray) return;

            var newConnectionCount = ScriptableObjectInventory.Instance.conSo.connections.Count;
            if (newConnectionCount == ScriptableObjectInventory.Instance.conSo.currentConnectionCount) return;

            var newArray = new NativeArray<float3>(newConnectionCount * 2, Allocator.Persistent);

            // Copy existing data up to the smaller of the two sizes
            var copyCount = math.min(ScriptableObjectInventory.Instance.conSo.currentConnectionCount,
                newConnectionCount) * 2;
            for (var i = 0; i < copyCount; i++)
            {
                newArray[i] = ScriptableObjectInventory.Instance.conSo.NativeConnections[i];
            }

            // Dispose old array and assign new one
            if (ScriptableObjectInventory.Instance.conSo.NativeConnections.IsCreated)
            {
                ScriptableObjectInventory.Instance.conSo.NativeConnections.Dispose();
            }

            ScriptableObjectInventory.Instance.conSo.NativeConnections = newArray;
            ScriptableObjectInventory.Instance.conSo.currentConnectionCount = newConnectionCount;
        }


        private void UpdateConnectionPositionsNative()
        {
            if (!ScriptableObjectInventory.Instance.conSo.usingNativeArray ||
                !ScriptableObjectInventory.Instance.conSo.NativeConnections.IsCreated) return;

            for (var i = 0; i < ScriptableObjectInventory.Instance.conSo.currentConnectionCount; i++)
            {
                if (!ScriptableObjectInventory.Instance.conSo.connections[i].startNode ||
                    !ScriptableObjectInventory.Instance.conSo.connections[i].endNode ||
                    !ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer) continue;

                // Update the native array
                ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2] = ScriptableObjectInventory.Instance
                    .conSo.connections[i].startNode.transform.position;
                ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2 + 1] = ScriptableObjectInventory
                    .Instance.conSo.connections[i].endNode.transform.position;

                // Update line renderer
                ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer.SetPosition(0,
                    ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2]);
                ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer.SetPosition(1,
                    ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2 + 1]);

                // UPDATE THE EDGE COLLIDER TOO
                var edgeCollider = ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer
                    .GetComponent<EdgeCollider2D>();
                if (edgeCollider != null)
                {
                    UpdateEdgeCollider(edgeCollider,
                        ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer);
                }
            }
        }


        // ReSharper disable once MemberCanBeMadeStatic.Global
        public void HighlightCycles(Color color, float duration)
        {
            if (CycleDetection.Instance.HasCycle(SceneHandler.GetNodesUsingTheNodegraphParentObject(), out var cycles))
            {
                foreach (var cycle in cycles)
                {
                    // Debug.Log("Cycle detected: " + string.Join(" -> ", cycle.Select(n => n.name)));
                    foreach (var go in cycle)
                    {
                        var col = go.GetComponent<ColoredObject>();
                        if (col)
                        {
                            col.Highlight(color, duration);
                        }
                        else
                        {
                            col = go.AddComponent<ColoredObject>();
                            var emissionColor = color * 5.0f;
                            col.Highlight(color, duration, actionAfterHighlight: () => Destroy(col));
                        }
                    }
                }
            }
            else
            {
                Debug.Log("No cycles detected.");
            }
        }

        /// <summary>
        /// Push cycles away from each other by applying a force -> only usable in component-based physics sim
        /// </summary>
        public void SeparateCycles()
        {
            CycleDetection.Instance.HasCycle(SceneHandler.GetNodesUsingTheNodegraphParentObject(), out var cycles);
            foreach (var cycle in cycles)
            {
                var center = cycle.Aggregate(Vector3.zero, (acc, node) => acc + node.transform.position) / cycle.Count;
                foreach (var node in cycle)
                {
                    var rb = node.GetComponent<Rigidbody2D>();
                    if (!rb) continue;
                    var forceDirection = (rb.position - (Vector2)center).normalized;
                    rb.AddForce(forceDirection * 5f, ForceMode2D.Impulse);
                }
            }
        }

        public NodeConnection GetConnection(GameObject start, GameObject end)
        {
            if (!start || !end) return null;
            return ScriptableObjectInventory.Instance.conSo.connections.FirstOrDefault(connection =>
                connection.startNode == start && connection.endNode == end);
        }

        /// <summary>
        /// Enables/Disables all edges of the given connection-type
        /// </summary>
        public static void SetConnectionType(string connectionType, bool enable)
        {
            ScriptableObjectInventory.Instance.conSo.connections.ForEach(connection =>
            {
                if (connection.connectionType != connectionType) return;
                try
                {
                    connection.lineRenderer.enabled = enable;
                }
                catch (MissingReferenceException)
                {
                    // ignore, happens when the connection is destroyed while this is running
                    Debug.Log("Missing reference exception");
                }
            });
        }

#if UNITY_EDITOR
        /// <summary>
        /// One update step where the line width is set as well
        /// </summary>
        [ContextMenu("Apply Line widths")]
        public void ApplyChangedNodeConnections()
        {
            if (ScriptableObjectInventory.Instance.conSo.usingNativeArray)
            {
                if (!ScriptableObjectInventory.Instance.conSo.usingNativeArray ||
                    !ScriptableObjectInventory.Instance.conSo.NativeConnections.IsCreated) return;

                for (var i = 0; i < ScriptableObjectInventory.Instance.conSo.currentConnectionCount; i++)
                {
                    if (!ScriptableObjectInventory.Instance.conSo.connections[i].startNode ||
                        !ScriptableObjectInventory.Instance.conSo.connections[i].endNode ||
                        !ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer) continue;
                    ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer.startWidth =
                        ScriptableObjectInventory.Instance.conSo.connections[i].lineWidth;
                    ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer.endWidth =
                        ScriptableObjectInventory.Instance.conSo.connections[i].lineWidth;
                }

                UpdateConnectionPositionsNative();
            }
            else if (ScriptableObjectInventory.Instance.conSo.connections.Count > 0)
            {
                foreach (var connection in ScriptableObjectInventory.Instance.conSo.connections.Where(connection =>
                             connection.startNode && connection.endNode && connection.lineRenderer))
                {
                    if (!connection.lineRenderer) return;
                    connection.lineRenderer.startWidth = connection.lineWidth;
                    connection.lineRenderer.endWidth = connection.lineWidth;
                }

                UpdateConnectionPositions();
            }
        }
#endif
    }
}