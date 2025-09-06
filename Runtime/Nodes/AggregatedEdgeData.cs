using System.Collections.Generic;
using UnityEngine;

namespace _3DConnections.Runtime.Managers.Clusters
{
    /// <summary>
    /// Component that stores data about aggregated edges between clusters
    /// </summary>
    public class AggregatedEdgeData : MonoBehaviour
    {
        [Header("Edge Information")]
        public List<NodeConnection> originalConnections = new();
        public GameObject startCluster;
        public GameObject endCluster;
        
        [Header("Statistics")]
        public int connectionCount;
        public float averageStrength = 1f;
        public List<string> connectionTypes = new();
    }
}