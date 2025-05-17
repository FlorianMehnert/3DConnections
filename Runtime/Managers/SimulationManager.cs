using System;
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
}