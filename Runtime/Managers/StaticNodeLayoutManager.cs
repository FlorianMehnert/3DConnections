using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StaticNodeLayoutManager : MonoBehaviour
{
    /// <summary>
    /// Requires existing connections in <see cref="NodeConnectionManager"/> to layout nodes as forest in a circular arrangement
    /// </summary>
    public static void Layout(LayoutParameters layoutParameters, NodeGraphScriptableObject nodeGraph)
    {
        if (!NodeConnectionManager.Instance) return;
        var forestManager = new ConnectionsBasedForestManager();
        List<TreeNode> rootNodes;
        switch (layoutParameters.layoutType)
        {
            case (int)LayoutType.Grid:
            {
                rootNodes = ConnectionsBasedForestManager.BuildForest(ScriptableObjectInventory.Instance.conSo.connections);
                forestManager.SetLayoutParameters(
                    layoutParameters
                );
                rootNodes = LevelBoxAvoidanceLayout(rootNodes[0].GameObject, nodeGraph);
                break;
            }
            case (int)LayoutType.Radial:
            {
                rootNodes = ConnectionsBasedForestManager.BuildForest(ScriptableObjectInventory.Instance.conSo.connections);
                forestManager.SetLayoutParameters(
                    layoutParameters
                );
                forestManager.LayoutForest(rootNodes);
                break;
            }
            case (int)LayoutType.Tree:
            {
                rootNodes = HierarchicalLayout(layoutParameters);
                break;
            }
            default:
                Debug.Log("Unknown layout type");
                return;
        }

        forestManager.FlattenToZPlane(rootNodes);
    }

    private static List<TreeNode> HierarchicalLayout(LayoutParameters layoutParameters)
    {
        var rootNodes = ConnectionsBasedForestManager.BuildForest(
            ScriptableObjectInventory.Instance.conSo.connections);

        var hierarchicalLayout = new HierarchicalTreeLayout();
        hierarchicalLayout.SetLayoutParameters(
            layoutParameters.levelSpacing,
            layoutParameters.nodeSpacing,
            layoutParameters.subtreeSpacing
        );

        hierarchicalLayout.LayoutTree(rootNodes);
        return rootNodes;
    }

    private static List<TreeNode> LevelBoxAvoidanceLayout(GameObject rootNode, NodeGraphScriptableObject nodeGraph)
    {
        // Create TreeNodes for all GameObjects
        List<TreeNode> treeNodes = new();
        Dictionary<GameObject, TreeNode> gameObjectToTreeNode = new();

        foreach (var obj in nodeGraph.AllNodes)
        {
            var node = new TreeNode { GameObject = obj };
            treeNodes.Add(node);
            gameObjectToTreeNode[obj] = node;
        }

        // Build connections using LocalNodeConnections
        foreach (var obj in nodeGraph.AllNodes)
        {
            var currentNode = gameObjectToTreeNode[obj];
            var connections = obj.GetComponent<LocalNodeConnections>();

            // Add children based on outDirection connections
            foreach (var childNode in connections.outConnections.Select(childObj => gameObjectToTreeNode[childObj]))
            {
                currentNode.Children.Add(childNode);
                childNode.Parents.Add(currentNode);
            }
        }

        // Initialize dictionary to track levels
        Dictionary<TreeNode, int> nodeLevels = new();
        foreach (var node in treeNodes) nodeLevels[node] = int.MaxValue;

        // Set root level
        var rootTreeNode = gameObjectToTreeNode[rootNode];
        nodeLevels[rootTreeNode] = 0;

        for (var currentLevel = 0; currentLevel < 10; currentLevel++)
        {
            var currentLevelNodes = treeNodes.Where(n => nodeLevels[n] == currentLevel).ToList();
            var level = currentLevel;
            foreach (var child in from node in currentLevelNodes from child in node.Children where nodeLevels[child] > level + 1 select child) nodeLevels[child] = currentLevel + 1;

            // Layout current level and next level without overlap
            LayoutLevels(treeNodes, nodeLevels, currentLevel);
        }

        return treeNodes;
    }

    private static void LayoutLevels(List<TreeNode> allNodes, Dictionary<TreeNode, int> nodeLevels, int currentLevel)
    {
        var currentLevelNodes = allNodes.Where(n => nodeLevels[n] == currentLevel).ToList();
        var nextLevelNodes = allNodes.Where(n => nodeLevels[n] == currentLevel + 1).ToList();

        if (!currentLevelNodes.Any() || !nextLevelNodes.Any())
            return;

        var padding = 20f; // Adjust this value based on your needs

        float xOffset = 10;
        foreach (var node in currentLevelNodes)
        {
            var bounds = node.GameObject.GetComponent<Renderer>().bounds;
            var newPosition = new Vector3(xOffset, currentLevel * -1, 0);
            node.GameObject.transform.position = newPosition;
            xOffset += bounds.size.x + padding;
        }

        xOffset = 0;
        foreach (var node in nextLevelNodes)
        {
            var bounds = node.GameObject.GetComponent<Renderer>().bounds;
            var newPosition = new Vector3(xOffset, (currentLevel + 1) * -50f, 0);
            node.GameObject.transform.position = newPosition;
            xOffset += bounds.size.x + padding;
        }

        CenterNodes(currentLevelNodes);
        CenterNodes(nextLevelNodes);
    }

    private static void CenterNodes(List<TreeNode> nodes)
    {
        if (!nodes.Any()) return;

        const float padding = 20f;

        var totalWidth = nodes.Select(node => node.GameObject.GetComponent<Renderer>().bounds).Select(bounds => bounds.size.x + padding).Sum();

        totalWidth -= padding; // Remove extra padding from the last node

        var centerOffset = -totalWidth / 2f;

        // Adjust positions
        foreach (var node in nodes)
        {
            var bounds = node.GameObject.GetComponent<Renderer>().bounds;
            var currentPos = node.GameObject.transform.position;
            node.GameObject.transform.position = new Vector3(
                currentPos.x + centerOffset,
                currentPos.y,
                currentPos.z
            );
            centerOffset += bounds.size.x + padding;
        }
    }

    /// <summary>
    /// Invoke ClearEvent, perform static layout and finally do onComplete action
    /// </summary>
    /// <param name="onComplete">Action that is completed after static layout has been performed</param>
    public void StaticLayout(Action onComplete = null)
    {
        var sceneAnalyzer = FindFirstObjectByType<SceneAnalyzer>();

        if (ScriptableObjectInventory.Instance?.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.TriggerEvent();

        if (!sceneAnalyzer ||
            !ScriptableObjectInventory.Instance?.applicationState ||
            ScriptableObjectInventory.Instance.applicationState.spawnedNodes)
        {
            onComplete?.Invoke();
            return;
        }

        Debug.Log("start analyze scene");
        sceneAnalyzer.AnalyzeScene(() =>
        {
            Debug.Log("after analyze scene (in callback)");

            if (ScriptableObjectInventory.Instance.applicationState)
                ScriptableObjectInventory.Instance.applicationState.spawnedNodes = true;

            if (ScriptableObjectInventory.Instance.layout &&
                ScriptableObjectInventory.Instance.graph)
                Layout(ScriptableObjectInventory.Instance.layout, ScriptableObjectInventory.Instance.graph);

            onComplete?.Invoke(); // continue chain
        });
    }


    /// <summary>
    /// Continuation function for StaticLayout required in case of the scene not yet being loaded
    /// </summary>
    private void ContinueStaticLayout()
    {
        if (!ScriptableObjectInventory.Instance) return;

        if (ScriptableObjectInventory.Instance.applicationState)
            ScriptableObjectInventory.Instance.applicationState.spawnedNodes = true;

        if (ScriptableObjectInventory.Instance.layout &&
            ScriptableObjectInventory.Instance.graph)
            Layout(ScriptableObjectInventory.Instance.layout, ScriptableObjectInventory.Instance.graph);
    }
}