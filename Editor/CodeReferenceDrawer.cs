namespace _3DConnections.Editor
{
    using UnityEngine;
    using UnityEditor;

    [CustomPropertyDrawer(typeof(CodeReference))]
    public class CodeReferenceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Draw the default fields
            EditorGUI.BeginProperty(position, label, property);
            var sourceFile = property.FindPropertyRelative("sourceFile");
            var lineNumber = property.FindPropertyRelative("lineNumber");

            Rect fieldRect = new Rect(position.x, position.y, position.width - 60, position.height);
            Rect buttonRect = new Rect(position.x + position.width - 55, position.y, 55, position.height);

            EditorGUI.PropertyField(fieldRect, sourceFile, label);

            if (GUI.Button(buttonRect, "Open"))
            {
                if (!string.IsNullOrEmpty(sourceFile.stringValue) && lineNumber.intValue > 0)
                {
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(
                        sourceFile.stringValue, lineNumber.intValue);
                }
                else
                {
                    Debug.LogWarning("Invalid CodeReference: file path or line number missing.");
                }
            }

            EditorGUI.EndProperty();
        }
    }

}