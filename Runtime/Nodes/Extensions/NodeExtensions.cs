using UnityEngine;

/// <summary>
/// This class is an extension for gameObjects. These extensions are useful in the context of creating node graphs
/// </summary>
public static class NodeExtensions
{
    /// <summary>
    /// Sets the material to according to the specified color for gameObjects, Components and ScriptableObjects
    /// </summary>
    /// <param name="go">the gameObject which is extended from</param>
    /// <param name="obj"></param>
    /// <param name="gameObjectColor">Color of the node if it is representing a gameObject</param>
    /// <param name="componentColor">Color of the node if it is representing a component</param>
    /// <param name="scriptableObjectColor">Color of the node if it is representing a scriptableObject</param>
    /// <param name="assetColor">Color of the node if it is representing an asset</param>
    /// <param name="isAsset"></param>
    public static void SetNodeColor(this GameObject go, Object obj, Color gameObjectColor, Color componentColor, Color scriptableObjectColor, Color assetColor, bool isAsset = false)
    {
        var componentRenderer = go.GetComponent<Renderer>();
        if (!componentRenderer) return;
        if (isAsset)
        {
            componentRenderer.material.color = assetColor;
        }
        else
        {
            componentRenderer.material.color = obj switch
            {
                GameObject => gameObjectColor,
                Component => componentColor,
                ScriptableObject => scriptableObjectColor,
                _ => Color.black
            };
        }
    }
    
    private static void ConnectNodes(GameObject inGameObject, GameObject outGameObject, Color connectionColor, int depth, string connectionType, uint maxWidthHierarchy)
    {
        if (NodeConnectionManager.Instance)
            NodeConnectionManager.Instance.AddConnection(inGameObject, outGameObject, connectionColor, lineWidth: Mathf.Clamp01(.98f - (float)depth / maxWidthHierarchy) + .1f, saturation: Mathf.Clamp01(.9f - (float)depth / 10) + .1f, connectionType);
        var inConnections = inGameObject.GetComponent<LocalNodeConnections>();
        var outConnections = outGameObject.GetComponent<LocalNodeConnections>();
        inConnections.outConnections.Add(outGameObject);
        outConnections.inConnections.Add(inGameObject);
    }
}