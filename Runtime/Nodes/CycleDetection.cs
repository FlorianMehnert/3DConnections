using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CycleDetection : MonoBehaviour
{
    private Dictionary<GameObject, NodeConnections> _graph = new();
    
    public bool HasCycle(List<GameObject> nodes)
    {
        BuildGraph(nodes);
        
        HashSet<GameObject> visited = new();
        if (nodes.Any(node => !visited.Contains(node) && HasCycleIterative(node, visited)))
        {
            Debug.Log("Cycle detected!");
            return true;
        }
        Debug.Log("No cycles found.");
        return false;
    }

    private void BuildGraph(List<GameObject> nodes)
    {
        _graph.Clear();
        foreach (var node in nodes)
        {
            var connections = node.GetComponent<NodeConnections>();
            if (connections != null)
            {
                _graph[node] = connections;
            }
        }
    }

    private bool HasCycleIterative(GameObject startNode, HashSet<GameObject> visited)
    {
        Stack<(GameObject node, HashSet<GameObject> path)> stack = new();
        stack.Push((startNode, new HashSet<GameObject> { startNode }));

        while (stack.Count > 0)
        {
            var (currentNode, currentPath) = stack.Pop();
            visited.Add(currentNode);
            if (!_graph.TryGetValue(currentNode, out var value)) continue;
            foreach (var neighbor in value.outConnections)
            {
                if (currentPath.Contains(neighbor))
                    return true;

                if (visited.Contains(neighbor)) continue;
                var newPath = new HashSet<GameObject>(currentPath) { neighbor };
                stack.Push((neighbor, newPath));
            }
        }
        return false;
    }
}