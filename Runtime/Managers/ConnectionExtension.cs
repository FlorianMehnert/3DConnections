namespace _3DConnections.Runtime.Managers
{
    using UnityEngine;
    using Nodes;

    public static class ConnectionExtension
    {
        public static void ConnectNodes(this GameObject inGameObject, GameObject outGameObject, Color connectionColor,
            int depth, string connectionType, uint maxWidthHierarchy)
        {
            if (NodeConnectionManager.Instance)
                NodeConnectionManager.Instance.AddConnection(inGameObject, outGameObject, connectionColor,
                    lineWidth: Mathf.Clamp01(.98f - (float)depth / maxWidthHierarchy) + .1f,
                    saturation: Mathf.Clamp01(.9f - (float)depth / 10) + .1f, connectionType);
            var inConnections = inGameObject.GetComponent<LocalNodeConnections>();
            var outConnections = outGameObject.GetComponent<LocalNodeConnections>();
            inConnections.outConnections.Add(outGameObject);
            outConnections.inConnections.Add(inGameObject);
        }
    }
}
