using UnityEngine;
using System.Collections;
using Unity.Mathematics;

public class NodeRenderingSystem : MonoBehaviour
{
    private static readonly int NodeBuffer = Shader.PropertyToID("NodeBuffer");
    private static readonly int NodeCount = Shader.PropertyToID("NodeCount");
    private static readonly int SpawnRadius = Shader.PropertyToID("SpawnRadius");
    private static readonly int SpawnProgress = Shader.PropertyToID("SpawnProgress");

    // Existing configurations
    public ComputeShader nodeComputeShader;
    public Shader nodeRenderShader;
    public Material nodeMaterial;

    [Header("Node Spawning Settings")]
    public int nodeCount = 10000;
    public float spawnRadius = 10f;
    public float spawnTime = 5f;
    public bool autoStart;

    // New spawn control variables
    private bool _isSpawning;
    private float _spawnProgress;

    // Compute buffer and kernel indices
    private ComputeBuffer _nodeBuffer;
    private int _initializeKernel;
    private int _updateKernel;

    // Node data structure (same as before)
    private struct NodeData
    {
        public float3 Position;
        public float3 Velocity;
        public float4 Color;
        public float Size;
        public int NodeType;
        public int CustomDataIndex;
    }

    void Start()
    {
        InitializeComputeShader();

        // Start spawning if autoStart is enabled
        if (autoStart)
        {
            StartNodeSpawn();
        }
    }

    void InitializeComputeShader()
    {
        // Ensure minimum node count
        nodeCount = Mathf.Max(nodeCount, 64);

        _nodeBuffer = new ComputeBuffer(nodeCount, System.Runtime.InteropServices.Marshal.SizeOf(typeof(NodeData)));
        _initializeKernel = nodeComputeShader.FindKernel("InitializeNodes");
        _updateKernel = nodeComputeShader.FindKernel("UpdateNodes");

        nodeComputeShader.SetBuffer(_initializeKernel, NodeBuffer, _nodeBuffer);
        nodeComputeShader.SetBuffer(_updateKernel, NodeBuffer, _nodeBuffer);

        nodeComputeShader.SetInt(NodeCount, nodeCount);
        nodeComputeShader.SetFloat(SpawnRadius, spawnRadius);
    }

    // Public method to start spawning nodes
    private void StartNodeSpawn()
    {
        if (!_isSpawning)
        {
            StartCoroutine(SpawnNodesOverTime());
        }
    }

    // Spawn nodes gradually over specified time
    private IEnumerator SpawnNodesOverTime()
    {
        _isSpawning = true;
        _spawnProgress = 0f;

        while (_spawnProgress < 1f)
        {
            // Safely calculate nodes to spawn
            int nodesToSpawn = Mathf.CeilToInt(nodeCount * _spawnProgress);
            
            // Ensure at least minimum thread group size
            int threadGroupSize = Mathf.Max(1, Mathf.CeilToInt(nodesToSpawn / 64f));
            
            // Set progress in shader
            nodeComputeShader.SetFloat(SpawnProgress, _spawnProgress);

            // Dispatch initialization safely
            nodeComputeShader.Dispatch(_initializeKernel, threadGroupSize, 1, 1);

            // Increment progress
            _spawnProgress += Time.deltaTime / spawnTime;
            yield return null;
        }

        // Final dispatch with safe thread group calculation
        int finalThreadGroupSize = Mathf.Max(1, Mathf.CeilToInt(nodeCount / 64f));
        nodeComputeShader.Dispatch(_initializeKernel, finalThreadGroupSize, 1, 1);

        _isSpawning = false;
    }

    // Method to manually trigger node spawn from another script
    public void TriggerNodeSpawn(int count, float radius)
    {
        // Ensure minimum node count
        nodeCount = Mathf.Max(count, 64);
        spawnRadius = radius;

        // Reinitialize compute shader with new count
        InitializeComputeShader();
        StartNodeSpawn();
    }

    private void Update()
    {
        if (_isSpawning) return;
        // Normal update logic
        var threadGroupSize = Mathf.CeilToInt(nodeCount / 64f);
        nodeComputeShader.Dispatch(_updateKernel, threadGroupSize, 1, 1);
    }

    private void OnRenderObject()
    {
        if (_nodeBuffer == null) return;
        nodeMaterial.SetBuffer(NodeBuffer, _nodeBuffer);
        nodeMaterial.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Points, nodeCount);
    }

    void OnDestroy()
    {
        // Clean up compute buffer
        if (_nodeBuffer != null)
            _nodeBuffer.Release();
    }
}