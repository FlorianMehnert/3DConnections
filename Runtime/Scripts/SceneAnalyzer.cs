using System.Linq;
using _3DConnections.Runtime.BurstPhysics;
using _3DConnections.Runtime.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using _3DConnections.Editor.CustomTags;
using _3DConnections.Runtime;
using _3DConnections.Runtime.Utils;
using TMPro;
using Unity.Collections;
using UnityEngine.SceneManagement;

public class SceneAnalyzer : MonoBehaviour
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private readonly HashSet<Object> _visitedObjects = new();
    private readonly HashSet<Object> _processingObjects = new();
    private readonly Dictionary<int, GameObject> _instanceIdToNode = new();
    [SerializeField] private NodeGraphScriptableObject nodeGraph;

    // required for node spawning
    [SerializeField] private OverlaySceneScriptableObject overlay;
    private GameObject _parentNode;
    [SerializeField] private GameObject nodePrefab;
    [SerializeField] private int nodeWidth = 2;
    [SerializeField] private int nodeHeight = 1;
    [SerializeField] internal Color gameObjectColor = new(0.2f, 0.6f, 1f); // Blue
    [ReadonlyColor] private Color _componentColor = new(0.4f, 0.8f, 0.4f); // Green
    [ReadonlyColor] private Color _scriptableObjectColor = new(0.8f, 0.4f, 0.8f); // Purple
    [ReadonlyColor] private Color _parentChildConnection = new(0.5f, 0.5f, 1f); // Light Blue
    [ReadonlyColor] private Color _componentConnection = new(0.5f, 1f, 0.5f); // Light Green
    [ReadonlyColor] private Color _referenceConnection = new(1f, 0f, 0.5f); // Light Yellow
    [SerializeField] private int maxNodes = 1000;
    [ReadOnly] private bool _ignoreTransforms;
    [ReadOnly] private bool _searchForPrefabsUsingNames;
    [SerializeField] private ClearNodesEvent clearNodesEvent;
    private int _currentNodes;

    // TODO: add some editor only shading/monoBehaviour to visualize prefab
    [SerializeField] internal Color prefabColor = new(1f, 0.6f, 0.2f); // Orange


    public void AnalyzeScene()
    {
        _currentNodes = 0;
        _visitedObjects.Clear();
        _processingObjects.Clear();
        _instanceIdToNode.Clear();
        var sceneHandler = GetComponent<SceneHandler>();
        Scene scene = default;
        if (sceneHandler != null)
        {
            scene = sceneHandler.analyzeScene;
        }

        if (scene.IsValid())
        {
            var rootGameObjects = scene.GetRootGameObjects();
            FinishAnalyzeScene(rootGameObjects);
        }
    }

    private void OnValidate()
    {
        var palette = Colorpalette.GeneratePaletteFromBaseColor(gameObjectColor);
        gameObjectColor = palette[0];
        _componentColor = palette[1];
        _scriptableObjectColor = palette[2];
        _parentChildConnection = palette[3];
        _componentConnection = palette[4];
        _referenceConnection = palette[5];
    }

    private static void LoadSceneCallback(Scene sceneByName, out GameObject[] rootGameObjects)
    {
        if (!sceneByName.IsValid())
        {
            Debug.Log("scene is not valid when trying to load at analyzeScene");
            rootGameObjects = null;
            return;
        }

        rootGameObjects = sceneByName.GetRootGameObjects();
        Debug.Log("there are " + rootGameObjects.Length + " root gameObjects during the callback of load scene");
    }

    private void FinishAnalyzeScene(GameObject[] rootGameObjects)
    {
        if (rootGameObjects == null)
        {
            Debug.Log("Trying to finalize analyzeScene with not root gameobjects");
            return;
        }

        var rootNode = SpawnNode(null);

        if (rootNode == null)
        {
            Debug.Log("Root Node could not be spawned");
            return;
        }

        foreach (var rootObject in rootGameObjects)
        {
            TraverseGameObject(rootObject, rootNode);
        }

        if (_instanceIdToNode != null && nodeGraph != null && nodeGraph.allNodes is { Count: 0 })
        {
            nodeGraph.allNodes = _instanceIdToNode.Values.ToList();
        }

        if (nodeGraph.allNodes is { Count: > 0 })
            nodeGraph.allNodes.Add(rootNode);
    }

    private GameObject SpawnNode(Object obj)
    {
        if (!overlay.GetCameraOfScene())
        {
            Debug.Log("No camera while trying to spawn a node in NodeBuilder");
            return null;
        }

        // try to resolve parent gameObject
        if (!_parentNode)
        {
            _parentNode = overlay.GetNodeGraph();
            if (!_parentNode)
            {
                Debug.Log("In SpawnTestNodeOnSecondDisplay node graph game object was not found");
            }
        }

        // create node object
        var nodeObject = Instantiate(nodePrefab, _parentNode.transform);
        _currentNodes++;
        nodeObject.transform.localPosition = new Vector3(0, 0, 0);
        nodeObject.transform.localScale = new Vector3(nodeWidth, nodeHeight, 1f);

        // TODO: try to make this more dynamic
        nodeObject.layer = LayerMask.NameToLayer("OverlayScene");

        // set nodeType
        var type = nodeObject.GetComponent<NodeType>();
        if (type != null)
        {
            SetNodeType(type, obj);
            type.reference = obj;
        }

        // add text
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(nodeObject.transform);
        textObj.transform.localPosition = new Vector3(0, 0.6f, -1f);
        var text = textObj.AddComponent<TextMeshPro>();
        text.text = obj != null ? obj.name : "null object";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 1.5f;

#if UNITY_EDITOR
        // set icons
        if (obj is Component componentObject)
        {
            var componentIcon = EditorGUIUtility.ObjectContent(null, componentObject.GetType()).image as Texture2D;
            var iconObj = new GameObject("Icon");
            if (componentIcon != null)
            {
                iconObj.transform.SetParent(nodeObject.transform);
                iconObj.transform.localPosition = new Vector3(0, 0, -1f);

                var iconRenderer = iconObj.AddComponent<SpriteRenderer>();
                iconRenderer.sprite = TextureToSprite(componentIcon);
                iconRenderer.sortingOrder = 1;
            }
        }
#endif

        // set name
        var prefixNode = "" + type.nodeTypeName switch
        {
            "GameObject" => "go_",
            "Component" => "co_",
            "ScriptableObject" => "so_",
            _ => ""
        };

        // handle initial node
        if (type.reference == null)
        {
            nodeObject.name = "tfRoot";
        }
        else
        {
            var postfixNode = prefixNode != "go_" ? "_" + type.reference.GetType().Name : string.Empty;
            nodeObject.name = prefixNode + obj.name + postfixNode;
        }

        // handle prefabs
        if (!IsPrefab(obj))
        {
            SetNodeColor(nodeObject, obj);
            return nodeObject;
        }

        var renderer = nodeObject.GetComponent<Renderer>();
        if (!renderer) return nodeObject;
        renderer.material.EnableKeyword("_EMISSION");
        var emissionColor = Color.HSVToRGB(0.1f, 1f, 1f) * 5.0f; // White with intensity
        renderer.material.SetColor(EmissionColor, emissionColor);
        return nodeObject;
    }

    private Sprite TextureToSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Checks whether the object has anything to do with prefabs.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    private bool IsPrefab(Object obj)
    {
#if UNITY_2018_3_OR_NEWER
        if (obj == null) return false;

        // Check if it's a prefab instance in the scene (root or child)
        var status = PrefabUtility.GetPrefabInstanceStatus(obj);
        if (status is PrefabInstanceStatus.Connected or PrefabInstanceStatus.MissingAsset)
            return true;

        // Check if part of any prefab
        if (PrefabUtility.IsPartOfPrefabInstance(obj) || PrefabUtility.IsPartOfAnyPrefab(obj))
            return true;

        // Additional checks for GameObjects
        if (obj is GameObject go)
        {
            // Check root object's prefab status
            var root = go.transform.root.gameObject;
            if (root != null && PrefabUtility.GetPrefabInstanceStatus(root) == PrefabInstanceStatus.Connected)
                return true;

            // Check for prefab asset path
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(path))
                return true;
        }

        if (!_searchForPrefabsUsingNames) return PrefabUtility.GetPrefabInstanceHandle(obj) != null;
        var gameObjectName = obj.name;

        var prefabPaths = AssetDatabase.FindAssets("t:Prefab");

        if (prefabPaths.Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Any(prefab => prefab != null && prefab.name == gameObjectName))
        {
            return true;
        }

        return false;

#else
	        return PrefabUtility.GetPrefabType(go) != PrefabType.None;
#endif
    }


    private static void SetNodeType(NodeType type, Object obj)
    {
        type.nodeTypeName = obj switch
        {
            GameObject => "GameObject",
            Component => "Component",
            ScriptableObject => "ScriptableObject",
            _ => type.nodeTypeName
        };
    }

    /// <summary>
    /// Sets the material to according to the specified color for gameObjects, Components and ScriptableObjects
    /// </summary>
    /// <param name="node"></param>
    /// <param name="obj"></param>
    private void SetNodeColor(GameObject node, Object obj)
    {
        var componentRenderer = node.GetComponent<Renderer>();
        if (componentRenderer)
            componentRenderer.material.color = obj switch
            {
                GameObject => gameObjectColor,
                Component => _componentColor,
                ScriptableObject => _scriptableObjectColor,
                _ => Color.black
            };
    }

    private static void ConnectNodes(GameObject inGameObject, GameObject outGameObject, Color connectionColor)
    {
        NodeConnectionManager.Instance.AddConnection(inGameObject, outGameObject, connectionColor);
        var inConnections = inGameObject.GetComponent<NodeConnections>();
        var outConnections = outGameObject.GetComponent<NodeConnections>();
        inConnections.outConnections.Add(outGameObject);
        outConnections.inConnections.Add(inGameObject);
    }

    private GameObject GetOrSpawnNode(Object obj, GameObject parentNodeObject = null)
    {
        if (obj == null) return null;

        var instanceId = obj.GetInstanceID();

        // Check if we already have a node for this instance ID
        if (_instanceIdToNode.TryGetValue(instanceId, out GameObject existingNode))
        {
            // If we have a parent node, connect to the existing node
            if (parentNodeObject != null)
            {
                ConnectNodes(parentNodeObject, existingNode,
                    obj switch
                    {
                        GameObject => new Color(_parentChildConnection.r, _parentChildConnection.g,
                            _parentChildConnection.b, 0.5f),
                        Component => new Color(_componentConnection.r, _componentConnection.g, _componentConnection.b,
                            0.5f),
                        _ => new Color(_referenceConnection.r, _referenceConnection.g, _referenceConnection.b, 0.5f)
                    });
            }

            return existingNode;
        }

        // If no existing node, create a new one
        var newNode = SpawnNode(obj);
        _instanceIdToNode[instanceId] = newNode;

        // Connect to parent if provided
        if (parentNodeObject != null)
        {
            ConnectNodes(parentNodeObject, newNode,
                obj switch
                {
                    GameObject => new Color(_parentChildConnection.r, _parentChildConnection.g,
                        _parentChildConnection.b, 0.5f),
                    Component => new Color(_componentConnection.r, _componentConnection.g, _componentConnection.b,
                        0.5f),
                    _ => new Color(_referenceConnection.r, _referenceConnection.g, _referenceConnection.b, 0.5f)
                });
        }

        return newNode;
    }


    /// <summary>
    /// Recursive function to Spawn a node for the given GameObject and Traverse Components/Children of the given gameObject
    /// </summary>
    /// <param name="toTraverseGameObject">To Traverse gameObject</param>
    /// <param name="parentNodeObject">node object which should be the parent of the node that is spawned for the given gameObject</param>
    /// <param name="isReference"><b>True</b> if this function was called from TraverseComponent as reference, <b>False</b> if this was called from TraverseGameObject as parent-child connection</param>
    private void TraverseGameObject(GameObject toTraverseGameObject, GameObject parentNodeObject = null,
        bool isReference = false)
    {
        if (toTraverseGameObject == null || _currentNodes >= maxNodes) return;

        var instanceId = toTraverseGameObject.GetInstanceID();

        // Check if we're already processing this object (circular reference)
        if (_processingObjects.Contains(toTraverseGameObject))
        {
            // If we're in a cycle, connect to the existing node if we have one
            if (_instanceIdToNode.TryGetValue(instanceId, out var existingNode) && parentNodeObject != null)
            {
                ConnectNodes(parentNodeObject, existingNode,
                    isReference ? _referenceConnection : _parentChildConnection);
            }

            return;
        }

        var needsTraversal = !_visitedObjects.Contains(toTraverseGameObject);
        _processingObjects.Add(toTraverseGameObject);

        try
        {
            var nodeObject = GetOrSpawnNode(toTraverseGameObject, parentNodeObject);

            // Only traverse children and components if we haven't visited this object before
            if (!needsTraversal) return;
            _visitedObjects.Add(toTraverseGameObject);

            // Traverse its components
            foreach (var component in toTraverseGameObject.GetComponents<Component>())
            {
                if (component != null)
                {
                    TraverseComponent(component, nodeObject);
                }
            }

            // Traverse its children
            foreach (Transform child in toTraverseGameObject.transform)
            {
                if (child != null && child.gameObject != null)
                {
                    TraverseGameObject(child.gameObject, nodeObject);
                }
            }
        }
        finally
        {
            _processingObjects.Remove(toTraverseGameObject);
        }
    }

    private void FindReferencesInScriptableObject(ScriptableObject scriptableObject, GameObject parentNodeObject)
    {
        if (scriptableObject == null || _currentNodes > maxNodes) return;
        var instanceId = scriptableObject.GetInstanceID();
        if (_processingObjects.Contains(scriptableObject))
        {
            if (_instanceIdToNode.TryGetValue(instanceId, out var existingNode) && parentNodeObject != null)
            {
                ConnectNodes(parentNodeObject, existingNode, _referenceConnection);
            }

            return;
        }

        var needsTraversal = !_visitedObjects.Contains(scriptableObject);
        _processingObjects.Add(scriptableObject);
        try
        {
            var nodeObject = GetOrSpawnNode(scriptableObject, parentNodeObject);
            if (!needsTraversal) return;
            _visitedObjects.Add(scriptableObject);


#if UNITY_EDITOR
            var serializedObject = new SerializedObject(scriptableObject);
            var property = serializedObject.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue == null) continue;
                TraverseGameObject(property.objectReferenceValue as GameObject, nodeObject);
            }
#endif
        }
        finally
        {
            _processingObjects.Remove(scriptableObject);
        }
    }

    /// <summary>
    /// Recursive function to Spawn a node for the given Component and Traverse References of the given Component which might be GameObjects or ScriptableObjects
    /// </summary>
    /// <param name="component">To Traverse component</param>
    /// <param name="parentNodeObject">node object which should be the parent of the node that is spawned for the given gameObject</param>
    private void TraverseComponent(Component component, GameObject parentNodeObject = null)
    {
        if (component == null || _currentNodes > maxNodes ||
            _ignoreTransforms && component.GetType() == typeof(Transform)) return;

        var instanceId = component.GetInstanceID();

        // Check if we're already processing this component
        if (_processingObjects.Contains(component))
        {
            // If we're in a cycle, connect to the existing node if we have one
            if (_instanceIdToNode.TryGetValue(instanceId, out GameObject existingNode) && parentNodeObject != null)
            {
                ConnectNodes(parentNodeObject, existingNode, _componentConnection);
            }

            return;
        }

        var needsTraversal = !_visitedObjects.Contains(component);
        _processingObjects.Add(component);

        try
        {
            var nodeObject = GetOrSpawnNode(component, parentNodeObject);

            // Only traverse references if we haven't visited this component before
            if (!needsTraversal) return;
            _visitedObjects.Add(component);

            var referencedObjects = GetComponentReferences(component);
            foreach (var referencedObject in referencedObjects)
            {
                if (referencedObject == null) continue;

                switch (referencedObject)
                {
                    case GameObject go when go != null:
                        TraverseGameObject(go, nodeObject, true);
                        break;
                    case Component comp when comp != null:
                        TraverseComponent(comp, nodeObject);
                        break;
                    case ScriptableObject so when so != null:
                        FindReferencesInScriptableObject(so, nodeObject);
                        break;
                }
            }
        }
        finally
        {
            _processingObjects.Remove(component);
        }
    }

    /// <summary>
    /// Delete internal datastructures of <see cref="SceneAnalyzer"/> and delete all children GameObjects (nodes) of the root node 
    /// </summary>
    public void ClearNodes()
    {
        if (!_parentNode)
        {
            Debug.Log("nodeGraph gameObject unknown in ClearNodes for 3DConnections.SceneAnalyzer");
        }


        _parentNode = overlay.GetNodeGraph();
        if (!_parentNode)
        {
            Debug.Log("Even after asking the overlay SO for the nodeGraph gameObject it could not be found");
        }

        Debug.Log("about to delete " + _parentNode.transform.childCount + " nodes");
        foreach (Transform child in _parentNode.transform)
        {
            Destroy(child.gameObject);
        }

        NodeConnectionManager.Instance.ClearConnections();
        var springSimulation = GetComponent<SpringSimulation>();
        if (springSimulation != null)
        {
            springSimulation.CleanupNativeArrays();
        }

        _instanceIdToNode.Clear();
        _visitedObjects.Clear();
        _processingObjects.Clear();
        _currentNodes = 0;
        nodeGraph.allNodes.Clear();
    }

    private static IEnumerable<Object> GetComponentReferences(Component component)
    {
        var fields = component.GetType().GetFields(System.Reflection.BindingFlags.Instance |
                                                   System.Reflection.BindingFlags.Public |
                                                   System.Reflection.BindingFlags.NonPublic);

        return (from field in fields
            where typeof(Object).IsAssignableFrom(field.FieldType)
            select field.GetValue(component)).OfType<Object>().ToList();
    }
}