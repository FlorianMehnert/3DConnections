using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace _3DConnections.Editor
{
    public class UpdateReferenceTracker : EditorWindow
    {
        private List<string> executionLog = new();
        private List<string> referenceLog = new();
        private bool isPaused;

        [MenuItem("Tools/Update Reference Tracker")]
        public static void ShowWindow()
        {
            GetWindow<UpdateReferenceTracker>("Update Reference Tracker");
        }

        private void OnEnable()
        {
            EditorApplication.update += Update;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            if (isPaused)
            {
                return;
            }

            TrackUpdateReferences();
        }

        private void TrackUpdateReferences()
        {
            executionLog.Clear();
            referenceLog.Clear();

            var allScripts = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var script in allScripts)
            {
                var updateMethod = script.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (updateMethod == null) continue;
                executionLog.Add(script.name);
                TrackReferences(script, updateMethod);
            }
        }

        private void TrackReferences(MonoBehaviour script, MethodInfo updateMethod)
        {
            // Use reflection to track references made during the Update method execution
            // This is a simplified example and may need to be adapted for your specific use case
            // You can use tools like Harmony or Mono.Cecil for more advanced instrumentation

            // Example: Log references to other MonoBehaviour components
            FieldInfo[] fields = script.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (typeof(MonoBehaviour).IsAssignableFrom(field.FieldType))
                {
                    referenceLog.Add($"{script.name} references {field.Name}");
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Update Reference Tracker", EditorStyles.boldLabel);

            if (GUILayout.Button("Pause/Resume Update Cycle"))
            {
                isPaused = !isPaused;
                EditorApplication.isPaused = isPaused;
            }

            GUILayout.Label("Execution Order:", EditorStyles.boldLabel);
            foreach (var entry in executionLog)
            {
                GUILayout.Label(entry);
            }

            GUILayout.Label("References:", EditorStyles.boldLabel);
            foreach (var entry in referenceLog)
            {
                GUILayout.Label(entry);
            }
        }
    }
}