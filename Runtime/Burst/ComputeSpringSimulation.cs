using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

public class ComputeSpringSimulation : MonoBehaviour, ILogable
{
    private static readonly int Nodes = Shader.PropertyToID("nodes");
    private static readonly int NodeCount = Shader.PropertyToID("node_count");
    private static readonly int DeltaTime = Shader.PropertyToID("delta_time");
    private static readonly int Stiffness = Shader.PropertyToID("stiffness");
    private static readonly int ColliderRadius = Shader.PropertyToID("collider_radius");
    private static readonly int Damping = Shader.PropertyToID("damping");
    private static readonly int CollisionResponseStrength = Shader.PropertyToID("collision_response_strength");
    private static readonly int MinIntegrationTimestep = Shader.PropertyToID("min_integration_timestep");
    private static readonly int RelaxationFactor = Shader.PropertyToID("relaxation_factor");
    private static readonly int MaxVelocityLimit = Shader.PropertyToID("max_velocity_limit");
    public ComputeShader computeShader;
    public NodeGraphScriptableObject nodeGraph;
    [SerializeField] private PhysicsSimulationConfiguration simConfig; 

    private ComputeBuffer _nodeBuffer;
    private Transform[] _nodes;
    private int _springKernel;
    private int _collisionKernel;
    private int _integrationKernel;

    private bool _isShuttingDown;
    [SerializeField] private RemovePhysicsEvent removePhysicsEvent;
    [SerializeField] private ClearEvent clearEvent;
    
    private static readonly int GoRestLength = Shader.PropertyToID("go_rest_length");
    private static readonly int GCRestLength = Shader.PropertyToID("gc_rest_length");
    private static readonly int CcRestLength = Shader.PropertyToID("cc_rest_length");
    
    [SerializeField] private float gameObjectRestLength = 2.0f;
    [SerializeField] private float gameObjectComponentRestLength = 0.5f;
    [SerializeField] private float componentRestLength = 1.0f;

    [Header("Lerp relaxation settings")]
    [SerializeField] private bool enableRelaxation = true;
    [SerializeField] private float relaxationDuration = 2.0f;
    [SerializeField] private float relaxationStrength = 0.2f;
    [SerializeField] private float initialMaxVelocity = 0.5f;
    [SerializeField] private float finalMaxVelocity = 10.0f;
    
    [Header("Simulation settings")]
    [SerializeField] private float minIntegrationTimeStep = 0.01f;
    private float _relaxationTimer;


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
        public float2 Velocity;
        public float2 Force;
        public int NodeType;
        public int ParentId; // ID of parent GameObject (-1 if none)
    }

    private void OnDestroy()
    {
        _isShuttingDown = true;
        CleanupBuffers();
    }

    private void CleanupBuffers()
    {
        if (_nodeBuffer == null) return;
        _nodeBuffer.Release();
        _nodeBuffer = null;
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

        // Get kernel IDs
        _springKernel = computeShader.FindKernel("spring_forces");
        _collisionKernel = computeShader.FindKernel("collision_response");
        _integrationKernel = computeShader.FindKernel("integrate_forces");

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

            nodeData[i] = new NodeData
            {
                Position = new float2(_nodes[i].position.x, _nodes[i].position.y),
                Velocity = float2.zero,
                Force = float2.zero,
                NodeType = nodeType,
                ParentId = parentId
            };
        }

        // Create buffer with space for the additional ParentId field
        _nodeBuffer = new ComputeBuffer(_nodes.Length, sizeof(float) * 6 + sizeof(int) * 2);
        _nodeBuffer.SetData(nodeData);

        // Set buffer for all kernels
        computeShader.SetBuffer(_springKernel, Nodes, _nodeBuffer);
        computeShader.SetBuffer(_collisionKernel, Nodes, _nodeBuffer);
        computeShader.SetBuffer(_integrationKernel, Nodes, _nodeBuffer);

        var types = new List<System.Type>
        {
            typeof(SpringJoint2D),
            typeof(Rigidbody2D)
        };
        nodeGraph.NodesRemoveComponents(types);
        _isShuttingDown = false;
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

        // Calculate thread groups
        var threadGroups = Mathf.CeilToInt(_nodes.Length / 64f);

        // Dispatch computes shader
        computeShader.Dispatch(_springKernel, threadGroups, 1, 1);
        computeShader.Dispatch(_collisionKernel, threadGroups, 1, 1);
        computeShader.Dispatch(_integrationKernel, threadGroups, 1, 1);

        // Read back results and update transforms
        var nodeData = new NodeData[_nodes.Length];
        _nodeBuffer.GetData(nodeData);

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

    private void HandleEvent()
    {
        CleanupBuffers();
    }

    public string GetStatus()
    {
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