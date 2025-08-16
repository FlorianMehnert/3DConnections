namespace _3DConnections.Runtime.Nodes
{
    using UnityEngine;

    /// <summary>
    /// Component that holds information about the node type. Used in the component-based node approach 
    /// </summary>
    public class NodeType : MonoBehaviour
    {
        [SerializeField] public NodeTypeName nodeTypeName;
        [SerializeField] public Object reference;

        public void SetNodeType(Object obj)
        {
            nodeTypeName = obj switch
            {
                GameObject => NodeTypeName.GameObject,
                Component => NodeTypeName.Component,
                ScriptableObject => NodeTypeName.ScriptableObject,
                _ => nodeTypeName
            };
        }
    }
}