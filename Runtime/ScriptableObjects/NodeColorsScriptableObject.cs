namespace _3DConnections.Runtime.ScriptableObjects
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/SpawnManager", order = 1)]
    public class NodeColorsScriptableObject : ScriptableObject
    {
        public Color nodeDefaultColor;
        public Color nodeSelectedColor;
        public Color nodeRootColor;

        public int palettePreset;
        public bool generateColor;

        public uint maxWidthHierarchy = 9;
    }
}