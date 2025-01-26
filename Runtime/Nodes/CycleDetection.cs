using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CycleDetection : MonoBehaviour
{
    private Dictionary<GameObject, NodeConnections> _graph = new();

    public bool HasCycle(List<GameObject> nodes, out List<List<GameObject>> cycles)
    {
        BuildGraph(nodes);
        HashSet<GameObject> visited = new();
        cycles = new List<List<GameObject>>();

        foreach (var node in nodes.Where(node => !visited.Contains(node)))
            FindCyclesIterative(node, visited, cycles);

        if (cycles.Count > 0)
        {
            Debug.Log($"Cycle detected! Found {cycles.Count} cycles.");
            foreach (var cycle in cycles)
            {
                Debug.Log("Cycle: " + string.Join(" -> ", cycle.Select(n => n.name)));
            }
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

    private void FindCyclesIterative(GameObject startNode, HashSet<GameObject> visited, List<List<GameObject>> cycles)
    {
        Stack<(GameObject node, List<GameObject> path)> stack = new();
        stack.Push((startNode, new List<GameObject> { startNode }));

        while (stack.Count > 0)
        {
            var (currentNode, currentPath) = stack.Pop();
            visited.Add(currentNode);

            if (!_graph.TryGetValue(currentNode, out var value)) continue;

            foreach (var neighbor in value.outConnections)
            {
                if (currentPath.Contains(neighbor))
                {
                    // Cycle found, extract cycle path
                    var cycleStartIndex = currentPath.IndexOf(neighbor);
                    var cycle = currentPath.Skip(cycleStartIndex).ToList();
                    cycle.Add(neighbor); // Closing the cycle

                    // Add only if it's a unique cycle (ignoring node order)
                    if (!cycles.Any(existingCycle => existingCycle.SequenceEqual(cycle)))
                    {
                        cycles.Add(cycle);
                    }
                    continue;
                }

                if (visited.Contains(neighbor)) continue;
                var newPath = new List<GameObject>(currentPath) { neighbor };
                stack.Push((neighbor, newPath));
            }
        }
    }
}
