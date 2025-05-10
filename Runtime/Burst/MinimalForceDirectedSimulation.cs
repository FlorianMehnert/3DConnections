// Updated C# Script
using UnityEngine;

public class MinimalForceDirectedSimulation : MonoBehaviour
{
    public ComputeShader computeShader;
    public Transform[] nodeTransforms;
    
    [SerializeField] private RemovePhysicsEvent removePhysicsEvent;
    [SerializeField] private ClearEvent clearEvent;
    [SerializeField] private int simulationStepsPerFrame = 10; // Number of simulation steps to run per frame

    private ComputeBuffer _nodeBuffer;
    private int _kernel;
    private bool _isShuttingDown;
    private static readonly int Nodes = Shader.PropertyToID("nodes");
    private static readonly int NodeCount = Shader.PropertyToID("node_count");
    private static readonly int DeltaTime = Shader.PropertyToID("delta_time");
    private static readonly int RepulsionStrength = Shader.PropertyToID("repulsion_strength");
    private static readonly int AttractionStrength = Shader.PropertyToID("attraction_strength");
    private static readonly int StepsPerDispatch = Shader.PropertyToID("steps_per_dispatch");

    private struct NodeData
    {
        public Vector2 Position;
        public Vector2 Velocity;
    }

    private void Start()
    {
        _kernel = computeShader.FindKernel("CSMain");
    }
    
    private void OnDisable()
    {
        _isShuttingDown = true;
        if (removePhysicsEvent != null)
            removePhysicsEvent.OnEventTriggered -= HandleEvent;
        if (clearEvent != null)
            clearEvent.OnEventTriggered -= HandleEvent;
    }

    public void Initialize()
    {
        var data = new NodeData[nodeTransforms.Length];
        
        Debug.Log(nodeTransforms.Length);
        for (var i = 0; i < nodeTransforms.Length; i++)
        {
            Vector2 pos = nodeTransforms[i].position;
            data[i] = new NodeData { Position = pos, Velocity = Vector2.zero };
        }

        _nodeBuffer = new ComputeBuffer(data.Length, sizeof(float) * 4);
        _nodeBuffer.SetData(data);

        computeShader.SetBuffer(_kernel, Nodes, _nodeBuffer);
        computeShader.SetInt(NodeCount, data.Length);
        computeShader.SetInt(StepsPerDispatch, simulationStepsPerFrame);
    }
    
    private void HandleEvent()
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

    private void OnEnable()
    {
        _isShuttingDown = false;
        if (removePhysicsEvent != null)
            removePhysicsEvent.OnEventTriggered += HandleEvent;
        if (clearEvent != null)
            clearEvent.OnEventTriggered += HandleEvent;
    }

    private void Update()
    {
        if (_nodeBuffer == null || !Application.isPlaying || _isShuttingDown) return;
        
        // Set simulation parameters
        float timeStep = Time.deltaTime / simulationStepsPerFrame; // Smaller time step for stability
        computeShader.SetFloat(DeltaTime, timeStep);
        computeShader.SetFloat(RepulsionStrength, 100.0f);
        computeShader.SetFloat(AttractionStrength, 0.01f);

        // Dispatch compute shader
        var threadGroups = Mathf.CeilToInt(nodeTransforms.Length / 64f);
        computeShader.Dispatch(_kernel, threadGroups, 1, 1);

        // Read back results only once per frame
        var nodeData = new NodeData[nodeTransforms.Length];
        _nodeBuffer.GetData(nodeData);

        // Update positions
        for (var i = 0; i < nodeTransforms.Length; i++)
        {
            nodeTransforms[i].position = nodeData[i].Position;
        }
    }

    private void OnDestroy()
    {
        _nodeBuffer?.Release();
    }
}