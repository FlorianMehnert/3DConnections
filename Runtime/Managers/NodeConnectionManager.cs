using System;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Color = UnityEngine.Color;

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
        if (rootEdgeTransform) return;
        var rootEdgeGameObject = GameObject.Find("ParentEdgesObject");
        rootEdgeTransform = rootEdgeGameObject.transform ? rootEdgeGameObject.transform : new GameObject("ParentEdgesObject").transform;
        ScriptableObjectInventory.Instance.edgeRoot = rootEdgeTransform;
    }

    private void Update()
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
        if (!ScriptableObjectInventory.InstanceExists) return;
        _isShuttingDown = true;
        try
        {
            if (ScriptableObjectInventory.Instance.conSo.usingNativeArray && ScriptableObjectInventory.Instance.conSo.NativeConnections.IsCreated)
            {
                ScriptableObjectInventory.Instance.conSo.NativeConnections.Dispose();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public NodeConnection AddConnection(GameObject startNode, GameObject endNode, Color? color = null, float lineWidth = 1f, float saturation = 1f, string connectionType = "parentChildConnection")
    {
        if (_isShuttingDown) return null;

        var lineObj = Instantiate(lineRendererPrefab, rootEdgeTransform);
        var lineRenderer = lineObj.GetComponent<LineRenderer>();
        lineObj.AddComponent<ArtificialGameObject>();
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

        newConnection.ApplyConnection();
        lineRenderer.positionCount = 2;

        ScriptableObjectInventory.Instance.conSo.connections.Add(newConnection);
        return newConnection;
    }

    private void UpdateConnectionPositions()
    {
        foreach (var connection in ScriptableObjectInventory.Instance.conSo.connections.Where(connection =>
                     connection.startNode && connection.endNode && connection.lineRenderer))
        {
            connection.lineRenderer.SetPosition(0, connection.startNode.transform.position);
            connection.lineRenderer.SetPosition(1, connection.endNode.transform.position);
        }
    }

    public static void ClearConnections()
    {
        var conSo = ScriptableObjectInventory.Instance.conSo;
        if (conSo && conSo.usingNativeArray && conSo.NativeConnections.IsCreated)
        {
            conSo.NativeConnections.Dispose();
            conSo.usingNativeArray = false;
        }

        try
        {
            if (!ScriptableObjectInventory.InstanceExists) return;
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
            var existingSprings = connection.startNode.GetComponents<SpringJoint2D>();

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

            var spring = (alreadyExists && springComponent) ? springComponent : alreadyExists ? null : connection.startNode.AddComponent<SpringJoint2D>();
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
        foreach (var spring in ScriptableObjectInventory.Instance.conSo.connections.Select(connection => connection.startNode.GetComponents<SpringJoint2D>())
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

        ScriptableObjectInventory.Instance.conSo.currentConnectionCount = ScriptableObjectInventory.Instance.conSo.connections.Count;
        if (ScriptableObjectInventory.Instance.conSo.currentConnectionCount == 0) return;

        // Create a new native array with the exact size needed
        ScriptableObjectInventory.Instance.conSo.NativeConnections = new NativeArray<float3>(ScriptableObjectInventory.Instance.conSo.currentConnectionCount * 2, Allocator.Persistent);

        // Copy existing connections to the native array
        for (var i = 0; i < ScriptableObjectInventory.Instance.conSo.currentConnectionCount; i++)
        {
            if (!ScriptableObjectInventory.Instance.conSo.connections[i].startNode || !ScriptableObjectInventory.Instance.conSo.connections[i].endNode) continue;
            ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2] = ScriptableObjectInventory.Instance.conSo.connections[i].startNode.transform.position;
            ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2 + 1] = ScriptableObjectInventory.Instance.conSo.connections[i].endNode.transform.position;
        }

        ScriptableObjectInventory.Instance.conSo.usingNativeArray = true;
    }

    public void UseNativeArray()
    {
        ScriptableObjectInventory.Instance.conSo.usingNativeArray = true;
    }

    // Call this when you want to resize the native array (e.g., when connections are added/removed)
    public void ResizeNativeArray()
    {
        if (!ScriptableObjectInventory.Instance.conSo.usingNativeArray) return;

        var newConnectionCount = ScriptableObjectInventory.Instance.conSo.connections.Count;
        if (newConnectionCount == ScriptableObjectInventory.Instance.conSo.currentConnectionCount) return;

        var newArray = new NativeArray<float3>(newConnectionCount * 2, Allocator.Persistent);

        // Copy existing data up to the smaller of the two sizes
        var copyCount = math.min(ScriptableObjectInventory.Instance.conSo.currentConnectionCount, newConnectionCount) * 2;
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
        if (!ScriptableObjectInventory.Instance.conSo.usingNativeArray || !ScriptableObjectInventory.Instance.conSo.NativeConnections.IsCreated) return;

        for (var i = 0; i < ScriptableObjectInventory.Instance.conSo.currentConnectionCount; i++)
        {
            if (!ScriptableObjectInventory.Instance.conSo.connections[i].startNode || !ScriptableObjectInventory.Instance.conSo.connections[i].endNode || !ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer) continue;
            // Update the native array
            ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2] = ScriptableObjectInventory.Instance.conSo.connections[i].startNode.transform.position;
            ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2 + 1] = ScriptableObjectInventory.Instance.conSo.connections[i].endNode.transform.position;

            // Update line renderer
            ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer.SetPosition(0, ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2]);
            ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer.SetPosition(1, ScriptableObjectInventory.Instance.conSo.NativeConnections[i * 2 + 1]);
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
                        col.Highlight(color, duration, actionAfterHighlight:() => Destroy(col), emissionColor: emissionColor);
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
        return ScriptableObjectInventory.Instance.conSo.connections.FirstOrDefault(connection => connection.startNode == start && connection.endNode == end);
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
            if (!ScriptableObjectInventory.Instance.conSo.usingNativeArray || !ScriptableObjectInventory.Instance.conSo.NativeConnections.IsCreated) return;

            for (var i = 0; i < ScriptableObjectInventory.Instance.conSo.currentConnectionCount; i++)
            {
                if (!ScriptableObjectInventory.Instance.conSo.connections[i].startNode || !ScriptableObjectInventory.Instance.conSo.connections[i].endNode || !ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer) continue;
                ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer.startWidth = ScriptableObjectInventory.Instance.conSo.connections[i].lineWidth; 
                ScriptableObjectInventory.Instance.conSo.connections[i].lineRenderer.endWidth = ScriptableObjectInventory.Instance.conSo.connections[i].lineWidth; 
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