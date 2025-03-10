using UnityEngine;

public class NodeType : MonoBehaviour
{
    [SerializeField] public NodeTypeName nodeTypeName;
    [SerializeField] public Object reference;

    public int GetNodeType()
    {
        return nodeTypeName switch
        {
            "GameObject" => 0,
            "Component" => 1,
            "ScriptableObject" => 2,
            _ => -1
        };
    }
}