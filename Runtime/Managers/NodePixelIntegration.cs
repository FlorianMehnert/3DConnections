#if UNITY_EDITOR
namespace _3DConnections.Runtime.Managers
{
    using JetBrains.Annotations;
    using UnityEngine;
    using ScriptableObjectInventory;
    /// <summary>
    /// Integration component that coordinates between the LOD system and pixel size management
    /// Ensures both systems work together harmoniously
    /// </summary>
    [RequireComponent(typeof(NodePixelManager))]
    public class NodePixelIntegration : MonoBehaviour
    {
        [Header("Integration Settings")]
        [SerializeField] private bool enableWithLOD = true;
        [SerializeField] private bool prioritizeClusterNodes = true;
        [SerializeField] private float lodPixelSizeMultiplier = 1.2f;

        private NodePixelManager _pixelSizeManager;
        private GraphLODManager _lodManager;
        private Camera _camera;

        // State tracking
        private bool _lodWasActive;
        private float _lastLodLevel;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Get required components
            _pixelSizeManager = GetComponent<NodePixelManager>();
            _lodManager = FindFirstObjectByType<GraphLODManager>();
            _camera = ScriptableObjectInventory.Instance.overlay.GetCameraOfScene();

            if (!_pixelSizeManager)
            {
                Debug.LogError("NodePixelSizeIntegration: NodePixelSizeManager not found!");
                enabled = false;
                return;
            }

            if (_camera) return;
            Debug.LogError("NodePixelSizeIntegration: Camera not found!");
            enabled = false;
        }

        private void Update()
        {
            if (!enableWithLOD || !_lodManager) 
                return;

            // Check if the LOD state has changed
            bool lodCurrentlyActive = IsLODActive();
            float currentLodLevel = GetCurrentLODLevel();

            if (lodCurrentlyActive == _lodWasActive &&
                !(Mathf.Abs(currentLodLevel - _lastLodLevel) > 0.05f)) return;
            OnLODStateChanged(lodCurrentlyActive, currentLodLevel);
            _lodWasActive = lodCurrentlyActive;
            _lastLodLevel = currentLodLevel;
        }

        private bool IsLODActive()
        {
            // Check if the LOD system is currently active by looking for cluster nodes
            var allNodes = ScriptableObjectInventory.Instance.graph.AllNodes;
            if (allNodes == null) return false;

            foreach (var node in allNodes)
            {
                if (node && node.GetComponent<ClusterNodeData>())
                    return true;
            }
            return false;
        }

        private float GetCurrentLODLevel()
        {
            if (!_camera) return 0f;

            // Calculate LOD level based on camera zoom (same logic as GraphLODManager)
            float minZoom = 50f; // Should match GraphLODManager settings
            float maxZoom = 150f;
            float currentZoom = _camera.orthographicSize;

            return Mathf.Clamp01((currentZoom - minZoom) / (maxZoom - minZoom));
        }

        private void OnLODStateChanged(bool lodActive, float lodLevel)
        {
            if (lodActive)
            {
                // LOD is active - adjusting pixel size settings for cluster visibility
                AdjustPixelSizesForLOD(lodLevel);
            }
            else
            {
                // LOD is inactive - use normal pixel size settings
                RestoreNormalPixelSizes();
            }

            // Force refresh the pixel size manager to pick up new nodes/settings
            _pixelSizeManager.ForceRefresh();
        }

        private void AdjustPixelSizesForLOD(float lodLevel)
        {
            if (!_pixelSizeManager) return;

            // Increase minimum pixel sizes when LOD is active to ensure cluster visibility
            float multiplier = 1f + lodLevel * lodPixelSizeMultiplier;
            
            // Clusters should be more visible during LOD
            _pixelSizeManager.SetMinPixelSizeForNodeType(
                NodePixelManager.NodeType.Cluster, 
                6f * multiplier
            );

            // Important nodes should remain highly visible
            _pixelSizeManager.SetMinPixelSizeForNodeType(
                NodePixelManager.NodeType.Important, 
                8f * multiplier
            );

            // Regular nodes (if any remain visible) get a slight boost
            _pixelSizeManager.SetMinPixelSize(4f * Mathf.Sqrt(multiplier));

            if (prioritizeClusterNodes)
            {
                // Give clusters even more visibility boost
                _pixelSizeManager.SetMinPixelSizeForNodeType(
                    NodePixelManager.NodeType.Cluster, 
                    8f * multiplier
                );
            }
        }

        private void RestoreNormalPixelSizes()
        {
            if (!_pixelSizeManager) return;

            // Restore default pixel sizes
            _pixelSizeManager.SetMinPixelSize(4f);
            _pixelSizeManager.SetMinPixelSizeForNodeType(
                NodePixelManager.NodeType.Cluster, 
                6f
            );
            _pixelSizeManager.SetMinPixelSizeForNodeType(
                NodePixelManager.NodeType.Important, 
                8f
            );
        }

        // Public API for external control
        [UsedImplicitly]
        public void SetLODPixelSizeMultiplier(float multiplier)
        {
            lodPixelSizeMultiplier = multiplier;
            
            // Immediately apply if LOD is active
            if (!_lodWasActive) return;
            AdjustPixelSizesForLOD(_lastLodLevel);
            _pixelSizeManager.ForceRefresh();
        }

        [UsedImplicitly]
        public void EnableLODIntegration(bool enable)
        {
            enableWithLOD = enable;

            if (enable) return;
            RestoreNormalPixelSizes();
            _pixelSizeManager.ForceRefresh();
        }

        // Debug information
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            // Draw debug info about LOD state
            var style = new GUIStyle
            {
                normal =
                {
                    textColor = Color.white
                }
            };

            string info = $"LOD Active: {_lodWasActive}\n" +
                          $"LOD Level: {_lastLodLevel:F2}\n" +
                          $"Camera Zoom: {(_camera ? _camera.orthographicSize.ToString("F1") : "N/A")}";
            
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, info, style);
        }
    }
}
#endif