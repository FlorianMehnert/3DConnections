using System.IO;
using UnityEngine.SceneManagement;

namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEngine.Events;
    using System.Reflection;
#endif

    using System.Linq;
    using ScriptableObjectInventory;

    public partial class SceneAnalyzer
    {
        /// <summary>
        /// Used in the CustomEditor. Types that will be ignored when traversing the scene. For example, Transform could be ignored,
        /// resulting in a cleaner graph.
        /// </summary>
        /// <returns>List of ignored component types</returns>
        private List<Type> GetIgnoredTypes()
        {
            return ignoredTypes.Select(Type.GetType).Where(type => type != null).ToList();
        }
#if UNITY_EDITOR
    /// <summary>
    /// Call this inside TraverseComponent to connect UnityEvent persistent listeners.
    /// </summary>
    /// <param name="component">The component to analyze for UnityEvent fields.</param>
    /// <param name="parentNodeObject">The parent node in the graph.</param>
    /// <param name="depth">The current depth in the graph.</param>
    private void ConnectUnityEventPersistentListeners(Component component, GameObject parentNodeObject, int depth)
    {
        if (component == null) return;

        var type = component.GetType();
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            if (!typeof(UnityEventBase).IsAssignableFrom(field.FieldType)) continue;

            var unityEvent = field.GetValue(component) as UnityEventBase;
            if (unityEvent == null) continue;

            int count = unityEvent.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                var target = unityEvent.GetPersistentTarget(i) as Component;
                var method = unityEvent.GetPersistentMethodName(i);

                if (target == null || string.IsNullOrEmpty(method)) continue;

                // Find or create nodes for the source (component) and target (target)
                var sourceNode = FindNodeByComponentType(type) ?? GetOrSpawnNode(component, depth, parentNodeObject);
                var targetNode = FindNodeByComponentType(target.GetType()) ?? GetOrSpawnNode(target, depth + 1, sourceNode);

                // Draw the connection (choose your color)
                var color = new Color(1f, 0.8f, 0.2f, 0.9f); // yellowish for UnityEvent
                sourceNode.ConnectNodes(targetNode, color, depth + 1, $"unityEvent_{field.Name}_{method}", 1, dashed: true);

                Debug.Log($"[SceneAnalyzer] UnityEvent: {type.Name}.{field.Name} -> {target.GetType().Name}.{method}");
            }
        }
    }
#endif
    }
}