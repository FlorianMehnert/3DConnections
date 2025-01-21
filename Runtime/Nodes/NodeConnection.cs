using UnityEngine;

namespace _3DConnections.Runtime
{
    [System.Serializable]
    public class NodeConnection
    {
        public GameObject startNode;
        public GameObject endNode;
        public LineRenderer lineRenderer;
        public Color connectionColor = new(1,255,50);
        public float lineWidth = 0.1f;
    }
}