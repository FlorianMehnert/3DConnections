using _3DConnections.Runtime.Managers;
using _3DConnections.Runtime.ScriptableObjects;
using _3DConnections.Runtime.Scripts;
using UnityEditor;
using UnityEngine;

namespace _3DConnections.Editor
{
    [CustomEditor(typeof(SceneAnalyzer))]
    public class SceneAnalyzerColorsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (SceneAnalyzer)target;
            
        }
    }
}