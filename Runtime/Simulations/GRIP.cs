using _3DConnections.Runtime.ScriptableObjects;

namespace _3DConnections.Runtime.Simulations
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using Unity.Mathematics;
    using Unity.Collections;
    using Unity.Burst;
    using Unity.Jobs;
    
    using ScriptableObjectInventory;

    /// <summary>
    /// Places nodes initially in a radius defined by @param initialPlacementRadius
    /// maxLevels ... number of hierarchy levels
    /// </summary>
// ReSharper disable once InconsistentNaming
    public class GRIP : SimulationBase
    {
        [Header("GRIP Parameters")] public int maxLevels = 5;
        public float initialPlacementRadius = 100f;

        [Header("Force Parameters")] public float repulsionStrength = 1000f;
        public float attractionStrength = 10f;
        public float dampingFactor = 0.9f;
        public float minDistanceToRepel = 1f;

        [Header("2D Constraint")] public float fixedZPosition = 1f; // Z position to lock nodes to

        private List<GameObject> _nodes;
        private List<GRIPLevel> _levels;
        private int _currentLevel;
        private bool _currentlyCalculating;

        // ReSharper disable once InconsistentNaming
        private class GRIPLevel
        {
            public readonly List<int> NodeIndices = new(); // Indices of nodes at this level

            public readonly Dictionary<int, List<int>>
                NodeToChildrenMap = new(); // Maps node index to children in finer level

            public NativeArray<float3> Positions;
            public NativeArray<float3> Velocities;
            public NativeArray<int> ConnectionStartIndices;
            public NativeArray<int> ConnectionEndIndices;

            public void Dispose()
            {
                if (Positions.IsCreated) Positions.Dispose();
                if (Velocities.IsCreated) Velocities.Dispose();
                if (ConnectionStartIndices.IsCreated) ConnectionStartIndices.Dispose();
                if (ConnectionEndIndices.IsCreated) ConnectionEndIndices.Dispose();
            }
        }

        public override void OnEnable()
        {
            base.OnEnable();
            simulationEvent.OnSimulationRequested += Simulate;
        }

        private void Simulate(SimulationType simulationType)
        {
            if (simulationType != SimulationType.GRIP) return;
            Initialize();
        }

        public void Initialize()
        {
            _currentlyCalculating = false;
            _nodes = ScriptableObjectInventory.Instance.graph.AllNodes;

            // Build multi-level hierarchy
            BuildHierarchy();

            // Start from the coarsest level
            _currentLevel = _levels.Count - 1;

            // Initial placement for coarsest level
            InitializeCoarsestLevel();

            activated = true;
        }

        private void BuildHierarchy()
        {
            DisposeLevels();
            _levels = new List<GRIPLevel>();

            // Create the finest level (all nodes)
            var finestLevel = new GRIPLevel();
            for (int i = 0; i < _nodes.Count; i++)
            {
                finestLevel.NodeIndices.Add(i);
            }

            _levels.Add(finestLevel);

            // Build coarser levels
            var currentNodeCount = _nodes.Count;
            var level = 0;

            while (currentNodeCount > 10 && level < maxLevels - 1)
            {
                var coarserLevel = CreateCoarserLevel(_levels[level]);
                if (coarserLevel.NodeIndices.Count >= _levels[level].NodeIndices.Count * 0.9f)
                {
                    // Stop if coarsening is not effective
                    break;
                }

                _levels.Add(coarserLevel);
                currentNodeCount = coarserLevel.NodeIndices.Count;
                level++;
            }

            // Initialize native arrays for each level
            for (int i = 0; i < _levels.Count; i++)
            {
                InitializeLevelArrays(_levels[i], i);
            }
        }

        private GRIPLevel CreateCoarserLevel(GRIPLevel finerLevel)
        {
            var coarserLevel = new GRIPLevel();
            var connections = ScriptableObjectInventory.Instance.conSo.connections;

            // Create adjacency information
            var adjacency = new Dictionary<int, HashSet<int>>();
            foreach (var nodeIdx in finerLevel.NodeIndices)
            {
                adjacency[nodeIdx] = new HashSet<int>();
            }

            foreach (var connection in connections)
            {
                var startIdx = _nodes.IndexOf(connection.startNode);
                var endIdx = _nodes.IndexOf(connection.endNode);

                if (!finerLevel.NodeIndices.Contains(startIdx) || !finerLevel.NodeIndices.Contains(endIdx)) continue;
                adjacency[startIdx].Add(endIdx);
                adjacency[endIdx].Add(startIdx);
            }

            // Heavy-edge matching for coarsening
            var matched = new HashSet<int>();
            var random = new System.Random();
            var shuffledNodes = finerLevel.NodeIndices.OrderBy(_ => random.Next()).ToList();

            foreach (var nodeIdx in shuffledNodes)
            {
                if (matched.Contains(nodeIdx)) continue;

                // Find the best match (the heaviest edge)
                var bestMatch = -1;
                var maxWeight = 0;

                foreach (var neighbor in adjacency[nodeIdx])
                {
                    if (matched.Contains(neighbor)) continue;
                    // Simple weight: number of common neighbors
                    var weight = adjacency[nodeIdx].Intersect(adjacency[neighbor]).Count() + 1;
                    if (weight <= maxWeight) continue;
                    maxWeight = weight;
                    bestMatch = neighbor;
                }

                // Create supernode
                var supernodeIdx = coarserLevel.NodeIndices.Count;
                coarserLevel.NodeIndices.Add(nodeIdx); // Use the first node as representative
                coarserLevel.NodeToChildrenMap[supernodeIdx] = new List<int> { nodeIdx };
                matched.Add(nodeIdx);

                if (bestMatch == -1) continue;
                coarserLevel.NodeToChildrenMap[supernodeIdx].Add(bestMatch);
                matched.Add(bestMatch);
            }

            return coarserLevel;
        }

        private void InitializeLevelArrays(GRIPLevel level, int levelIndex)
        {
            var nodeCount = level.NodeIndices.Count;
            level.Positions = new NativeArray<float3>(nodeCount, Allocator.Persistent);
            level.Velocities = new NativeArray<float3>(nodeCount, Allocator.Persistent);

            // Initialize positions
            if (levelIndex == 0)
            {
                // Finest level - use actual positions but constrain to 2D
                for (int i = 0; i < nodeCount; i++)
                {
                    var pos = _nodes[level.NodeIndices[i]].transform.position;
                    level.Positions[i] = new float3(pos.x, pos.y, fixedZPosition);
                    level.Velocities[i] = float3.zero;
                }
            }
            else
            {
                // Coarser levels will be initialized later
                for (int i = 0; i < nodeCount; i++)
                {
                    level.Velocities[i] = float3.zero;
                }
            }

            // Build connection arrays for this level
            var connections = new List<(int start, int end)>();
            var originalConnections = ScriptableObjectInventory.Instance.conSo.connections;

            // Map original node indices to level indices
            var nodeToLevelIndex = new Dictionary<int, int>();
            for (int i = 0; i < level.NodeIndices.Count; i++)
            {
                if (levelIndex == 0)
                {
                    nodeToLevelIndex[level.NodeIndices[i]] = i;
                }
                else
                {
                    // For coarser levels, map all children
                    foreach (var child in level.NodeToChildrenMap[i])
                    {
                        nodeToLevelIndex[child] = i;
                    }
                }
            }

            // Create connections for this level
            var addedConnections = new HashSet<(int, int)>();
            foreach (var connection in originalConnections)
            {
                var startIdx = _nodes.IndexOf(connection.startNode);
                var endIdx = _nodes.IndexOf(connection.endNode);

                if (!nodeToLevelIndex.ContainsKey(startIdx) ||
                    !nodeToLevelIndex.TryGetValue(endIdx, out var levelEnd)) continue;
                var levelStart = nodeToLevelIndex[startIdx];

                if (levelStart == levelEnd) continue;
                var connectionPair = levelStart < levelEnd ? (levelStart, levelEnd) : (levelEnd, levelStart);

                if (addedConnections.Contains(connectionPair)) continue;
                connections.Add((levelStart, levelEnd));
                addedConnections.Add(connectionPair);
            }

            level.ConnectionStartIndices = new NativeArray<int>(connections.Count, Allocator.Persistent);
            level.ConnectionEndIndices = new NativeArray<int>(connections.Count, Allocator.Persistent);

            for (int i = 0; i < connections.Count; i++)
            {
                level.ConnectionStartIndices[i] = connections[i].start;
                level.ConnectionEndIndices[i] = connections[i].end;
            }
        }

        private void InitializeCoarsestLevel()
        {
            var coarsestLevel = _levels[^1];
            var nodeCount = coarsestLevel.NodeIndices.Count;

            // Place nodes in a circle on the XY plane
            var angleStep = 2 * Mathf.PI / nodeCount;
            for (int i = 0; i < nodeCount; i++)
            {
                var angle = i * angleStep;
                coarsestLevel.Positions[i] = new float3(
                    Mathf.Cos(angle) * initialPlacementRadius,
                    Mathf.Sin(angle) * initialPlacementRadius,
                    fixedZPosition // Fixed Z position for 2D
                );
            }
        }

        protected override void AdditionalEnableSteps()
        {
            activated = true;
            if (_nodes == null || _nodes.Count == 0)
                Initialize();

            if (ScriptableObjectInventory.Instance.removePhysicsEvent)
                ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered += HandleEvent;
            if (ScriptableObjectInventory.Instance.clearEvent)
                ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered += HandleEvent;
        }

        protected override void RunStep()
        {
            if (_currentlyCalculating) return;
            _currentlyCalculating = true;

            if (_currentLevel >= 0)
            {
                var currentLevelData = _levels[_currentLevel];
                CalculateForces(currentLevelData);

                if (ShouldRefine())
                {
                    if (_currentLevel > 0)
                    {
                        RefineToNextLevel();
                        _currentLevel--;
                        CurrentTemperature = startTemperature; // reset for a new level
                    }
                    else
                    {
                        UpdateNodePositions();
                        activated = false;
                    }
                }
            }

            _currentlyCalculating = false;
        }


        private bool ShouldRefine()
        {
            return CurrentTemperature < 0.1f;
        }


        private void RefineToNextLevel()
        {
            var coarserLevel = _levels[_currentLevel];
            var finerLevel = _levels[_currentLevel - 1];

            // Transfer positions from coarser to finer level
            for (int i = 0; i < coarserLevel.NodeIndices.Count; i++)
            {
                var coarsePos = coarserLevel.Positions[i];

                if (_currentLevel - 1 == 0)
                {
                    // Refining to the finest level
                    var nodeIdx = coarserLevel.NodeIndices[i];
                    var finerIdx = finerLevel.NodeIndices.IndexOf(nodeIdx);
                    if (finerIdx >= 0)
                    {
                        finerLevel.Positions[finerIdx] = new float3(coarsePos.x, coarsePos.y, fixedZPosition);
                    }
                }
                else
                {
                    // Refining to intermediate level
                    if (!coarserLevel.NodeToChildrenMap.TryGetValue(i, out var children)) continue;
                    var childCount = children.Count;

                    // Place children around a parent position
                    for (int j = 0; j < childCount; j++)
                    {
                        var childNodeIdx = children[j];
                        var childLevelIdx = -1;

                        // Find child in finer level
                        for (int k = 0; k < finerLevel.NodeIndices.Count; k++)
                        {
                            if (finerLevel.NodeIndices[k] != childNodeIdx &&
                                (!finerLevel.NodeToChildrenMap.ContainsKey(k) ||
                                 !finerLevel.NodeToChildrenMap[k].Contains(childNodeIdx))) continue;
                            childLevelIdx = k;
                            break;
                        }

                        if (childLevelIdx < 0) continue;
                        // Add a small random offset in 2D only
                        var offset = new float3(
                            UnityEngine.Random.Range(-5f, 5f),
                            UnityEngine.Random.Range(-5f, 5f),
                            0 // No Z offset
                        );
                        finerLevel.Positions[childLevelIdx] = new float3(
                            coarsePos.x + offset.x,
                            coarsePos.y + offset.y,
                            fixedZPosition
                        );
                    }
                }
            }
        }

        private void UpdateNodePositions()
        {
            var finestLevel = _levels[0];
            for (int i = 0; i < finestLevel.NodeIndices.Count; i++)
            {
                var nodeIdx = finestLevel.NodeIndices[i];
                if (nodeIdx >= _nodes.Count) continue;
                var pos = finestLevel.Positions[i];
                _nodes[nodeIdx].transform.position = new Vector3(pos.x, pos.y, fixedZPosition);
            }
        }

        private void CalculateForces(GRIPLevel level)
        {
            var forceCalculationJob = new GRIP2DForceCalculationJob
            {
                Positions = level.Positions,
                Velocities = level.Velocities,
                ConnectionStartIndices = level.ConnectionStartIndices,
                ConnectionEndIndices = level.ConnectionEndIndices,
                RepulsionStrength = repulsionStrength * CurrentTemperature,
                AttractionStrength = attractionStrength,
                DampingFactor = dampingFactor,
                MinDistanceToRepel = minDistanceToRepel,
                DeltaTime = updateInterval,
                Temperature = CurrentTemperature,
                FixedZPosition = fixedZPosition
            };

            forceCalculationJob.Run();
        }

        private void OnDisable()
        {
            if (ScriptableObjectInventory.Instance == null) return;
            if (ScriptableObjectInventory.Instance.removePhysicsEvent)
                ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered -= HandleEvent;
            if (ScriptableObjectInventory.Instance.clearEvent)
                ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered -= HandleEvent;
            simulationEvent.OnSimulationRequested -= Simulate;
        }

        private void HandleEvent()
        {
            activated = false;
        }

        private void OnDestroy()
        {
            DisposeLevels();
        }

        private void DisposeLevels()
        {
            if (_levels == null) return;
            foreach (var level in _levels)
            {
                level.Dispose();
            }

            _levels = null;
        }

        [BurstCompile]
        // ReSharper disable once InconsistentNaming
        private struct GRIP2DForceCalculationJob : IJob
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
            public float Temperature;
            public float FixedZPosition;

            public void Execute()
            {
                var nodeCount = Positions.Length;

                // Temporary array for storing forces
                var forces = new NativeArray<float3>(nodeCount, Allocator.Temp);

                // Calculate repulsive forces with Barnes-Hut approximation for GRIP
                for (var i = 0; i < nodeCount; i++)
                {
                    for (var j = i + 1; j < nodeCount; j++)
                    {
                        // Calculate 2D distance (ignore Z component)
                        var pos12D = new float2(Positions[i].x, Positions[i].y);
                        var pos22D = new float2(Positions[j].x, Positions[j].y);
                        var direction2d = pos12D - pos22D;
                        var distance = math.length(direction2d);

                        // Skip if the distance is zero to avoid division by zero
                        if (distance <= float.Epsilon) continue;

                        var normalizedDirection2d = direction2d / distance;

                        // GRIP uses modified repulsion calculation
                        var forceMagnitude = RepulsionStrength / (distance * distance);

                        // Apply temperature-based scaling
                        forceMagnitude *= Temperature;

                        var force2d = normalizedDirection2d * forceMagnitude;

                        // Convert back to 3D with Z=0
                        var force = new float3(force2d.x, force2d.y, 0);
                        forces[i] += force;
                        forces[j] -= force;
                    }
                }

                // Calculate attractive forces with a spring model
                for (var i = 0; i < ConnectionStartIndices.Length; i++)
                {
                    var startIndex = ConnectionStartIndices[i];
                    var endIndex = ConnectionEndIndices[i];

                    // Skip invalid connections
                    if (startIndex < 0 || startIndex >= nodeCount || endIndex < 0 || endIndex >= nodeCount)
                        continue;

                    // Calculate 2D distance
                    var pos12D = new float2(Positions[startIndex].x, Positions[startIndex].y);
                    var pos22D = new float2(Positions[endIndex].x, Positions[endIndex].y);
                    var direction2d = pos22D - pos12D;
                    var distance = math.length(direction2d);

                    // Skip if the distance is zero
                    if (distance <= float.Epsilon) continue;

                    // GRIP uses logarithmic spring forces
                    var idealLength = MinDistanceToRepel * 2f;
                    var forceMagnitude = AttractionStrength * math.log(distance / idealLength + 1);
                    var force2d = (direction2d / distance) * forceMagnitude;

                    // Convert back to 3D with Z=0
                    var force = new float3(force2d.x, force2d.y, 0);

                    forces[startIndex] += force;
                    forces[endIndex] -= force;
                }

                // Apply forces with temperature-based displacement limiting
                var maxDisplacement = Temperature * 10f;

                for (var i = 0; i < nodeCount; i++)
                {
                    // Apply forces only in XY plane
                    var force2d = new float2(forces[i].x, forces[i].y);
                    var velocity2d = new float2(Velocities[i].x, Velocities[i].y);

                    velocity2d += force2d * DeltaTime;
                    velocity2d *= DampingFactor;

                    // Limit displacement based on temperature
                    var displacement = velocity2d * DeltaTime;
                    var displacementLength = math.length(displacement);
                    if (displacementLength > maxDisplacement)
                    {
                        displacement = (displacement / displacementLength) * maxDisplacement;
                    }

                    // Update position in 2D
                    var newPos2d = new float2(Positions[i].x, Positions[i].y) + displacement;

                    // Store back in 3D arrays with fixed Z
                    Positions[i] = new float3(newPos2d.x, newPos2d.y, FixedZPosition);
                    Velocities[i] = new float3(velocity2d.x, velocity2d.y, 0);
                }

                forces.Dispose();
            }
        }
    }
}