using UnityEditor;
using UnityEngine;

namespace _3DConnections.Editor
{
    [CustomEditor(typeof(SettingsMenuGeneral))]
    public class SettingsMenuEditor : UnityEditor.Editor
    {
        private SettingsMenuGeneral _nodeConnectionManager;

        private void OnEnable()
        {
            _nodeConnectionManager = (SettingsMenuGeneral)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.LabelField("Execute Functions of NodeConnectionsManager", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical();
            if (GUILayout.Button("Highlight Cycles"))
            {
                _nodeConnectionManager.DebugSelf();
            }

            EditorGUILayout.EndVertical();
        }
    }
}