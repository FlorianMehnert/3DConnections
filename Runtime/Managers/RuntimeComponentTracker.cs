using _3DConnections.Runtime.Nodes.Extensions;

namespace _3DConnections.Runtime.Managers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    
    using ScriptableObjectInventory;

    /// <summary>
    /// Tracks dynamic component references using runtime type discovery and reflection
    /// </summary>
    public class RuntimeComponentTracker : MonoBehaviour
    {
        private static RuntimeComponentTracker _instance;
        private static readonly Dictionary<int, List<ComponentReference>> DynamicReferences = new();
        private SceneAnalyzer _sceneAnalyzer;

        // Track all GameObjects that we need to monitor
        private readonly HashSet<GameObject> _monitoredObjects = new();
        private readonly Dictionary<GameObject, Dictionary<Type, Component>> _lastKnownComponents = new();

        public struct ComponentReference
        {
            public GameObject Source;
            public Component Target;
            public string MethodType;
            public Type ComponentType;
            public float Timestamp;
        }

        [Header("Tracking Settings")] [SerializeField]
        private bool enableContinuousTracking = true;

        [SerializeField] private float trackingInterval = 0.1f;
        [SerializeField] private bool trackGetComponentCalls = true;
        [SerializeField] private bool trackAddComponentCalls = true;
        [SerializeField] private bool logDiscoveredReferences = false;

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            _sceneAnalyzer = GetComponent<SceneAnalyzer>();

            if (enableContinuousTracking)
            {
                InvokeRepeating(nameof(ScanForComponentChanges), trackingInterval, trackingInterval);
            }
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            CancelInvoke();
        }

        /// <summary>
        /// Register a GameObject to be monitored for component changes
        /// </summary>
        public static void RegisterGameObject(GameObject gameObject)
        {
            if (_instance == null || gameObject == null) return;

            // Don't track artificial game objects (nodes)
            if (gameObject.GetComponent<ArtificialGameObject>() != null) return;

            _instance._monitoredObjects.Add(gameObject);
            _instance.InitializeComponentSnapshot(gameObject);
        }

        /// <summary>
        /// Unregister a GameObject from monitoring
        /// </summary>
        public static void UnregisterGameObject(GameObject gameObject)
        {
            if (_instance == null || gameObject == null) return;

            _instance._monitoredObjects.Remove(gameObject);
            _instance._lastKnownComponents.Remove(gameObject);
        }

        /// <summary>
        /// Manually trigger a scan for component changes
        /// </summary>
        public static void TriggerScan()
        {
            if (_instance == null) return;
            _instance.ScanForComponentChanges();
        }

        /// <summary>
        /// Discover component references by analyzing MonoBehaviour scripts
        /// </summary>
        public void DiscoverComponentReferences()
        {
            var allGameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.InstanceID);

            foreach (var go in allGameObjects)
            {
                // Skip artificial objects
                if (go.GetComponent<ArtificialGameObject>() != null) continue;

                RegisterGameObject(go);
                AnalyzeGameObjectForReferences(go);
            }
        }

        private void InitializeComponentSnapshot(GameObject go)
        {
            if (go == null) return;

            var components = go.GetComponents<Component>();
            var componentDict = new Dictionary<Type, Component>();

            foreach (var component in components)
            {
                if (component != null)
                {
                    componentDict[component.GetType()] = component;
                }
            }

            _lastKnownComponents[go] = componentDict;
        }

        private void ScanForComponentChanges()
        {
            var objectsToRemove = new List<GameObject>();

            foreach (var gameObject in _monitoredObjects.ToList())
            {
                if (gameObject == null)
                {
                    objectsToRemove.Add(gameObject);
                    continue;
                }

                CheckForNewComponents(gameObject);
            }

            // Clean up null references
            foreach (var obj in objectsToRemove)
            {
                UnregisterGameObject(obj);
            }
        }

        private void CheckForNewComponents(GameObject gameObject)
        {
            if (!_lastKnownComponents.TryGetValue(gameObject, out var lastComponents))
            {
                InitializeComponentSnapshot(gameObject);
                return;
            }

            var currentComponents = gameObject.GetComponents<Component>();

            foreach (var component in currentComponents)
            {
                if (component == null) continue;

                var componentType = component.GetType();

                // Check if this is a new component
                if (!lastComponents.ContainsKey(componentType))
                {
                    if (trackAddComponentCalls)
                    {
                        RecordComponentReference(gameObject, component, "AddComponent", componentType);

                        if (logDiscoveredReferences)
                        {
                            Debug.Log($"Discovered new component: {componentType.Name} on {gameObject.name}");
                        }
                    }

                    lastComponents[componentType] = component;
                }
            }
        }

        private void AnalyzeGameObjectForReferences(GameObject gameObject)
        {
            var components = gameObject.GetComponents<MonoBehaviour>();

            foreach (var component in components)
            {
                if (component == null) continue;
                AnalyzeComponentForReferences(component);
            }
        }

        private void AnalyzeComponentForReferences(MonoBehaviour component)
        {
            var componentType = component.GetType();
            var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                AnalyzeFieldForComponentReferences(component, field);
            }

            // Also check properties
            var properties =
                componentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var property in properties)
            {
                AnalyzePropertyForComponentReferences(component, property);
            }
        }

        private void AnalyzeFieldForComponentReferences(MonoBehaviour source, FieldInfo field)
        {
            try
            {
                var fieldValue = field.GetValue(source);
                ProcessPotentialComponentReference(source, fieldValue, field.FieldType, field.Name);
            }
            catch (Exception e)
            {
                if (logDiscoveredReferences)
                {
                    Debug.LogWarning($"Error analyzing field {field.Name} in {source.GetType().Name}: {e.Message}");
                }
            }
        }

        private void AnalyzePropertyForComponentReferences(MonoBehaviour source, PropertyInfo property)
        {
            // Only analyze readable properties
            if (!property.CanRead || property.GetIndexParameters().Length > 0) return;

            try
            {
                var propertyValue = property.GetValue(source);
                ProcessPotentialComponentReference(source, propertyValue, property.PropertyType, property.Name);
            }
            catch (Exception e)
            {
                if (logDiscoveredReferences)
                {
                    Debug.LogWarning(
                        $"Error analyzing property {property.Name} in {source.GetType().Name}: {e.Message}");
                }
            }
        }

        private void ProcessPotentialComponentReference(MonoBehaviour source, object value, Type fieldType,
            string fieldName)
        {
            if (value == null) return;

            // Check if it's a Component reference
            if (typeof(Component).IsAssignableFrom(fieldType) && value is Component targetComponent)
            {
                if (trackGetComponentCalls)
                {
                    RecordComponentReference(source.gameObject, targetComponent, "GetComponent", fieldType);

                    if (logDiscoveredReferences)
                    {
                        Debug.Log(
                            $"Found component reference: {source.name}.{fieldName} -> {targetComponent.name} ({fieldType.Name})");
                    }
                }
            }
            // Check if it's a GameObject reference
            else if (typeof(GameObject).IsAssignableFrom(fieldType) && value is GameObject targetGameObject)
            {
                // We could track GameObject references here if needed
                if (logDiscoveredReferences)
                {
                    Debug.Log($"Found GameObject reference: {source.name}.{fieldName} -> {targetGameObject.name}");
                }
            }
            // Check for arrays or lists of components
            else if (fieldType.IsArray && typeof(Component).IsAssignableFrom(fieldType.GetElementType()))
            {
                if (value is Component[] componentArray)
                {
                    foreach (var comp in componentArray)
                    {
                        if (comp != null)
                        {
                            RecordComponentReference(source.gameObject, comp, "GetComponent", comp.GetType());
                        }
                    }
                }
            }
            // Check for generic collections
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = fieldType.GetGenericArguments()[0];
                if (typeof(Component).IsAssignableFrom(elementType) && value is System.Collections.IList list)
                {
                    foreach (var item in list)
                    {
                        if (item is Component comp)
                        {
                            RecordComponentReference(source.gameObject, comp, "GetComponent", comp.GetType());
                        }
                    }
                }
            }
        }

        public static void RecordComponentReference(GameObject source, Component target, string methodType,
            Type componentType)
        {
            if (_instance == null || source == null || target == null) return;

            // Don't track artificial game objects (nodes)
            if (source.GetComponent<ArtificialGameObject>() != null) return;

            var reference = new ComponentReference
            {
                Source = source,
                Target = target,
                MethodType = methodType,
                ComponentType = componentType,
                Timestamp = Time.time
            };

            var sourceId = source.GetInstanceID();
            if (!DynamicReferences.ContainsKey(sourceId))
            {
                DynamicReferences[sourceId] = new List<ComponentReference>();
            }

            // Check if we already have this exact reference
            var existingRef = DynamicReferences[sourceId].FirstOrDefault(r =>
                r.Target == target && r.MethodType == methodType && r.ComponentType == componentType);

            if (existingRef.Target == null) // Not found
            {
                DynamicReferences[sourceId].Add(reference);

                // Immediately create edge if nodes exist
                _instance.CreateDynamicEdge(reference);
            }
        }

        // TODO: this needs to spawn new edges only when analyze scene was called
        private void CreateDynamicEdge(ComponentReference reference)
        {
            if (_sceneAnalyzer == null) return;

            // Access the instance ID lookup from SceneAnalyzer using reflection
            var lookupField = typeof(SceneAnalyzer).GetField("_instanceIdToNodeLookup",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (lookupField?.GetValue(_sceneAnalyzer) is not Dictionary<int, GameObject> lookup) return;

            var sourceId = reference.Source.GetInstanceID();
            var targetId = reference.Target.GetInstanceID();

            if (!lookup.TryGetValue(sourceId, out var sourceNode) ||
                !lookup.TryGetValue(targetId, out var targetNode)) return;

            // Create a dynamic edge with a distinct color
            var dynamicEdgeColor = reference.MethodType == "GetComponent"
                ? new Color(1f, 0.5f, 0f, 0.7f) // Orange for GetComponent
                : new Color(0f, 1f, 1f, 0.7f); // Cyan for AddComponent

            CreateEdgeConnection(sourceNode, targetNode, dynamicEdgeColor,
                $"dynamic_{reference.MethodType}");
        }

        private void CreateEdgeConnection(GameObject sourceNode, GameObject targetNode, Color color,
            string connectionType)
        {
            if (NodeConnectionManager.Instance == null) return;

            var depth = 0;
            var maxWidth = ScriptableObjectInventory.Instance.nodeColors.maxWidthHierarchy;

            NodeConnectionManager.Instance.AddConnection(
                sourceNode,
                targetNode,
                color,
                lineWidth: Mathf.Clamp01(.98f - (float)depth / maxWidth) + .1f,
                saturation: Mathf.Clamp01(.9f - (float)depth / 10) + .1f,
                connectionType
            );
        }

        public void RefreshDynamicEdges()
        {
            foreach (var kvp in DynamicReferences)
            {
                foreach (var reference in kvp.Value)
                {
                    CreateDynamicEdge(reference);
                }
            }
        }

        /// <summary>
        /// Clear all tracked references
        /// </summary>
        public static void ClearReferences()
        {
            DynamicReferences.Clear();
            if (_instance != null)
            {
                _instance._monitoredObjects.Clear();
                _instance._lastKnownComponents.Clear();
            }
        }

        /// <summary>
        /// Get all currently tracked references for debugging
        /// </summary>
        public static Dictionary<int, List<ComponentReference>> GetAllReferences()
        {
            return DynamicReferences;
        }

        /// <summary>
        /// Perform a deep analysis of the scene to discover existing component relationships
        /// </summary>
        public void PerformDeepAnalysis()
        {
            Debug.Log("Starting deep component reference analysis...");

            var startTime = Time.realtimeSinceStartup;
            var discoveredReferences = 0;

            // Clear existing data
            ClearReferences();

            // Find all GameObjects in the scene
            var allGameObjects = FindObjectsOfType<GameObject>();

            foreach (var gameObject in allGameObjects)
            {
                if (gameObject.GetComponent<ArtificialGameObject>() != null) continue;

                RegisterGameObject(gameObject);

                // Analyze all MonoBehaviour components
                var components = gameObject.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    if (component != null)
                    {
                        var beforeCount = DynamicReferences.Values.Sum(list => list.Count);
                        AnalyzeComponentForReferences(component);
                        var afterCount = DynamicReferences.Values.Sum(list => list.Count);
                        discoveredReferences += afterCount - beforeCount;
                    }
                }
            }

            var endTime = Time.realtimeSinceStartup;
            Debug.Log(
                $"Deep analysis complete. Discovered {discoveredReferences} component references in {(endTime - startTime):F2} seconds.");
        }
    }
}