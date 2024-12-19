using System.Collections.Generic;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime
{
    public class ParentChildConnections : MonoBehaviour
    {
        /// <summary>
        /// Calculates connections between nodes in the current Unity scene
        /// </summary>
        /// <returns>A dictionary where each node maps to its connected child nodes</returns>
        internal static Dictionary<Node, HashSet<Node>> CalculateNodeConnections()
        {
            var nodeConnections = new Dictionary<Node, HashSet<Node>>();
            var allGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);

            foreach (var gameObject in allGameObjects)
            {
                var parentNode = CreateNodeFromGameObject(gameObject);
                if (!nodeConnections.ContainsKey(parentNode))
                {
                    nodeConnections[parentNode] = new HashSet<Node>();
                }

                var components = gameObject.GetComponents<Component>();
                foreach (var component in components)
                {
                    // Skip Transform and other base components
                    if (component is Transform) continue;

                    // Create a node for the component
                    var componentNode = CreateNodeFromComponent(component);
                
                    // Add the component node as a child of the parent node
                    nodeConnections[parentNode].Add(componentNode);
                }

                // Add child GameObjects as connections
                foreach (Transform childTransform in gameObject.transform)
                {
                    var childNode = CreateNodeFromGameObject(childTransform.gameObject);
                    nodeConnections[parentNode].Add(childNode);
                }
            }

            return nodeConnections;
        }

        /// <summary>
        /// Creates a Node from a GameObject
        /// </summary>
        private static Node CreateNodeFromGameObject(GameObject gameObject)
        {
            var rect = GetGameObjectRect(gameObject);
            return new Node(
                gameObject.name, 
                rect.x, 
                rect.y, 
                rect.width, 
                rect.height
            );
        }

        /// <summary>
        /// Creates a Node from a Component
        /// </summary>
        private static Node CreateNodeFromComponent(Component component)
        {
            // For components, use the parent GameObject's rect and append component type to name
            var rect = GetGameObjectRect(component.gameObject);
            return new Node(
                $"{component.gameObject.name}_{component.GetType().Name}", 
                rect.x, 
                rect.y, 
                rect.width, 
                rect.height
            );
        }

        /// <summary>
        /// Calculates the rectangle bounds of a GameObject
        /// </summary>
        private static Rect GetGameObjectRect(GameObject gameObject)
        {
            // Try to get Renderer component to determine bounds
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                return new Rect(
                    bounds.center.x - bounds.extents.x,
                    bounds.center.y - bounds.extents.y,
                    bounds.size.x,
                    bounds.size.y
                );
            }

            // Fallback to zero rect if no renderer
            return new Rect(0, 0, 0, 0);
        }
    }
}