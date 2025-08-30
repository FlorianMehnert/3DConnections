using System.Collections.Generic;
using System.Linq;
using _3DConnections.Runtime.Managers.Scene;
using UnityEngine;
using UnityEngine.Events;

namespace _3DConnections.Runtime.Managers
{
    using ScriptableObjects;


    /// <summary>
    /// Controls node scaling based on mouse proximity using compute shaders for performance
    /// <br/>
    /// Enable from code:
    /// <code>
    /// nodeProximityController.EnableSimulation();
    /// </code>
    /// Enable via UnityEvent (from UI button, trigger, etc.)
    /// Just drag the EnableSimulation method to your event
    /// Check status
    /// <code>
    /// if (nodeProximityController.IsSimulationEnabled)
    /// {
    ///    Debug.Log($"Running with {nodeProximityController.CurrentNodeCount} nodes");
    /// }
    /// </code>
    /// Toggle on/off
    /// <code>
    /// nodeProximityController.ToggleSimulation();
    /// </code>
    /// </summary>
    public class NodeProximityController : MonoBehaviour
    {
        private static readonly int NodePositions = Shader.PropertyToID("node_positions");
        private static readonly int ScaleFactors = Shader.PropertyToID("scale_factors");
        private static readonly int MousePosition = Shader.PropertyToID("mousePosition");
        private static readonly int MaxDistance = Shader.PropertyToID("maxDistance");
        private static readonly int MinScale = Shader.PropertyToID("minScale");
        private static readonly int MaxScale = Shader.PropertyToID("maxScale");
        private static readonly int DeltaTime = Shader.PropertyToID("deltaTime");
        private static readonly int LerpSpeed = Shader.PropertyToID("lerpSpeed");
        private static readonly int NodeCount = Shader.PropertyToID("nodeCount");

        [Header("Node Graph")] [SerializeField]
        private NodeGraphScriptableObject nodeGraph;

        [Header("Proximity Settings")] [SerializeField]
        private float maxDistance = 10f;

        [SerializeField] private float minScale = 1f;
        [SerializeField] private float maxScale = 10f;
        [SerializeField] private float lerpSpeed = 5f;

        [Tooltip("Amount of distance the mouse has to move before everything is recalculated")] [SerializeField]
        private float mouseMoveThreshold = 0.1f;

        [Header("Performance")] [SerializeField]
        private int maxNodes = 1000;

        [Header("Events")] [SerializeField]
        private UnityEvent onSimulationEnabled;

        [SerializeField] private UnityEvent onSimulationDisabled;

        // Compute shader and buffers
        [SerializeField] private ComputeShader proximityComputeShader;
        private ComputeBuffer _nodePositionsBuffer;
        private ComputeBuffer _scaleFactorsBuffer;
        private int _kernelIndex;

        // Mouse tracking
        private Vector3 _lastMousePosition;
        private Vector3 _currentMouseWorldPos;
        private Camera _camera;

        // Node data
        private List<GameObject> _cachedNodes;
        private Vector3[] _nodePositions;
        private float[] _scaleFactors;
        private Vector3[] _originalScales;

        // Performance tracking
        private float _lastUpdateTime;
        private const float UpdateInterval = 0.016f; // ~60 FPS

        // Simulation state
        private bool _isInitialized;

        // Public properties
        public bool IsSimulationEnabled { get; private set; }

        public int CurrentNodeCount => _cachedNodes?.Count ?? 0;
        public bool IsInitialized => _isInitialized;

        private void Awake()
        {
            // Initialize basic components but don't start simulation
            InitializeBasicComponents();
        }

        private void Update()
        {
            // Early exit if simulation is disabled
            if (!IsSimulationEnabled || !_isInitialized)
                return;

            if (Time.time - _lastUpdateTime < UpdateInterval)
                return;

            _lastUpdateTime = Time.time;

            // Check if the mouse moved significantly
            var currentMousePos = Input.mousePosition;
            if (Vector3.Distance(currentMousePos, _lastMousePosition) < mouseMoveThreshold)
                return;

            _lastMousePosition = currentMousePos;
            UpdateMouseWorldPosition();

            // Update node data if needed
            if (ShouldUpdateNodeData())
            {
                UpdateNodeData();
            }

            if (_cachedNodes == null || _cachedNodes.Count == 0)
                return;

            // Run compute shader
            RunProximityCompute();

            // Apply results to nodes
            ApplyScaleResults();
        }

        /// <summary>
        /// Enables the proximity scaling. Counts available nodes and sets up all necessary components
        /// </summary>
        /// <param name="forceReinitialize">If true, forces reinitialization even if already enabled</param>
        public void EnableSimulation(bool forceReinitialize = false)
        {
            switch (IsSimulationEnabled)
            {
                case true when !forceReinitialize:
                    Debug.LogWarning(
                        "NodeProximityController: Scaling is already active. Use forceReinitialize=true to reinitialize.");
                    return;
                case true:
                    DisableSimulation();
                    break;
            }

            if (!ValidateRequirements())
            {
                Debug.LogError("NodeProximityController: Missing required components or invalid setup.");
                return;
            }

            // Initialize all components for simulation
            if (!InitializeSimulationComponents())
            {
                Debug.LogError("NodeProximityController: Failed to initialize simulation components");
                return;
            }

            // Count and cache nodes
            CountAndCacheNodes();

            // Setup compute shader
            if (!SetupComputeShader())
            {
                Debug.LogError("NodeProximityController: Failed to setup compute shader.");
                CleanupSimulation();
                return;
            }

            // Initialize node data
            if (CurrentNodeCount > 0)
            {
                UpdateNodeData();
            }

            IsSimulationEnabled = true;
            _isInitialized = true;
            _lastMousePosition = Input.mousePosition;

            // Invoke events
            onSimulationEnabled?.Invoke();
        }

        /// <summary>
        /// Disables the proximity simulation and cleans up all resources to ensure zero performance impact.
        /// </summary>
        public void DisableSimulation()
        {
            if (!IsSimulationEnabled)
                return;

            IsSimulationEnabled = false;
            RestoreNodeScales();
            CleanupSimulation();
            onSimulationDisabled?.Invoke();
        }

        /// <summary>
        /// Toggles the simulation state
        /// </summary>
        public void ToggleSimulation()
        {
            if (IsSimulationEnabled)
                DisableSimulation();
            else
                EnableSimulation();
        }

        private void InitializeBasicComponents()
        {
            _camera = SceneHandler.GetCameraOfOverlayedScene();
            if (_camera == null)
                _camera = FindFirstObjectByType<Camera>();
        }

        private bool ValidateRequirements()
        {
            if (nodeGraph == null)
            {
                Debug.LogError("NodeGraphScriptableObject is not assigned!");
                return false;
            }

            if (proximityComputeShader == null)
            {
                Debug.LogError("Compute shader is not assigned!");
                return false;
            }

            if (_camera != null) return true;
            Debug.LogError("No camera found for mouse world position calculations!");
            return false;
        }

        private bool InitializeSimulationComponents()
        {
            // Reset initialization state
            _isInitialized = false;

            // Clear any existing data
            _cachedNodes?.Clear();
            _nodePositions = null;
            _scaleFactors = null;
            _originalScales = null;

            return true;
        }

        private void CountAndCacheNodes()
        {
            if (nodeGraph?.AllNodes == null)
            {
                _cachedNodes = new List<GameObject>();
                return;
            }

            // Filter out null nodes and respect the max nodes limit
            _cachedNodes = new List<GameObject>();
            var count = 0;

            foreach (var node in nodeGraph.AllNodes.Where(node => node != null && count < maxNodes))
            {
                _cachedNodes.Add(node);
                count++;
            }
        }

        private bool SetupComputeShader()
        {
            if (proximityComputeShader == null)
                return false;

            _kernelIndex = proximityComputeShader.FindKernel("CSMain");

            if (_kernelIndex >= 0) return true;
            Debug.LogError("Could not find CSMain kernel in compute shader!");
            return false;
        }

        private void UpdateMouseWorldPosition()
        {
            if (!_camera)
                return;

            // Convert mouse position to world space
            var mousePos = Input.mousePosition;
            mousePos.z = 10f;
            _currentMouseWorldPos = _camera.ScreenToWorldPoint(mousePos);
        }

        private bool ShouldUpdateNodeData()
        {
            return _nodePositions == null ||
                   _nodePositions.Length != _cachedNodes.Count;
        }

        private void UpdateNodeData()
        {
            if (_cachedNodes == null || _cachedNodes.Count == 0)
                return;

            int nodeCount = _cachedNodes.Count;

            // Initialize arrays
            _nodePositions = new Vector3[nodeCount];
            _scaleFactors = new float[nodeCount];
            _originalScales = new Vector3[nodeCount];

            // Get current positions and store original scales
            for (int i = 0; i < nodeCount; i++)
            {
                if (_cachedNodes[i] == null) continue;
                _nodePositions[i] = _cachedNodes[i].transform.position;
                _originalScales[i] = _cachedNodes[i].transform.localScale;
                _scaleFactors[i] = 1f; // Start with normal scale
            }

            // Update compute buffers
            UpdateComputeBuffers(nodeCount);
        }

        private void UpdateComputeBuffers(int nodeCount)
        {
            // Release existing buffers
            ReleaseBuffers();

            // Create new buffers
            _nodePositionsBuffer = new ComputeBuffer(nodeCount, sizeof(float) * 3);
            _scaleFactorsBuffer = new ComputeBuffer(nodeCount, sizeof(float));

            // Set initial data
            _nodePositionsBuffer.SetData(_nodePositions);
            _scaleFactorsBuffer.SetData(_scaleFactors);

            // Bind buffers to compute shader
            proximityComputeShader.SetBuffer(_kernelIndex, NodePositions, _nodePositionsBuffer);
            proximityComputeShader.SetBuffer(_kernelIndex, ScaleFactors, _scaleFactorsBuffer);
        }

        private void RunProximityCompute()
        {
            if (_nodePositionsBuffer == null || _scaleFactorsBuffer == null)
                return;

            int nodeCount = _cachedNodes.Count;

            // Update node positions
            for (int i = 0; i < nodeCount && i < _nodePositions.Length; i++)
            {
                if (_cachedNodes[i] != null)
                    _nodePositions[i] = _cachedNodes[i].transform.position;
            }

            _nodePositionsBuffer.SetData(_nodePositions);

            // Set compute shader parameters
            proximityComputeShader.SetVector(MousePosition,
                new Vector2(_currentMouseWorldPos.x, _currentMouseWorldPos.z));
            proximityComputeShader.SetFloat(MaxDistance, maxDistance);
            proximityComputeShader.SetFloat(MinScale, minScale);
            proximityComputeShader.SetFloat(MaxScale, maxScale);
            proximityComputeShader.SetFloat(DeltaTime, Time.deltaTime);
            proximityComputeShader.SetFloat(LerpSpeed, lerpSpeed);
            proximityComputeShader.SetInt(NodeCount, nodeCount);

            // Dispatch compute shader
            int threadGroups = Mathf.CeilToInt(nodeCount / 64f);
            proximityComputeShader.Dispatch(_kernelIndex, threadGroups, 1, 1);
        }

        private void ApplyScaleResults()
        {
            if (_scaleFactorsBuffer == null)
                return;

            // Get results from compute shader
            _scaleFactorsBuffer.GetData(_scaleFactors);

            // Apply scaling to nodes
            for (var i = 0; i < _cachedNodes.Count && i < _scaleFactors.Length; i++)
            {
                if (!_cachedNodes[i]) continue;
                var scaleFactor = _scaleFactors[i];
                var newScale = _originalScales[i] * scaleFactor;
                _cachedNodes[i].transform.localScale = new Vector3(newScale.x, newScale.y, _originalScales[i].z);
            }
        }

        private void RestoreNodeScales()
        {
            if (_cachedNodes == null || _originalScales == null) return;
            for (var i = 0; i < _cachedNodes.Count && i < _originalScales.Length; i++)
            {
                if (_cachedNodes[i] != null)
                {
                    _cachedNodes[i].transform.localScale = _originalScales[i];
                }
            }
        }

        private void ReleaseBuffers()
        {
            _nodePositionsBuffer?.Release();
            _scaleFactorsBuffer?.Release();
            _nodePositionsBuffer = null;
            _scaleFactorsBuffer = null;
        }

        private void CleanupSimulation()
        {
            ReleaseBuffers();

            _cachedNodes?.Clear();
            _cachedNodes = null;
            _nodePositions = null;
            _scaleFactors = null;
            _originalScales = null;
            _kernelIndex = -1;
            _isInitialized = false;
        }

        private void OnDestroy()
        {
            if (IsSimulationEnabled)
            {
                DisableSimulation();
            }
        }

        private void OnDisable()
        {
            if (IsSimulationEnabled)
            {
                RestoreNodeScales();
            }
        }

        // Editor validation
        private void OnValidate()
        {
            maxDistance = Mathf.Max(0.1f, maxDistance);
            minScale = Mathf.Max(0.1f, minScale);
            maxScale = Mathf.Max(minScale, maxScale);
            lerpSpeed = Mathf.Max(0.1f, lerpSpeed);
            maxNodes = Mathf.Max(1, maxNodes);
        }
    }
}