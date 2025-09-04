namespace _3DConnections.Runtime.Managers
{
    using UnityEngine;
    using Nodes;

    public static class ConnectionExtension
    {
        /// <summary>
        /// Connects two nodes visually, with optional dashed line.
        /// </summary>
        /// <param name="inGameObject">Source node</param>
        /// <param name="outGameObject">Target node</param>
        /// <param name="connectionColor">Line color</param>
        /// <param name="depth">Hierarchy depth</param>
        /// <param name="connectionType">Type string</param>
        /// <param name="maxWidthHierarchy">Max width for line thickness</param>
        /// <param name="dashed">If true, draw a dashed line</param>
        public static void ConnectNodes(
            this GameObject inGameObject,
            GameObject outGameObject,
            Color connectionColor,
            int depth,
            string connectionType,
            uint maxWidthHierarchy,
            bool dashed = false)
        {
            if (NodeConnectionManager.Instance)
                NodeConnectionManager.Instance.AddConnection(
                    inGameObject,
                    outGameObject,
                    connectionColor,
                    lineWidth: Mathf.Clamp01(.98f - (float)depth / maxWidthHierarchy) + .1f,
                    saturation: Mathf.Clamp01(.9f - (float)depth / 10) + .1f,
                    connectionType: connectionType,
                    dashed: dashed
                );

            var inConnections = inGameObject.GetComponent<LocalNodeConnections>();
            var outConnections = outGameObject.GetComponent<LocalNodeConnections>();
            inConnections.outConnections.Add(outGameObject);
            outConnections.inConnections.Add(inGameObject);
        }
    }
}
