using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

public class ForceDirectedLayoutV2 : MonoBehaviour
{
    [Header("References")]
    public float repulsionStrength = 10f;
    public float attractionStrength = 0.1f;
    public float dampingFactor = 0.9f;
    public float minDistanceToRepel = 1f;
    public float updateInterval = 0.02f; // Time in seconds between layout updates

    private List<GameObject> _nodes;
    private NativeArray<float3> _positions;
    private NativeArray<float3> _velocities;
    private NativeArray<int> _connectionStartIndices;
    private NativeArray<int> _connectionEndIndices;
    private float _timer;
    public bool activated = true;
    
    private bool _currentlyCalculating;

    public void Initialize()
    {
        _currentlyCalculating = false;
        _nodes = ScriptableObjectInventory.Instance.graph.AllNodes;

        // Dispose of existing native arrays if they exist
        DisposeNativeArrays();

        // Create new native arrays
        _positions = new NativeArray<float3>(_nodes.Count, Allocator.Persistent);
        _velocities = new NativeArray<float3>(_nodes.Count, Allocator.Persistent);

        // Initialize positions and velocities
        for (int i = 0; i < _nodes.Count; i++)
        {
            _positions[i] = _nodes[i].transform.position;
            _velocities[i] = float3.zero;
        }

        // Create connection arrays
        var connections = ScriptableObjectInventory.Instance.conSo.connections;
        _connectionStartIndices = new NativeArray<int>(connections.Count, Allocator.Persistent);
        _connectionEndIndices = new NativeArray<int>(connections.Count, Allocator.Persistent);

        for (int i = 0; i < connections.Count; i++)
        {
            _connectionStartIndices[i] = _nodes.IndexOf(connections[i].startNode);
            _connectionEndIndices[i] = _nodes.IndexOf(connections[i].endNode);
        }

        activated = true;
    }
    
    private void OnEnable()
    {
        activated = true;
        if (_nodes == null || _nodes.Count == 0)
            Initialize();

        if (ScriptableObjectInventory.Instance.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered += HandleEvent;
        if (ScriptableObjectInventory.Instance.clearEvent)
            ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered += HandleEvent;
    }

    private void Update()
    {
        if (!activated || _currentlyCalculating) return;
        
        _timer += Time.deltaTime;
        if (!(_timer >= updateInterval)) return;
        _timer -= updateInterval;

        _currentlyCalculating = true;
        for (var i = 0; i < _nodes.Count; i++)
        {
            _positions[i] = _nodes[i].transform.position;
        }
        
        // Schedule and run the force calculation job
        CalculateForces();
        
        for (var i = 0; i < _nodes.Count; i++)
        {
            _nodes[i].transform.position = _positions[i];
        }

        _currentlyCalculating = false;
    }

    private void CalculateForces()
    {
        var forceCalculationJob = new ForceCalculationJob
        {
            Positions = _positions,
            Velocities = _velocities,
            ConnectionStartIndices = _connectionStartIndices,
            ConnectionEndIndices = _connectionEndIndices,
            RepulsionStrength = repulsionStrength,
            AttractionStrength = attractionStrength,
            DampingFactor = dampingFactor,
            MinDistanceToRepel = minDistanceToRepel,
            DeltaTime = updateInterval
        };

        // Execute the job immediately on the main thread (for simplicity)
        // For better performance in larger graphs, you could use Schedule() and complete later
        forceCalculationJob.Run();
    }

    private void OnDisable()
    {
        if (ScriptableObjectInventory.Instance.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered -= HandleEvent;
        if (ScriptableObjectInventory.Instance.clearEvent)
            ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered -= HandleEvent;
            
        DisposeNativeArrays();
    }
    
    private void HandleEvent()
    {
        activated = false;
    }
    
    private void OnDestroy()
    {
        DisposeNativeArrays();
    }
    
    private void DisposeNativeArrays()
    {
        if (_positions.IsCreated) _positions.Dispose();
        if (_velocities.IsCreated) _velocities.Dispose();
        if (_connectionStartIndices.IsCreated) _connectionStartIndices.Dispose();
        if (_connectionEndIndices.IsCreated) _connectionEndIndices.Dispose();
    }

    [BurstCompile]
    private struct ForceCalculationJob : IJob
    {
        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<int> ConnectionStartIndices;
        public NativeArray<int> ConnectionEndIndices;
        
        public float RepulsionStrength;
        public float AttractionStrength;
        public float DampingFactor;
        public float MinDistanceToRepel;
        public float DeltaTime;

        public void Execute()
        {
            int nodeCount = Positions.Length;
            
            // Temporary array for storing forces
            var forces = new NativeArray<float3>(nodeCount, Allocator.Temp);
            
            // Calculate repulsive forces
            for (int i = 0; i < nodeCount; i++)
            {
                for (int j = i + 1; j < nodeCount; j++)
                {
                    float3 direction = Positions[i] - Positions[j];
                    float distance = math.length(direction);
                    
                    // Skip if distance is zero to avoid division by zero
                    if (distance <= float.Epsilon) continue;
                    
                    float3 normalizedDirection = direction / distance;
                    
                    // Apply repulsion only if nodes are close enough
                    if (distance < MinDistanceToRepel)
                    {
                        float forceMagnitude = RepulsionStrength / (distance * distance);
                        float3 force = normalizedDirection * forceMagnitude;
                        forces[i] += force;
                        forces[j] -= force;
                    }
                    else // Add a weak long-range repulsion to help spread out initially
                    {
                        float weakRepulsionMagnitude = RepulsionStrength / (distance * distance * distance * 0.1f);
                        float3 weakRepulsion = normalizedDirection * weakRepulsionMagnitude;
                        forces[i] += weakRepulsion;
                        forces[j] -= weakRepulsion;
                    }
                }
            }
            
            // Calculate attractive forces
            for (int i = 0; i < ConnectionStartIndices.Length; i++)
            {
                int startIndex = ConnectionStartIndices[i];
                int endIndex = ConnectionEndIndices[i];
                
                // Skip invalid connections
                if (startIndex < 0 || startIndex >= nodeCount || endIndex < 0 || endIndex >= nodeCount)
                    continue;
                
                float3 direction = Positions[endIndex] - Positions[startIndex];
                float distance = math.length(direction);
                
                // Skip if distance is zero
                if (distance <= float.Epsilon) continue;
                
                float forceMagnitude = AttractionStrength * distance;
                float3 force = (direction / distance) * forceMagnitude;
                
                forces[startIndex] += force;
                forces[endIndex] -= force;
            }
            
            // Apply calculated forces to velocities and update positions
            for (int i = 0; i < nodeCount; i++)
            {
                Velocities[i] += forces[i] * DeltaTime;
                Velocities[i] *= DampingFactor; // Apply damping
                Positions[i] += Velocities[i] * DeltaTime;
            }
            
            forces.Dispose();
        }
    }
}