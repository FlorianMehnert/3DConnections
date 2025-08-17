#if UNITY_EDITOR
namespace _3DConnections.Runtime.Managers
{
    using UnityEngine;
    using ScriptableObjectInventory;
    /// <summary>
    /// Easy setup component for the Node Pixel Size system
    /// Adds this to a GameObject to automatically configure the pixel size management
    /// </summary>
    public class NodePixelSetup : MonoBehaviour
    {
        [Header("Auto Setup")]
        [SerializeField] private bool setupOnStart = true;
        [SerializeField] private bool integrateWithLOD = true;
        
        [Header("Compute Shader")]
        [SerializeField] private ComputeShader nodeScalingCompute;
        
        [Header("Default Settings")]
        [SerializeField] private float defaultMinPixelSize = 4f;
        [SerializeField] private float clusterMinPixelSize = 6f;
        [SerializeField] private float importantNodeMinPixelSize = 8f;

        private void Start()
        {
            if (setupOnStart)
            {
                SetupPixelSizeSystem();
            }
        }

        [ContextMenu("Setup Pixel Size System")]
        public void SetupPixelSizeSystem()
        {
            // Check prerequisites
            if (!ValidatePrerequisites())
            {
                Debug.LogError("NodePixelSetup: Prerequisites not met!");
                return;
            }

            // Create or get the pixel size manager
            NodePixelManager pixelManager = GetOrCreatePixelSizeManager();
            
            // Configure the pixel size manager
            ConfigurePixelSizeManager(pixelManager);

            // Set up LOD integration if requested
            if (integrateWithLOD)
            {
                SetupLODIntegration();
            }

            Debug.Log("NodePixelSetup: Pixel size system configured successfully!");
        }

        private bool ValidatePrerequisites()
        {
            // Check for ScriptableObjectInventory
            if (ScriptableObjectInventory.Instance == null)
            {
                Debug.LogError("ScriptableObjectInventory instance not found!");
                return false;
            }

            // Check for graph
            if (ScriptableObjectInventory.Instance.graph == null)
            {
                Debug.LogError("NodeGraphScriptableObject not found in ScriptableObjectInventory!");
                return false;
            }

            // Check for camera
            if (ScriptableObjectInventory.Instance.overlay?.GetCameraOfScene() == null)
            {
                Debug.LogError("Camera not found!");
                return false;
            }

            // Check for compute shader
            if (nodeScalingCompute != null) return true;
            Debug.LogError("Compute shader not assigned! Please assign NodeScalingCompute.compute");
            return false;

        }

        private NodePixelManager GetOrCreatePixelSizeManager()
        {
            // Check if a pixel size manager already exists
            NodePixelManager existing = FindFirstObjectByType<NodePixelManager>();
            if (existing != null)
            {
                Debug.Log("Found existing NodePixelManager, using it.");
                return existing;
            }

            // Create a new GameObject for the pixel size manager
            GameObject pixelSizeObject = new GameObject("NodePixelManager");
            pixelSizeObject.transform.SetParent(transform);
            
            return pixelSizeObject.AddComponent<NodePixelManager>();
        }

        private void ConfigurePixelSizeManager(NodePixelManager manager)
        {
            // Use reflection to set private fields (since they're SerializeField)
            var type = typeof(NodePixelManager);
            
            // Set compute shader
            var computeField = type.GetField("nodeScalingCompute", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            computeField?.SetValue(manager, nodeScalingCompute);

            // Set pixel sizes
            var defaultSizeField = type.GetField("defaultMinPixelSize", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            defaultSizeField?.SetValue(manager, defaultMinPixelSize);

            var clusterSizeField = type.GetField("clusterMinPixelSize", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            clusterSizeField?.SetValue(manager, clusterMinPixelSize);

            var importantSizeField = type.GetField("importantNodeMinPixelSize", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            importantSizeField?.SetValue(manager, importantNodeMinPixelSize);

            Debug.Log($"Configured NodePixelManager with sizes: Default={defaultMinPixelSize}, Cluster={clusterMinPixelSize}, Important={importantNodeMinPixelSize}");
        }

        private void SetupLODIntegration()
        {
            // Check if LOD manager exists
            GraphLODManager lodManager = FindFirstObjectByType<GraphLODManager>();
            if (lodManager == null)
            {
                Debug.LogWarning("GraphLODManager not found. LOD integration will be disabled.");
                return;
            }

            // Check if integration already exists
            NodePixelIntegration existing = FindFirstObjectByType<NodePixelIntegration>();
            if (existing != null)
            {
                Debug.Log("NodePixelIntegration already exists.");
                return;
            }

            // Add an integration component to the same GameObject as NodePixelManager
            NodePixelManager pixelManager = FindFirstObjectByType<NodePixelManager>();
            if (pixelManager == null) return;
            pixelManager.gameObject.AddComponent<NodePixelIntegration>();
            Debug.Log("Added NodePixelIntegration component.");
        }

        [ContextMenu("Remove Pixel Size System")]
        public void RemovePixelSizeSystem()
        {
            // Find and destroy pixel size manager
            NodePixelManager pixelManager = FindFirstObjectByType<NodePixelManager>();
            if (pixelManager != null)
            {
                DestroyImmediate(pixelManager.gameObject);
                Debug.Log("Removed NodePixelManager.");
            }

            // Find and destroy an integration component
            NodePixelIntegration integration = FindFirstObjectByType<NodePixelIntegration>();
            if (integration == null) return;
            DestroyImmediate(integration);
            Debug.Log("Removed NodePixelIntegration.");
        }

        [ContextMenu("Test Pixel Size System")]
        public void TestPixelSizeSystem()
        {
            NodePixelManager pixelManager = FindFirstObjectByType<NodePixelManager>();
            if (pixelManager == null)
            {
                Debug.LogError("NodePixelManager not found! Run setup first.");
                return;
            }

            // Test by temporarily changing camera zoom
            Camera cam = ScriptableObjectInventory.Instance.overlay.GetCameraOfScene();
            if (cam == null) return;
            float originalSize = cam.orthographicSize;
                
            Debug.Log("Testing pixel size system...");
                
            // Zoom out to make nodes tiny
            cam.orthographicSize = 200f;
            pixelManager.ForceRefresh();
                
            // Wait a moment, then restore
            StartCoroutine(RestoreCameraAfterDelay(cam, originalSize, 2f));
        }

        private System.Collections.IEnumerator RestoreCameraAfterDelay(Camera cam, float originalSize, float delay)
        {
            yield return new WaitForSeconds(delay);
            cam.orthographicSize = originalSize;
            Debug.Log("Camera zoom restored. Pixel size test complete.");
        }

        // Validation methods
        private void OnValidate()
        {
            // Ensure pixel sizes are reasonable
            defaultMinPixelSize = Mathf.Max(1f, defaultMinPixelSize);
            clusterMinPixelSize = Mathf.Max(defaultMinPixelSize, clusterMinPixelSize);
            importantNodeMinPixelSize = Mathf.Max(defaultMinPixelSize, importantNodeMinPixelSize);
        }

        // Inspector helpers
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;

            // Draw info about the current state
            var style = new GUIStyle
            {
                normal =
                {
                    textColor = Color.cyan
                }
            };

            NodePixelManager pixelManager = FindFirstObjectByType<NodePixelManager>();
            string status = pixelManager != null ? "Active" : "Not Setup";
            
            NodePixelIntegration integration = FindFirstObjectByType<NodePixelIntegration>();
            string lodStatus = integration != null ? "Integrated" : "No LOD Integration";
            
            string info = $"Pixel Size System: {status}\n" +
                         $"LOD Integration: {lodStatus}\n" +
                         $"Node Count: {ScriptableObjectInventory.Instance.graph?.NodeCount ?? 0}";
            
            UnityEditor.Handles.Label(transform.position, info, style);
        }
    }
}
#endif