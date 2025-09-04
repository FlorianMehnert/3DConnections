using _3DConnections.Runtime.Events;

namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    
    using soi = ScriptableObjectInventory.ScriptableObjectInventory;
    using ScriptableObjects;
    using Nodes.Layout;
    using Nodes;
    using Layout.Type;

    public class LayoutManager : MonoBehaviour
    {
        public bool createRealGrid = true;
        
        public LayoutEvent layoutEvent;

        public void OnEnable()
        {
            layoutEvent.OnEventTriggered += StaticLayout;
        }
        
        public void OnDisable()
        {
            layoutEvent.OnEventTriggered += StaticLayout;
        }

        /// <summary>
        /// Requires existing connections in <see cref="NodeConnectionManager"/> to layout nodes as forest in a circular arrangement
        /// </summary>
        private void Layout()
        {
            var layoutParameters = soi.Instance.layout;
            var connections = soi.Instance.conSo.connections;
            if (!NodeConnectionManager.Instance) return;
            
            var forestManager = new ConnectionsBasedForestManager();
            var gripManager = new GRIPLayoutManager(layoutParameters);
            var gridManager = new GridLayoutManager();
            var sugiyamaManager = new SugiyamaLayoutManager();
            var multiscaleManager = new FM3LayoutManager();
            
            List<TreeNode> rootNodes;
            
            switch (layoutParameters.layoutType)
            {
                case (int)LayoutType.Grid:
                {
                    rootNodes = ConnectionsBasedForestManager.BuildGraphUsingConnections(connections);
                    gridManager.SetLayoutParameters(layoutParameters);
                    
                    // Choose between regular grid and hierarchical grid based on connections
                    if (createRealGrid)
                    {
                        gridManager.LayoutHierarchicalGrid(rootNodes);
                    }
                    else
                    {
                        gridManager.LayoutGrid(rootNodes);
                    }
                    break;
                }
                case (int)LayoutType.Radial:
                {
                    rootNodes = ConnectionsBasedForestManager.BuildGraphUsingConnections(connections);
                    forestManager.SetLayoutParameters(layoutParameters);
                    forestManager.LayoutForest(rootNodes);
                    break;
                }
                case (int)LayoutType.Tree:
                {
                    rootNodes = HierarchicalLayout(layoutParameters);
                    break;
                }
                case (int)LayoutType.GRIP:
                {
                    rootNodes = ConnectionsBasedForestManager.BuildGraphUsingConnections(connections);
                    gripManager.ApplyGRIPLayout(connections);
                    break;
                }
                case (int)LayoutType.Sugiyama:
                {
                    rootNodes = ConnectionsBasedForestManager.BuildGraphUsingConnections(connections);
                    sugiyamaManager.SetLayoutParameters(layoutParameters);
                    sugiyamaManager.LayoutHierarchy(rootNodes);
                    break;
                }
                case (int)LayoutType.Multiscale:
                {
                    rootNodes = ConnectionsBasedForestManager.BuildGraphUsingConnections(connections);
                    multiscaleManager.SetLayoutParameters(layoutParameters);
                    
                    
                    var totalNodeCount = CountAllNodes(rootNodes);
                    multiscaleManager.LayoutFM3(rootNodes);
                    break;
                }
                default:
                    Debug.Log("Unknown layout type: " + layoutParameters.layoutType);
                    return;
            }

            // Flatten to Z plane for all layouts except those that specifically handle 3D positioning
            if (layoutParameters.layoutType != (int)LayoutType.Multiscale)
            {
                forestManager.FlattenToZPlane(rootNodes);
            }
        }

        private static List<TreeNode> HierarchicalLayout(LayoutParameters layoutParameters)
        {
            var rootNodes = ConnectionsBasedForestManager.BuildGraphUsingConnections(soi.Instance.conSo.connections);

            var hierarchicalLayout = new HierarchicalTreeLayout();
            hierarchicalLayout.SetLayoutParameters(
                layoutParameters.levelSpacing,
                layoutParameters.nodeSpacing,
                layoutParameters.subtreeSpacing
            );

            hierarchicalLayout.LayoutTree(rootNodes);
            return rootNodes;
        }

        /// <summary>
        /// Determines if the node structure has a clear hierarchical organization
        /// </summary>
        /// <param name="rootNodes">Root nodes to analyze</param>
        /// <returns>True if hierarchical structure is detected</returns>
        private bool HasHierarchicalStructure(List<TreeNode> rootNodes)
        {
            if (rootNodes == null || rootNodes.Count == 0) return false;

            // Check if there are clear parent-child relationships
            var visited = new HashSet<TreeNode>();
            var totalNodes = 0;
            var nodesWithChildren = 0;

            foreach (var root in rootNodes)
            {
                CountHierarchicalNodes(root, visited, ref totalNodes, ref nodesWithChildren);
            }

            // If more than 30% of nodes have children, consider it hierarchical
            return totalNodes > 0 && (float)nodesWithChildren / totalNodes > 0.3f;
        }

        private void CountHierarchicalNodes(TreeNode node, HashSet<TreeNode> visited, 
            ref int totalNodes, ref int nodesWithChildren)
        {
            if (node == null || !visited.Add(node)) return;

            totalNodes++;
            if (node.Children.Count > 0)
                nodesWithChildren++;

            foreach (var child in node.Children)
            {
                CountHierarchicalNodes(child, visited, ref totalNodes, ref nodesWithChildren);
            }
        }

        /// <summary>
        /// Counts all nodes in the forest
        /// </summary>
        /// <param name="rootNodes">Root nodes</param>
        /// <returns>Total number of nodes</returns>
        private int CountAllNodes(List<TreeNode> rootNodes)
        {
            if (rootNodes == null) return 0;

            var visited = new HashSet<TreeNode>();
            var count = 0;

            foreach (var root in rootNodes)
            {
                CountNodesRecursive(root, visited, ref count);
            }

            return count;
        }

        private void CountNodesRecursive(TreeNode node, HashSet<TreeNode> visited, ref int count)
        {
            if (node == null || !visited.Add(node)) return;

            count++;

            foreach (var child in node.Children)
            {
                CountNodesRecursive(child, visited, ref count);
            }
        }

        /// <summary>
        /// Invoke ClearEvent, perform static layout and finally do onComplete action
        /// </summary>
        /// <param name="onComplete">Action that is completed after a static layout has been performed</param>
        public void StaticLayout(Action onComplete = null)
        {
            var sceneAnalyzer = FindFirstObjectByType<SceneAnalyzer>();

            if (soi.Instance?.removePhysicsEvent)
                soi.Instance.removePhysicsEvent.TriggerEvent();

            if (!sceneAnalyzer ||
                !soi.Instance?.applicationState ||
                soi.Instance.applicationState.spawnedNodes)
            {
                onComplete?.Invoke();
                return;
            }

            Debug.Log("start analyze scene");
            sceneAnalyzer.AnalyzeScene(() =>
            {
                Debug.Log("after analyze scene (in callback)");

                if (soi.Instance.applicationState)
                    soi.Instance.applicationState.spawnedNodes = true;

                if (soi.Instance.layout &&
                    soi.Instance.graph)
                    Layout();

                onComplete?.Invoke(); // continue chain
            });
        }

        /// <summary>
        /// Gets recommended layout type based on graph characteristics
        /// </summary>
        /// <param name="nodeCount">Number of nodes in the graph</param>
        /// <param name="hasHierarchy">Whether the graph has hierarchical structure</param>
        /// <returns>Recommended layout type</returns>
        public LayoutType GetRecommendedLayoutType(int nodeCount, bool hasHierarchy)
        {
            if (nodeCount > 1000)
            {
                return LayoutType.Multiscale; // Best for very large graphs
            }
            else if (nodeCount > 200)
            {
                return hasHierarchy ? LayoutType.Sugiyama : LayoutType.GRIP;
            }
            else if (hasHierarchy)
            {
                return nodeCount > 50 ? LayoutType.Sugiyama : LayoutType.Tree;
            }
            else if (nodeCount > 20)
            {
                return LayoutType.Radial;
            }
            else
            {
                return LayoutType.Grid;
            }
        }

        /// <summary>
        /// Automatically selects and applies the best layout for the current graph
        /// </summary>
        public void AutoLayout()
        {
            var connections = soi.Instance.conSo.connections;
            var rootNodes = ConnectionsBasedForestManager.BuildGraphUsingConnections(connections);
            var nodeCount = CountAllNodes(rootNodes);
            var hasHierarchy = HasHierarchicalStructure(rootNodes);

            var recommendedType = GetRecommendedLayoutType(nodeCount, hasHierarchy);
            
            // Temporarily override the layout type
            var originalType = soi.Instance.layout.layoutType;
            soi.Instance.layout.layoutType = (int)recommendedType;
            
            Debug.Log($"Auto-selected layout: {recommendedType} for {nodeCount} nodes (hierarchical: {hasHierarchy})");
            
            Layout();
            
            // Restore original type
            soi.Instance.layout.layoutType = originalType;
        }
    }
}