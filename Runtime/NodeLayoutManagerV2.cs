using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Node = Runtime.Node;

namespace _3DConnections.Runtime
{
    public class NodeLayoutManagerV2 : MonoBehaviour
    {
        private static string _dataPath;
        private void Awake()
        {
            _dataPath = Application.persistentDataPath;
        }
        /// <summary>
        /// Compact layout algorithm with fixed aspect ratio for all nodes
        /// </summary>
        /// <param name="nodes">List of nodes to be positioned</param>
        /// <param name="targetAspectRatio">Desired aspect ratio (width/height)</param>
        /// <param name="minimumPadding">Minimum spacing between nodes</param>
        public static void CompactFixedAspectRatioLayout(List<Node> nodes, float targetAspectRatio = 1.0f, float minimumPadding = 5f)
        {
            if (nodes == null || nodes.Count == 0) return;

            // Determine layout grid dimensions
            var nodeCount = nodes.Count;
            var gridColumns = Mathf.CeilToInt(Mathf.Sqrt(nodeCount));
            var gridRows = Mathf.CeilToInt((float)nodeCount / gridColumns);

            // Normalize node sizes to a fixed aspect ratio while preserving total area
            var normalizedNodes = nodes.Select(node =>
            {
                // Calculate new dimensions maintaining the target aspect ratio
                var newWidth = Mathf.Sqrt(node.Width * node.Height * targetAspectRatio);
                var newHeight = newWidth / targetAspectRatio;

                return new Node(node.name, 0, 0, newWidth, newHeight);
            }).ToList();

            // Determine max node dimensions in the grid
            var maxNodeWidth = normalizedNodes.Max(n => n.Width);
            var maxNodeHeight = normalizedNodes.Max(n => n.Height);

            // Parallel processing for positioning
            Parallel.For(0, normalizedNodes.Count, i =>
            {
                // Calculate grid position
                var row = i / gridColumns;
                var col = i % gridColumns;

                // Position calculation with padding
                var x = col * (maxNodeWidth + minimumPadding);
                var y = row * (maxNodeHeight + minimumPadding);

                // Update node position and size
                var originalNode = nodes[i];

                // Preserve original scaling while positioning
                originalNode.X = x + (maxNodeWidth - originalNode.Width) / 2;
                originalNode.Y = y + (maxNodeHeight - originalNode.Height) / 2;
            });
        }

        /// <summary>
        /// Advanced layout with more precise aspect ratio and spacing control
        /// </summary>
        /// <param name="nodes">List of nodes to be positioned</param>
        /// <param name="targetAspectRatio">Desired aspect ratio (width/height)</param>
        /// <param name="minimumPadding">Minimum spacing between nodes</param>
        /// <param name="maxIterations">Maximum layout optimization iterations</param>
        public static void PreciseFixedAspectRatioLayout(List<Node> nodes, float targetAspectRatio = 1.0f, float minimumPadding = 5f, int maxIterations = 5)
        {
            // First, apply compact layout
            CompactFixedAspectRatioLayout(nodes, targetAspectRatio, minimumPadding);

            // Refinement pass to optimize spacing and maintain an aspect ratio
            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                Parallel.For(0, nodes.Count, (int i) =>
                {
                    Node currentNode = nodes[i];

                    // Enforce an aspect ratio
                    var currentAspect = currentNode.Width / currentNode.Height;
                    if (!(Math.Abs(currentAspect - targetAspectRatio) > 0.01f)) return;
                    // Adjust dimensions to match a target aspect ratio
                    if (currentAspect > targetAspectRatio)
                    {
                        // Width is too large, reduce width
                        currentNode.Width = currentNode.Height * targetAspectRatio;
                    }
                    else
                    {
                        // Height is too large, reduce height
                        currentNode.Height = currentNode.Width / targetAspectRatio;
                    }
                });

                // Re-run layout to adjust positioning after dimension changes
                CompactFixedAspectRatioLayout(nodes, targetAspectRatio, minimumPadding);
            }
        }


        /// <summary>
        /// Recursively creates a TreeNodeSO from a Transform and its children.
        /// </summary>
        /// <param name="transform">The transform to convert</param>
        /// <returns>A TreeNodeSO representing this transform and its children</returns>
        public static TreeNodeSO CreateNodeFromTransform(Transform transform)
        {
            // Create a new ScriptableObject for this node
            var node = ScriptableObject.CreateInstance<TreeNodeSO>();
            node.Initialize(new Node(transform.gameObject.name), transform.gameObject);

            // Recursively create nodes for all children
            foreach (Transform child in transform)
            {
                var childNode = CreateNodeFromTransform(child);
                node.children.Add(childNode);
            }

            return node;
        }
        
        [Header("Output")]
        private static string _assetPath = "/home/florian/RiderProjects/UnityConnections/Assets" + "/TreeData.json"; // Save in a location accessible at runtime

        /// <summary>
        /// Saves the TreeData ScriptableObject as a JSON file.
        /// </summary>
        /// <param name="treeData">The tree data to save</param>
        private static void SaveTreeDataAsset(TreeDataSO treeData)
        {
            if (!Path.HasExtension(_assetPath) || Path.GetExtension(_assetPath) != ".json")
                _assetPath += ".json";

            // Convert the ScriptableObject to JSON
            string jsonData = JsonUtility.ToJson(treeData, true);

            // Write the JSON to a file
            File.WriteAllText(_assetPath, jsonData);

            Debug.Log($"TreeData saved at {_assetPath}");
        }

        /// <summary>
        /// Build and save the tree as a ScriptableObject.
        /// </summary>
        [ContextMenu("Build and Save Tree")]
        public void BuildAndSaveTree()
        {
            var treeData = ScriptableObject.CreateInstance<TreeDataSO>();
            var rootNode = ScriptableObject.CreateInstance<TreeNodeSO>();
            rootNode.Initialize(null, null);

            var rootTransforms = SceneHandler.GetSceneRootObjects();
            foreach (var rootTransform in rootTransforms)
            {
                var childNode = NodeLayoutManagerV2.CreateNodeFromTransform(rootTransform);
                rootNode.children.Add(childNode);
            }

            treeData.rootNode = rootNode;
            SaveTreeDataAsset(treeData);
        }



    }
}