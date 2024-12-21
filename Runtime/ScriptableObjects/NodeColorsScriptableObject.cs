using UnityEngine;

namespace _3DConnections.Runtime.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/SpawnManagerScriptableObject", order = 1)]
    public class NodeColorsScriptableObject : ScriptableObject
    {
        public Color nodeDefaultColor;
        public Color nodeSelectedColor;
        public Color nodeRootColor;
    }
}