using System;
using System.Drawing;
using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class ComputeSpringSimulation : MonoBehaviour, ILogable
{
    private static readonly int Nodes = Shader.PropertyToID("nodes");
    private static readonly int Connections = Shader.PropertyToID("connections");
    private static readonly int NodeCount = Shader.PropertyToID("nodeCount");
    private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
    private static readonly int NodeCount = Shader.PropertyToID("node_count");
    private static readonly int DeltaTime = Shader.PropertyToID("delta_time");
    private static readonly int Stiffness = Shader.PropertyToID("stiffness");
    private static readonly int ColliderRadius = Shader.PropertyToID("collider_radius");
    private static readonly int Damping = Shader.PropertyToID("damping");
    private static readonly int CollisionResponseStrength = Shader.PropertyToID("collision_response_strength");
    private static readonly int MinIntegrationTimestep = Shader.PropertyToID("min_integration_timestep");
    private static readonly int RelaxationFactor = Shader.PropertyToID("relaxation_factor");
    private static readonly int MaxVelocityLimit = Shader.PropertyToID("max_velocity_limit");
    private static readonly int ForceArrows = Shader.PropertyToID("force_arrows");
    private static readonly int EConstant = Shader.PropertyToID("eConstant");
    private static readonly int CollisionResponseStrength = Shader.PropertyToID("collisionResponseStrength");
    public ComputeShader computeShader;
    public NodeGraphScriptableObject nodeGraph;
    [SerializeField] private PhysicsSimulationConfiguration simConfig;

    private ComputeBuffer _nodeBuffer;
    private ComputeBuffer _forceArrowsBuffer;
    private ComputeBuffer _connectionBuffer;
    private Transform[] _nodes;
    private int _springKernel;
    private int _springConnectionsKernel;
    private int _collisionKernel;
    private int _integrationKernel;
    private int _forceArrowsKernel;

    private bool _isShuttingDown;
    [SerializeField] private RemovePhysicsEvent removePhysicsEvent;
    [SerializeField] private ClearEvent clearEvent;
    
    private static readonly int GoRestLength = Shader.PropertyToID("go_rest_length");
    private static readonly int GCRestLength = Shader.PropertyToID("gc_rest_length");
    private static readonly int CcRestLength = Shader.PropertyToID("cc_rest_length");
    
    // Radial layout parameters
    private static readonly int RadialDistance = Shader.PropertyToID("radial_distance");
    private static readonly int RadialAngleOffset = Shader.PropertyToID("radial_angle_offset");
    private static readonly int AngleSeparation = Shader.PropertyToID("angle_separation");
    
    [SerializeField] private float gameObjectRestLength = 2.0f;
    [SerializeField] private float gameObjectComponentRestLength = 0.5f;
    [SerializeField] private float componentRestLength = 1.0f;

    [Header("Radial Layout Settings")]
    [SerializeField] private float radialDistance = 1.5f;
    [SerializeField] private float radialAngleOffset;
    [SerializeField] private float minAngleSeparation = 0.1f;
    
    [Header("Force Arrow Settings")]
    [SerializeField] private bool showForceArrows = true;
    [SerializeField] private float arrowHeadSize = 0.2f;
    [SerializeField] private Color gameObjectArrowColor = Color.blue;
    [SerializeField] private Color componentArrowColor = Color.green;
    [SerializeField] private float minArrowAlpha = 0.2f;
    [SerializeField] private float maxArrowAlpha = 0.8f;
    
    [Header("Trail Settings")]
    [SerializeField] private bool showTrails = true;
    [SerializeField] private int trailHistoryLength = 30;
    [SerializeField] private Color gameObjectTrailColor = new Color(0.2f, 0.2f, 0.8f, 0.5f);
    [SerializeField] private Color componentTrailColor = new Color(0.2f, 0.8f, 0.2f, 0.5f);
    [SerializeField] private float trailWidth = 0.15f;
    private Vector2[][] _positionHistory;
    private int _currentHistoryIndex;
    private bool _historyFilled;

    [Header("Lerp relaxation settings")]
    [SerializeField] private bool enableRelaxation = true;
    [SerializeField] private float relaxationDuration = 2.0f;
    [SerializeField] private float relaxationStrength = 0.2f;
    [SerializeField] private float initialMaxVelocity = 0.5f;
    [SerializeField] private float finalMaxVelocity = 10.0f;
    
    [Header("Simulation settings")]
    [SerializeField] private float minIntegrationTimeStep = 0.01f;
    private float _relaxationTimer;

    // Arrow data structure to match compute shader
    private struct ArrowData
    {
        public float2 Start;
        public float2 End;
        public float Strength;
    }

    private ArrowData[] _arrowData;

    private void OnDisable()
    {
        _isShuttingDown = true;
        if (removePhysicsEvent != null)
            removePhysicsEvent.OnEventTriggered -= HandleEvent;
        if (clearEvent != null)
            clearEvent.OnEventTriggered -= HandleEvent;
    }

    private void OnEnable()
    {
        _isShuttingDown = false;
        if (removePhysicsEvent != null)
            removePhysicsEvent.OnEventTriggered += HandleEvent;
        if (clearEvent != null)
            clearEvent.OnEventTriggered += HandleEvent;
    }

    private struct NodeData
    {
        public float2 Position;
        public float2 PreviousPosition; // Added to store previous position
        public float2 Velocity;
        public float2 Force;
        public int NodeType;
        public int ParentId; // ID of parent GameObject (-1 if none)
    }

    // TODO: use to store spring forces/clusters based on node types
    // TODO: -> adjust strength also based on hierarchy level
    private struct NodeConnection
    {
        public int2 NodeIndex;
        public float ConnectionType;
        public int ConnectionHierarchy; // root connections are order 0, connections of root children are 1 and so on
    }

    private void OnDestroy()
    {
        _isShuttingDown = true;
        CleanupBuffers();
    }

    private void CleanupBuffers()
    {
        if (_nodeBuffer != null)
        {
            _nodeBuffer.Release();
            _nodeBuffer = null;
        }

        if (_forceArrowsBuffer != null)
        {
            _forceArrowsBuffer.Release();
            _forceArrowsBuffer = null;
        }
        
        _arrowData = null;
        _positionHistory = null;
        _historyFilled = false;
        _currentHistoryIndex = 0;
    }

    public void Initialize()
    {
        CleanupBuffers();

        _nodes = nodeGraph.AllNodeTransforms2D;
        if (_nodes.Length == 0)
        {
            Debug.Log("no nodes while trying to create compute buffer");
            return;
        }

        // Reset relaxation timer when initializing
        _relaxationTimer = 0.0f;

        // Initialize position history buffer
        _positionHistory = new Vector2[trailHistoryLength][];
        for (int i = 0; i < trailHistoryLength; i++)
        {
            _positionHistory[i] = new Vector2[_nodes.Length];
        }
        _currentHistoryIndex = 0;
        _historyFilled = false;

        // Get kernel IDs
        _springKernel = computeShader.FindKernel("SpringForces");
        _springConnectionsKernel = computeShader.FindKernel("SpringForcesConnectionBased");
        _collisionKernel = computeShader.FindKernel("CollisionResponse");
        _integrationKernel = computeShader.FindKernel("IntegrateForces");
        _forceArrowsKernel = computeShader.FindKernel("calculate_force_arrows");

        
        // Create a map of GameObject instances to their index in the nodes array
        var gameObjectIndices = new Dictionary<GameObject, int>();
        for (var i = 0; i < _nodes.Length; i++)
        {
            var nodeObj = nodeGraph.AllNodes[i];
            var nodeTypeComponent = nodeObj.GetComponent<NodeType>();
            if (nodeTypeComponent != null && nodeTypeComponent.GetNodeType() == 0) // If it's a GameObject
            {
                gameObjectIndices[nodeObj] = i;
            }
        }

        var nodeData = new NodeData[_nodes.Length];
        for (var i = 0; i < _nodes.Length; i++)
        {
            var nodeObj = nodeGraph.AllNodes[i];
            var nodeType = 0; // Default to GameObject
            var parentId = -1; // Default to no parent
            
            // Get the actual component to check its type
            var nodeTypeComponent = nodeObj.GetComponent<NodeType>();
            if (nodeTypeComponent)
            {
                nodeType = nodeTypeComponent.GetNodeType();
                
                // If this is a Component node, find its parent GameObject
                if (nodeType == 1) // Component
                {
                    var localNodeConnections = nodeObj.GetComponent<LocalNodeConnections>();
                    if (localNodeConnections)
                    {
                        var parentObject = nodeTypeComponent.GetParentOfComponentNode(); // You'd need to implement this method
                        var found = gameObjectIndices.TryGetValue(parentObject, out var parentIndex);
                        if (parentObject && found)
                        {
                            parentId = parentIndex;
                        }                        
                    }
                }
            }

            var currentPosition = new float2(_nodes[i].position.x, _nodes[i].position.y);
            
            nodeData[i] = new NodeData
            {
                Position = currentPosition,
                PreviousPosition = currentPosition, // Initialize previous position to current
                Velocity = float2.zero,
                Force = float2.zero,
                NodeType = nodeType,
                ParentId = parentId
            };
            
            // Initialize history with current positions
            for (int h = 0; h < trailHistoryLength; h++)
            {
                _positionHistory[h][i] = currentPosition;
            }
        }

        // Create node amount squared node connection information
        var nodeConnections = new NodeConnection[(int)Math.Pow(_nodes.Length, 2)];
        for (var i = 0; i < _nodes.Length; i++)
        {
            for (var j = 0; j < _nodes.Length; j++)
            {
                nodeConnections[i] = new NodeConnection
                {
                    NodeIndex = new int2(i, j),
                    ConnectionType = ConnectionStrength(i, j),
                    ConnectionHierarchy = GetHierarchyDepth(_nodes[i]),
                };
            }
        }

        // Create buffer with space for the previous position field
        _nodeBuffer = new ComputeBuffer(_nodes.Length, sizeof(float) * 8 + sizeof(int) * 2); // 8 floats (2 positions, velocity, force) + 2 ints
        _nodeBuffer.SetData(nodeData);
        _connectionBuffer = new ComputeBuffer((int)Math.Pow(_nodes.Length, 2), sizeof(int) * 3 + sizeof(float));
        _connectionBuffer.SetData(nodeConnections);

        // Initialize arrow data and buffer
        _arrowData = new ArrowData[_nodes.Length];
        _forceArrowsBuffer = new ComputeBuffer(_nodes.Length, sizeof(float) * 5); // Start(2) + End(2) + Strength(1)
        _forceArrowsBuffer.SetData(_arrowData);

        // Set buffer for all kernels
        computeShader.SetBuffer(_springKernel, Nodes, _nodeBuffer);
        computeShader.SetBuffer(_collisionKernel, Nodes, _nodeBuffer);
        computeShader.SetBuffer(_integrationKernel, Nodes, _nodeBuffer);
        computeShader.SetBuffer(_springConnectionsKernel, Connections, _connectionBuffer);
        computeShader.SetBuffer(_forceArrowsKernel, Nodes, _nodeBuffer);
        computeShader.SetBuffer(_forceArrowsKernel, ForceArrows, _forceArrowsBuffer);

        var types = new List<System.Type>
        {
            typeof(SpringJoint2D),
            typeof(Rigidbody2D)
        };
        nodeGraph.NodesRemoveComponents(types);
        _isShuttingDown = false;
        computeShader.SetFloat(EConstant, (float)Math.E);
    }

    // TODO: return connection strength based on hierarchy level and node types
    private float ConnectionStrength(int i, int j)
    {
        // 1. get nodes
        var type1 = _nodes[i].gameObject.GetComponent<NodeType>().nodeTypeName;
        var type2 = _nodes[j].gameObject.GetComponent<NodeType>().nodeTypeName;

        // 2. get connection type
        // gogo = 0.1
        // goco = 10
        // coco = ? -> 1
        // coso = 0.5
        // soso = 0.5
        const float referenceStrength = 0.01f;
        const float parentChildStrength = .1f;
        switch (type1)
        {
            case NodeTypeName.GameObject when type2 == NodeTypeName.GameObject:
                return 0.001f;
            case NodeTypeName.GameObject when type2 == NodeTypeName.Component:
            case NodeTypeName.Component when type2 == NodeTypeName.GameObject:
                return parentChildStrength;
            case NodeTypeName.Component when type2 == NodeTypeName.Component:
                return .0001f;
            case NodeTypeName.GameObject when type2 == NodeTypeName.ScriptableObject:
            case NodeTypeName.Component when type2 == NodeTypeName.ScriptableObject:
            case NodeTypeName.ScriptableObject when type2 == NodeTypeName.GameObject:
            case NodeTypeName.ScriptableObject when type2 == NodeTypeName.Component:
            case NodeTypeName.ScriptableObject when type2 == NodeTypeName.ScriptableObject:
                return referenceStrength;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private int GetHierarchyDepth(Transform transform)
    {
        var depth = 0;
        while (transform.parent)
        {
            depth++;
            transform = transform.parent;
        }

        return depth;
    }

    private void Update()
    {
        if (_nodeBuffer == null || !Application.isPlaying || _isShuttingDown) return;
        var deltaTime = Time.deltaTime;

        // Update relaxation timer if enabled
        float currentRelaxFactor = 1.0f;
        float currentMaxVelocity = finalMaxVelocity;
        
        if (enableRelaxation)
        {
            _relaxationTimer += deltaTime;
            if (_relaxationTimer < relaxationDuration)
            {
                // Gradually increase from initial relaxation strength to 1.0
                var t = _relaxationTimer / relaxationDuration;
                currentRelaxFactor = Mathf.Lerp(relaxationStrength, 1.0f, t);
                currentMaxVelocity = Mathf.Lerp(initialMaxVelocity, finalMaxVelocity, t);
            }
        }

        // Update shader parameters
        computeShader.SetInt(NodeCount, _nodes.Length);
        computeShader.SetFloat(DeltaTime, deltaTime);
        computeShader.SetFloat(Stiffness, simConfig.Stiffness);
        computeShader.SetFloat(Damping, simConfig.damping);
        computeShader.SetFloat(ColliderRadius, simConfig.colliderRadius);
        computeShader.SetFloat(CollisionResponseStrength, simConfig.CollisionResponseStrength);
        computeShader.SetFloat(MinIntegrationTimestep, minIntegrationTimeStep);
        computeShader.SetFloat(RelaxationFactor, currentRelaxFactor);
        computeShader.SetFloat(MaxVelocityLimit, currentMaxVelocity);
        
        computeShader.SetFloat(GoRestLength, gameObjectRestLength);
        computeShader.SetFloat(GCRestLength, gameObjectComponentRestLength);
        computeShader.SetFloat(CcRestLength, componentRestLength);
        
        // Set radial layout parameters
        computeShader.SetFloat(RadialDistance, radialDistance);
        computeShader.SetFloat(RadialAngleOffset, radialAngleOffset * Mathf.Deg2Rad); // Convert to radians
        computeShader.SetFloat(AngleSeparation, minAngleSeparation * Mathf.Deg2Rad); // Convert to radians

        // Calculate thread groups
        var threadGroups = Mathf.CeilToInt(_nodes.Length / 64f);
        var threadGroupsConnections = Mathf.CeilToInt(Mathf.Pow(_nodes.Length, 2) / 64f);

        // Dispatch compute shader
        computeShader.Dispatch(_springKernel, threadGroups, 1, 1);
        computeShader.Dispatch(_springConnectionsKernel, threadGroupsConnections, 1, 1);
        computeShader.Dispatch(_collisionKernel, threadGroups, 1, 1);
        computeShader.Dispatch(_integrationKernel, threadGroups, 1, 1);
        
        // Calculate force arrows if enabled
        if (showForceArrows)
        {
            computeShader.Dispatch(_forceArrowsKernel, threadGroups, 1, 1);
            _forceArrowsBuffer.GetData(_arrowData);
        }

        // Read back results and update transforms
        var nodeData = new NodeData[_nodes.Length];
        _nodeBuffer.GetData(nodeData);

        // Store current positions in history before updating
        if (showTrails && _positionHistory != null)
        {
            _currentHistoryIndex = (_currentHistoryIndex + 1) % trailHistoryLength;
            if (_currentHistoryIndex == 0)
            {
                _historyFilled = true;
            }
            
            for (var i = 0; i < _nodes.Length; i++)
            {
                _positionHistory[_currentHistoryIndex][i] = new Vector2(nodeData[i].Position.x, nodeData[i].Position.y);
            }
        }

        for (var i = 0; i < _nodes.Length; i++)
        {
            if (_nodes[i])
            {
                _nodes[i].position = new Vector3(
                    nodeData[i].Position.x,
                    nodeData[i].Position.y,
                    _nodes[i].position.z
                );
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (_isShuttingDown || _nodes == null) return;
        
        // Draw force arrows
        if (showForceArrows && _arrowData != null)
        {
            for (int i = 0; i < _arrowData.Length; i++)
            {
                if (i >= _nodes.Length) continue;
                
                var start = new Vector3(_arrowData[i].Start.x, _arrowData[i].Start.y, _nodes[i].position.z);
                var end = new Vector3(_arrowData[i].End.x, _arrowData[i].End.y, _nodes[i].position.z);
                
                // Determine arrow color based on node type
                Color arrowColor = i < _nodes.Length && nodeGraph.AllNodes[i].GetComponent<NodeType>()?.GetNodeType() == 1 
                    ? componentArrowColor 
                    : gameObjectArrowColor;
                
                // Scale alpha based on force strength
                float normalizedStrength = Mathf.Clamp01(_arrowData[i].Strength / 10.0f); // Adjust divisor as needed
                arrowColor.a = Mathf.Lerp(minArrowAlpha, maxArrowAlpha, normalizedStrength);
                
                // Draw arrow line
                Gizmos.color = arrowColor;
                Gizmos.DrawLine(start, end);
                
                // Draw arrow head if line is long enough
                Vector3 direction = end - start;
                float magnitude = direction.magnitude;
                
                if (magnitude > 0.1f)
                {
                    direction.Normalize();
                    Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
                    
                    // Draw arrowhead
                    Gizmos.DrawLine(end, end - direction * arrowHeadSize + right * arrowHeadSize * 0.5f);
                    Gizmos.DrawLine(end, end - direction * arrowHeadSize - right * arrowHeadSize * 0.5f);
                }
            }
        }
        
        // Draw position history trails
        if (!showTrails || _positionHistory == null || _nodes == null) return;
        {
            var historyCount = _historyFilled ? trailHistoryLength : _currentHistoryIndex;
            
            for (var i = 0; i < _nodes.Length; i++)
            {
                if (nodeGraph.AllNodes[i] == null) continue;
                
                // Get node type to determine trail color
                var isComponent = nodeGraph.AllNodes[i].GetComponent<NodeType>()?.GetNodeType() == 1;
                var trailColor = isComponent ? componentTrailColor : gameObjectTrailColor;
                
                for (var h = 1; h < historyCount; h++)
                {
                    var currentIndex = (_currentHistoryIndex - h + trailHistoryLength) % trailHistoryLength;
                    var prevIndex = (_currentHistoryIndex - h + 1 + trailHistoryLength) % trailHistoryLength;
                    
                    // Fade the trail as it gets older
                    var alpha = trailColor.a * (1.0f - (float)h / historyCount);
                    var fadedColor = trailColor;
                    fadedColor.a = alpha;
                    Gizmos.color = fadedColor;
                    
                    var current = new Vector3(_positionHistory[currentIndex][i].x, _positionHistory[currentIndex][i].y, _nodes[i].position.z);
                    var prev = new Vector3(_positionHistory[prevIndex][i].x, _positionHistory[prevIndex][i].y, _nodes[i].position.z);
                    
                    // Adjust width based on distance from current position
                    var widthScale = 1.0f - (float)h / historyCount;
                    var segmentWidth = trailWidth * widthScale;
                    
                    // Draw line segment
                    Gizmos.DrawLine(current, prev);
                }
            }
        }
    }
    
    private void HandleEvent()
    {
        _isShuttingDown = true;
        CleanupBuffers();
    }

    public string GetStatus()
    {
        return "enabled keywords: " + computeShader.enabledKeywords.Length + " nodes: " + _nodes.Length +
               " nodeBuffer is " + (_nodeBuffer != null ? " not null " + _nodeBuffer.count : " null");
        var relaxationStatus = enableRelaxation ? 
            $" (Relaxation: {(_relaxationTimer < relaxationDuration ? $"active {_relaxationTimer:F1}/{relaxationDuration:F1}" : "complete")})" : 
            " (Relaxation: disabled)";

        return "enabled keywords: " + computeShader.enabledKeywords.Length +
               " nodes: " + _nodes.Length +
               " nodeBuffer is " + (_nodeBuffer != null ? " not null " + _nodeBuffer.count : " null") +
               relaxationStatus;
    }
}

// Helper class to associate components with their parent GameObjects
public static class NodeTypeExtensions
{
    public static GameObject GetParentOfComponentNode(this NodeType nodeType)
    {
        if (!nodeType || nodeType.nodeTypeName != "Component") return null;
        return ((Component)nodeType.reference).gameObject;
    }
}