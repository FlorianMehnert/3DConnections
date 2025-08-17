namespace _3DConnections.Runtime.Managers
{
    using System.Collections.Generic;
    using UnityEngine;
    using ScriptableObjectInventory;
    /// <summary>
    /// GPU-accelerated system for ensuring nodes maintain minimum pixel size visibility
    /// Uses compute shaders to process all nodes in parallel
    /// </summary>
    public class NodePixelManager : MonoBehaviour
    {
        [Header("Pixel Size Settings")]
        [SerializeField] private float defaultMinPixelSize = 4f;
        [SerializeField] private float clusterMinPixelSize = 6f;
        [SerializeField] private float importantNodeMinPixelSize = 8f;
        [SerializeField] private bool enableSmoothTransitions = true;
        [SerializeField] private float transitionSpeed = 5f;

        [Header("Performance Settings")]
        [SerializeField] private bool updateOnlyOnCameraChange = true;
        [SerializeField] private float cameraMovementThreshold = 0.1f;
        [SerializeField] private int maxNodesPerFrame = 1000;

        [Header("Compute Shader")]
        [SerializeField] private ComputeShader nodeScalingCompute;

        // GPU buffers
        private ComputeBuffer _nodeDataBuffer;
        private NodeData[] _nodeDataArray;
        
        // Cached references
        private Camera _camera;
        private Vector3 _lastCameraPosition;
        private float _lastCameraOrthoSize;
        
        // Node tracking
        private List<NodeInfo> _trackedNodes = new List<NodeInfo>();
        private Dictionary<GameObject, int> _nodeToBufferIndex = new Dictionary<GameObject, int>();
        
        // Compute shader kernel
        private int _mainKernel;
        
        // Performance tracking
        private int _currentUpdateIndex = 0;

        [System.Serializable]
        private struct NodeData
        {
            public Vector3 worldPosition;
            public Vector3 originalScale;
            public Vector3 currentScale;
            public float minPixelSize;
            public int nodeType; // 0: regular, 1: cluster, 2: important
            public float priority;
        }

        private class NodeInfo
        {
            public GameObject gameObject;
            public Transform transform;
            public Vector3 originalScale;
            public NodeType type;
            public bool isDirty;
        }

        public enum NodeType
        {
            Regular = 0,
            Cluster = 1,
            Important = 2
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Get camera reference
            _camera = ScriptableObjectInventory.Instance.overlay.GetCameraOfScene();
            if (!_camera)
            {
                Debug.LogError("NodePixelSizeManager: Could not find camera!");
                enabled = false;
                return;
            }

            // Initialize compute shader
            if (!nodeScalingCompute)
            {
                Debug.LogError("NodePixelSizeManager: No compute shader assigned!");
                enabled = false;
                return;
            }

            _mainKernel = nodeScalingCompute.FindKernel("CSMain");
            
            // Subscribe to node changes
            var graph = ScriptableObjectInventory.Instance.graph;
            if (graph != null)
            {
                graph.OnGoCountChanged += OnNodeCountChanged;
            }

            // Initial setup
            RefreshNodeList();
            InitializeGPUBuffers();
        }

        private void Update()
        {
            if (!_camera || _nodeDataArray == null || _nodeDataArray.Length == 0)
                return;

            // Check if we should update based on camera movement
            if (updateOnlyOnCameraChange && !HasCameraMovedSignificantly())
                return;

            // Update node scaling
            UpdateNodeScaling();
        }

        private bool HasCameraMovedSignificantly()
        {
            Vector3 currentPos = _camera.transform.position;
            float currentOrthoSize = _camera.orthographicSize;

            bool moved = Vector3.Distance(currentPos, _lastCameraPosition) > cameraMovementThreshold ||
                        Mathf.Abs(currentOrthoSize - _lastCameraOrthoSize) > cameraMovementThreshold;

            if (!moved) return moved;
            _lastCameraPosition = currentPos;
            _lastCameraOrthoSize = currentOrthoSize;

            return moved;
        }

        private void OnNodeCountChanged()
        {
            // Refresh our node list when nodes are added/removed
            RefreshNodeList();
            InitializeGPUBuffers();
        }

        private void RefreshNodeList()
        {
            _trackedNodes.Clear();
            _nodeToBufferIndex.Clear();

            var allNodes = ScriptableObjectInventory.Instance.graph.AllNodes;
            if (allNodes == null) return;

            for (int i = 0; i < allNodes.Count; i++)
            {
                var node = allNodes[i];
                if (!node) continue;

                var nodeInfo = new NodeInfo
                {
                    gameObject = node,
                    transform = node.transform,
                    originalScale = node.transform.localScale,
                    type = DetermineNodeType(node),
                    isDirty = true
                };

                _trackedNodes.Add(nodeInfo);
                _nodeToBufferIndex[node] = i;
            }
        }

        private NodeType DetermineNodeType(GameObject node)
        {
            // Check if it's a cluster node
            if (node.GetComponent<ClusterNodeData>())
                return NodeType.Cluster;

            // Check if it's marked as important (you can add your own logic here)
            if (node.name.ToLower().Contains("important") || node.name.ToLower().Contains("key"))
                return NodeType.Important;

            return NodeType.Regular;
        }

        private void InitializeGPUBuffers()
        {
            // Clean up existing buffer
            _nodeDataBuffer?.Release();

            if (_trackedNodes.Count == 0) return;

            // Create node data array
            _nodeDataArray = new NodeData[_trackedNodes.Count];
            
            // Initialize GPU buffer
            _nodeDataBuffer = new ComputeBuffer(_trackedNodes.Count, System.Runtime.InteropServices.Marshal.SizeOf<NodeData>());

            // Populate initial data
            PopulateNodeDataArray();
            _nodeDataBuffer.SetData(_nodeDataArray);
        }

        private void PopulateNodeDataArray()
        {
            for (int i = 0; i < _trackedNodes.Count; i++)
            {
                var nodeInfo = _trackedNodes[i];
                if (!nodeInfo.gameObject) continue;

                _nodeDataArray[i] = new NodeData
                {
                    worldPosition = nodeInfo.transform.position,
                    originalScale = nodeInfo.originalScale,
                    currentScale = nodeInfo.transform.localScale,
                    minPixelSize = GetMinPixelSizeForType(nodeInfo.type),
                    nodeType = (int)nodeInfo.type,
                    priority = GetPriorityForType(nodeInfo.type)
                };
            }
        }

        private float GetMinPixelSizeForType(NodeType type)
        {
            return type switch
            {
                NodeType.Cluster => clusterMinPixelSize,
                NodeType.Important => importantNodeMinPixelSize,
                _ => defaultMinPixelSize
            };
        }

        private float GetPriorityForType(NodeType type)
        {
            return type switch
            {
                NodeType.Important => 1f,
                NodeType.Cluster => 0.8f,
                _ => 0.5f
            };
        }

        private void UpdateNodeScaling()
        {
            if (_nodeDataBuffer == null || _trackedNodes.Count == 0)
                return;

            // Update node positions (only update a subset per frame for performance)
            UpdateNodePositions();

            // Set compute shader parameters
            Matrix4x4 viewProjection = _camera.projectionMatrix * _camera.worldToCameraMatrix;
            nodeScalingCompute.SetMatrix("viewProjectionMatrix", viewProjection);
            nodeScalingCompute.SetVector("screenSize", new Vector2(Screen.width, Screen.height));
            nodeScalingCompute.SetFloat("cameraOrthoSize", _camera.orthographicSize);
            nodeScalingCompute.SetFloat("deltaTime", Time.deltaTime);
            nodeScalingCompute.SetFloat("transitionSpeed", transitionSpeed);
            nodeScalingCompute.SetBool("enableSmoothTransitions", enableSmoothTransitions);

            // Set buffer
            nodeScalingCompute.SetBuffer(_mainKernel, "nodeBuffer", _nodeDataBuffer);

            // Dispatch compute shader
            int threadGroups = Mathf.CeilToInt(_trackedNodes.Count / 64f);
            nodeScalingCompute.Dispatch(_mainKernel, threadGroups, 1, 1);

            // Read back results and apply to GameObjects
            _nodeDataBuffer.GetData(_nodeDataArray);
            ApplyScalesToNodes();
        }

        private void UpdateNodePositions()
        {
            // Update positions for a subset of nodes each frame to avoid performance spikes
            int nodesToUpdate = Mathf.Min(maxNodesPerFrame, _trackedNodes.Count);
            int endIndex = Mathf.Min(_currentUpdateIndex + nodesToUpdate, _trackedNodes.Count);

            for (int i = _currentUpdateIndex; i < endIndex; i++)
            {
                var nodeInfo = _trackedNodes[i];
                if (!nodeInfo.gameObject) continue;

                _nodeDataArray[i].worldPosition = nodeInfo.transform.position;
            }

            _currentUpdateIndex = endIndex >= _trackedNodes.Count ? 0 : endIndex;

            // Update buffer with new positions
            _nodeDataBuffer.SetData(_nodeDataArray);
        }

        private void ApplyScalesToNodes()
        {
            for (int i = 0; i < _trackedNodes.Count; i++)
            {
                var nodeInfo = _trackedNodes[i];
                if (!nodeInfo.gameObject) continue;

                Vector3 newScale = _nodeDataArray[i].currentScale;
                
                // Only apply if scale has changed significantly
                if (Vector3.Distance(nodeInfo.transform.localScale, newScale) > 0.01f)
                {
                    nodeInfo.transform.localScale = newScale;
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up GPU buffer
            _nodeDataBuffer?.Release();

            // Unsubscribe from events
            var graph = ScriptableObjectInventory.Instance?.graph;
            if (graph != null)
            {
                graph.OnGoCountChanged -= OnNodeCountChanged;
            }

            // Restore original scales
            RestoreOriginalScales();
        }

        private void RestoreOriginalScales()
        {
            foreach (var nodeInfo in _trackedNodes)
            {
                if (nodeInfo.gameObject)
                {
                    nodeInfo.transform.localScale = nodeInfo.originalScale;
                }
            }
        }

        private void OnDisable()
        {
            RestoreOriginalScales();
        }

        // Public API
        public void ForceRefresh()
        {
            RefreshNodeList();
            InitializeGPUBuffers();
        }

        public void SetMinPixelSize(float size)
        {
            defaultMinPixelSize = size;
        }

        public void SetMinPixelSizeForNodeType(NodeType type, float size)
        {
            switch (type)
            {
                case NodeType.Cluster:
                    clusterMinPixelSize = size;
                    break;
                case NodeType.Important:
                    importantNodeMinPixelSize = size;
                    break;
            }
        }

        // Debug methods
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void DebugLogNodeInfo()
        {
            Debug.Log($"NodePixelSizeManager: Tracking {_trackedNodes.Count} nodes");
            foreach (var nodeInfo in _trackedNodes)
            {
                Debug.Log($"Node: {nodeInfo.gameObject.name}, Type: {nodeInfo.type}, Original Scale: {nodeInfo.originalScale}");
            }
        }
    }
}