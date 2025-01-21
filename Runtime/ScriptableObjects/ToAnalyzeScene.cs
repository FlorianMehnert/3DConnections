using UnityEngine;

namespace _3DConnections.Runtime.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/SceneAnalyzationConfig", order = 1)]
    public class ToAnalyzeSceneScriptableObject : ScriptableObject
    {
        // Scene which will be analyzed using tree config
        public SceneReference reference;
        
        // nodes representing the scene
        public float nodeStandardWidth;
        public float nodeStandardHeight;
    }
}