using _3DConnections.Runtime.Events;
using _3DConnections.Runtime.ScriptableObjects;

namespace _3DConnections.Runtime.Simulations
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Unity.Mathematics;
    using Unity.Collections;
    using Unity.Burst;
    using Unity.Jobs;
    using ScriptableObjectInventory;

    public class ForceDirectedSimulationV2 : MonoBehaviour
    {
        [Header("References")] public float repulsionStrength = 1000f;
        public float attractionStrength = 10f;
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
        
        [SerializeField] private SimulationEvent simulationEvent;
        
        [ContextMenu( "Refresh" )]
        public void Initialize()
        {
            _currentlyCalculating = false;
            _nodes = ScriptableObjectInventory.Instance.graph.AllNodes;

            // Dispose of existing native arrays if they exist
            DisposeNativeArrays();

            // Create new native arrays
            _positions = new NativeArray<float3>(_nodes.Count, Allocator.Persistent);
            _velocities = new NativeArray<float3>(_nodes.Count, Allocator.Persistent);

            try
            {
                // Initialize positions and velocities
                for (var i = 0; i < _nodes.Count; i++)
                {
                    _positions[i] = _nodes[i].transform.position;
                    _velocities[i] = float3.zero;
                }
            }
            catch (Exception)
            {
                
            }

            // Create connection arrays
            var connections = ScriptableObjectInventory.Instance.conSo.connections;
            _connectionStartIndices = new NativeArray<int>(connections.Count, Allocator.Persistent);
            _connectionEndIndices = new NativeArray<int>(connections.Count, Allocator.Persistent);

            for (var i = 0; i < connections.Count; i++)
            {
                _connectionStartIndices[i] = _nodes.IndexOf(connections[i].startNode);
                _connectionEndIndices[i] = _nodes.IndexOf(connections[i].endNode);
            }

            activated = true;
        }

        private void OnEnable()
        {
            activated = true;
            Initialize();

            if (ScriptableObjectInventory.Instance.removePhysicsEvent)
                ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered += HandleEvent;
            if (ScriptableObjectInventory.Instance.clearEvent)
                ScriptableObjectInventory.Instance.clearEvent.onEventTriggered.AddListener(HandleEvent);
            simulationEvent.OnSimulationRequested += Simulate;
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
                try
                {
                    _positions[i] = _nodes[i].transform.position;
                }
                catch (IndexOutOfRangeException)
                {

                }
                catch (MissingReferenceException)
                {

                }
                catch (NullReferenceException)
                {
                    
                }
            }

            // Schedule and run the force calculation job
            CalculateForces();

            for (var i = 0; i < _nodes.Count; i++)
            {
                try
                {
                    _nodes[i].transform.position = _positions[i];
                }
                catch (IndexOutOfRangeException)
                {

                }
                catch (MissingReferenceException)
                {
                    
                }
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

            forceCalculationJob.Run();
        }

        private void OnDisable()
        {
            if (ScriptableObjectInventory.Instance == null) return;
            if (ScriptableObjectInventory.Instance.removePhysicsEvent)
                ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered -= HandleEvent;
            if (ScriptableObjectInventory.Instance.clearEvent)
                ScriptableObjectInventory.Instance.clearEvent.onEventTriggered.RemoveListener(HandleEvent);
            simulationEvent.OnSimulationRequested -= Simulate;
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
                var nodeCount = Positions.Length;

                // Temporary array for storing forces
                var forces = new NativeArray<float3>(nodeCount, Allocator.Temp);

                // Calculate repulsive forces
                for (var i = 0; i < nodeCount; i++)
                {
                    for (var j = i + 1; j < nodeCount; j++)
                    {
                        var direction = Positions[i] - Positions[j];
                        var distance = math.length(direction);

                        // Skip if distance is zero to avoid division by zero
                        if (distance <= float.Epsilon) continue;

                        var normalizedDirection = direction / distance;

                        // Apply repulsion only if nodes are close enough
                        if (distance < MinDistanceToRepel)
                        {
                            var forceMagnitude = RepulsionStrength / (distance * distance);
                            var force = normalizedDirection * forceMagnitude;
                            forces[i] += force;
                            forces[j] -= force;
                        }
                        else // Add a weak long-range repulsion to help spread out initially
                        {
                            var weakRepulsionMagnitude = RepulsionStrength / (distance * distance * distance * 0.1f);
                            var weakRepulsion = normalizedDirection * weakRepulsionMagnitude;
                            forces[i] += weakRepulsion;
                            forces[j] -= weakRepulsion;
                        }
                    }
                }

                // Calculate attractive forces
                for (var i = 0; i < ConnectionStartIndices.Length; i++)
                {
                    var startIndex = ConnectionStartIndices[i];
                    var endIndex = ConnectionEndIndices[i];

                    // Skip invalid connections
                    if (startIndex < 0 || startIndex >= nodeCount || endIndex < 0 || endIndex >= nodeCount)
                        continue;

                    var direction = Positions[endIndex] - Positions[startIndex];
                    var distance = math.length(direction);

                    // Skip if the distance is zero
                    if (distance <= float.Epsilon) continue;

                    var forceMagnitude = AttractionStrength * distance;
                    var force = (direction / distance) * forceMagnitude;

                    forces[startIndex] += force;
                    forces[endIndex] -= force;
                }

                // Apply calculated forces to velocities and update positions
                for (var i = 0; i < nodeCount; i++)
                {
                    Velocities[i] += forces[i] * DeltaTime;
                    Velocities[i] *= DampingFactor; // Apply damping
                    Positions[i] += Velocities[i] * DeltaTime;
                }

                forces.Dispose();
            }
        }
        private void Simulate(SimulationType simulationType)
        {
            if (simulationType != SimulationType.ComponentV2) return;
            Initialize();
        }
    }
}