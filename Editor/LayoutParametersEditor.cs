using _3DConnections.Runtime.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace _3DConnections.Editor
{
    [CustomEditor(typeof(LayoutParameters))] // no [CanEditMultipleObjects]
    public class LayoutParametersEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var parameters = (LayoutParameters)target;
            if (GUILayout.Button("Reset All Parameters"))
            {
                parameters.ResetToDefaultParameters();
            }
        }
    }
}