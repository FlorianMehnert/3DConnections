namespace _3DConnections.Runtime.Nodes.Extensions
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Object = UnityEngine.Object;

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
        /// <param name="overrideColor"></param>
        public static void SetNodeColor(this GameObject go, Object obj, Color gameObjectColor, Color componentColor,
            Color scriptableObjectColor, Color assetColor, bool isAsset = false, Color overrideColor = default)
        {
            var componentRenderer = go.GetComponent<Renderer>();
            if (!componentRenderer) return;
            if (overrideColor != default)
            {
                componentRenderer.material.color = overrideColor;
                return;
            }

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

        public static void ScaleNodeUsingComplexityMap(this GameObject nodeObject, Component component,
            Dictionary<string, float> complexityMap)
        {
            // Check if the complexity value exists for the component's class name
            if (!complexityMap.TryGetValue(component.GetType().Name, out var complexity)) return;

            // compute all scales maybe and adjust
            var scaleFactor = Math.Abs(complexity - 90f) * 0.3f; // Clamp to prevent extreme scaling


            if (nodeObject && nodeObject.transform)
            {
                nodeObject.transform.localScale =
                    new Vector3(scaleFactor * 2, scaleFactor, nodeObject.transform.localScale.z);
            }
        }
    }
}