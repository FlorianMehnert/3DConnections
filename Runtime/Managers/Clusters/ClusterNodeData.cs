using System.Collections.Generic;
using UnityEngine;

namespace _3DConnections.Runtime.Managers.Clusters
{
    /// <summary>
    /// Component that stores data about a cluster node and its contained nodes
    /// </summary>
    public class ClusterNodeData : MonoBehaviour
    {
        [Header("Cluster Information")] public List<GameObject> containedNodes = new();
        public int nodeCount;
        public string clusterName;

        [Header("Visual Properties")] public Color originalColor = Color.white;
        public Vector3 originalScale = Vector3.one;

        [Header("Metadata")] public List<string> componentTypes = new();
        public List<string> tags = new();
        public List<string> layers = new();
        public int hierarchyDepth;
        public string prefabSource;

        [Header("Statistics")] public int inboundConnections;
        public int outboundConnections;
        public float densityScore;

        private void Awake()
        {
            // Initialize with default values
            if (string.IsNullOrEmpty(clusterName))
            {
                clusterName = $"Cluster_{GetInstanceID()}";
            }
        }
    }
}