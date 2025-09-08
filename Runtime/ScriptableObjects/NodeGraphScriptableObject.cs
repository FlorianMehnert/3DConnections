using NUnit.Framework;

namespace _3DConnections.Runtime.ScriptableObjects
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TMPro;
    using UnityEngine;
    using Managers.Scene;
    using Nodes;

    /// <summary>
    /// Manager for Node class containing utilities and data to keep track of nodes and their game object counterpart
    /// </summary>
    [CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/NodeGraph", order = 1)]
    public class NodeGraphScriptableObject : ScriptableObject
    {
        // connections of Node objects and their visually representing GameObjects
        public GameObject currentlySelectedGameObject;
        public Bounds currentlySelectedBounds;
        private List<GameObject> _allNodes = new();
        private bool _workingOnAllNodes;
        private GameObject _parentObject;

        public int goCount;
        public int coCount;
        public int soCount;
        public int voCount;

        public event Action OnGoCountChanged;
        public event Action OnCoCountChanged;
        public event Action OnSoCountChanged;
        public event Action OnVoCountChanged;

        private readonly object _lock = new();

        [Header("Search Settings")] public Color searchHighlightColor = Color.yellow;
        public Color searchDimColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        public float searchHighlightIntensity = 2.0f;


        private void InvokeOnGoCountChanged()
        {
            OnGoCountChanged?.Invoke();
        }

        private void InvokeOnCoCountChanged()
        {
            OnCoCountChanged?.Invoke();
        }

        private void InvokeOnSoCountChanged()
        {
            OnSoCountChanged?.Invoke();
        }

        private void InvokeOnVoCountChanged()
        {
            OnVoCountChanged?.Invoke();
        }

        public void InvokeOnAllCountChanged()
        {
            InvokeOnGoCountChanged();
            InvokeOnCoCountChanged();
            InvokeOnSoCountChanged();
            InvokeOnVoCountChanged();
        }


        public List<GameObject> AllNodes
        {
            get
            {
                lock (_lock)
                {
                    if (_allNodes == null)
                        _allNodes = new List<GameObject>();

                    // Clean up destroyed objects before returning
                    _allNodes.RemoveAll(n => n == null);

                    if (_allNodes.Count == 0)
                    {
                        _parentObject ??= SceneHandler.GetParentObject();
                        if (_parentObject)
                        {
                            _allNodes = SceneHandler.GetNodesUsingTheNodegraphParentObject()
                                .Where(n => n != null)
                                .ToList();
                        }
                    }

                    return _allNodes;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (!_workingOnAllNodes)
                        _allNodes = value.Where(n => n != null).ToList();
                    else
                        Debug.LogWarning("Trying to alter AllNodes while iteration is in progress.");
                }
            }
        }

        public int NodeCount => AllNodes.Count;


        public bool IsEmpty()
        {
            return AllNodes.Count == 0;
        }

        public Transform[] AllNodeTransforms2D
        {
            get
            {
                _workingOnAllNodes = true;
                var tf2d = AllNodes.Select(n => n.transform).ToArray();
                _workingOnAllNodes = false;
                return tf2d;
            }
        }

        public void NodesAddComponent(Type componentType)
        {
            if (componentType == null)
                throw new ArgumentNullException(nameof(componentType));

            if (AllNodes?.Count == 0)
                return;
            try
            {
                if (AllNodes == null) return;
                var nodeCopy = AllNodes.ToArray();

                foreach (var node in nodeCopy)
                {
                    if (!node) continue;
                    if (node.GetComponent(componentType)) continue;
                    var newComponent = node.AddComponent(componentType);
                    if (newComponent is not Rigidbody2D rigidbody2D) continue;
                    rigidbody2D.gravityScale = 0;
                    rigidbody2D.freezeRotation = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error adding component {componentType.Name}: {ex.Message}");
            }
            finally
            {
                _workingOnAllNodes = false;
            }
        }


        /// <summary>
        /// Remove component of a given type from all nodes
        /// </summary>
        /// <param name="componentType">Component type to remove</param>
        /// <param name="customNodesList"></param>
        private void NodesRemoveComponent(Type componentType, List<GameObject> customNodesList = null)
        {
            // Check if the target object already has the component
            foreach (var component in (customNodesList ?? AllNodes).Where(node => node && componentType != null)
                     .Select(node => node.GetComponents(componentType)).SelectMany(components => components))
            {
                DestroyImmediate(component);
            }
        }

        public void NodesRemoveComponents(List<Type> componentTypes, List<GameObject> customNodesList = null)
        {
            var orderedTypes = componentTypes.OrderBy(t => t != typeof(SpringJoint2D)).ToList();
            foreach (var componentType in orderedTypes)
            {
                NodesRemoveComponent(componentType, customNodesList);
            }
        }

        public void ChangeTextSize(GameObject node, float size)
        {
            var textComponent = node.GetComponentInChildren<TMP_Text>();
            if (!textComponent) return;
            textComponent.fontSize = size;
            textComponent.ForceMeshUpdate();

            var preferredValues = textComponent.GetPreferredValues();
            var textObject = textComponent.gameObject;

            if (textObject.TryGetComponent<RectTransform>(out var rectTransform))
            {
                rectTransform.sizeDelta = new Vector2(preferredValues.x * 1.5f, preferredValues.y);
            }
            else
            {
                var scaleFactor = size / 36f;
                textObject.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            }
        }

        /// <summary>
        /// Highlights a specific node and all its connected nodes/edges while fading out everything else
        /// </summary>
        /// <param name="targetNode">The node to highlight along with its connections</param>
        /// <param name="maxDepth">Maximum depth to traverse connections (default: 5)</param>
        public void HighlightNodeConnections(GameObject targetNode, int maxDepth = 5)
        {
            if (targetNode == null)
            {
                Debug.LogWarning("Target node is null, cannot highlight connections");
                return;
            }

            ClearAllHighlights();
            var connectedObjects = GetConnectedObjects(targetNode, maxDepth);
            var parentNodes = GameObject.Find("ParentNodesObject");
            var parentEdges = GameObject.Find("ParentEdgesObject");
            var allObjects = new List<GameObject>();
            if (parentNodes != null)
            {
                allObjects.AddRange(from Transform child in parentNodes.transform select child.gameObject);
            }

            if (parentEdges != null)
            {
                allObjects.AddRange(from Transform child in parentEdges.transform select child.gameObject);
            }

            // Objects that should be faded out (all objects minus the connected ones)
            var fadeOutObjects = allObjects.Except(connectedObjects).ToList();

            // Highlight the target node in bright red
            var targetColoredObject = targetNode.GetComponent<ColoredObject>();
            if (targetColoredObject != null)
            {
                targetColoredObject.Highlight(Color.red, duration: 1, highlightForever: true);
            }

            // Highlight connected objects in orange/yellow
            var highlightColor = new Color(1f, 0.6f, 0f, 1f); // Orange color
            foreach (var connectedObj in connectedObjects.Where(obj => obj != targetNode))
            {
                var coloredObject = connectedObj.GetComponent<ColoredObject>();
                if (coloredObject == null) continue;

                coloredObject.Highlight(highlightColor, duration: 1, highlightForever: true);
            }

            // Fade out all unconnected objects
            var dimColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            foreach (var fadeObj in fadeOutObjects)
            {
                var coloredObject = fadeObj.GetComponent<ColoredObject>();
                if (coloredObject == null) continue;

                coloredObject.Highlight(dimColor, duration: 1, highlightForever: true);
            }

            Debug.Log(
                $"Highlighted target node and {connectedObjects.Count - 1} connected objects, faded out {fadeOutObjects.Count} others");
        }

        /// <summary>
        /// Gets all objects (nodes and edges) connected to the target node up to a specified depth
        /// </summary>
        /// <param name="targetNode">The starting node</param>
        /// <param name="maxDepth">Maximum traversal depth</param>
        /// <returns>List of all connected GameObjects including the target node</returns>
        private static List<GameObject> GetConnectedObjects(GameObject targetNode, int maxDepth)
        {
            var connectedObjects = new HashSet<GameObject> { targetNode };
            var nodesToProcess = new Queue<(GameObject node, int depth)>();
            nodesToProcess.Enqueue((targetNode, 0));
            while (nodesToProcess.Count > 0)
            {
                var (currentNode, currentDepth) = nodesToProcess.Dequeue();
                if (currentDepth >= maxDepth || currentNode == null) continue;
                var nodeConnections = currentNode.GetComponent<LocalNodeConnections>();
                if (nodeConnections == null) continue;
                foreach (var connectedNode in nodeConnections.outConnections.Where(connectedNode =>
                             connectedNode != null && !connectedObjects.Contains(connectedNode)))
                {
                    connectedObjects.Add(connectedNode);
                    nodesToProcess.Enqueue((connectedNode, currentDepth + 1));
                    var edgeObject = FindEdgeBetweenNodes(currentNode, connectedNode);
                    if (edgeObject != null)
                    {
                        connectedObjects.Add(edgeObject);
                    }
                }

                // Process incoming connections if the LocalNodeConnections has them
                var inConnectionsProperty = nodeConnections.GetType().GetField("inConnections");
                if (inConnectionsProperty == null) continue;
                {
                    if (inConnectionsProperty.GetValue(nodeConnections) is not List<GameObject> inConnections) continue;
                    foreach (var connectedNode in inConnections.Where(connectedNode =>
                                 connectedNode != null && !connectedObjects.Contains(connectedNode)))
                    {
                        connectedObjects.Add(connectedNode);
                        nodesToProcess.Enqueue((connectedNode, currentDepth + 1));

                        // Add the edge/connection visual if it exists
                        var edgeObject = FindEdgeBetweenNodes(connectedNode, currentNode);
                        if (edgeObject != null)
                        {
                            connectedObjects.Add(edgeObject);
                        }
                    }
                }
            }

            return connectedObjects.ToList();
        }

        /// <summary>
        /// Finds the edge GameObject between two connected nodes
        /// </summary>
        /// <param name="fromNode">Source node</param>
        /// <param name="toNode">Target node</param>
        /// <returns>The edge GameObject if found, null otherwise</returns>
        private static GameObject FindEdgeBetweenNodes(GameObject fromNode, GameObject toNode)
        {
            var parentEdges = GameObject.Find("ParentEdgesObject");
            return parentEdges == null
                ? null
                :
                // Look for an edge that connects these two nodes
                (from Transform edgeTransform in parentEdges.transform
                    select edgeTransform.gameObject
                    into edgeObj
                    let edgeName = edgeObj.name
                    where edgeName.Contains(fromNode.name) && edgeName.Contains(toNode.name) ||
                          edgeName.Contains(toNode.name) && edgeName.Contains(fromNode.name)
                    select edgeObj).FirstOrDefault();
        }

        /// <summary>
        /// Clears all highlights from nodes and edges
        /// </summary>
        public static void ClearAllHighlights()
        {
            var parentNodes = GameObject.Find("ParentNodesObject");
            var parentEdges = GameObject.Find("ParentEdgesObject");

            var allObjects = new List<GameObject>();

            // Get all nodes
            if (parentNodes != null)
            {
                allObjects.AddRange(from Transform child in parentNodes.transform select child.gameObject);
            }

            // Get all edges
            if (parentEdges != null)
            {
                allObjects.AddRange(from Transform child in parentEdges.transform select child.gameObject);
            }

            // Clear highlights from all objects
            foreach (var coloredObject in allObjects.Select(obj => obj.GetComponent<ColoredObject>())
                         .Where(coloredObject => coloredObject != null))
            {
                coloredObject.ManualClearHighlight();
            }
        }

        public void Initialize()
        {
            currentlySelectedGameObject = null;
            currentlySelectedBounds = new Bounds();
            _allNodes = new List<GameObject>();
            _workingOnAllNodes = false;
            _parentObject = null;
        }

        /// <summary>
        /// Search and highlight nodes based on search string using ColoredObject
        /// </summary>
        /// <param name="searchString">String to search for in node names</param>
        public void SearchNodes(string searchString)
        {
            if (AllNodes == null)
                return;

            bool hasSearchTerm = !string.IsNullOrEmpty(searchString);

            foreach (var nodeObj in AllNodes)
            {
                var coloredObject = nodeObj.GetComponent<ColoredObject>();
                if (!coloredObject) continue;

                bool isMatch = !hasSearchTerm ||
                               nodeObj.name.Contains(searchString, StringComparison.OrdinalIgnoreCase);

                if (isMatch)
                {
                    // Highlight matching nodes
                    if (hasSearchTerm)
                    {
                        var emissionColor = searchHighlightColor * searchHighlightIntensity;
                        coloredObject.Highlight(
                            highlightColor: searchHighlightColor,
                            duration: 1f,
                            highlightForever: true,
                            emissionColor: emissionColor
                        );

                        // Also increase text size for better visibility
                        ChangeTextSize(nodeObj, 30f);
                    }
                    else
                    {
                        // No search term - reset to original
                        coloredObject.ManualClearHighlight();
                        ChangeTextSize(nodeObj, 1.5f);
                    }
                }
                else
                {
                    // Dim non-matching nodes
                    coloredObject.Highlight(
                        highlightColor: searchDimColor,
                        duration: 1f,
                        highlightForever: true
                    );

                    // Make text smaller for non-matches
                    ChangeTextSize(nodeObj, 1.5f);
                }
            }
        }

        /// <summary>
        /// Clear all search highlights and reset nodes to original state
        /// </summary>
        public void ClearSearchHighlights()
        {
            if (AllNodes == null)
                return;

            foreach (var nodeObj in AllNodes)
            {
                var coloredObject = nodeObj.GetComponent<ColoredObject>();
                if (coloredObject)
                {
                    coloredObject.ManualClearHighlight();
                }

                // Reset text size
                ChangeTextSize(nodeObj, 1.5f);
            }
        }
    }
}