using _3DConnections.Runtime.Managers;
using UnityEngine;

namespace _3DConnections.Runtime.Nodes
{
    public static class NodeConnectionExtensions
    {
        public static GameObject ConnectNodesWithCodeReference(
            this GameObject startNode,
            GameObject endNode,
            Color connectionColor,
            int depth,
            string connectionType,
            float maxWidth,
            string sourceFile = null,
            int lineNumber = 0,
            string methodName = null,
            bool dashed = false)
        {
            var connection = NodeConnectionManager.Instance.AddConnection(
                startNode, 
                endNode, 
                connectionColor, 
                Mathf.Clamp01(.98f - depth / maxWidth) + .1f, 
                1f, 
                connectionType, 
                dashed,
                new CodeReference
                {
                    sourceFile = sourceFile,
                    lineNumber = lineNumber,
                    methodName = methodName,
                    className = startNode.name
                });
            
            return connection?.lineRenderer?.gameObject;
        }
    }

}