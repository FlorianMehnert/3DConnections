using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Color = UnityEngine.Color;

/// <summary>
/// Singleton Manager that handles all the connections in the node graph. Singleton because connections are only important for the overlay scene.
/// </summary>
public sealed class NodeConnectionManager : MonoBehaviour
{
    private static NodeConnectionManager _instance;

    [Header("Component based physics sim")]
    public PhysicsSimulationConfiguration simConfig;

    public NodeConnectionsScriptableObject conSo;

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
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void Update()
    {
        if (conSo.usingNativeArray)
        {
            UpdateConnectionPositionsNative();
        }
        else if (conSo.connections.Count > 0)
        {
            UpdateConnectionPositions();
        }
    }

    private void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }

    // Make sure to clean up the native array when the component is destroyed
    private void OnDestroy()
    {
        if (conSo.usingNativeArray && conSo.NativeConnections.IsCreated)
        {
            conSo.NativeConnections.Dispose();
        }

        if (_instance != this) return;
        ClearConnections();
        _instance = null;
    }

    private void OnDisable()
    {
        if (_instance == this)
        {
            ClearConnections();
        }
    }

    public NodeConnection AddConnection(GameObject startNode, GameObject endNode, Color? color = null, float lineWidth = 1f, float saturation = 1f, string connectionType = "parentChildConnection")
    {
        if (_isShuttingDown) return null;

        var lineObj = Instantiate(lineRendererPrefab, transform);
        var lineRenderer = lineObj.GetComponent<LineRenderer>();
        lineRenderer.name = startNode.name + "-" + endNode.name;
        var knownColor = color ?? Color.white;
        Color.RGBToHSV(knownColor, out var h, out _, out var v);

        knownColor = Color.HSVToRGB(h, saturation, v);
        knownColor.a = .5f;
        var newConnection = new NodeConnection
        {
            startNode = startNode,
            endNode = endNode,
            lineRenderer = lineRenderer,
            connectionColor = knownColor,
            lineWidth = lineWidth,
            connectionType = connectionType
        };

        // Configure line renderer
        newConnection.ApplyConnection();
        lineRenderer.positionCount = 2;

        conSo.connections.Add(newConnection);
        return newConnection;
    }

    private void UpdateConnectionPositions()
    {
        foreach (var connection in conSo.connections.Where(connection =>
                     connection.startNode && connection.endNode && connection.lineRenderer))
        {
            connection.lineRenderer.SetPosition(0, connection.startNode.transform.position);
            connection.lineRenderer.SetPosition(1, connection.endNode.transform.position);
        }
    }

    public void ClearConnections()
    {
        if (conSo.usingNativeArray && conSo.NativeConnections.IsCreated)
        {
            conSo.NativeConnections.Dispose();
            conSo.usingNativeArray = false;
        }

        foreach (var connection in conSo.connections.Where(connection => connection.lineRenderer != null))
        {
            Destroy(connection.lineRenderer.gameObject);
        }

        conSo.connections.Clear();
        conSo.currentConnectionCount = 0;
    }

    public void ClearNativeArray()
    {
        if (!conSo.usingNativeArray || !conSo.NativeConnections.IsCreated) return;
        conSo.NativeConnections.Dispose();
        conSo.usingNativeArray = false;
    }

    public void AddSpringsToConnections()
    {
        foreach (var connection in conSo.connections)
        {
            var existingSprings = connection.startNode.GetComponents<SpringJoint2D>();

            // avoid duplicating spring joints
            var alreadyExists = false;
            SpringJoint2D springComponent = null;
            foreach (var existingSpring in existingSprings)
            {
                if (existingSpring.connectedBody.gameObject == connection.endNode.gameObject)
                {
                    alreadyExists = true;
                    springComponent = existingSpring;
                    break;
                }
            }

            var spring = (alreadyExists && springComponent) ? springComponent : alreadyExists ? null : connection.startNode.AddComponent<SpringJoint2D>();
            if (!spring) return;
            spring.autoConfigureDistance = true;
            spring.connectedBody = connection.endNode.GetComponent<Rigidbody2D>();
            spring.dampingRatio = simConfig.damping * 2;
            spring.distance = simConfig.colliderRadius;
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
        foreach (var spring in conSo.connections.Select(connection => connection.startNode.GetComponents<SpringJoint2D>())
                     .SelectMany(springComponents => springComponents))
        {
            spring.dampingRatio = simConfig.damping;
            spring.frequency = simConfig.Stiffness;
            spring.distance = simConfig.colliderRadius;
        }
    }

    public void ConvertToNativeArray()
    {
        if (conSo.usingNativeArray)
        {
            if (conSo.NativeConnections.IsCreated)
            {
                conSo.NativeConnections.Dispose();
            }
        }

        conSo.currentConnectionCount = conSo.connections.Count;
        if (conSo.currentConnectionCount == 0) return;

        // Create a new native array with the exact size needed
        conSo.NativeConnections = new NativeArray<float3>(conSo.currentConnectionCount * 2, Allocator.Persistent);

        // Copy existing connections to the native array
        for (var i = 0; i < conSo.currentConnectionCount; i++)
        {
            if (!conSo.connections[i].startNode || !conSo.connections[i].endNode) continue;
            conSo.NativeConnections[i * 2] = conSo.connections[i].startNode.transform.position;
            conSo.NativeConnections[i * 2 + 1] = conSo.connections[i].endNode.transform.position;
        }

        conSo.usingNativeArray = true;
    }

    public void UseNativeArray()
    {
        conSo.usingNativeArray = true;
    }

    // Call this when you want to resize the native array (e.g., when connections are added/removed)
    public void ResizeNativeArray()
    {
        if (!conSo.usingNativeArray) return;

        var newConnectionCount = conSo.connections.Count;
        if (newConnectionCount == conSo.currentConnectionCount) return;

        var newArray = new NativeArray<float3>(newConnectionCount * 2, Allocator.Persistent);

        // Copy existing data up to the smaller of the two sizes
        var copyCount = math.min(conSo.currentConnectionCount, newConnectionCount) * 2;
        for (var i = 0; i < copyCount; i++)
        {
            newArray[i] = conSo.NativeConnections[i];
        }

        // Dispose old array and assign new one
        if (conSo.NativeConnections.IsCreated)
        {
            conSo.NativeConnections.Dispose();
        }

        conSo.NativeConnections = newArray;
        conSo.currentConnectionCount = newConnectionCount;
    }


    private void UpdateConnectionPositionsNative()
    {
        if (!conSo.usingNativeArray || !conSo.NativeConnections.IsCreated) return;

        for (var i = 0; i < conSo.currentConnectionCount; i++)
        {
            if (!conSo.connections[i].startNode || !conSo.connections[i].endNode || !conSo.connections[i].lineRenderer) continue;
            // Update the native array
            conSo.NativeConnections[i * 2] = conSo.connections[i].startNode.transform.position;
            conSo.NativeConnections[i * 2 + 1] = conSo.connections[i].endNode.transform.position;

            // Update line renderer
            conSo.connections[i].lineRenderer.SetPosition(0, conSo.NativeConnections[i * 2]);
            conSo.connections[i].lineRenderer.SetPosition(1, conSo.NativeConnections[i * 2 + 1]);
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
                        col.Highlight(color, duration, () => Destroy(col), emissionColor: emissionColor);
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
                if (rb == null) continue;
                var forceDirection = (rb.position - (Vector2)center).normalized;
                rb.AddForce(forceDirection * 5f, ForceMode2D.Impulse);
            }
        }
    }
    
    public NodeConnection GetConnection(GameObject start, GameObject end)
    {
        if (!start || !end) return null;
        return conSo.connections.FirstOrDefault(connection => connection.startNode == start && connection.endNode == end);
    }
}