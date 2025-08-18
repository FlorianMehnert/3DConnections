namespace _3DConnections.Runtime.ScriptableObjects
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/SpawnManager", order = 1)]
    public class NodeColorsScriptableObject : ScriptableObject
    {
        public static Color NodeSelectedColor;
        public static int ColorPreset;
        public static bool GenerateColors;
        
        public static Color GameObjectColor = new(0.2f, 0.6f, 1f); // Blue
        public static Color ComponentColor = new(0.4f, 0.8f, 0.4f); // Green
        public static Color ScriptableObjectColor = new(0.8f, 0.4f, 0.8f); // Purple
        public static Color AssetColor = new(0.1f, 0.9f, 0.9f); // Cyan
        public static Color ParentChildConnection = new(0.5f, 0.5f, 1f); // Light Blue
        public static Color ComponentConnection = new(0.5f, 1f, 0.5f); // Light Green
        public static Color ReferenceConnection = new(1f, 0f, 0.5f); // Pink
        public static Color DynamicComponentConnection = new(1f, 0.6f, 0f); // Orange
        public static Color UnityEventConnection = new(1f, 0.85f, 0f); // Gold

        public static uint MaxWidthHierarchy = 9;
        
        public static Color DimColor(Color color, float dim)
        {
            return new Color(color.r, color.g, color.b, dim);
        }
    }
}