using UnityEngine;
using UnityEngine.SceneManagement;

namespace _3DConnections.Runtime.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/SceneAnalyzationConfig", order = 1)]
    public class ToAnalyzeSceneScriptableObject : ScriptableObject
    {
        public Scene scene;
    }
}