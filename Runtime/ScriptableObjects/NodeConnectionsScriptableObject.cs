using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/NodeConnections", order = 1)]
public class NodeConnectionsScriptableObject : ScriptableObject
{
    public List<NodeConnection> connections = new();
    public NativeArray<float3> nativeConnections;
    public bool usingNativeArray;
    public int currentConnectionCount;
    public GameObject lineRendererPrefab;
}