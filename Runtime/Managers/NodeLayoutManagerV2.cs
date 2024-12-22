using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using _3DConnections.Runtime.ScriptableObjects;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Node = Runtime.Node;

namespace _3DConnections.Runtime.Managers
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
        public static void GridLayout(List<Node> nodes, float targetAspectRatio = 1.0f, float minimumPadding = 5f)
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
            GridLayout(nodes, targetAspectRatio, minimumPadding);

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
                GridLayout(nodes, targetAspectRatio, minimumPadding);
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

        [Header("Output")] private static string _assetPath = "/home/florian/RiderProjects/UnityConnections/Assets" + "/TreeData.json"; // Save in a location accessible at runtime

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


        /// <summary>
        /// Calculates connections between nodes in the current Unity scene
        /// </summary>
        /// <returns>A dictionary where each node maps to its connected child nodes</returns>
        internal static Dictionary<Node, HashSet<Node>> CalculateNodeConnections()
        {
            var nodeConnections = new Dictionary<Node, HashSet<Node>>();
            var allGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);

            foreach (var gameObject in allGameObjects)
            {
                var parentNode = CreateNodeFromGameObject(gameObject);
                if (!nodeConnections.ContainsKey(parentNode))
                {
                    nodeConnections[parentNode] = new HashSet<Node>();
                }

                var components = gameObject.GetComponents<Component>();
                foreach (var component in components)
                {
                    // Skip Transform and other base components
                    if (component is Transform) continue;

                    // Create a node for the component
                    var componentNode = CreateNodeFromComponent(component);

                    // Add the component node as a child of the parent node
                    nodeConnections[parentNode].Add(componentNode);
                }

                // Add child GameObjects as connections
                foreach (Transform childTransform in gameObject.transform)
                {
                    var childNode = CreateNodeFromGameObject(childTransform.gameObject);
                    nodeConnections[parentNode].Add(childNode);
                }
            }

            return nodeConnections;
        }

        /// <summary>
        /// Creates a Node from a GameObject
        /// </summary>
        internal static Node CreateNodeFromGameObject(GameObject gameObject)
        {
            var rect = GetGameObjectRect(gameObject);
            return new Node(
                gameObject.name,
                rect.x,
                rect.y,
                rect.width,
                rect.height
            );
        }

        /// <summary>
        /// Creates a Node from a Component
        /// </summary>
        internal static Node CreateNodeFromComponent(Component component)
        {
            // For components, use the parent GameObject's rect and append component type to name
            var rect = GetGameObjectRect(component.gameObject);
            return new Node(
                $"{component.gameObject.name}_{component.GetType().Name}",
                rect.x,
                rect.y,
                rect.width,
                rect.height
            );
        }

        /// <summary>
        /// Calculates the rectangle bounds of a GameObject
        /// </summary>
        private static Rect GetGameObjectRect(GameObject gameObject)
        {
            // Try to get Renderer component to determine bounds
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                return new Rect(
                    bounds.center.x - bounds.extents.x,
                    bounds.center.y - bounds.extents.y,
                    bounds.size.x,
                    bounds.size.y
                );
            }

            // Fallback to zero rect if no renderer
            return new Rect(0, 0, 0, 0);
        }
    }
}