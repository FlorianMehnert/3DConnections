using System;
using JetBrains.Annotations;
using TMPro;

namespace _3DConnections.Runtime.Managers
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using Nodes;
    using ScriptableObjectInventory;
    using Clusters;
    using System.Collections;

    /// <summary>
    /// Manages Level-of-Detail rendering for the node graph visualization
    /// Implements density-based node aggregation, edge cumulation, and node introspection
    /// </summary>
    public class GraphLODManager : MonoBehaviour
    {
        [Header("LOD Settings")] 
        [SerializeField] private float nodeHeightThreshold = 20f; // Projected height threshold for merging
        [SerializeField] private float minZoomForFullDetail = 50f;
        [SerializeField] private float maxZoomForMinDetail = 150f;
        [SerializeField] private int gridCellSize = 2;
        [SerializeField] private float nodeAggregationThreshold = 1f;
        [SerializeField] private float lodUpdateThreshold = 0.02f; // Minimum change needed to update LOD
        
        [Header("Visual Settings")] 
        [SerializeField] public GameObject clusterNodePrefab; // Prefab for aggregated nodes
        [SerializeField] private float transitionSpeed = 5f; // Speed of LOD transitions
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 2f);
        
        private Material _aggregatedEdgeMaterial;
        private const float AggregatedEdgeWidth = 2f;
        private const float BaseNodeSize = 5f; // Base size for individual nodes

        private Camera _camera;
        private Dictionary<Vector2Int, List<GameObject>> _spatialGrid;
        private Dictionary<Vector2Int, GameObject> _clusterNodes;
        private Dictionary<string, GameObject> _aggregatedEdges;
        private List<GameObject> _originalNodes;
        private List<NodeConnection> _originalConnections;

        // Track which nodes are clustered
        private Dictionary<GameObject, Vector2Int> _nodeToClusterMap;
        private HashSet<GameObject> _clusteredNodes;
        private Dictionary<GameObject, List<GameObject>> _clusterToNodesMap; // cluster -> contained nodes

        // Cache for LOD state management
        private float _currentLODLevel;
        private float _lastAppliedLODLevel = -1f;
        private bool _isLODActive;
        private Dictionary<string, List<NodeConnection>> _cachedEdgeGroups;
        
        // Transition management
        private Dictionary<GameObject, Vector3> _targetPositions;
        private Dictionary<GameObject, Vector3> _targetScales;
        private Dictionary<GameObject, Color> _targetColors;
        private Coroutine _transitionCoroutine;
        
        // Move integration
        private Dictionary<GameObject, IMoveHandler> _moveHandlers;
        
        public interface IMoveHandler
        {
            void OnNodeMoved(GameObject node, Vector3 newPosition);
            void OnClusterMoved(GameObject cluster, Vector3 newPosition);
        }

        private void OnEnable()
        {
            _spatialGrid = new Dictionary<Vector2Int, List<GameObject>>();
            _clusterNodes = new Dictionary<Vector2Int, GameObject>();
            _aggregatedEdges = new Dictionary<string, GameObject>();
            _nodeToClusterMap = new Dictionary<GameObject, Vector2Int>();
            _clusteredNodes = new HashSet<GameObject>();
            _clusterToNodesMap = new Dictionary<GameObject, List<GameObject>>();
            _cachedEdgeGroups = new Dictionary<string, List<NodeConnection>>();
            _targetPositions = new Dictionary<GameObject, Vector3>();
            _targetScales = new Dictionary<GameObject, Vector3>();
            _targetColors = new Dictionary<GameObject, Color>();
            _moveHandlers = new Dictionary<GameObject, IMoveHandler>();
            
            ForceClearAllState();
            Initialize();
            RestoreFullDetail();
        }

        private void OnDisable()
        {
            // Stop any running transitions
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }
            
            // Always restore original graph visuals before disabling
            if (_isLODActive)
            {
                RestoreFullDetail();
            }

            // Clear all collections and temporary objects
            ForceClearAllState();
        }

        private void Initialize()
        {
            _camera = ScriptableObjectInventory.Instance.overlay.GetCameraOfScene();
            _originalNodes = new List<GameObject>(ScriptableObjectInventory.Instance.graph.AllNodes);
            _originalConnections = new List<NodeConnection>(ScriptableObjectInventory.Instance.conSo.connections);
            if (_isLODActive)
            {
                RestoreFullDetail();
            }
        }

        private void Update()
        {
            if (!_camera || !ScriptableObjectInventory.Instance.graph) return;

            var currentZoom = _camera.orthographicSize;
            var newLODLevel = CalculateLODLevel(currentZoom);

            // Only update if the change is significant enough
            if (!(Mathf.Abs(newLODLevel - _currentLODLevel) > lodUpdateThreshold)) return;
            _currentLODLevel = newLODLevel;
            UpdateLOD();
    
            // Add this: Update aggregated edge positions every frame when LOD is active
            if (_isLODActive)
            {
                UpdateAggregatedEdgePositions();
            }
        }

        private float CalculateProjectedNodeHeight(GameObject node)
        {
            if (!node || !_camera) return 0f;
            
            // Calculate the projected height of the node in screen space
            var bounds = GetNodeBounds(node);
            var worldHeight = bounds.size.y;
            
            // Convert world height to screen height
            var screenHeight = worldHeight / _camera.orthographicSize * Screen.height * 0.5f;
            return screenHeight;
        }

        private Bounds GetNodeBounds(GameObject node)
        {
            var nodeRenderer = node.GetComponent<Renderer>();
            return nodeRenderer ? nodeRenderer.bounds : new Bounds(node.transform.position, Vector3.one);
        }

        private void ForceClearAllState()
        {
            // Stop any running transitions
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }
            
            // Clear cluster nodes
            foreach (var cluster in _clusterNodes.Values.Where(cluster => cluster))
            {
                if (!ScriptableObjectInventory.Instance) continue;
                ScriptableObjectInventory.Instance.graph.AllNodes.Remove(cluster);
                DestroyImmediate(cluster);
            }

            _clusterNodes.Clear();
            
            // Clear aggregated edges
            foreach (var edge in _aggregatedEdges.Values)
            {
                if (edge) DestroyImmediate(edge);
            }

            _aggregatedEdges.Clear();

            // Clear all collections
            _spatialGrid?.Clear();
            _originalNodes?.Clear();
            _originalConnections?.Clear();
            _nodeToClusterMap?.Clear();
            _clusteredNodes?.Clear();
            _clusterToNodesMap?.Clear();
            _cachedEdgeGroups?.Clear();
            _targetPositions?.Clear();
            _targetScales?.Clear();
            _targetColors?.Clear();
            _moveHandlers?.Clear();

            _isLODActive = false;
            _lastAppliedLODLevel = -1f;
        }

        /// <summary>
        /// Calculates LOD level based on zoom and node projected height
        /// </summary>
        private float CalculateLODLevel(float zoom)
        {
            // Primary LOD calculation based on zoom
            var zoomBasedLOD = Mathf.Clamp01((zoom - minZoomForFullDetail) / (maxZoomForMinDetail - minZoomForFullDetail));
            
            // Secondary consideration: average projected node height
            var avgProjectedHeight = 0f;
            var visibleNodes = 0;
            
            foreach (var projectedHeight in from node in _originalNodes where node && node.activeInHierarchy select CalculateProjectedNodeHeight(node) into projectedHeight where projectedHeight > 0 select projectedHeight)
            {
                avgProjectedHeight += projectedHeight;
                visibleNodes++;
            }

            if (visibleNodes <= 0) return zoomBasedLOD;
            avgProjectedHeight /= visibleNodes;
                
            // If nodes are too small on screen, increase the LOD level
            if (!(avgProjectedHeight < nodeHeightThreshold)) return zoomBasedLOD;
            var heightBasedLOD = 1f - (avgProjectedHeight / nodeHeightThreshold);
            zoomBasedLOD = Mathf.Max(zoomBasedLOD, heightBasedLOD);

            return zoomBasedLOD;
        }

        private void UpdateLOD()
        {
            if (_currentLODLevel <= 0.01f)
            {
                // Show full detail
                if (_isLODActive)
                {
                    StartSmoothTransition(RestoreFullDetail);
                }
            }
            else
            {
                // Apply LOD only if the level changed significantly
                if (!(Mathf.Abs(_currentLODLevel - _lastAppliedLODLevel) > lodUpdateThreshold)) return;
                StartSmoothTransition(() => ApplyLOD(_currentLODLevel));
                _lastAppliedLODLevel = _currentLODLevel;
            }
        }
        
        public void ForceUpdateEdges()
        {
            if (!_isLODActive) return;
            UpdateEdgeCumulation();
            UpdateAggregatedEdgePositions();
        }

        
        private void UpdateAggregatedEdgePositions()
        {
            foreach (var edgePair in _aggregatedEdges)
            {
                if (!edgePair.Value) continue;
        
                var edgeData = edgePair.Value.GetComponent<AggregatedEdgeData>();
                if (!edgeData || !edgeData.startCluster || !edgeData.endCluster) continue;
        
                var lr = edgePair.Value.GetComponent<LineRenderer>();
                if (!lr) continue;
        
                // Update the line renderer positions to follow the clusters
                lr.SetPosition(0, edgeData.startCluster.transform.position);
                lr.SetPosition(1, edgeData.endCluster.transform.position);
            }
        }


        private void StartSmoothTransition(Action targetState)
        {
            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            _transitionCoroutine = StartCoroutine(SmoothTransitionCoroutine(targetState));
        }

        private IEnumerator SmoothTransitionCoroutine(Action targetState)
        {
            // Prepare a target state
            targetState.Invoke();
    
            // Animate transitions over a few frames
            const float transitionTime = 0.3f;
            var elapsed = 0f;
    
            while (elapsed < transitionTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / transitionTime;
                t = Mathf.SmoothStep(0f, 1f, t);
        
                // Interpolate positions, scales, and colors
                foreach (var kvp in _targetPositions.Where(kvp => kvp.Key))
                {
                    kvp.Key.transform.position = Vector3.Lerp(kvp.Key.transform.position, kvp.Value, t);
                }
        
                if (_isLODActive)
                {
                    UpdateAggregatedEdgePositions();
                }
        
                yield return null;
            }
            
            // Clear transition targets
            _targetPositions.Clear();
            _targetScales.Clear();
            _targetColors.Clear();
            
            _transitionCoroutine = null;
        }

        private void ApplyLOD(float lodLevel)
        {
            _isLODActive = true;

            if (_originalNodes == null)
            {
                _originalNodes = new List<GameObject>(ScriptableObjectInventory.Instance.graph.AllNodes);
                _originalConnections = new List<NodeConnection>(ScriptableObjectInventory.Instance.conSo.connections);
            }

            // Build spatial grid for node aggregation
            BuildSpatialGrid(lodLevel);

            // Update cluster nodes with smooth transitions
            UpdateClusterNodes(lodLevel);

            // Apply edge cumulation
            UpdateEdgeCumulation();
        }

        private void BuildSpatialGrid(float lodLevel)
        {
            _spatialGrid.Clear();

            var cellSize = gridCellSize * (1 + lodLevel * 2); // Increase cell size with LOD

            foreach (var node in _originalNodes)
            {
                if (!node) continue;

                var gridPos = GetGridPosition(node.transform.position, cellSize);

                if (!_spatialGrid.ContainsKey(gridPos))
                {
                    _spatialGrid[gridPos] = new List<GameObject>();
                }

                _spatialGrid[gridPos].Add(node);
            }
        }

        private static Vector2Int GetGridPosition(Vector3 worldPos, float cellSize)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / cellSize),
                Mathf.FloorToInt(worldPos.y / cellSize)
            );
        }

        private void UpdateClusterNodes(float lodLevel)
        {
            var threshold = Mathf.Max(1, nodeAggregationThreshold - lodLevel * nodeAggregationThreshold * 0.5f);

            // Track which clusters are still needed
            var activeClusterPositions = new HashSet<Vector2Int>();
            
            // Clear tracking collections for rebuild
            _nodeToClusterMap.Clear();
            _clusteredNodes.Clear();

            // First pass: identify which clusters should exist
            foreach (var cell in _spatialGrid.Where(cell => cell.Value.Count >= threshold))
            {
                activeClusterPositions.Add(cell.Key);
            }

            // Remove clusters that are no longer needed
            var clustersToRemove = (from clusterPair in _clusterNodes where !activeClusterPositions.Contains(clusterPair.Key) select clusterPair.Key).ToList();

            foreach (var clusterPos in clustersToRemove)
            {
                if (_clusterNodes.TryGetValue(clusterPos, out var clusterToRemove) && clusterToRemove)
                {
                    ScriptableObjectInventory.Instance.graph.AllNodes.Remove(clusterToRemove);
                    _clusterToNodesMap.Remove(clusterToRemove);
                    Destroy(clusterToRemove);
                }
                _clusterNodes.Remove(clusterPos);
            }

            // Create or update existing clusters
            foreach (var cell in _spatialGrid.Where(cell => cell.Value.Count >= threshold))
            {
                if (_clusterNodes.ContainsKey(cell.Key))
                {
                    // Update existing cluster
                    UpdateExistingCluster(cell.Key, cell.Value);
                }
                else
                {
                    // Create a new cluster
                    var clusterNode = CreateClusterNode(cell.Key, cell.Value);
                    _clusterNodes[cell.Key] = clusterNode;
                    _clusterToNodesMap[clusterNode] = new List<GameObject>(cell.Value);
                }

                // Track clustered nodes
                foreach (var node in cell.Value)
                {
                    node.SetActive(false);
                    _clusteredNodes.Add(node);
                    _nodeToClusterMap[node] = cell.Key;
                }
            }

            // Ensure non-clustered nodes are visible
            foreach (var node in _originalNodes.Where(node => node && !_clusteredNodes.Contains(node)))
            {
                node.SetActive(true);
            }
        }

        private void UpdateExistingCluster(Vector2Int gridPos, List<GameObject> nodes)
        {
            if (!_clusterNodes.TryGetValue(gridPos, out var cluster) || !cluster) return;

            // Calculate a new center position
            Vector3 center = nodes.Aggregate(Vector3.zero, (current, node) => current + node.transform.position);
            center /= nodes.Count;
            
            // Set up a smooth transition
            _targetPositions[cluster] = center;

            // Update cluster data
            var clusterData = cluster.GetComponent<ClusterNodeData>();
            if (clusterData)
            {
                clusterData.containedNodes = new List<GameObject>(nodes);
                clusterData.nodeCount = nodes.Count;
            }

            // Update contained node mapping
            _clusterToNodesMap[cluster] = new List<GameObject>(nodes);

            // Calculate target scale based on node count (with a curve)
            var scaleMultiplier = scaleCurve.Evaluate(nodes.Count / 10f); // Normalize to a reasonable range
            var targetScale = new Vector3(1, 1, 1) * (BaseNodeSize * scaleMultiplier);
            targetScale.z = 1; // Ensure z-axis is always 1
            _targetScales[cluster] = targetScale;
            
            

            // Calculate target color using HSV averaging
            var avgColor = CalculateAverageColor(nodes, 
                node => node.TryGetComponent<Renderer>(out var r) ? r.material.color : Color.black, 
                useHSV: true);
            _targetColors[cluster] = avgColor;

            // Update ColoredObject if present
            var coloredObject = cluster.GetComponent<ColoredObject>();
            if (coloredObject)
            {
                coloredObject.SetOriginalColor(avgColor);
            }
        }

        private GameObject CreateClusterNode(Vector2Int gridPos, List<GameObject> nodes)
        {
            // Calculate cluster center
            Vector3 center = nodes.Aggregate(Vector3.zero, (current, node) => current + node.transform.position);
            center /= nodes.Count;

            // Create cluster node
            GameObject cluster = Instantiate(clusterNodePrefab, center, Quaternion.identity);
            cluster.GetComponent<Renderer>().sortingOrder = 0; // Clusters should be on top of edges
            cluster.name = $"Cluster_{gridPos.x}_{gridPos.y} ({nodes.Count} nodes)";
            cluster.transform.SetParent(ScriptableObjectInventory.Instance.graph.AllNodes[0].transform.parent);
            
            // label to display contained nodes
            var text = cluster.GetComponentInChildren<TextMeshPro>();
            text.sortingOrder = 1; // Label should be on top of the cluster
            text.text = $"{nodes.Count}";
            text.alignment = TextAlignmentOptions.Center;

            // Store cluster data
            var clusterData = cluster.AddComponent<ClusterNodeData>();
            clusterData.containedNodes = new List<GameObject>(nodes);
            clusterData.nodeCount = nodes.Count;

            // Scale based on node count with a curve
            var scaleMultiplier = scaleCurve.Evaluate(nodes.Count / 10f);
            var newScale = Vector3.one * (BaseNodeSize * scaleMultiplier);
            newScale.z = 1; // Ensure z-axis is always 1
            cluster.transform.localScale = newScale;

            // Compute average HSV color
            var avgColor = CalculateAverageColor(nodes, 
                node => node.TryGetComponent<Renderer>(out var r) ? r.material.color : Color.black, 
                useHSV: true);

            if (cluster.TryGetComponent<Renderer>(out var clusterRenderer))
            {
                // Ensure we are not modifying a shared material
                clusterRenderer.material = new Material(clusterRenderer.material)
                {
                    color = avgColor
                };
            }

            var coloredObject = cluster.GetComponent<ColoredObject>();
            if (coloredObject)
            {
                coloredObject.SetOriginalColor(avgColor);
            }

            // Add a move handler for cluster movement
            var moveHandler = cluster.AddComponent<ClusterMoveHandler>();
            moveHandler.Initialize(this, nodes);

            ScriptableObjectInventory.Instance.graph.AllNodes.Add(cluster);
            return cluster;
        }

        /// <summary>
        /// Register a move handler for a specific node or cluster
        /// </summary>
        public void RegisterMoveHandler(GameObject obj, IMoveHandler handler)
        {
            _moveHandlers[obj] = handler;
        }

        /// <summary>
        /// Unregister a move handler
        /// </summary>
        public void UnregisterMoveHandler(GameObject obj)
        {
            _moveHandlers.Remove(obj);
        }

        /// <summary>
        /// Called when a cluster is moved - moves all contained nodes accordingly
        /// </summary>
        public void OnClusterMoved(GameObject cluster, Vector3 newPosition)
        {
            if (!_clusterToNodesMap.TryGetValue(cluster, out var containedNodes)) return;

            Vector3 deltaMove = newPosition - cluster.transform.position;

            // Move all contained nodes by the same delta
            foreach (var node in containedNodes)
            {
                if (!node) continue;
                node.transform.position += deltaMove;
            }

            // Update cluster position
            cluster.transform.position = newPosition;

            // Notify any registered move handlers
            if (_moveHandlers.TryGetValue(cluster, out var handler))
            {
                handler.OnClusterMoved(cluster, newPosition);
            }
        }

        private static Color CalculateAverageColor<T>(IEnumerable<T> items, Func<T, Color> colorSelector, bool useHSV = true)
        {
            int count = 0;

            if (useHSV)
            {
                float sumH = 0f, sumS = 0f, sumV = 0f;

                foreach (var item in items)
                {
                    var col = colorSelector(item);
                    Color.RGBToHSV(col, out float h, out float s, out float v);

                    sumH += h;
                    sumS += s;
                    sumV += v;
                    count++;
                }

                if (count == 0) return Color.black;

                float avgH = sumH / count;
                float avgS = sumS / count;
                float avgV = sumV / count;

                return Color.HSVToRGB(avgH, avgS, avgV);
            }
            else
            {
                // RGB mixing
                Color sum = Color.black;
                foreach (var item in items)
                {
                    sum += colorSelector(item);
                    count++;
                }

                if (count == 0) return Color.black;
                return sum / count;
            }
        }

        public void UpdateEdgeCumulation()
        {
            // Group edges by their connected clusters
            var currentEdgeGroups = new Dictionary<string, List<NodeConnection>>();

            foreach (var connection in _originalConnections)
            {
                if (!connection.startNode || !connection.endNode) continue;

                // Get the actual nodes that should represent the connection endpoints
                var startRepresentative = GetNodeRepresentative(connection.startNode);
                var endRepresentative = GetNodeRepresentative(connection.endNode);

                // Skip if both nodes are clustered into the same cluster
                if (startRepresentative == endRepresentative) continue;

                // Skip if either representative is null
                if (!startRepresentative || !endRepresentative) continue;

                var key = GetEdgeGroupKey(startRepresentative, endRepresentative);

                if (!currentEdgeGroups.ContainsKey(key))
                {
                    currentEdgeGroups[key] = new List<NodeConnection>();
                }

                currentEdgeGroups[key].Add(connection);
            }

            // Remove aggregated edges that are no longer needed
            var edgesToRemove = new List<string>();
            foreach (var edgePair in _aggregatedEdges)
            {
                if (!currentEdgeGroups.ContainsKey(edgePair.Key))
                {
                    edgesToRemove.Add(edgePair.Key);
                }
            }

            foreach (var edgeKey in edgesToRemove)
            {
                if (_aggregatedEdges.TryGetValue(edgeKey, out var edgeToRemove) && edgeToRemove)
                {
                    Destroy(edgeToRemove);
                }
                _aggregatedEdges.Remove(edgeKey);
            }

            // Update or create aggregated edges
            foreach (var group in currentEdgeGroups.Where(group => group.Value.Count > 0))
            {
                if (_aggregatedEdges.ContainsKey(group.Key))
                {
                    // Update existing edge
                    UpdateAggregatedEdge(group.Key, group.Value);
                }
                else
                {
                    // Create a new aggregated edge
                    CreateAggregatedEdge(group.Key, group.Value);
                }
            }

            // Hide/show original edges based on whether they're part of the aggregation
            foreach (var connection in _originalConnections)
            {
                if (!connection.startNode || !connection.endNode || !connection.lineRenderer) continue;

                var startRep = GetNodeRepresentative(connection.startNode);
                var endRep = GetNodeRepresentative(connection.endNode);
                
                // Hide if part of an aggregation, show if not
                bool shouldHide = startRep != connection.startNode || endRep != connection.endNode;
                connection.lineRenderer.enabled = !shouldHide;
            }

            _cachedEdgeGroups = currentEdgeGroups;
        }

        private void UpdateAggregatedEdge(string key, List<NodeConnection> connections)
        {
            if (!_aggregatedEdges.TryGetValue(key, out var edgeObj) || !edgeObj) return;

            var startNode = GetNodeRepresentative(connections[0].startNode);
            var endNode = GetNodeRepresentative(connections[0].endNode);

            if (!startNode || !endNode) return;

            var lr = edgeObj.GetComponent<LineRenderer>();
            if (!lr) return;
            
            lr.sortingLayerName = "Default";
            lr.sortingOrder = -1; // Edges behind clusters

            // Set up a smooth transition for edge positions
            _targetPositions[edgeObj] = Vector3.Lerp(startNode.transform.position, endNode.transform.position, 0.5f);

            // Update positions immediately for now (could be smoothed)
            lr.SetPosition(0, startNode.transform.position);
            lr.SetPosition(1, endNode.transform.position);

            // Update visual properties based on aggregation
            float width = Mathf.Sqrt(connections.Count) * AggregatedEdgeWidth * 0.5f;
            lr.startWidth = width;
            lr.endWidth = width;

            // Update color
            var avgColor = CalculateAverageColor(connections, 
                conn => conn.lineRenderer.startColor, 
                useHSV: true);

            avgColor.a = 0.7f; // slightly transparent
            _targetColors[edgeObj] = avgColor;

            // Update edge data
            var edgeData = edgeObj.GetComponent<AggregatedEdgeData>();
            if (!edgeData) return;
            edgeData.originalConnections = connections;
            edgeData.startCluster = startNode;
            edgeData.endCluster = endNode;
        }

        private GameObject GetNodeRepresentative(GameObject node)
        {
            // If the node is clustered, return the cluster node
            if (_clusteredNodes.Contains(node) && _nodeToClusterMap.TryGetValue(node, out var clusterGrid))
            {
                return _clusterNodes[clusterGrid];
            }

            // If the node is not clustered, return the original node (it should still be visible)
            return node;
        }

        private static string GetEdgeGroupKey(GameObject startNode, GameObject endNode)
        {
            // Create a consistent key based on the actual GameObjects
            var startId = startNode.GetInstanceID();
            var endId = endNode.GetInstanceID();

            // Ensure a consistent key regardless of direction
            return startId < endId ? $"{startId}_{endId}" : $"{endId}_{startId}";
        }

        private void CreateAggregatedEdge(string key, List<NodeConnection> connections)
        {
            // Get the representative nodes from the first connection
            var startNode = GetNodeRepresentative(connections[0].startNode);
            var endNode = GetNodeRepresentative(connections[0].endNode);

            if (!startNode || !endNode) return;

            // Create aggregated edge
            GameObject edgeObj = Instantiate(
                NodeConnectionManager.Instance.lineRendererPrefab,
                ScriptableObjectInventory.Instance.edgeRoot
            );

            LineRenderer lr = edgeObj.GetComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, startNode.transform.position);
            lr.SetPosition(1, endNode.transform.position);

            // Set visual properties based on aggregation
            float width = Mathf.Sqrt(connections.Count) * AggregatedEdgeWidth * 0.5f;
            lr.startWidth = width;
            lr.endWidth = width;

            if (_aggregatedEdgeMaterial)
            {
                lr.material = _aggregatedEdgeMaterial;
            }

            var avgColor = CalculateAverageColor(connections, 
                conn => conn.lineRenderer.startColor, 
                useHSV: true);

            avgColor.a = 0.7f; // slightly transparent
            lr.startColor = avgColor;
            lr.endColor = avgColor;

            var coloredObject = edgeObj.GetComponent<ColoredObject>();
            if (coloredObject)
            {
                coloredObject.SetOriginalColor(avgColor);
            }

            // Store aggregated edge data
            var edgeData = edgeObj.AddComponent<AggregatedEdgeData>();
            edgeData.originalConnections = connections;
            edgeData.startCluster = startNode;
            edgeData.endCluster = endNode;

            _aggregatedEdges[key] = edgeObj;
        }

        private void RestoreFullDetail()
        {
            _isLODActive = false;
            _lastAppliedLODLevel = -1f;

            // Restore all original nodes
            if (_originalNodes != null)
            {
                foreach (var node in _originalNodes)
                {
                    if (node) node.SetActive(true);
                }
            }

            // Restore all original edges
            if (_originalConnections != null)
            {
                foreach (var connection in _originalConnections.Where(connection => connection.lineRenderer))
                {
                    connection.lineRenderer.enabled = true;
                }
            }

            // Hide LOD elements without destroying them
            HideLODElements();
        }

        private void HideLODElements()
        {
            // Hide cluster nodes instead of destroying them
            foreach (var cluster in _clusterNodes.Values.Where(cluster => cluster))
            {
                cluster.SetActive(false);
            }

            // Hide aggregated edges instead of destroying them
            foreach (var edge in _aggregatedEdges.Values.Where(edge => edge))
            {
                edge.SetActive(false);
            }

            _nodeToClusterMap.Clear();
            _clusteredNodes.Clear();
        }

        private void OnDestroy()
        {
            if (_isLODActive)
            {
                RestoreFullDetail();
            }
            ForceClearAllState();
        }

        public static void Init()
        {
            var lod = FindFirstObjectByType<GraphLODManager>();
            lod?.Initialize();
        }

        /// <summary>
        /// Get all nodes contained in a cluster
        /// </summary>
        public List<GameObject> GetClusterContents(GameObject cluster)
        {
            return _clusterToNodesMap.TryGetValue(cluster, out var nodes) ? new List<GameObject>(nodes) : new List<GameObject>();
        }

        /// <summary>
        /// Check if a node is currently clustered
        /// </summary>
        public bool IsNodeClustered(GameObject node)
        {
            return _clusteredNodes.Contains(node);
        }

        /// <summary>
        /// Get the cluster that contains a specific node
        /// </summary>
        public GameObject GetClusterForNode(GameObject node)
        {
            if (_nodeToClusterMap.TryGetValue(node, out var gridPos))
            {
                return _clusterNodes.GetValueOrDefault(gridPos);
            }
            return null;
        }

        /// <summary>
        /// Force refresh of LOD system
        /// </summary>
        [UsedImplicitly]
        public void ForceRefresh()
        {
            _lastAppliedLODLevel = -1f;
            UpdateLOD();
        }

        
        /// <summary>
        /// Enable or disable smooth transitions
        /// </summary>
        [UsedImplicitly]
        public void SetTransitionsEnabled(bool enableTransition)
        {
            if (enableTransition || _transitionCoroutine == null) return;
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
                
            // Apply all target states immediately
            foreach (var kvp in _targetPositions)
            {
                if (kvp.Key) kvp.Key.transform.position = kvp.Value;
            }
            foreach (var kvp in _targetScales)
            {
                if (!kvp.Key) continue;
                Vector3 startScale = new Vector3(kvp.Key.transform.localScale.x, kvp.Key.transform.localScale.y, 1f);
                Vector3 targetScale = new Vector3(kvp.Value.x, kvp.Value.y, 1f);
                kvp.Key.transform.localScale = Vector3.Lerp(startScale, targetScale, 1);
            }
            foreach (var kvp in _targetColors)
            {
                if (!kvp.Key) continue;
                var objectRenderer = kvp.Key.GetComponent<Renderer>();
                if (objectRenderer) objectRenderer.material.color = kvp.Value;
            }
                
            _targetPositions.Clear();
            _targetScales.Clear();
            _targetColors.Clear();
        }
    }
}