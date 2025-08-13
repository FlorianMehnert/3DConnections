using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Helper class to provide easy access to component tracking functionality
/// and demonstrate how to integrate with the runtime tracker
/// </summary>
public static class ComponentTrackingHelper
{
    /// <summary>
    /// Enhanced GetComponent that automatically tracks the reference
    /// Use this instead of GameObject.GetComponent() when you want tracking
    /// </summary>
    public static T GetComponentTracked<T>(this GameObject gameObject) where T : Component
    {
        var component = gameObject.GetComponent<T>();
        if (component != null)
        {
            RuntimeComponentTracker.RecordComponentReference(gameObject, component, "GetComponent", typeof(T));
        }
        return component;
    }

    /// <summary>
    /// Enhanced GetComponent that automatically tracks the reference
    /// </summary>
    public static Component GetComponentTracked(this GameObject gameObject, Type componentType)
    {
        var component = gameObject.GetComponent(componentType);
        if (component != null)
        {
            RuntimeComponentTracker.RecordComponentReference(gameObject, component, "GetComponent", componentType);
        }
        return component;
    }

    /// <summary>
    /// Enhanced AddComponent that automatically tracks the reference
    /// Use this instead of GameObject.AddComponent() when you want tracking
    /// </summary>
    public static T AddComponentTracked<T>(this GameObject gameObject) where T : Component
    {
        var component = gameObject.AddComponent<T>();
        if (component != null)
        {
            RuntimeComponentTracker.RecordComponentReference(gameObject, component, "AddComponent", typeof(T));
        }
        return component;
    }

    /// <summary>
    /// Enhanced AddComponent that automatically tracks the reference
    /// </summary>
    public static Component AddComponentTracked(this GameObject gameObject, Type componentType)
    {
        var component = gameObject.AddComponent(componentType);
        if (component != null)
        {
            RuntimeComponentTracker.RecordComponentReference(gameObject, component, "AddComponent", componentType);
        }
        return component;
    }

    /// <summary>
    /// Get all tracked references for a specific GameObject
    /// </summary>
    public static List<RuntimeComponentTracker.ComponentReference> GetTrackedReferences(GameObject gameObject)
    {
        if (gameObject == null) return new List<RuntimeComponentTracker.ComponentReference>();
        
        var allReferences = RuntimeComponentTracker.GetAllReferences();
        var gameObjectId = gameObject.GetInstanceID();
        
        return allReferences.TryGetValue(gameObjectId, out var references) 
            ? references 
            : new List<RuntimeComponentTracker.ComponentReference>();
    }

    /// <summary>
    /// Check if a GameObject has any tracked component references
    /// </summary>
    public static bool HasTrackedReferences(GameObject gameObject)
    {
        return GetTrackedReferences(gameObject).Count > 0;
    }

    /// <summary>
    /// Register multiple GameObjects for tracking at once
    /// </summary>
    public static void RegisterGameObjects(IEnumerable<GameObject> gameObjects)
    {
        foreach (var gameObject in gameObjects)
        {
            if (gameObject != null)
            {
                RuntimeComponentTracker.RegisterGameObject(gameObject);
            }
        }
    }

    /// <summary>
    /// Register all GameObjects in a scene for tracking
    /// </summary>
    public static void RegisterAllGameObjectsInScene()
    {
        var allGameObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        RegisterGameObjects(allGameObjects);
    }

    /// <summary>
    /// Get statistics about component tracking
    /// </summary>
    public static ComponentTrackingStats GetTrackingStats()
    {
        var allReferences = RuntimeComponentTracker.GetAllReferences();
        var stats = new ComponentTrackingStats();
        
        foreach (var kvp in allReferences)
        {
            stats.TrackedGameObjects++;
            foreach (var reference in kvp.Value)
            {
                stats.TotalReferences++;
                if (reference.MethodType == "GetComponent")
                    stats.GetComponentReferences++;
                else if (reference.MethodType == "AddComponent")
                    stats.AddComponentReferences++;
                
                if (!stats.ComponentTypesCounts.ContainsKey(reference.ComponentType))
                    stats.ComponentTypesCounts[reference.ComponentType] = 0;
                stats.ComponentTypesCounts[reference.ComponentType]++;
            }
        }
        
        return stats;
    }

    public struct ComponentTrackingStats
    {
        public int TrackedGameObjects;
        public int TotalReferences;
        public int GetComponentReferences;
        public int AddComponentReferences;
        public Dictionary<Type, int> ComponentTypesCounts;

        public ComponentTrackingStats(bool initialize)
        {
            TrackedGameObjects = 0;
            TotalReferences = 0;
            GetComponentReferences = 0;
            AddComponentReferences = 0;
            ComponentTypesCounts = new Dictionary<Type, int>();
        }
    }
}

/// <summary>
/// Example MonoBehaviour showing how to use the tracking system
/// </summary>
public class ComponentTrackingExample : MonoBehaviour
{
    [Header("Example Usage")]
    [SerializeField] private bool useTrackedMethods = true;
    [SerializeField] private bool logTrackingStats = false;

    private void Start()
    {
        // Register this GameObject for tracking
        RuntimeComponentTracker.RegisterGameObject(gameObject);
        
        // Example of using tracked component access
        if (useTrackedMethods)
        {
            // This will be tracked
            var rigidbody = this.gameObject.GetComponentTracked<Rigidbody>();
            if (rigidbody == null)
            {
                // This will also be tracked
                rigidbody = this.gameObject.AddComponentTracked<Rigidbody>();
            }

            // Check for a Transform component (tracked)
            var transform = this.gameObject.GetComponentTracked<Transform>();
        }
        else
        {
            // Standard Unity methods - won't be tracked by our system
            var rigidbody = GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = gameObject.AddComponent<Rigidbody>();
            }
        }

        if (logTrackingStats)
        {
            LogTrackingStats();
        }
    }

    [ContextMenu("Log Tracking Stats")]
    public void LogTrackingStats()
    {
        var stats = ComponentTrackingHelper.GetTrackingStats();
        
        Debug.Log($"Component Tracking Stats:\n" +
                  $"Tracked GameObjects: {stats.TrackedGameObjects}\n" +
                  $"Total References: {stats.TotalReferences}\n" +
                  $"GetComponent calls: {stats.GetComponentReferences}\n" +
                  $"AddComponent calls: {stats.AddComponentReferences}");

        Debug.Log("Component types accessed:");
        foreach (var kvp in stats.ComponentTypesCounts)
        {
            Debug.Log($"  {kvp.Key.Name}: {kvp.Value} times");
        }
        
        // Log references for this specific GameObject
        var myReferences = ComponentTrackingHelper.GetTrackedReferences(gameObject);
        Debug.Log($"This GameObject has {myReferences.Count} tracked references:");
        foreach (var reference in myReferences)
        {
            Debug.Log($"  {reference.MethodType}: {reference.ComponentType.Name} at {reference.Timestamp:F2}s");
        }
    }

    [ContextMenu("Trigger Manual Scan")]
    public void TriggerManualScan()
    {
        RuntimeComponentTracker.TriggerScan();
        Debug.Log("Manual component scan triggered");
    }
}