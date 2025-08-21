namespace _3DConnections.Runtime.ScriptableObjects
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "AnalyzerConfigurations", menuName = "3DConnections/ScriptableObjects/AnalyzerConfigurations",
        order = 1)]
    public class AnalyzerConfigurations : ScriptableObject
    {
        public int sceneIndex;
        
        // using roslyn
        public bool lookupDynamicReferences;
    }
}