namespace _3DConnections.Runtime.Managers
{
    
    using UnityEngine;
    using UnityEngine.EventSystems;
    using Clusters;
    

    /// <summary>
    /// Allows interactive expansion of cluster nodes
    /// </summary>
    public class ClusterInteraction : MonoBehaviour, IPointerClickHandler
    {
        private GraphLODManager _lodManager;

        private void Start()
        {
            _lodManager = FindFirstObjectByType<GraphLODManager>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var clusterData = GetComponent<ClusterNodeData>();
            if (clusterData != null && _lodManager != null)
            {
                // Temporarily show contained nodes
                StartCoroutine(ExpandCluster(clusterData));
            }
        }

        private System.Collections.IEnumerator ExpandCluster(ClusterNodeData clusterData)
        {
            // Hide cluster
            gameObject.SetActive(false);

            // Show contained nodes
            foreach (var node in clusterData.containedNodes)
            {
                if (node) node.SetActive(true);
            }

            // Wait for a few seconds
            yield return new WaitForSeconds(3f);

            // Re-hide nodes and show cluster again (if still in LOD mode)
            foreach (var node in clusterData.containedNodes)
            {
                if (node) node.SetActive(false);
            }

            gameObject.SetActive(true);
        }
    }
}