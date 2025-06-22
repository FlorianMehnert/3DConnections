using System;
using System.Linq;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [SerializeField] private Transform rootSimulation;

    private void OnEnable()
    {
        if (rootSimulation) return;
        var rootEdgeGameObject = GameObject.Find("Simulations");
        rootSimulation = rootEdgeGameObject.transform ? rootEdgeGameObject.transform : new GameObject("ParentEdgesObject").transform;
        ScriptableObjectInventory.Instance.simulationRoot = rootSimulation;
    }

    public void ApplyComponentPhysics()
    {
        if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
        ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
        var springSimulation = FindFirstObjectByType<SpringSimulation>();
        if (springSimulation)
            springSimulation.CleanupNativeArrays();

        ScriptableObjectInventory.Instance.graph.NodesAddComponent(typeof(Rigidbody2D));

        // required to avoid intersections when using components
        foreach (var boxCollider2D in ScriptableObjectInventory.Instance.graph.AllNodes.Select(node => node.GetComponent<BoxCollider2D>()))
        {
            boxCollider2D.isTrigger = false;
            boxCollider2D.size = Vector2.one * 5;
        }

        NodeConnectionManager.Instance.AddSpringsToConnections();
    }

    public void ApplyBurstPhysics()
    {
        Debug.Log("apply burst physics");
        var springSimulation = FindFirstObjectByType<SpringSimulation>();
        if (springSimulation)
        {
            if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
            ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
            NodeConnectionManager.Instance.UseNativeArray();
            ScriptableObjectInventory.Instance.graph.NodesAddComponent(typeof(Rigidbody2D));
            NodeConnectionManager.Instance.AddSpringsToConnections();
            NodeConnectionManager.Instance.ResizeNativeArray();
            NodeConnectionManager.Instance.ConvertToNativeArray();
            springSimulation.Simulate();
        }
        else
        {
            Debug.Log("missing springSimulation Script on the Manager");
        }
    }

    public void ApplyGPUPhysics()
    {
        Debug.Log("apply gpu physics");
        var gpuSpringSim = FindFirstObjectByType<ComputeSpringSimulation>();
        if (gpuSpringSim)
        {
            if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
            ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
            NodeConnectionManager.Instance.UseNativeArray();
            ScriptableObjectInventory.Instance.graph.NodesAddComponent(typeof(Rigidbody2D));
            NodeConnectionManager.Instance.AddSpringsToConnections();
            NodeConnectionManager.Instance.ResizeNativeArray();
            NodeConnectionManager.Instance.ConvertToNativeArray(); // convert connections to a burst array
            Debug.Log("initializing gpu physics");
            var springSimulation = GetComponent<SpringSimulation>();
            if (springSimulation)
                springSimulation.Disable();
            gpuSpringSim.Initialize();
        }
        else
        {
            Debug.Log("missing ComputeSpringSimulation Script on the Manager");
        }
    }

    public void ApplySimpleGPUPhysics()
    {
        var forceDirectedSim = FindFirstObjectByType<MinimalForceDirectedSimulation>();
        if (!forceDirectedSim) return;
        if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
        ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
        NodeConnectionManager.Instance.UseNativeArray();
        ScriptableObjectInventory.Instance.graph.NodesAddComponent(typeof(Rigidbody2D));
        NodeConnectionManager.Instance.AddSpringsToConnections();
        NodeConnectionManager.Instance.ResizeNativeArray();
        NodeConnectionManager.Instance.ConvertToNativeArray(); // convert connections to a burst array
        Debug.Log("initializing gpu physics");
        var springSimulation = GetComponent<SpringSimulation>();
        if (springSimulation)
            springSimulation.Disable();
        forceDirectedSim.nodeTransforms = ScriptableObjectInventory.Instance.graph.AllNodes.Select(node => node.transform).ToArray();
        forceDirectedSim.Initialize();
    }

    public void ApplyStaticLayout()
    {
        if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
        ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
        var springSimulation = FindFirstObjectByType<SpringSimulation>();
        if (springSimulation)
            springSimulation.CleanupNativeArrays();
        StaticNodeLayoutManager.Layout(ScriptableObjectInventory.Instance.layout, ScriptableObjectInventory.Instance.graph);
    }

    public void ApplyForceDirectedComponentPhysics()
    {
        var layout = ScriptableObjectInventory.Instance.simulationRoot.gameObject.GetComponent<ForceDirectedSimulationV2>();
        if (!layout) layout = ScriptableObjectInventory.Instance.simulationRoot.gameObject.AddComponent<ForceDirectedSimulationV2>();

        if (!layout) return;
        if (ScriptableObjectInventory.Instance.graph.AllNodes.Count <= 0) return;
        ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();
        layout.Initialize();
    }
}