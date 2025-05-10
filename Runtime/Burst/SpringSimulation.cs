using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class SpringSimulation : MonoBehaviour, ILogable
{

    private NativeArray<float2> _positions;
    private NativeArray<float2> _newPositions;
    private NativeArray<float2> _velocities;
    private NativeArray<float2> _newVelocities;
    private NativeArray<float2> _forces;
    private List<GameObject> _nodes;
    private bool _isSetUp;
    

    private void OnDisable()
    {
        if (ScriptableObjectInventory.Instance.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered -= HandleEvent;
        CleanupNativeArrays();
    }

    private void OnEnable()
    {
        if (ScriptableObjectInventory.Instance.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered += HandleEvent;
    }
    

    private void OnDestroy()
    {
        CleanupNativeArrays();
    }

    /// <summary>
    /// Required to prevent memory leaks
    /// </summary>
    public void CleanupNativeArrays()
    {
        if (!_isSetUp) return;

        if (_positions.IsCreated) _positions.Dispose();
        if (_newPositions.IsCreated) _newPositions.Dispose();
        if (_velocities.IsCreated) _velocities.Dispose();
        if (_newVelocities.IsCreated) _newVelocities.Dispose();
        if (_forces.IsCreated) _forces.Dispose();

        _isSetUp = false;
    }

    public void Simulate()
    {
        CleanupNativeArrays();

        _nodes = ScriptableObjectInventory.Instance.graph.AllNodes;

        _positions = new NativeArray<float2>(_nodes.Count, Allocator.Persistent);
        _newPositions = new NativeArray<float2>(_nodes.Count, Allocator.Persistent);
        _velocities = new NativeArray<float2>(_nodes.Count, Allocator.Persistent);
        _newVelocities = new NativeArray<float2>(_nodes.Count, Allocator.Persistent);
        _forces = new NativeArray<float2>(_nodes.Count, Allocator.Persistent);

        for (var i = 0; i < _nodes.Count; i++)
        {
            _positions[i] = new float2(_nodes[i].transform.position.x, _nodes[i].transform.position.y);
            _newPositions[i] = _positions[i];
            _velocities[i] = float2.zero;
            _newVelocities[i] = float2.zero;
            _forces[i] = float2.zero;
        }

        _isSetUp = true;

        var types = new List<System.Type>
        {
            typeof(SpringJoint2D),
            typeof(Rigidbody2D)
        };
        ScriptableObjectInventory.Instance.graph.NodesRemoveComponents(types);

        ConvertCollidersToTriggers();
    }

    private void Update()
    {
        if (!_isSetUp || !Application.isPlaying) return;
        var deltaTime = Time.deltaTime;

        // Clear forces
        for (int i = 0; i < _forces.Length; i++)
        {
            _forces[i] = float2.zero;
        }

        // Create jobs without using statements
        var springJob = new SpringJob2D
        {
            CurrentPositions = _positions,
            NewPositions = _newPositions,
            CurrentVelocities = _velocities,
            NewVelocities = _newVelocities,
            Forces = _forces,
            Stiffness = ScriptableObjectInventory.Instance.simConfig.Stiffness,
            Damping = ScriptableObjectInventory.Instance.simConfig.damping,
            DeltaTime = deltaTime
        };

        var collisionJob = new CollisionResponseJob
        {
            CurrentPositions = _positions,
            NewPositions = _newPositions,
            CurrentVelocities = _velocities,
            NewVelocities = _newVelocities,
            ColliderRadius = ScriptableObjectInventory.Instance.simConfig.colliderRadius,
            CollisionResponseStrength = ScriptableObjectInventory.Instance.simConfig.CollisionResponseStrength,
            DeltaTime = deltaTime
        };

        // Schedule and complete jobs
        var springHandle = springJob.Schedule(_nodes.Count, 64);
        var collisionHandle = collisionJob.Schedule(_nodes.Count, 64, springHandle);
        collisionHandle.Complete();

        SwapBuffers();

        // Update transforms
        for (var i = 0; i < _nodes.Count; i++)
        {
            _nodes[i].transform.position = new Vector3(_positions[i].x, _positions[i].y, _nodes[i].transform.position.z);
        }
    }

    private void SwapBuffers()
    {
        (_positions, _newPositions) = (_newPositions, _positions);

        (_velocities, _newVelocities) = (_newVelocities, _velocities);
    }

    private static void ConvertCollidersToTriggers()
    {
        foreach (var col in ScriptableObjectInventory.Instance.graph.AllNodes.Select(node => node.GetComponent<Collider2D>()).Where(col => col))
        {
            col.isTrigger = true;
        }
    }

    [BurstCompile]
    private struct SpringJob2D : IJobParallelFor
    {
        public float Stiffness;
        public float Damping;
        public float DeltaTime;

        [ReadOnly] public NativeArray<float2> CurrentPositions;
        public NativeArray<float2> NewPositions;
        [ReadOnly] public NativeArray<float2> CurrentVelocities;
        public NativeArray<float2> NewVelocities;
        public NativeArray<float2> Forces;

        public void Execute(int index)
        {
            var targetPosition = float2.zero;
            var displacement = targetPosition - CurrentPositions[index];
            var springForce = Stiffness * displacement;
            var dampingForce = -Damping * CurrentVelocities[index];
            Forces[index] = springForce + dampingForce;

            NewVelocities[index] = CurrentVelocities[index] + Forces[index] * DeltaTime;
            NewPositions[index] = CurrentPositions[index] + NewVelocities[index] * DeltaTime;
        }
    }

    [BurstCompile]
    private struct CollisionResponseJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> CurrentPositions;
        public NativeArray<float2> NewPositions;
        [ReadOnly] public NativeArray<float2> CurrentVelocities;
        public NativeArray<float2> NewVelocities;

        [ReadOnly] public float ColliderRadius;
        [ReadOnly] public float CollisionResponseStrength;
        [ReadOnly] public float DeltaTime;

        public void Execute(int index)
        {
            var position = NewPositions[index];
            var velocity = NewVelocities[index];

            for (var j = 0; j < CurrentPositions.Length; j++)
            {
                if (index == j) continue;

                var delta = position - CurrentPositions[j];
                var distance = math.length(delta);
                var minDist = ColliderRadius * 2;

                if (!(distance < minDist) || !(distance > float.Epsilon)) continue;
                var direction = delta / distance;
                var overlap = minDist - distance;

                position += direction * (overlap * 0.5f);

                var relativeVelocity = velocity - CurrentVelocities[j];
                var velocityAlongNormal = math.dot(relativeVelocity, direction);

                if (!(velocityAlongNormal < 0)) continue;
                const float restitution = 0.5f;
                var jScalar = -(1 + restitution) * velocityAlongNormal;
                jScalar *= CollisionResponseStrength;

                velocity += direction * jScalar;
            }

            NewPositions[index] = position;
            NewVelocities[index] = velocity;
        }
    }

    public string GetStatus()
    {
        return _nodes.Count + " is setup: " + _isSetUp;
    }

    public void Disable()
    {
        _isSetUp = false;
    }


    private void HandleEvent()
    {
        CleanupNativeArrays();
    }
}