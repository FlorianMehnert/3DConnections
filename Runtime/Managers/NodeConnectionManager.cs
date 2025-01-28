using System.Collections.Generic;
using System.Linq;
using _3DConnections.Assets.ScriptableObjects.Configurations;
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

    private NativeArray<float3> _nativeConnections;
    private bool _usingNativeArray;
    private int _currentConnectionCount;
    [SerializeField] private NodeGraphScriptableObject nodeGraph;

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

    private void Update()
    {
        if (_usingNativeArray)
        {
            UpdateConnectionPositionsNative();
        }
        else
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
        if (_usingNativeArray && _nativeConnections.IsCreated)
        {
            _nativeConnections.Dispose();
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

    public void AddConnection(GameObject startNode, GameObject endNode, Color? color = null, float lineWidth = 1f, float saturation = 1f)
    {
        if (_isShuttingDown) return;

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

    private void UpdateConnectionPositions()
    {
        foreach (var connection in connections.Where(connection =>
                     connection.startNode && connection.endNode && connection.lineRenderer))
        {
            connection.lineRenderer.SetPosition(0, connection.startNode.transform.position);
            connection.lineRenderer.SetPosition(1, connection.endNode.transform.position);
        }
    }

    public void ClearConnections()
    {
        if (_usingNativeArray && _nativeConnections.IsCreated)
        {
            _nativeConnections.Dispose();
            _usingNativeArray = false;
        }

        foreach (var connection in connections.Where(connection => connection.lineRenderer != null))
        {
            Destroy(connection.lineRenderer.gameObject);
        }

        connections.Clear();
        _currentConnectionCount = 0;
    }

    public void AddSpringsToConnections()
    {
        foreach (var connection in connections)
        {
            var spring = connection.startNode.AddComponent<SpringJoint2D>();
            spring.connectedBody = connection.endNode.GetComponent<Rigidbody2D>();
            spring.frequency = simConfig.stiffness;
            spring.dampingRatio = simConfig.damping;
            spring.distance = simConfig.colliderRadius;
            if (spring.connectedBody == null) return;
            spring.connectedBody.freezeRotation = true;
        }
    }

    /// <summary>
    /// DO NOT CLEAR, MIGHT BE USEFUL LATER WHEN READDING ITS BUTTON
    /// </summary>
    public void UpdateSpringParameters()
    {
        foreach (var spring in connections.Select(connection => connection.startNode.GetComponents<SpringJoint2D>())
                     .SelectMany(springComponents => springComponents))
        {
            spring.dampingRatio = simConfig.damping;
            spring.frequency = simConfig.stiffness;
            spring.distance = simConfig.colliderRadius;
        }
    }

    public void ConvertToNativeArray()
    {
        if (_usingNativeArray)
        {
            if (_nativeConnections.IsCreated)
            {
                _nativeConnections.Dispose();
            }
        }

        _currentConnectionCount = connections.Count;
        if (_currentConnectionCount == 0) return;

        // Create a new native array with the exact size needed
        _nativeConnections = new NativeArray<float3>(_currentConnectionCount * 2, Allocator.Persistent);

        // Copy existing connections to the native array
        for (var i = 0; i < _currentConnectionCount; i++)
        {
            if (!connections[i].startNode || !connections[i].endNode) continue;
            _nativeConnections[i * 2] = connections[i].startNode.transform.position;
            _nativeConnections[i * 2 + 1] = connections[i].endNode.transform.position;
        }

        _usingNativeArray = true;
    }

    // Call this when you want to resize the native array (e.g., when connections are added/removed)
    public void ResizeNativeArray()
    {
        if (!_usingNativeArray) return;

        var newConnectionCount = connections.Count;
        if (newConnectionCount == _currentConnectionCount) return;

        var newArray = new NativeArray<float3>(newConnectionCount * 2, Allocator.Persistent);

        // Copy existing data up to the smaller of the two sizes
        var copyCount = math.min(_currentConnectionCount, newConnectionCount) * 2;
        for (var i = 0; i < copyCount; i++)
        {
            newArray[i] = _nativeConnections[i];
        }

        // Dispose old array and assign new one
        if (_nativeConnections.IsCreated)
        {
            _nativeConnections.Dispose();
        }

        _nativeConnections = newArray;
        _currentConnectionCount = newConnectionCount;
    }


    private void UpdateConnectionPositionsNative()
    {
        if (!_usingNativeArray || !_nativeConnections.IsCreated) return;

        for (var i = 0; i < _currentConnectionCount; i++)
        {
            if (!connections[i].startNode || !connections[i].endNode || !connections[i].lineRenderer) continue;
            // Update the native array
            _nativeConnections[i * 2] = connections[i].startNode.transform.position;
            _nativeConnections[i * 2 + 1] = connections[i].endNode.transform.position;

            // Update line renderer
            connections[i].lineRenderer.SetPosition(0, _nativeConnections[i * 2]);
            connections[i].lineRenderer.SetPosition(1, _nativeConnections[i * 2 + 1]);
        }
    }

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
                        col.Highlight(color, duration, () => Destroy(col), emissionColor:emissionColor);
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
}