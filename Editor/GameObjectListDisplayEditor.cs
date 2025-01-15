using _3DConnections.Runtime;

namespace _3DConnections.Editor
{
    using UnityEngine;
    using UnityEditor;

    [CustomEditor(typeof(NodeConnections))]
    public class GameObjectListDisplayEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var script = (NodeConnections)target;

            // Display the "In Connections"
            EditorGUILayout.LabelField("In Connections", EditorStyles.boldLabel);
            if (script.inConnections is { Count: > 0 })
            {
                for (var i = 0; i < script.inConnections.Count; i++)
                {
                    script.inConnections[i] = (GameObject)EditorGUILayout.ObjectField(
                        $"In {i}",
                        script.inConnections[i],
                        typeof(GameObject),
                        true
                    );
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No In Connections defined.", MessageType.Info);
            }
            EditorGUILayout.Space();
            // Display the "Out Connections"
            EditorGUILayout.LabelField("Out Connections", EditorStyles.boldLabel);
            if (script.outConnections is { Count: > 0 })
            {
                for (var i = 0; i < script.outConnections.Count; i++)
                {
                    script.outConnections[i] = (GameObject)EditorGUILayout.ObjectField(
                        $"Out {i}",
                        script.outConnections[i],
                        typeof(GameObject),
                        true
                    );
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No Out Connections defined.", MessageType.Info);
            }

            // Optionally add buttons for managing the lists
            EditorGUILayout.Space();
            if (GUILayout.Button("Add In Connection"))
            {
                script.inConnections?.Add(null);
            }

            if (GUILayout.Button("Add Out Connection"))
            {
                script.outConnections?.Add(null);

            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(script);
            }
        }
    }
}