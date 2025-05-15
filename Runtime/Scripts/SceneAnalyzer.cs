using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.Collections.Generic;
using JetBrains.Annotations;
using TMPro;
using SimpleJSON;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class SceneAnalyzer : MonoBehaviour
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private readonly HashSet<Object> _visitedObjects = new();
    private readonly HashSet<Object> _processingObjects = new();
    private readonly Dictionary<int, GameObject> _instanceIdToNodeLookup = new();

    [SerializeField] private TextAsset analysisData; // Assign the JSON file here
    [SerializeField] private GameObject parentNode;
    [SerializeField] private GameObject nodePrefab;

    [Header("Node Settings")] [SerializeField]
    private int nodeWidth = 2;

    [SerializeField] private int nodeHeight = 1;
    [SerializeField] private int maxNodes = 1000;
    [SerializeField] private bool ignoreTransforms;
    [SerializeField] private bool scaleNodesUsingMaintainability;

    [Header("Display Settings")] [SerializeField]
    internal Color gameObjectColor = new(0.2f, 0.6f, 1f); // Blue

    [SerializeField] private Color componentColor = new(0.4f, 0.8f, 0.4f); // Green
    [SerializeField] private Color scriptableObjectColor = new(0.8f, 0.4f, 0.8f); // Purple
    [SerializeField] private Color assetColor = new(0.1f, 0.9f, 0.9f); // Purple
    [SerializeField] private Color parentChildConnection = new(0.5f, 0.5f, 1f); // Light Blue
    [SerializeField] private Color componentConnection = new(0.5f, 1f, 0.5f); // Light Green
    [SerializeField] private Color referenceConnection = new(1f, 0f, 0.5f); // Light Yellow_i
    [SerializeField] private int colorPreset;
    [SerializeField] private bool generateColors;
    [SerializeField] private ToAnalyzeScene toAnalyzeScene;
    public bool setIcons;

    [Header("Performance Settings")] [SerializeField]
    private bool searchForPrefabsUsingNames;

    public bool spawnRootNode;
    
    [Header("Ignored Types Settings")] public List<string> ignoredTypes = new();

    /// <summary>
    /// used to determine if a gameobject is part of a prefab (will be more accurate if this is running in the editor)
    /// </summary>
    private List<string> _cachedPrefabPaths = new();
    
    /// <summary>
    /// Keep track of the current node amount in the generation algorithm. Easy fix for node creation leading to more gameobjects leading to more nodes
    /// </summary>
    private int _currentNodes;

    private Dictionary<string, float> _complexityMap;


    private void Start()
    {
#if UNITY_EDITOR
        _cachedPrefabPaths = AssetDatabase.FindAssets("t:Prefab").ToList();
#endif
    }

    private List<Type> GetIgnoredTypes()
    {
        return ignoredTypes.Select(Type.GetType).Where(type => type != null).ToList();
    }

    public void AnalyzeScene()
    {
        _currentNodes = 0;
        _visitedObjects.Clear();
        _processingObjects.Clear();
        _instanceIdToNodeLookup.Clear();
#if UNITY_EDITOR
        _cachedPrefabPaths = AssetDatabase.FindAssets("t:Prefab").ToList();
#endif
        var scene = SceneManager.GetSceneByBuildIndex(toAnalyzeScene.sceneIndex);
        if (!scene.IsValid())
        {
            Debug.Log("scene is not valid");
            return;
        }

        Debug.Log(scene.name + " " + toAnalyzeScene.sceneIndex);
        LoadComplexityMetrics(analysisData.ToString());
        _cachedPrefabPaths.Clear();
        var rootGameObjects = scene.GetRootGameObjects();
        TraverseScene(rootGameObjects);
    }

    private void OnValidate()
    {
        var palette = Colorpalette.GeneratePaletteFromBaseColor(gameObjectColor, colorPreset, generateColors);
        gameObjectColor = palette[0];
        componentColor = palette[1];
        scriptableObjectColor = palette[2];
        assetColor = palette[3];
        parentChildConnection = palette[4];
        componentConnection = palette[5];
        referenceConnection = palette[6];
    }

    private void OnEnable()
    {
        if (ScriptableObjectInventory.Instance.clearEvent)
            ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered += HandleEvent;
        if (ScriptableObjectInventory.Instance.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered += HandleRemovePhysicsEvent;
    }

    private void OnDisable()
    {
        if (ScriptableObjectInventory.Instance.clearEvent)
            ScriptableObjectInventory.Instance.clearEvent.OnEventTriggered -= HandleEvent;
        if (ScriptableObjectInventory.Instance.removePhysicsEvent)
            ScriptableObjectInventory.Instance.removePhysicsEvent.OnEventTriggered -= HandleRemovePhysicsEvent;
    }

    private void TraverseScene(GameObject[] rootGameObjects)
    {
        if (rootGameObjects == null)
        {
            Debug.Log("In traverse scene, however there are not gameobjects in the scene");
            return;
        }

        GameObject rootNode = null;
        if (spawnRootNode)
        {
            rootNode = SpawnNode(null);
            if (!rootNode)
            {
                Debug.Log("Root Node could not be spawned");
                return;
            }
        }

        foreach (var rootObject in rootGameObjects)
            TraverseGameObject(rootObject, parentNodeObject: rootNode, depth: 0);

        if (_instanceIdToNodeLookup != null && ScriptableObjectInventory.Instance.graph && ScriptableObjectInventory.Instance.graph.AllNodes is { Count: 0 })
            ScriptableObjectInventory.Instance.graph.AllNodes = _instanceIdToNodeLookup.Values.ToList();

        if (ScriptableObjectInventory.Instance.graph.AllNodes is { Count: > 0 } && rootNode)
            ScriptableObjectInventory.Instance.graph.AllNodes.Add(rootNode);
            
    }

    /// <summary>
    /// Spawn a node gameObject in the overlay scene
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="isAsset"></param>
    /// <returns></returns>
    private GameObject SpawnNode(Object obj, bool isAsset = false)
    {
        if (!ScriptableObjectInventory.Instance.overlay.GetCameraOfScene())
        {
            Debug.Log("No camera while trying to spawn a node in NodeBuilder");
            return null;
        }

        // try to resolve parent gameObject
        if (!parentNode)
        {
            parentNode = ScriptableObjectInventory.Instance.overlay.GetNodeGraph();
            if (!parentNode)
            {
                Debug.Log("In SpawnTestNodeOnSecondDisplay node graph game object was not found");
            }
        }

        // create node object
        var nodeObject = Instantiate(nodePrefab, parentNode.transform);
        _currentNodes++;
        nodeObject.transform.localPosition = new Vector3(0, 0, 0);
        nodeObject.transform.localScale = new Vector3(nodeWidth, nodeHeight, 1f);

        nodeObject.layer = LayerMask.NameToLayer("OverlayScene");

        // set nodeType
        var type = nodeObject.GetComponent<NodeType>();
        if (type)
        {
            type.SetNodeType(obj);
            type.reference = obj;
        }

        // used to avoid recursive node spawning if applied to overlay scene
        nodeObject.AddComponent<ArtificialGameObject>();

        // add text
        var textObj = new GameObject("Text");
        textObj.transform.SetParent(nodeObject.transform);
        textObj.transform.localPosition = new Vector3(0, 0.6f, -1f);
        var text = textObj.AddComponent<TextMeshPro>();
        text.text = obj ? obj.name : "null object";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 1.5f;

#if UNITY_EDITOR
        // set icons
        if (obj is Component componentObject && setIcons)
        {
            var componentIcon = EditorGUIUtility.ObjectContent(null, componentObject.GetType()).image as Texture2D;
            var iconObj = new GameObject("Icon");
            if (componentIcon)
            {
                iconObj.transform.SetParent(nodeObject.transform);
                iconObj.transform.localPosition = new Vector3(0, 0, -1f);
                var iconRenderer = iconObj.AddComponent<SpriteRenderer>();
                var sprite = TextureToSprite(componentIcon);
                iconRenderer.sprite = sprite;
                iconRenderer.sortingOrder = 1;
                const float desiredHeight = 0.5f;
                Vector2 spriteSize = sprite.bounds.size;
                var scaleY = desiredHeight / spriteSize.y;
                var scaleX = scaleY * (spriteSize.x / spriteSize.y) * .5f; // since nodes are scale 2:1
                iconObj.transform.localScale = new Vector3(scaleX, scaleY, 1);
            }
        }
#endif

        // set name
        var prefixNode = "" + type.nodeTypeName switch
        {
            NodeTypeName.GameObject => "go_",
            NodeTypeName.Component => "co_",
            NodeTypeName.ScriptableObject => "so_",
            _ => ""
        };

        // handle initial node
        if (!type.reference)
        {
            nodeObject.name = "tfRoot";
            
            // since this is required if analyzing parent-child relations
            var nodeType = nodeObject.AddComponent<NodeType>();
            nodeType.reference = null;
            nodeType.nodeTypeName = NodeTypeName.GameObject;
        }
        else
        {
            var postfixNode = prefixNode != "go_" ? "_" + type.reference.GetType().Name : string.Empty;
            nodeObject.name = prefixNode + obj.name + postfixNode;
        }

        // handle prefabs
        if (!IsPrefab(obj))
        {
            nodeObject.SetNodeColor(obj, gameObjectColor, componentColor, scriptableObjectColor, assetColor, isAsset);
            return nodeObject;
        }

        var nodeRenderer = nodeObject.GetComponent<Renderer>();
        if (!nodeRenderer) return nodeObject;
        nodeRenderer.material.EnableKeyword("_EMISSION");
        var emissionColor = Color.HSVToRGB(0.1f, 1f, 1f) * 5.0f; // White with intensity
        nodeRenderer.material.SetColor(EmissionColor, emissionColor);
        return nodeObject;
    }

    [UsedImplicitly]
    private static Sprite TextureToSprite(Texture2D texture)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// Checks whether the object has anything to do with prefabs.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
#if UNITY_EDITOR
    private bool IsPrefab(Object obj)
    {
#if UNITY_2018_3_OR_NEWER
        if (!obj) return false;

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
            if (root && PrefabUtility.GetPrefabInstanceStatus(root) == PrefabInstanceStatus.Connected)
                return true;

            // Check for prefab asset path
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(path))
                return true;
        }

        if (!searchForPrefabsUsingNames) return PrefabUtility.GetPrefabInstanceHandle(obj);
        var gameObjectName = obj.name;


        return _cachedPrefabPaths.Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Any(prefab => prefab && prefab.name == gameObjectName);
#else
	        return PrefabUtility.GetPrefabType(go) != PrefabType.None;
#endif
    }
#else
            private static bool IsPrefab(Object obj){
                return false;
            }
#endif



    private GameObject GetOrSpawnNode(Object obj, int depth, GameObject parentNodeObject = null, bool isAsset = false)
    {
        if (!obj) return null;

        var instanceId = obj.GetInstanceID();

        // Check if this node already exists
        if (_instanceIdToNodeLookup.TryGetValue(instanceId, out var existingNode))
        {
            // Connect existing node
            if (!parentNodeObject) return existingNode;
            if (isAsset)
                parentNodeObject.ConnectNodes(existingNode, new Color(referenceConnection.r, referenceConnection.g, referenceConnection.b, 0.5f), depth + 1, "referenceConnection", ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);
            else
            {
                parentNodeObject.ConnectNodes(existingNode,
                    obj switch
                    {
                        GameObject => new Color(parentChildConnection.r, parentChildConnection.g,
                            parentChildConnection.b, 0.5f),
                        Component => new Color(componentConnection.r, componentConnection.g, componentConnection.b,
                            0.5f),
                        _ => new Color(referenceConnection.r, referenceConnection.g, referenceConnection.b, 0.5f)
                    }, depth: depth, obj switch
                    {
                        GameObject => "parentChildConnection",
                        Component => "componentConnection",
                        _ => "referenceConnection"
                    }, ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);
            }

            return existingNode;
        }

        // Create a new node
        var newNode = SpawnNode(obj, isAsset);
        _instanceIdToNodeLookup[instanceId] = newNode;
        if (!parentNodeObject) return newNode;
        if (isAsset)
            parentNodeObject.ConnectNodes(newNode, new Color(referenceConnection.r, referenceConnection.g, referenceConnection.b, 0.5f), depth + 1, "referenceConnection", ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);
        else
        {
            parentNodeObject.ConnectNodes(newNode,
                obj switch
                {
                    GameObject => new Color(parentChildConnection.r, parentChildConnection.g,
                        parentChildConnection.b, 0.5f),
                    Component => new Color(componentConnection.r, componentConnection.g, componentConnection.b,
                        0.5f),
                    _ => new Color(referenceConnection.r, referenceConnection.g, referenceConnection.b, 0.5f)
                }, depth: depth, obj switch
                {
                    GameObject => "parentChildConnection",
                    Component => "componentConnection",
                    _ => "referenceConnection"
                }, ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);
        }

        return newNode;
    }


    /// <summary>
    /// Recursive function to Spawn a node for the given GameObject and Traverse Components/Children of the given gameObject
    /// </summary>
    /// <param name="toTraverseGameObject">To Traverse gameObject</param>
    /// <param name="parentNodeObject">node object which should be the parent of the node that is spawned for the given gameObject</param>
    /// <param name="isReference"><b>True</b> if this function was called from TraverseComponent as reference, <b>False</b> if this was called from TraverseGameObject as parent-child connection</param>
    /// <param name="depth">Depth of the node</param>
    private void TraverseGameObject(GameObject toTraverseGameObject, int depth, GameObject parentNodeObject = null,
        bool isReference = false)
    {
        // do not investigate the game object when node count is too large or when the gameobject is a "node" gameobject
        if (!toTraverseGameObject || _currentNodes >= maxNodes) return;
        if (toTraverseGameObject.GetComponent<ArtificialGameObject>()) return;

        var toTraverseGameObjectID = toTraverseGameObject.GetInstanceID();

        // avoid circles
        if (_processingObjects.Contains(toTraverseGameObject))
        {
            // connect to existing node if already exists
            if (_instanceIdToNodeLookup.TryGetValue(toTraverseGameObjectID, out var existingNode) && parentNodeObject)
            {
                parentNodeObject.ConnectNodes(existingNode,
                    isReference ? referenceConnection : parentChildConnection, depth: depth, isReference ? "referenceConnection" : "parentChildConnection", ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);
            }
            return;
        }

        var needsTraversal = !_visitedObjects.Contains(toTraverseGameObject);
        _processingObjects.Add(toTraverseGameObject);

        try
        {
            var nodeObject = GetOrSpawnNode(toTraverseGameObject, depth, parentNodeObject);

            // Only traverse that was not visited before
            if (!needsTraversal) return;
            _visitedObjects.Add(toTraverseGameObject);
            foreach (var component in toTraverseGameObject.GetComponents<Component>())
            {
                if (component)
                {
                    TraverseComponent(component, depth + 1, parentNodeObject: nodeObject);
                }
            }

            // Traverse its children
            foreach (Transform child in toTraverseGameObject.transform)
            {
                if (child && child.gameObject)
                {
                    TraverseGameObject(child.gameObject, depth + 1, nodeObject);
                }
            }
        }
        finally
        {
            _processingObjects.Remove(toTraverseGameObject);
        }
    }

    private void FindReferencesInScriptableObject(ScriptableObject scriptableObject, GameObject parentNodeObject, int depth)
    {
        if (!scriptableObject || _currentNodes > maxNodes) return;
        var instanceId = scriptableObject.GetInstanceID();
        if (_processingObjects.Contains(scriptableObject))
        {
            if (_instanceIdToNodeLookup.TryGetValue(instanceId, out var existingNode) && parentNodeObject)
            {
                parentNodeObject.ConnectNodes(existingNode, referenceConnection, depth: depth, "referenceConnection", ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);
            }

            return;
        }

        var needsTraversal = !_visitedObjects.Contains(scriptableObject);
        _processingObjects.Add(scriptableObject);
        try
        {
            var nodeObject = GetOrSpawnNode(scriptableObject, depth, parentNodeObject);
            if (!needsTraversal) return;
            _visitedObjects.Add(scriptableObject);


#if UNITY_EDITOR
            var serializedObject = new SerializedObject(scriptableObject);
            var property = serializedObject.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    !property.objectReferenceValue) continue;
                TraverseGameObject(property.objectReferenceValue as GameObject, depth, nodeObject);
            }
#endif
        }
        finally
        {
            _processingObjects.Remove(scriptableObject);
        }
    }

    private static string GetClassNameFromMetric(string metricName)
    {
        // Split the metric name to extract the class name (e.g., "Program.cs::AnalyzeCodeMetrics" -> "Program")
        var parts = metricName.Split(new[] { "::" }, System.StringSplitOptions.None);
        return parts.Length > 0
            ? parts[0].Split('.')[0]
            : // Extract "Program" from "Program.cs"
            metricName; // Fallback in case of unexpected format
    }


    


    /// <summary>
    /// Function to load metrics generated by external roslyn script which analyzes code complexity and maintainability  
    /// </summary>
    /// <param name="json"></param>
    private void LoadComplexityMetrics(string json)
    {
        var root = JSON.Parse(json);
        _complexityMap = new Dictionary<string, float>();

        foreach (var metricNode in root["Metrics"].AsArray)
        {
            var maintainability = metricNode.Value["Maintainability"].AsFloat;
            string metricName = metricNode.Value["Name"];
            var className = GetClassNameFromMetric(metricName);

            _complexityMap.TryAdd(className, maintainability);
        }
    }

    /// <summary>
    /// Recursive function to Spawn a node for the given Component and Traverse References of the given Component which might be GameObjects or ScriptableObjects
    /// </summary>
    /// <param name="component">To Traverse component</param>
    /// <param name="parentNodeObject">node object which should be the parent of the node that is spawned for the given gameObject</param>
    /// <param name="depth">Depth of node</param>
    private void TraverseComponent(Component component, int depth, GameObject parentNodeObject = null)
    {
        if (!component || _currentNodes > maxNodes || GetIgnoredTypes().Contains(component.GetType()) ||
            ignoreTransforms && component.GetType() == typeof(Transform)) return;

        var instanceId = component.GetInstanceID();

        // Check if we're already processing this component
        if (_processingObjects.Contains(component))
        {
            // If we're in a cycle, connect to the existing node if we have one
            if (_instanceIdToNodeLookup.TryGetValue(instanceId, out var existingNode) && parentNodeObject)
            {
                parentNodeObject.ConnectNodes(existingNode, componentConnection, depth: depth, "componentConnection", ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);
            }

            return;
        }

        var needsTraversal = !_visitedObjects.Contains(component);
        _processingObjects.Add(component);

        try
        {
            var nodeObject = GetOrSpawnNode(component, depth + 1, parentNodeObject);
            if (scaleNodesUsingMaintainability)
                nodeObject.ScaleNodeUsingComplexityMap(component, _complexityMap);

            // Only traverse references if we haven't visited this component before
            if (!needsTraversal) return;
            _visitedObjects.Add(component);

            var referencedObjects = GetComponentReferences(component);
            foreach (var referencedObject in referencedObjects)
            {
                if (!referencedObject) continue;
                if (IsAsset(referencedObject))
                {
                    var idOfAssetObject = referencedObject.GetInstanceID();
                    if (_processingObjects.Contains(referencedObject))
                    {
                        if (_instanceIdToNodeLookup.TryGetValue(idOfAssetObject, out var existingNode) && parentNodeObject)
                            parentNodeObject.ConnectNodes(existingNode, referenceConnection, depth: depth, "referenceConnection", ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy);
                        return;
                    }

                    GetOrSpawnNode(referencedObject, depth, parentNodeObject, true);
                    return;
                }

                switch (referencedObject)
                {
                    case GameObject go when go:
                        TraverseGameObject(go, parentNodeObject: nodeObject, isReference: true, depth: depth + 1);
                        break;
                    case Component comp when comp:
                        TraverseComponent(comp, parentNodeObject: nodeObject, depth: depth + 1);
                        break;
                    case ScriptableObject so when so:
                        FindReferencesInScriptableObject(so, nodeObject, depth + 1);
                        break;
                }
            }
        }
        finally
        {
            _processingObjects.Remove(component);
        }
    }

    private static bool IsAsset(Object obj)
    {
        if (!obj)
            return false;

        // Check if the instance ID is negative (asset) or positive (scene object)
        return obj.GetInstanceID() < 0;
    }

    /// <summary>
    /// Delete internal datastructures of <see cref="SceneAnalyzer"/> and delete all children GameObjects (nodes) of the root node 
    /// </summary>
    private void ClearNodes()
    {
        if (!parentNode)
        {
            Debug.Log("nodeGraph gameObject unknown in ClearNodes for 3DConnections.SceneAnalyzer");
        }

        parentNode = ScriptableObjectInventory.Instance.overlay.GetNodeGraph();
        if (!parentNode)
        {
            Debug.Log("Even after asking the overlay SO for the nodeGraph gameObject it could not be found");
        }

        Debug.Log("about to delete " + parentNode.transform.childCount + " nodes");
        foreach (Transform child in parentNode.transform)
        {
            Destroy(child.gameObject);
        }

        if (NodeConnectionManager.Instance)
            NodeConnectionManager.Instance.ClearConnections();
        var springSimulation = GetComponent<SpringSimulation>();
        if (springSimulation)
        {
            springSimulation.CleanupNativeArrays();
        }

        _instanceIdToNodeLookup.Clear();
        _visitedObjects.Clear();
        _processingObjects.Clear();
        _currentNodes = 0;
        ScriptableObjectInventory.Instance.graph.AllNodes.Clear();
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

    private void HandleEvent()
    {
        ClearNodes();
        ScriptableObjectInventory.Instance.applicationState.spawnedNodes = false;
        ScriptableObjectInventory.Instance.graph.Initialize();
    }

    private void HandleRemovePhysicsEvent()
    {
        var types = new List<Type>
        {
            typeof(SpringJoint2D),
            typeof(Rigidbody2D)
        };
        var parentObject = SceneHandler.GetParentObject();
        if (!parentObject)
            return;
        ScriptableObjectInventory.Instance.graph.NodesRemoveComponents(types, SceneHandler.GetNodesUsingTheNodegraphParentObject());
    }
}