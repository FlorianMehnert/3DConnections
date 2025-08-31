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
        /// Finds all UnityEvent fields in MonoBehaviours in the scene and creates connections for their persistent listeners.
        /// </summary>
        private void AnalyzeUnityEventPersistentConnections()
        {
            // Traverse all GameObjects in the loaded scene
            if (!ScriptableObjectInventory.Instance) return;
            var scenePath =
                SceneUtility.GetScenePathByBuildIndex(ScriptableObjectInventory.Instance.analyzerConfigurations
                    .sceneIndex);
            if (string.IsNullOrEmpty(scenePath))
            {
                Debug.LogError(
                    $"No scene found at build index {ScriptableObjectInventory.Instance.analyzerConfigurations.sceneIndex}");
                return;
            }

            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var scene = SceneManager.GetSceneByName(sceneName);

            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!mb) continue;
                    var type = mb.GetType();
                    var fields =
                        type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    foreach (var field in fields)
                    {
                        if (!typeof(UnityEventBase).IsAssignableFrom(field.FieldType)) continue;

                        if (field.GetValue(mb) is not UnityEventBase unityEvent) continue;

                        var count = unityEvent.GetPersistentEventCount();
                        for (var i = 0; i < count; i++)
                        {
                            var target = unityEvent.GetPersistentTarget(i) as Component;
                            var method = unityEvent.GetPersistentMethodName(i);

                            if (!target || string.IsNullOrEmpty(method)) continue;

                            // Find or create nodes for the source (mb) and target (target)
                            var sourceNode = FindNodeByComponentType(type);
                            var targetNode = FindNodeByComponentType(target.GetType());

                            // If not found, spawn them
                            if (!sourceNode)
                                sourceNode = GetOrSpawnNode(mb, 1);
                            if (!targetNode)
                                targetNode = GetOrSpawnNode(target, 1);

                            // Draw the connection (choose your color)
                            var color = new Color(0f, 1f, 0.86f, 0.9f);
                            sourceNode.ConnectNodes(targetNode, color, 1, $"unityEvent_{field.Name}_{method}", 1);
                        }
                    }
                }
            }
        }
#endif
    }
}