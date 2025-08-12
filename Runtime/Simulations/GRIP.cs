using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;

public class GRIP : MonoBehaviour
{
    [Header("GRIP Parameters")]
    public int maxLevels = 5;
    public float coarseningRatio = 0.5f; // Ratio of nodes to keep at each level
    public int refinementIterations = 50;
    public float initialPlacementRadius = 100f;
    
    [Header("Force Parameters")]
    public float repulsionStrength = 1000f;
    public float attractionStrength = 10f;
    public float dampingFactor = 0.9f;
    public float minDistanceToRepel = 1f;
    public float updateInterval = 0.02f;
    
    [Header("Adaptive Parameters")]
    public float coolingFactor = 0.95f;
    public float minTemperature = 0.05f;
    
    [Header("2D Constraint")]
    public float fixedZPosition = 1f; // Z position to lock nodes to

    private List<GameObject> _nodes;
    private List<GRIPLevel> _levels;
    private int _currentLevel;
    private float _currentTemperature = 1.0f;
    private float _timer;
    public bool activated = true;
    private bool _currentlyCalculating;
    
    private class GRIPLevel
    {
        public List<int> nodeIndices; // Indices of nodes at this level
        public Dictionary<int, List<int>> nodeToChildrenMap; // Maps node index to children in finer level
        public NativeArray<float3> positions;
        public NativeArray<float3> velocities;
        public NativeArray<int> connectionStartIndices;
        public NativeArray<int> connectionEndIndices;
        
        public GRIPLevel()
        {
            nodeIndices = new List<int>();
            nodeToChildrenMap = new Dictionary<int, List<int>>();
        }
        
        public void Dispose()
        {
            if (positions.IsCreated) positions.Dispose();
            if (velocities.IsCreated) velocities.Dispose();
            if (connectionStartIndices.IsCreated) connectionStartIndices.Dispose();
            if (connectionEndIndices.IsCreated) connectionEndIndices.Dispose();
        }
    }

    public void Initialize()
    {
        _currentlyCalculating = false;
        _nodes = ScriptableObjectInventory.Instance.graph.AllNodes;
        _currentTemperature = 1.0f;
        
        // Build multi-level hierarchy
        BuildHierarchy();
        
        // Start from coarsest level
        _currentLevel = _levels.Count - 1;
        
        // Initial placement for coarsest level
        InitializeCoarsestLevel();
        
        activated = true;
    }
    
    private void BuildHierarchy()
    {
        DisposeLevels();
        _levels = new List<GRIPLevel>();
        
        // Create finest level (all nodes)
        var finestLevel = new GRIPLevel();
        for (int i = 0; i < _nodes.Count; i++)
        {
            finestLevel.nodeIndices.Add(i);
        }
        _levels.Add(finestLevel);
        
        // Build coarser levels
        var currentNodeCount = _nodes.Count;
        var level = 0;
        
        while (currentNodeCount > 10 && level < maxLevels - 1)
        {
            var coarserLevel = CreateCoarserLevel(_levels[level]);
            if (coarserLevel.nodeIndices.Count >= _levels[level].nodeIndices.Count * 0.9f)
            {
                // Stop if coarsening is not effective
                break;
            }
            _levels.Add(coarserLevel);
            currentNodeCount = coarserLevel.nodeIndices.Count;
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
        foreach (var nodeIdx in finerLevel.nodeIndices)
        {
            adjacency[nodeIdx] = new HashSet<int>();
        }
        
        foreach (var connection in connections)
        {
            var startIdx = _nodes.IndexOf(connection.startNode);
            var endIdx = _nodes.IndexOf(connection.endNode);
            
            if (finerLevel.nodeIndices.Contains(startIdx) && finerLevel.nodeIndices.Contains(endIdx))
            {
                adjacency[startIdx].Add(endIdx);
                adjacency[endIdx].Add(startIdx);
            }
        }
        
        // Heavy-edge matching for coarsening
        var matched = new HashSet<int>();
        var random = new System.Random();
        var shuffledNodes = finerLevel.nodeIndices.OrderBy(x => random.Next()).ToList();
        
        foreach (var nodeIdx in shuffledNodes)
        {
            if (matched.Contains(nodeIdx)) continue;
            
            // Find best match (heaviest edge)
            var bestMatch = -1;
            var maxWeight = 0;
            
            foreach (var neighbor in adjacency[nodeIdx])
            {
                if (!matched.Contains(neighbor))
                {
                    // Simple weight: number of common neighbors
                    var weight = adjacency[nodeIdx].Intersect(adjacency[neighbor]).Count() + 1;
                    if (weight > maxWeight)
                    {
                        maxWeight = weight;
                        bestMatch = neighbor;
                    }
                }
            }
            
            // Create supernode
            var supernodeIdx = coarserLevel.nodeIndices.Count;
            coarserLevel.nodeIndices.Add(nodeIdx); // Use first node as representative
            coarserLevel.nodeToChildrenMap[supernodeIdx] = new List<int> { nodeIdx };
            matched.Add(nodeIdx);
            
            if (bestMatch != -1)
            {
                coarserLevel.nodeToChildrenMap[supernodeIdx].Add(bestMatch);
                matched.Add(bestMatch);
            }
        }
        
        return coarserLevel;
    }
    
    private void InitializeLevelArrays(GRIPLevel level, int levelIndex)
    {
        var nodeCount = level.nodeIndices.Count;
        level.positions = new NativeArray<float3>(nodeCount, Allocator.Persistent);
        level.velocities = new NativeArray<float3>(nodeCount, Allocator.Persistent);
        
        // Initialize positions
        if (levelIndex == 0)
        {
            // Finest level - use actual positions but constrain to 2D
            for (int i = 0; i < nodeCount; i++)
            {
                var pos = _nodes[level.nodeIndices[i]].transform.position;
                level.positions[i] = new float3(pos.x, pos.y, fixedZPosition);
                level.velocities[i] = float3.zero;
            }
        }
        else
        {
            // Coarser levels - will be initialized later
            for (int i = 0; i < nodeCount; i++)
            {
                level.velocities[i] = float3.zero;
            }
        }
        
        // Build connection arrays for this level
        var connections = new List<(int start, int end)>();
        var originalConnections = ScriptableObjectInventory.Instance.conSo.connections;
        
        // Map original node indices to level indices
        var nodeToLevelIndex = new Dictionary<int, int>();
        for (int i = 0; i < level.nodeIndices.Count; i++)
        {
            if (levelIndex == 0)
            {
                nodeToLevelIndex[level.nodeIndices[i]] = i;
            }
            else
            {
                // For coarser levels, map all children
                foreach (var child in level.nodeToChildrenMap[i])
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
            
            if (nodeToLevelIndex.ContainsKey(startIdx) && nodeToLevelIndex.ContainsKey(endIdx))
            {
                var levelStart = nodeToLevelIndex[startIdx];
                var levelEnd = nodeToLevelIndex[endIdx];
                
                if (levelStart != levelEnd)
                {
                    var connectionPair = levelStart < levelEnd ? 
                        (levelStart, levelEnd) : (levelEnd, levelStart);
                    
                    if (!addedConnections.Contains(connectionPair))
                    {
                        connections.Add((levelStart, levelEnd));
                        addedConnections.Add(connectionPair);
                    }
                }
            }
        }
        
        level.connectionStartIndices = new NativeArray<int>(connections.Count, Allocator.Persistent);
        level.connectionEndIndices = new NativeArray<int>(connections.Count, Allocator.Persistent);
        
        for (int i = 0; i < connections.Count; i++)
        {
            level.connectionStartIndices[i] = connections[i].start;
            level.connectionEndIndices[i] = connections[i].end;
        }
    }
    
    private void InitializeCoarsestLevel()
    {
        var coarsestLevel = _levels[_levels.Count - 1];
        var nodeCount = coarsestLevel.nodeIndices.Count;
        
        // Place nodes in a circle on the XY plane
        var angleStep = 2 * Mathf.PI / nodeCount;
        for (int i = 0; i < nodeCount; i++)
        {
            var angle = i * angleStep;
            coarsestLevel.positions[i] = new float3(
                Mathf.Cos(angle) * initialPlacementRadius,
                Mathf.Sin(angle) * initialPlacementRadius,
                fixedZPosition  // Fixed Z position for 2D
            );
        }
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
        
        // Run force-directed layout on current level
        if (_currentLevel >= 0)
        {
            var currentLevelData = _levels[_currentLevel];
            
            // Calculate forces
            CalculateForces(currentLevelData);
            
            // Check for convergence and refinement
            if (ShouldRefine())
            {
                if (_currentLevel > 0)
                {
                    RefineToNextLevel();
                    _currentLevel--;
                    _currentTemperature = 1.0f;
                }
                else
                {
                    // We're at the finest level, update actual node positions
                    UpdateNodePositions();
                }
            }
            
            // Cool down temperature
            _currentTemperature *= coolingFactor;
            _currentTemperature = Mathf.Max(_currentTemperature, minTemperature);
        }

        _currentlyCalculating = false;
    }
    
    private bool ShouldRefine()
    {
        // Refine when temperature is low enough or after certain iterations
        return _currentTemperature < 0.1f;
    }
    
    private void RefineToNextLevel()
    {
        var coarserLevel = _levels[_currentLevel];
        var finerLevel = _levels[_currentLevel - 1];
        
        // Transfer positions from coarser to finer level
        for (int i = 0; i < coarserLevel.nodeIndices.Count; i++)
        {
            var coarsePos = coarserLevel.positions[i];
            
            if (_currentLevel - 1 == 0)
            {
                // Refining to finest level
                var nodeIdx = coarserLevel.nodeIndices[i];
                var finerIdx = finerLevel.nodeIndices.IndexOf(nodeIdx);
                if (finerIdx >= 0)
                {
                    finerLevel.positions[finerIdx] = new float3(coarsePos.x, coarsePos.y, fixedZPosition);
                }
            }
            else
            {
                // Refining to intermediate level
                if (coarserLevel.nodeToChildrenMap.ContainsKey(i))
                {
                    var children = coarserLevel.nodeToChildrenMap[i];
                    var childCount = children.Count;
                    
                    // Place children around parent position
                    for (int j = 0; j < childCount; j++)
                    {
                        var childNodeIdx = children[j];
                        var childLevelIdx = -1;
                        
                        // Find child in finer level
                        for (int k = 0; k < finerLevel.nodeIndices.Count; k++)
                        {
                            if (finerLevel.nodeIndices[k] == childNodeIdx ||
                                (finerLevel.nodeToChildrenMap.ContainsKey(k) && 
                                 finerLevel.nodeToChildrenMap[k].Contains(childNodeIdx)))
                            {
                                childLevelIdx = k;
                                break;
                            }
                        }
                        
                        if (childLevelIdx >= 0)
                        {
                            // Add small random offset in 2D only
                            var offset = new float3(
                                UnityEngine.Random.Range(-5f, 5f),
                                UnityEngine.Random.Range(-5f, 5f),
                                0  // No Z offset
                            );
                            finerLevel.positions[childLevelIdx] = new float3(
                                coarsePos.x + offset.x,
                                coarsePos.y + offset.y,
                                fixedZPosition
                            );
                        }
                    }
                }
            }
        }
    }
    
    private void UpdateNodePositions()
    {
        var finestLevel = _levels[0];
        for (int i = 0; i < finestLevel.nodeIndices.Count; i++)
        {
            var nodeIdx = finestLevel.nodeIndices[i];
            if (nodeIdx < _nodes.Count)
            {
                var pos = finestLevel.positions[i];
                _nodes[nodeIdx].transform.position = new Vector3(pos.x, pos.y, fixedZPosition);
            }
        }
    }

    private void CalculateForces(GRIPLevel level)
    {
        var forceCalculationJob = new GRIP2DForceCalculationJob
        {
            Positions = level.positions,
            Velocities = level.velocities,
            ConnectionStartIndices = level.connectionStartIndices,
            ConnectionEndIndices = level.connectionEndIndices,
            RepulsionStrength = repulsionStrength * _currentTemperature,
            AttractionStrength = attractionStrength,
            DampingFactor = dampingFactor,
            MinDistanceToRepel = minDistanceToRepel,
            DeltaTime = updateInterval,
            Temperature = _currentTemperature,
            FixedZPosition = fixedZPosition
        };

        forceCalculationJob.Run();
    }

    private void OnDisable()
    {
        if (!ScriptableObjectInventory.InstanceExists) return; 
        if (ScriptableObjectInventory.Instance.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered -= HandleEvent;
        if (ScriptableObjectInventory.Instance.clearEvent)
            ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered -= HandleEvent;
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
        if (_levels != null)
        {
            foreach (var level in _levels)
            {
                level.Dispose();
            }
            _levels = null;
        }
    }

    [BurstCompile]
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
                    var pos1_2d = new float2(Positions[i].x, Positions[i].y);
                    var pos2_2d = new float2(Positions[j].x, Positions[j].y);
                    var direction2d = pos1_2d - pos2_2d;
                    var distance = math.length(direction2d);
                    
                    // Skip if distance is zero to avoid division by zero
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
            
            // Calculate attractive forces with spring model
            for (var i = 0; i < ConnectionStartIndices.Length; i++)
            {
                var startIndex = ConnectionStartIndices[i];
                var endIndex = ConnectionEndIndices[i];
                
                // Skip invalid connections
                if (startIndex < 0 || startIndex >= nodeCount || endIndex < 0 || endIndex >= nodeCount)
                    continue;
                
                // Calculate 2D distance
                var pos1_2d = new float2(Positions[startIndex].x, Positions[startIndex].y);
                var pos2_2d = new float2(Positions[endIndex].x, Positions[endIndex].y);
                var direction2d = pos2_2d - pos1_2d;
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
                var dispLength = math.length(displacement);
                if (dispLength > maxDisplacement)
                {
                    displacement = (displacement / dispLength) * maxDisplacement;
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
