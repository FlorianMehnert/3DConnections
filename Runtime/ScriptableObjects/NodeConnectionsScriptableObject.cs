using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/NodeConnections", order = 1)]
public class NodeConnectionsScriptableObject : ScriptableObject
{
    public List<NodeConnection> connections = new();
}