namespace _3DConnections.Runtime.Managers
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using Nodes;
    
    using ScriptableObjectInventory;
    using Clusters;

    /// <summary>
    /// Manages Level-of-Detail rendering for the node graph visualization
    /// Implements density-based node aggregation and edge cumulation
    /// </summary>
    public class GraphLODManager : MonoBehaviour
    {
        [Header("LOD Settings")] [SerializeField]
        private float minZoomForFullDetail = 50f;

        [SerializeField] private float maxZoomForMinDetail = 150f;
        [SerializeField] private int gridCellSize = 2;
        [SerializeField] private float nodeAggregationThreshold = 1f;

        [Header("Visual Settings")] [SerializeField]
        public GameObject clusterNodePrefab; // Prefab for aggregated nodes

        private Material _aggregatedEdgeMaterial;
        private const float AggregatedEdgeWidth = 2f;

        private Camera _camera;
        private Dictionary<Vector2Int, List<GameObject>> _spatialGrid;
        private Dictionary<Vector2Int, GameObject> _clusterNodes;
        private Dictionary<string, GameObject> _aggregatedEdges;
        private List<GameObject> _originalNodes;
        private List<NodeConnection> _originalConnections;
        
        // Track which nodes are clustered
        private Dictionary<GameObject, Vector2Int> _nodeToClusterMap;
        private HashSet<GameObject> _clusteredNodes;

        private float _currentLODLevel;
        private bool _isLODActive;

        private void OnEnable()
        {
            _spatialGrid = new Dictionary<Vector2Int, List<GameObject>>();
            _clusterNodes = new Dictionary<Vector2Int, GameObject>();
            _aggregatedEdges = new Dictionary<string, GameObject>();
            _nodeToClusterMap = new Dictionary<GameObject, Vector2Int>();
            _clusteredNodes = new HashSet<GameObject>();
            ForceClearAllState();
            Initialize();
            RestoreFullDetail();
        }

        private void OnDisable()
        {
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

            _currentLODLevel = newLODLevel;
            UpdateLOD();
        }

        private void ForceClearAllState()
        {
            // Clear cluster nodes
            foreach (var cluster in _clusterNodes.Values.Where(cluster => cluster))
            {
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

            // Clear grids and lists
            _spatialGrid?.Clear();
            _originalNodes?.Clear();
            _originalConnections?.Clear();
            _nodeToClusterMap?.Clear();
            _clusteredNodes?.Clear();

            _isLODActive = false;
        }

        /// <summary>
        /// Calculates LOD level. Minimal zoom level is reached when Zoom is minZoomForFullDetail and Max is reached when zoom is MaxZoomForFullDetail
        /// </summary>
        /// <param name="zoom">Zoom of the camera currently tracking</param>
        /// <returns>Float between 0 and 1 representing the LOD level</returns>
        private float CalculateLODLevel(float zoom)
        {
            // Return value between 0 (full detail) and 1 (minimum detail)
            return Mathf.Clamp01((zoom - minZoomForFullDetail) / (maxZoomForMinDetail - minZoomForFullDetail));
        }

        private void UpdateLOD()
        {
            if (_currentLODLevel <= 0.01f)
            {
                // Show full detail
                if (_isLODActive)
                {
                    RestoreFullDetail();
                }
            }
            else
            {
                // Apply LOD
                ApplyLOD(_currentLODLevel);
            }
        }

        private void ApplyLOD(float lodLevel)
        {
            _isLODActive = true;

            // Store original state if not already stored
            if (_originalNodes == null)
            {
                _originalNodes = new List<GameObject>(ScriptableObjectInventory.Instance.graph.AllNodes);
                _originalConnections = new List<NodeConnection>(ScriptableObjectInventory.Instance.conSo.connections);
            }

            // Clear previous LOD state
            ClearLODState();

            // Build spatial grid for node aggregation
            BuildSpatialGrid(lodLevel);

            // Create cluster nodes for dense regions
            CreateClusterNodes(lodLevel);

            // Apply edge cumulation
            ApplyEdgeCumulation();
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

        private void CreateClusterNodes(float lodLevel)
        {
            var threshold = Mathf.Max(1, nodeAggregationThreshold - lodLevel * nodeAggregationThreshold * 0.5f);

            // Clear tracking collections
            _nodeToClusterMap.Clear();
            _clusteredNodes.Clear();

            foreach (var cell in _spatialGrid)
            {
                if (!(cell.Value.Count >= threshold)) continue;
                // Create a cluster node for this cell
                var clusterNode = CreateClusterNode(cell.Key, cell.Value);
                _clusterNodes[cell.Key] = clusterNode;

                // Track clustered nodes
                foreach (var node in cell.Value)
                {
                    node.SetActive(false);
                    _clusteredNodes.Add(node);
                    _nodeToClusterMap[node] = cell.Key;
                }
            }
        }

        private GameObject CreateClusterNode(Vector2Int gridPos, List<GameObject> nodes)
        {
            // Calculate cluster center
            Vector3 center = nodes.Aggregate(Vector3.zero, (current, node) => current + node.transform.position);
            center /= nodes.Count;

            // Create cluster node
            GameObject cluster = Instantiate(clusterNodePrefab, center, Quaternion.identity);
            cluster.name = $"Cluster_{gridPos.x}_{gridPos.y}";
            cluster.transform.SetParent(ScriptableObjectInventory.Instance.graph.AllNodes[0].transform.parent);

            // Store cluster data
            var clusterData = cluster.AddComponent<ClusterNodeData>();
            clusterData.containedNodes = new List<GameObject>(nodes);
            clusterData.nodeCount = nodes.Count;

            // Scale based on node count
            cluster.transform.localScale = Vector3.one + new Vector3(1, 1, 0) * nodes.Count;

            // Compute and assign average color
            var avgColor = Color.black;
            var count = 0;

            foreach (var node in nodes)
            {
                if (!node.TryGetComponent<Renderer>(out var rendererOfContainedNode)) continue;
                avgColor += rendererOfContainedNode.material.color;
                count++;
            }

            if (count > 0)
            {
                avgColor /= count;
                avgColor.a = 1f;
                if (cluster.TryGetComponent<Renderer>(out var clusterRenderer))
                {
                    // Ensure we are not modifying a shared material
                    clusterRenderer.material = new Material(clusterRenderer.material)
                    {
                        color = avgColor
                    };
                }
            }
            var coloredObject = cluster.GetComponent<ColoredObject>();
            coloredObject.SetOriginalColor(avgColor);

            ScriptableObjectInventory.Instance.graph.AllNodes.Add(cluster);
            return cluster;
        }


        private void ApplyEdgeCumulation()
        {
            // Group edges by their connected clusters
            var edgeGroups = new Dictionary<string, List<NodeConnection>>();

            foreach (var connection in _originalConnections)
            {
                if (!connection.startNode || !connection.endNode) continue;

                // Get the actual nodes that should represent the connection endpoints
                var startRepresentative = GetNodeRepresentative(connection.startNode);
                var endRepresentative = GetNodeRepresentative(connection.endNode);

                // Skip if both nodes are clustered into the same cluster
                if (startRepresentative == endRepresentative) continue;

                // Skip if either representative is null (shouldn't happen, but safety check)
                if (!startRepresentative || !endRepresentative) continue;

                var key = GetEdgeGroupKey(startRepresentative, endRepresentative);

                if (!edgeGroups.ContainsKey(key))
                {
                    edgeGroups[key] = new List<NodeConnection>();
                }

                edgeGroups[key].Add(connection);

                // Hide original edge
                if (connection.lineRenderer)
                {
                    connection.lineRenderer.enabled = false;
                }
            }

            // Create aggregated edges
            foreach (var group in edgeGroups.Where(group => group.Value.Count > 0))
            {
                CreateAggregatedEdge(group.Key, group.Value);
            }
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
            // (all connections in this group should have the same representatives)
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

            // Set color based on average of actual lineRenderer materials
            Color avgColor = connections.Aggregate(Color.black, (current, conn) => current + conn.lineRenderer.startColor);

            avgColor /= connections.Count;
            avgColor.a = 0.7f;
            lr.startColor = avgColor;
            lr.endColor = avgColor;
            
            var coloredObject = edgeObj.GetComponent<ColoredObject>();
            coloredObject.SetOriginalColor(avgColor);            

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

            // Clear LOD state
            ClearLODState();
        }

        private void ClearLODState()
        {
            // Remove cluster nodes
            foreach (var cluster in _clusterNodes.Values.Where(cluster => cluster))
            {
                ScriptableObjectInventory.Instance.graph.AllNodes.Remove(cluster);
                Destroy(cluster);
            }

            _clusterNodes.Clear();

            // Remove aggregated edges
            foreach (var edge in _aggregatedEdges.Values.Where(edge => edge))
            {
                Destroy(edge);
            }

            _aggregatedEdges.Clear();

            _spatialGrid.Clear();
            _nodeToClusterMap.Clear();
            _clusteredNodes.Clear();
        }

        private void OnDestroy()
        {
            if (_isLODActive)
            {
                RestoreFullDetail();
            }
        }

        public static void Init()
        {
            var lod = FindFirstObjectByType<GraphLODManager>();
            lod.Initialize();
        }
    }
}