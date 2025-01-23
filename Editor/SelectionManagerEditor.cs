using UnityEditor;

namespace _3DConnections.Editor
{
    [CustomEditor(typeof(CubeSelector))]
    public class SelectionManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // Use SerializedObject to track changes
            serializedObject.Update();

            DrawDefaultInspector();

            var script = (CubeSelector)target;

            // Custom display logic
            EditorGUILayout.LabelField("Selected Nodes", script.GetSelectionCount().ToString());
            var selection = script.GetSelectionRectangle();
            EditorGUILayout.LabelField("Starting Position", $"x: {selection.x}, y: {selection.y}");
            EditorGUILayout.LabelField("Extend", $"width: {selection.width}, height: {selection.height}");
            // Apply changes and repaint
            serializedObject.ApplyModifiedProperties();
            Repaint();
        }
    }
}