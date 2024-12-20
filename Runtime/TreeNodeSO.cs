using System.Collections.Generic;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime
{
    [CreateAssetMenu(fileName = "TreeNode", menuName = "3DConnections/TreeNode", order = 1)]

    public class TreeNodeSO : ScriptableObject
    {
        [SerializeField] public Node Node; // Name of the GameObject
        [SerializeField] public GameObject gameObjectReference; // Reference to the GameObject (optional)
        [SerializeField] public List<TreeNodeSO> children = new List<TreeNodeSO>();

        /// <summary>
        /// Initialize the tree node.
        /// </summary>
        /// <param name="name">The name of the node</param>
        /// <param name="gameObject">The reference to the GameObject</param>
        public void Initialize(Node node, GameObject gameObject)
        {
            Node = node;
            gameObjectReference = gameObject;
            children = new List<TreeNodeSO>();
        }
    }
}