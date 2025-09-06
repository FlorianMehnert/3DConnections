using System.Collections.Generic;
using UnityEngine;

namespace _3DConnections.Runtime.Managers
{
    /// <summary>
    /// Component that handles moving clusters and their contained nodes
    /// </summary>
    public class ClusterMoveHandler : MonoBehaviour
    {
        private GraphLODManager _lodManager;
        private List<GameObject> _containedNodes;
        private Vector3 _lastPosition;

        public void Initialize(GraphLODManager lodManager, List<GameObject> containedNodes)
        {
            _lodManager = lodManager;
            _containedNodes = new List<GameObject>(containedNodes);
            _lastPosition = transform.position;
        }

        private void Update()
        {
            // Check if the cluster has moved
            if (!(Vector3.Distance(transform.position, _lastPosition) > 0.001f)) return;
            OnClusterMoved();
            _lastPosition = transform.position;
        }

        private void OnClusterMoved()
        {
            if (!_lodManager) return;
            _lodManager.OnClusterMoved(gameObject, transform.position);
            // Force-edge update when the cluster moves
            _lodManager.UpdateEdgeCumulation();
        }
    }
}