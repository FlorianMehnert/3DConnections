using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Manages Level-of-Detail rendering for the node graph visualization
/// Implements density-based node aggregation and edge cumulation
/// </summary>
public class GraphLODManager : MonoBehaviour
{
    [Header("LOD Settings")]
    [SerializeField] private float minZoomForFullDetail = 50f;
    [SerializeField] private float maxZoomForMinDetail = 150f;
    [SerializeField] private int gridCellSize = 2;
    [SerializeField] private float nodeAggregationThreshold = 2f;
    
    [Header("Visual Settings")]
    [SerializeField] private GameObject clusterNodePrefab; // Prefab for aggregated nodes
    private Material _aggregatedEdgeMaterial;
    private const float AggregatedEdgeWidth = 2f;

    private Camera _camera;
    private Dictionary<Vector2Int, List<GameObject>> _spatialGrid;
    private Dictionary<Vector2Int, GameObject> _clusterNodes;
    private Dictionary<string, GameObject> _aggregatedEdges;
    private List<GameObject> _originalNodes;
    private List<NodeConnection> _originalConnections;
    
    private float _currentLODLevel;
    private bool _isLODActive;
    
    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        _camera = ScriptableObjectInventory.Instance.overlay.GetCameraOfScene();
        _spatialGrid = new Dictionary<Vector2Int, List<GameObject>>();
        _clusterNodes = new Dictionary<Vector2Int, GameObject>();
        _aggregatedEdges = new Dictionary<string, GameObject>();
        
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
        
        // Only update if LOD level changed significantly
        if (!(Mathf.Abs(newLODLevel - _currentLODLevel) > 0.1f)) return;
        _currentLODLevel = newLODLevel;
        UpdateLOD();
    }
    
    private float CalculateLODLevel(float zoom)
    {
        // Return value between 0 (full detail) and 1 (minimum detail)
        return Mathf.Clamp01((zoom - minZoomForFullDetail) / (maxZoomForMinDetail - minZoomForFullDetail));
    }
    
    private void UpdateLOD()
    {
        if (_currentLODLevel < 0.1f)
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
        var threshold = nodeAggregationThreshold * (1 + lodLevel);
        
        foreach (var cell in _spatialGrid)
        {
            if (!(cell.Value.Count >= threshold)) continue;
            // Create cluster node for this cell
            var clusterNode = CreateClusterNode(cell.Key, cell.Value);
            _clusterNodes[cell.Key] = clusterNode;
                
            // Hide individual nodes
            foreach (var node in cell.Value)
            {
                node.SetActive(false);
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
        cluster.transform.localScale = Vector3.one + new Vector3(1,1,0) * nodes.Count;

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

        ScriptableObjectInventory.Instance.graph.AllNodes.Add(cluster);

        return cluster;
    }

    
    private void ApplyEdgeCumulation()
    {
        // Group edges by their connected clusters
        Dictionary<string, List<NodeConnection>> edgeGroups = new Dictionary<string, List<NodeConnection>>();
        
        foreach (var connection in _originalConnections)
        {
            if (!connection.startNode || !connection.endNode) continue;
            
            // Find which cluster each node belongs to
            Vector2Int startGrid = GetClusterForNode(connection.startNode);
            Vector2Int endGrid = GetClusterForNode(connection.endNode);
            
            // Skip if both nodes are in the same cluster
            if (startGrid == endGrid) continue;
            
            var key = GetEdgeGroupKey(startGrid, endGrid);
            
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
    
    private Vector2Int GetClusterForNode(GameObject node)
    {
        float cellSize = gridCellSize * (1 + _currentLODLevel * 2);
        Vector2Int gridPos = GetGridPosition(node.transform.position, cellSize);
        
        // Check if this grid position has a cluster
        if (_clusterNodes.ContainsKey(gridPos))
        {
            return gridPos;
        }
        
        // Node is not clustered, return its own grid position
        return gridPos;
    }
    
    private string GetEdgeGroupKey(Vector2Int start, Vector2Int end)
    {
        // Ensure consistent key regardless of direction
        if (start.x < end.x || (start.x == end.x && start.y < end.y))
        {
            return $"{start.x},{start.y}_{end.x},{end.y}";
        }
        return $"{end.x},{end.y}_{start.x},{start.y}";
    }
    
    private void CreateAggregatedEdge(string key, List<NodeConnection> connections)
    {
        // Parse key to get grid positions
        string[] parts = key.Split('_');
        string[] start = parts[0].Split(',');
        string[] end = parts[1].Split(',');
        
        Vector2Int startGrid = new Vector2Int(int.Parse(start[0]), int.Parse(start[1]));
        Vector2Int endGrid = new Vector2Int(int.Parse(end[0]), int.Parse(end[1]));
        
        GameObject startNode = _clusterNodes.TryGetValue(startGrid, out var node) 
            ? node 
            : connections[0].startNode;
            
        GameObject endNode = _clusterNodes.TryGetValue(endGrid, out var clusterNode) 
            ? clusterNode 
            : connections[0].endNode;
        
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
        
        // Set color based on average of connections
        Color avgColor = connections.Aggregate(Color.black, (current, conn) => current + conn.connectionColor);

        avgColor /= connections.Count;
        avgColor.a = 0.7f;
        lr.startColor = avgColor;
        lr.endColor = avgColor;
        
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
            foreach (var connection in _originalConnections)
            {
                if (connection.lineRenderer)
                {
                    connection.lineRenderer.enabled = true;
                }
            }
        }
        
        // Clear LOD state
        ClearLODState();
    }
    
    private void ClearLODState()
    {
        // Remove cluster nodes
        foreach (var cluster in _clusterNodes.Values)
        {
            if (cluster)
            {
                ScriptableObjectInventory.Instance.graph.AllNodes.Remove(cluster);
                Destroy(cluster);
            }
        }
        _clusterNodes.Clear();
        
        // Remove aggregated edges
        foreach (var edge in _aggregatedEdges.Values.Where(edge => edge))
        {
            Destroy(edge);
        }
        _aggregatedEdges.Clear();
        
        _spatialGrid.Clear();
    }

    private void OnDestroy()
    {
        if (_isLODActive)
        {
            RestoreFullDetail();
        }
    }
}

// Helper component to store cluster data
public class ClusterNodeData : MonoBehaviour
{
    public List<GameObject> containedNodes;
    public int nodeCount;
}

// Helper component to store aggregated edge data
public class AggregatedEdgeData : MonoBehaviour
{
    public List<NodeConnection> originalConnections;
    public GameObject startCluster;
    public GameObject endCluster;
}
