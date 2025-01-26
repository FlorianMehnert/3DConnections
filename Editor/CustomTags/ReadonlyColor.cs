using UnityEditor;
using UnityEngine;

namespace _3DConnections.Editor.CustomTags
{
    // Custom attribute to mark a color as readonly
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class ReadonlyColor : PropertyAttribute { }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReadonlyColor))]
    public class ReadonlyColorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Ensure the property is a color
            if (property.propertyType != SerializedPropertyType.Color)
            {
                EditorGUI.LabelField(position, label.text, "Use ReadonlyColor with Color type only");
                return;
            }

            // Disable GUI to make it readonly
            EditorGUI.BeginDisabledGroup(true);
        
            // Draw the color field
            EditorGUI.ColorField(position, label, property.colorValue);
        
            EditorGUI.EndDisabledGroup();
        }
    }
#endif
}