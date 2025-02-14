using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Internal representation of all connections between nodes for gameObject handling look into <see cref="3DConnections.Runtime.Managers.NodeConnectionManager"/>
/// </summary>
[CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/NodeConnections", order = 1)]
public class NodeConnectionsScriptableObject : ScriptableObject
{
    public List<NodeConnection> connections = new();
    public NativeArray<float3> NativeConnections;
    public bool usingNativeArray;
    public int currentConnectionCount;
}