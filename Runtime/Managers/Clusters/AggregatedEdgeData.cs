namespace _3DConnections.Runtime.Managers.Clusters
{
    using System.Collections.Generic;
    using JetBrains.Annotations;
    using UnityEngine;
    public class AggregatedEdgeData : MonoBehaviour
    {
        [UsedImplicitly] public List<NodeConnection> originalConnections;
        [UsedImplicitly] public GameObject startCluster;
        [UsedImplicitly] public GameObject endCluster;
    }
}