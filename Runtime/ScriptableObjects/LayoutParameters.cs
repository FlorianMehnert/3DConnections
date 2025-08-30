namespace _3DConnections.Runtime.ScriptableObjects
{
    using UnityEngine;
    using Layout.Type;

#if UNITY_EDITOR
    [CreateAssetMenu(fileName = "NodeGraphLayoutParameters",
        menuName = "3DConnections/ScriptableObjects/LayoutParameters", order = 1)]
#endif
    public class LayoutParameters : ScriptableObject
    {
        public int layoutType = (int)LayoutType.Radial;
        public float minDistance = 2f;
        public float startRadius = 3f;
        public float radiusInc = 4f;
        public float rootSpacing = 10f;

        public float levelSpacing = 10f;
        public float nodeSpacing = 2f;
        public float subtreeSpacing = 2f;
        
        [Header("GRIP")]
        public float attractionStrength = 1f;
        public float repulsion = 100f;
        public float idealEdgeLength = 5f;
        public int iterations = 50;
        public float coolingFactor = 0.95f;
        public float initialTemperature = 10.0f;
        public float coarseningLevels = 3f;
        public float coarseningRatio = 0.5f;
    }
}