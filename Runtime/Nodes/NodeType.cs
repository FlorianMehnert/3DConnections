using UnityEngine;

/// <summary>
/// Component that holds information about the node type. Used in the component-based node approach 
/// </summary>
public class NodeType : MonoBehaviour
{
    [SerializeField] public NodeTypeName nodeTypeName;
    [SerializeField] public Object reference;
}