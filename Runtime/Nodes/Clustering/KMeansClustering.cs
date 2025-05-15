using System.Collections.Generic;
using UnityEngine;

public class KMeansClustering : MonoBehaviour
{
    public int k = 3; // Number of clusters
    public int maxIterations = 100; // Maximum iterations for k-means
    private List<Vector3> _centroids;
    private Dictionary<GameObject, int> _nodeClusterMap;

#if UNITY_EDITOR
    [ContextMenu("Cluster Nodes")]
#endif
    private void DoClustering()
    {
        InitializeCentroids();
        _nodeClusterMap = new Dictionary<GameObject, int>();
        PerformKMeans();
        VisualizeClusters();
    }

    private void InitializeCentroids()
    {
        _centroids = new List<Vector3>();
        for (var i = 0; i < k; i++)
        {
            var randomPosition = ScriptableObjectInventory.Instance.graph.AllNodes[Random.Range(0, ScriptableObjectInventory.Instance.graph.AllNodes.Count)].transform.position;
            _centroids.Add(randomPosition);
        }
    }

    private void PerformKMeans()
    {
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var clustersChanged = AssignNodesToClusters();
            UpdateCentroids();
            if (!clustersChanged) break;
        }
    }

    private bool AssignNodesToClusters()
    {
        var clustersChanged = false;
        foreach (var node in ScriptableObjectInventory.Instance.graph.AllNodes)
        {
            var nodePosition = node.transform.position;
            var nearestCluster = -1;
            var minDistance = float.MaxValue;

            for (var i = 0; i < _centroids.Count; i++)
            {
                var distance = Vector3.Distance(nodePosition, _centroids[i]);
                if (!(distance < minDistance)) continue;
                minDistance = distance;
                nearestCluster = i;
            }

            if (_nodeClusterMap.ContainsKey(node) && _nodeClusterMap[node] == nearestCluster) continue;
            clustersChanged = true;
            _nodeClusterMap[node] = nearestCluster;
        }

        return clustersChanged;
    }

    private void UpdateCentroids()
    {
        // Reset centroids
        var newCentroids = new Vector3[k];
        var clusterSizes = new int[k];

        foreach (var (node, cluster) in _nodeClusterMap)
        {
            newCentroids[cluster] += node.transform.position;
            clusterSizes[cluster]++;
        }

        for (var i = 0; i < k; i++)
        {
            if (clusterSizes[i] > 0)
            {
                newCentroids[i] /= clusterSizes[i];
            }

            _centroids[i] = newCentroids[i];
        }
    }

    private void VisualizeClusters()
    {
        // Clear existing visualization first (optional)
        ClearVisualization();

        // Create a new sphere for each centroid
        for (var i = 0; i < k; i++)
        {
            var clusterArea = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            clusterArea.transform.position = _centroids[i];
            clusterArea.transform.localScale = new Vector3(50f, 50f, 0f);

            var material = new Material(Shader.Find("Standard"))
            {
                color = GetClusterColor(i)
            };

            clusterArea.GetComponent<Renderer>().material = material;
            clusterArea.name = $"Cluster {i + 1}";

            clusterArea.transform.parent = this.transform;
        }
    }

    private void ClearVisualization()
    {
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Cluster "))
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static Color GetClusterColor(int clusterIndex)
    {
        return (clusterIndex % 5) switch
        {
            0 => new Color(1, 0, 0, 0.2f) // Red
            ,
            1 => new Color(0, 1, 0, 0.2f) // Green
            ,
            2 => new Color(0, 0, 1, 0.2f) // Blue
            ,
            3 => new Color(1, 1, 0, 0.2f) // Yellow
            ,
            4 => new Color(1, 0, 1, 0.2f) // Magenta
            ,
            _ => new Color(0, 1, 1, 0.2f)
        };
    }
}