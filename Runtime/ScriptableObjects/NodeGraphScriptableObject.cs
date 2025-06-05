using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

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

    private readonly object _lock = new();

    public List<GameObject> AllNodes
    {
        get
        {
            lock (_lock)
            {
                if (_allNodes != null)
                {
                    if (_allNodes.Count != 0) return _allNodes ?? new List<GameObject>();
                    _parentObject ??= SceneHandler.GetParentObject();
                    if (!_parentObject)
                        return new List<GameObject>();
                    _allNodes = SceneHandler.GetNodesUsingTheNodegraphParentObject();
                    return _allNodes;
                }

                _parentObject ??= SceneHandler.GetParentObject();
                if (!_parentObject)
                    return new List<GameObject>();

                _allNodes = _parentObject.transform.Cast<Transform>()
                    .Select(child => child.gameObject)
                    .ToList();

                return _allNodes ?? new List<GameObject>();
            }
        }
        set
        {
            lock (_lock)
            {
                if (!_workingOnAllNodes)
                {
                    _allNodes = value;
                }
                else
                {
                    Debug.LogWarning("Trying to alter allNodes while some other function is iterating it.");
                }
            }
        }
    }


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
        foreach (var component in (customNodesList ?? AllNodes).Where(node => node && componentType != null).Select(node => node.GetComponents(componentType)).SelectMany(components => components))
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

    /// <summary>
    /// Recursive function to reenable nodes that are connected
    /// </summary>
    /// <param name="node">root node to be selected</param>
    /// <param name="depth">current depth used in the recursive part to terminate</param>
    /// <param name="maxDepth">maximum depth after which to terminate</param>
    public void ReenableConnectedNodes(GameObject node, int depth, int maxDepth = 5)
    {
        if (depth >= maxDepth || !node) return; // Prevent excessive recursion and null node issues
        var nodeRenderer = node.GetComponent<MeshRenderer>();
        if (nodeRenderer)
            nodeRenderer.enabled = true;
        var nodeConnections = node.GetComponent<LocalNodeConnections>();
        if (!nodeConnections) return; // Check if nodeConnections exists
        foreach (var outwardsConnectedNode in nodeConnections.outConnections.Where(outwardsConnectedNode => outwardsConnectedNode))
        {
            nodeRenderer = outwardsConnectedNode.GetComponent<MeshRenderer>();
            if (nodeRenderer) // Only enable if MeshRenderer exists
                nodeRenderer.enabled = true;
            foreach (Transform child in node.transform)
            {
                child.gameObject.SetActive(true);
            }
            ReenableConnectedNodes(outwardsConnectedNode, depth + 1, maxDepth);
        }
    }
    
    public void SearchNodes(string searchString)
    {
        if (AllNodes == null)
            return;
        foreach (var nodeObj in AllNodes)
        {
            var node = nodeObj.GetComponent<ColoredObject>();
            if (!node) continue;
            if (string.IsNullOrEmpty(searchString) || nodeObj.name.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                ChangeTextSize(nodeObj, 30f);
            }
            else
            {
                ChangeTextSize(nodeObj, 1.5f);
            }
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
            rectTransform.sizeDelta = new Vector2(preferredValues.x*1.5f, preferredValues.y);
        }
        else
        {
            var scaleFactor = size / 36f;
            textObject.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
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
}