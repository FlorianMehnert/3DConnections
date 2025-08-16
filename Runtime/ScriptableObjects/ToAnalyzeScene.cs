namespace _3DConnections.Runtime.ScriptableObjects
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "ToAnalyzeScene", menuName = "3DConnections/ScriptableObjects/ToAnalyzeScene",
        order = 1)]
    public class ToAnalyzeScene : ScriptableObject
    {
        public int sceneIndex;
    }
}