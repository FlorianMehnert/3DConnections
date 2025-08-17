namespace _3DConnections.Runtime.Managers
{
    using System.Collections.Generic;
    using JetBrains.Annotations;
    using UnityEngine;
    public class ClusterNodeData : MonoBehaviour
    {
        public List<GameObject> containedNodes;
        [UsedImplicitly] public int nodeCount;
    }
}