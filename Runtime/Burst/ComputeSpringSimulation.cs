using UnityEngine;
using Unity.Mathematics;

public class ComputeSpringSimulation : MonoBehaviour, ILogable
{
    private static readonly int Nodes = Shader.PropertyToID("nodes");
    private static readonly int NodeCount = Shader.PropertyToID("nodeCount");
    private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
    private static readonly int Stiffness = Shader.PropertyToID("stiffness");
    private static readonly int ColliderRadius = Shader.PropertyToID("colliderRadius");
    private static readonly int Damping = Shader.PropertyToID("damping");
    private static readonly int CollisionResponseStrength = Shader.PropertyToID("collisionResponseStrength");
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
    
    private static readonly int GORestLength = Shader.PropertyToID("goRestLength");
    private static readonly int GCRestLength = Shader.PropertyToID("gcRestLength");
    private static readonly int CCRestLength = Shader.PropertyToID("ccRestLength");
    
    [SerializeField] private float gameObjectRestLength = 2.0f;
    [SerializeField] private float gameObjectComponentRestLength = 0.5f;
    [SerializeField] private float componentRestLength = 1.0f;


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
    }

    private void OnDestroy()
    {
        _isShuttingDown = true;
        CleanupBuffers();
    }

    public void CleanupBuffers()
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

        // Get kernel IDs
        _springKernel = computeShader.FindKernel("SpringForces");
        _collisionKernel = computeShader.FindKernel("CollisionResponse");
        _integrationKernel = computeShader.FindKernel("IntegrateForces");

        var nodeData = new NodeData[_nodes.Length];
        for (var i = 0; i < _nodes.Length; i++)
        {
            var nodeType = 0; // Default to GameObject
            // Get the actual component to check its type
            var nodeTypeComponent = nodeGraph.AllNodes[i].GetComponent<NodeType>();
            if (nodeTypeComponent != null)
            {
                nodeType = nodeTypeComponent.GetNodeType(); // Assuming NodeType has a 'type' field
            }

            nodeData[i] = new NodeData
            {
                Position = new float2(_nodes[i].position.x, _nodes[i].position.y),
                Velocity = float2.zero,
                Force = float2.zero,
                NodeType = nodeType
            };
        }

        _nodeBuffer = new ComputeBuffer(_nodes.Length, sizeof(float) * 6 + sizeof(int)); // Added int for NodeType
        _nodeBuffer.SetData(nodeData);

        // Set buffer for all kernels
        computeShader.SetBuffer(_springKernel, Nodes, _nodeBuffer);
        computeShader.SetBuffer(_collisionKernel, Nodes, _nodeBuffer);
        computeShader.SetBuffer(_integrationKernel, Nodes, _nodeBuffer);

        var types = new System.Collections.Generic.List<System.Type>
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

        // Update shader parameters
        computeShader.SetInt(NodeCount, _nodes.Length);
        computeShader.SetFloat(DeltaTime, deltaTime);
        computeShader.SetFloat(Stiffness, simConfig.Stiffness);
        computeShader.SetFloat(Damping, simConfig.damping);
        computeShader.SetFloat(ColliderRadius, simConfig.colliderRadius);
        computeShader.SetFloat(CollisionResponseStrength, simConfig.CollisionResponseStrength);
        
        computeShader.SetFloat(GORestLength, gameObjectRestLength);
        computeShader.SetFloat(GCRestLength, gameObjectComponentRestLength);
        computeShader.SetFloat(CCRestLength, componentRestLength);

        // Calculate thread groups
        var threadGroups = Mathf.CeilToInt(_nodes.Length / 64f);

        // Dispatch compute shader
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

    public void Status()
    {
        Debug.Log(_nodeBuffer == null || !Application.isPlaying || _isShuttingDown);
    }

    public string GetStatus()
    {
        return "enabled keywords: " + computeShader.enabledKeywords.Length + " nodes: " + _nodes.Length + " nodeBuffer is " + (_nodeBuffer != null ? " not null " + _nodeBuffer.count : " null");
    }
}