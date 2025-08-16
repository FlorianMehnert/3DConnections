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
    }
}