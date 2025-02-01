using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/NativeNodeConnections", order = 1)]
public class NativeNodeConnectionsScriptableObject : ScriptableObject
{
    private NativeArray<float3> _nativeConnections;
}