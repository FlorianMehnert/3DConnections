using UnityEngine.Serialization;

namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Object = UnityEngine.Object;

#if UNITY_EDITOR
    using UnityEditor;
#endif
    
    using ScriptableObjects;

    public partial class SceneAnalyzer : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private readonly HashSet<Object> _visitedObjects = new();
        private readonly HashSet<Object> _processingObjects = new();
        private readonly Dictionary<int, GameObject> _instanceIdToNodeLookup = new();

        // Track discovered MonoBehaviour scripts for Roslyn analysis
        private readonly HashSet<Type> _discoveredMonoBehaviours = new();
        private readonly Dictionary<Type, List<ComponentReference>> _dynamicComponentReferences = new();

        // Tracks event publishers found in ScriptableObjects in the scene
        private readonly Dictionary<Type, List<string>> _eventPublishers = new();

        // Tracks event subscriptions found in MonoBehaviours via Roslyn
        private readonly List<EventSubscription> _eventSubscriptions = new();

        private struct EventSubscription
        {
            public Type SubscriberType;
            public string EventFieldName;
            public Type PublisherType;
        }


        [SerializeField] private TextAsset analysisData;
        [SerializeField] private GameObject parentNode;
        [SerializeField] private GameObject nodePrefab;

        [Header("Node Settings")] [SerializeField]
        private int nodeWidth = 2;

        [SerializeField] private int nodeHeight = 1;
        [SerializeField] private int maxNodes = 1000;
        [SerializeField] private bool ignoreTransforms;
        [SerializeField] private bool scaleNodesUsingMaintainability;
        
        [SerializeField] private int colorPreset;
        [SerializeField] private bool generateColors;
        public bool setIcons;


        [SerializeField] private bool showAddComponentCalls = true;
        [SerializeField] private bool showGetComponentCalls = true;

        [Header("Performance Settings")] [SerializeField]
        private bool searchForPrefabsUsingNames;

        public bool spawnRootNode;

        [Header("Ignored Types Settings")] public List<string> ignoredTypes = new();

        /// <summary>
        /// Structure to hold information about dynamic component references
        /// </summary>
        private struct ComponentReference
        {
            public Type ReferencedComponentType;
            public string MethodName; // "AddComponent" or "GetComponent"
            public int LineNumber;
            public string SourceFile;
        }

        /// <summary>
        /// Used to determine if a gameObject is part of a prefab (will be more accurate if this is running in the editor)
        /// </summary>
        private List<string> _cachedPrefabPaths = new();

        /// <summary>
        /// Keep track of the current node amount in the generation algorithm. Easy fix for node creation leading to more gameobjects leading to more nodes
        /// </summary>
        private int _currentNodes;

        private Dictionary<string, float> _complexityMap;
    }
}