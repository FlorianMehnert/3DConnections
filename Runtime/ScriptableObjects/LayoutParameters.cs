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

        [Header("GRIP")] public float attractionStrength = 1f;
        public float repulsion = 100f;
        public float idealEdgeLength = 5f;
        public int iterations = 50;
        public float coolingFactor = 0.95f;
        public float initialTemperature = 10.0f;
        public float coarseningLevels = 3f;
        public float coarseningRatio = 0.5f;

        [Header("FM³")] public bool isDirected = true;
        public float directionalForce = 2f;
        public float cycleHandlingStrength = 0.5f;
        public bool useBarnesHut = true;


        public void ResetToDefaultParameters()
        {
            Debug.Log("resetting layout parameters to default parameters");
            minDistance = 2f;
            startRadius = 3f;
            radiusInc = 4f;
            rootSpacing = 10f;

            levelSpacing = 10f;
            nodeSpacing = 2f;
            subtreeSpacing = 2f;
            
            isDirected = true;

            attractionStrength = 1f;
            repulsion = 100f;
            idealEdgeLength = 5f;
            iterations = 50;
            coolingFactor = 0.95f;
            initialTemperature = 10.0f;
            coarseningLevels = 3f;
            coarseningRatio = 0.5f;

            directionalForce = 2f;
            cycleHandlingStrength = 0.5f;
            useBarnesHut = true;
        }
    }
}