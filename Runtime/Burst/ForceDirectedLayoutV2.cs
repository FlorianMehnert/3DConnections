using System;
using System.Collections.Generic;
using UnityEngine;

public class ForceDirectedLayoutV2 : MonoBehaviour
{
    [Header("References")]
    public float repulsionStrength = 10f;
    public float attractionStrength = 0.1f;
    public float dampingFactor = 0.9f;
    public float minDistanceToRepel = 1f;
    public float updateInterval = 0.02f; // Time in seconds between layout updates

    private List<GameObject> _nodes;
    private Dictionary<GameObject, Vector3> _velocities;
    private float _timer;
    public bool activated = true;

    public void Initialize()
    {
        _nodes = ScriptableObjectInventory.Instance.graph.AllNodes;
        _velocities = new Dictionary<GameObject, Vector3>();
        foreach (var node in _nodes)
        {
            _velocities[node] = Vector3.zero;
        }
        activated = true;
    }
    
    private void OnEnable()
    {
        activated = true;
        if (ScriptableObjectInventory.Instance.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered += HandleEvent;
        if (ScriptableObjectInventory.Instance.clearEvent)
            ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered += HandleEvent;
    }

    private void Update()
    {
        if (!activated) return;
        _timer += Time.deltaTime;
        if (!(_timer >= updateInterval)) return;
        _timer -= updateInterval;
        CalculateForces();
        ApplyForces();
    }

    private void CalculateForces()
    {
        // Reset forces on all nodes
        var forces = new Dictionary<GameObject, Vector3>();
        foreach (var node in _nodes)
        {
            forces[node] = Vector3.zero;
        }

        // Calculate repulsive forces
        for (var i = 0; i < _nodes.Count; i++)
        {
            for (var j = i + 1; j < _nodes.Count; j++)
            {
                var nodeA = _nodes[i];
                var nodeB = _nodes[j];
                var direction = nodeA.transform.position - nodeB.transform.position;
                var distance = direction.magnitude;

                // Apply repulsion only if nodes are close enough
                if (distance < minDistanceToRepel && distance > 0)
                {
                    float forceMagnitude = repulsionStrength / (distance * distance);
                    Vector3 force = direction.normalized * forceMagnitude;
                    forces[nodeA] += force;
                    forces[nodeB] -= force;
                }
                else if (distance > 0) // Add a weak long-range repulsion to help spread out initially
                {
                    var weakRepulsionMagnitude = repulsionStrength / (distance * distance * distance * 0.1f); // Weaker and falls off faster
                    Vector3 weakRepulsion = direction.normalized * weakRepulsionMagnitude;
                    forces[nodeA] += weakRepulsion;
                    forces[nodeB] -= weakRepulsion;
                }
            }
        }

        // Calculate attractive forces
        foreach (var connection in ScriptableObjectInventory.Instance.conSo.connections)
        {
            if (!_nodes.Contains(connection.startNode) || !_nodes.Contains(connection.endNode)) continue;
            var direction = connection.endNode.transform.position - connection.startNode.transform.position;
            var forceMagnitude = attractionStrength * direction.magnitude;
            var force = direction.normalized * forceMagnitude;
            forces[connection.startNode] += force;
            forces[connection.endNode] -= force;
        }

        // Apply calculated forces to velocities
        foreach (var node in _nodes)
        {
            _velocities[node] += forces[node] * updateInterval;
            _velocities[node] *= dampingFactor; // Apply damping to stabilize the layout
        }
    }

    private void ApplyForces()
    {
        foreach (var node in _nodes)
        {
            node.transform.position += _velocities[node] * updateInterval;
        }
    }

    private void OnDisable()
    {
        if (ScriptableObjectInventory.Instance.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered -= HandleEvent;
        if (ScriptableObjectInventory.Instance.clearEvent)
            ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered -= HandleEvent;
    }
    
    private void HandleEvent()
    {
        activated = false;
    }
}