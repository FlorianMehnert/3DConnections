using UnityEngine;

namespace _3DConnections.Runtime
{
    [CreateAssetMenu(fileName = "TreeData", menuName = "3DConnections/TreeData", order = 2)]
    public class TreeDataSO : ScriptableObject
    {
        [SerializeField] public TreeNodeSO rootNode; // The root node of the tree
    }
}