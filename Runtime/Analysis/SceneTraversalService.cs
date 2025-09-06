// SceneTraversalService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using _3DConnections.Runtime.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using _3DConnections.Runtime.Nodes;
using _3DConnections.Runtime.Nodes.Extensions;
using cols = _3DConnections.Runtime.ScriptableObjects.NodeColorsScriptableObject;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _3DConnections.Runtime.Analysis
{
    public class SceneTraversalService
    {
        private readonly INodeGraphManager _nodeManager;
        private readonly ILogger _logger;
        private readonly AnalysisContext _context;
        private readonly TraversalSettings _settings;

        public SceneTraversalService(INodeGraphManager nodeManager, ILogger logger, TraversalSettings settings)
        {
            _nodeManager = nodeManager;
            _logger = logger;
            _settings = settings;
            _context = new AnalysisContext { MaxNodes = settings.MaxNodes };
        }

        public void TraverseScene(GameObject[] rootGameObjects)
        {
            if (rootGameObjects == null)
            {
                _logger.Log("No GameObjects found in scene");
                return;
            }

            GameObject rootNode = null;
            if (_settings.SpawnRootNode)
            {
                rootNode = _nodeManager.CreateNode(null, 0);
                if (rootNode == null)
                {
                    _logger.LogError("Root node could not be spawned");
                    return;
                }
            }

            foreach (var rootObject in rootGameObjects)
            {
                TraverseGameObject(rootObject, 0, rootNode);
            }

            _logger.Log($"Scene traversal completed. Created {_context.CurrentNodeCount} nodes.");
        }

        private void TraverseGameObject(GameObject gameObject, int depth, GameObject parentNode = null, bool isReference = false)
        {
            if (!gameObject || _context.CurrentNodeCount >= _context.MaxNodes) return;
            if (gameObject.GetComponent<ArtificialGameObject>()) return;

            // Avoid cycles
            if (_context.ProcessingObjects.Contains(gameObject))
            {
                ConnectToExistingNode(gameObject, parentNode, depth, isReference);
                return;
            }

            var needsTraversal = !_context.VisitedObjects.Contains(gameObject);
            _context.ProcessingObjects.Add(gameObject);

            try
            {
                var nodeObject = _nodeManager.CreateNode(gameObject, depth, parentNode, isReference);
                _context.CurrentNodeCount++;

                if (!needsTraversal) return;
                
                _context.VisitedObjects.Add(gameObject);

                // Traverse components
                foreach (var component in gameObject.GetComponents<Component>())
                {
                    if (component && !ShouldIgnoreComponent(component))
                    {
                        TraverseComponent(component, depth + 1, nodeObject);
                    }
                }

                // Traverse children
                foreach (Transform child in gameObject.transform)
                {
                    if (child && child.gameObject)
                    {
                        TraverseGameObject(child.gameObject, depth + 1, nodeObject);
                    }
                }
            }
            finally
            {
                _context.ProcessingObjects.Remove(gameObject);
            }
        }

        private void TraverseComponent(Component component, int depth, GameObject parentNode = null)
        {
            if (!component || _context.CurrentNodeCount > _context.MaxNodes) return;
            if (ShouldIgnoreComponent(component)) return;

            // Track MonoBehaviour types for dynamic analysis
            if (component is MonoBehaviour)
            {
                _context.DiscoveredMonoBehaviours.Add(component.GetType());
            }

            // Avoid cycles
            if (_context.ProcessingObjects.Contains(component))
            {
                ConnectToExistingNode(component, parentNode, depth, false);
                return;
            }

            var needsTraversal = !_context.VisitedObjects.Contains(component);
            _context.ProcessingObjects.Add(component);

            try
            {
                var nodeObject = _nodeManager.CreateNode(component, depth, parentNode);
                _context.CurrentNodeCount++;

                if (!needsTraversal) return;
                
                _context.VisitedObjects.Add(component);

                // Traverse references
                var references = GetComponentReferences(component);
                foreach (var reference in references)
                {
                    if (!reference) continue;

                    if (IsAsset(reference))
                    {
                        _nodeManager.CreateNode(reference, depth + 1, nodeObject, true);
                        continue;
                    }

                    switch (reference)
                    {
                        case GameObject go:
                            TraverseGameObject(go, depth + 1, nodeObject, true);
                            break;
                        case Component comp:
                            TraverseComponent(comp, depth + 1, nodeObject);
                            break;
                        case ScriptableObject so:
                            TraverseScriptableObject(so, depth + 1, nodeObject);
                            break;
                    }
                }

#if UNITY_EDITOR
                ConnectUnityEventPersistentListeners(component, parentNode, depth);
#endif
            }
            finally
            {
                _context.ProcessingObjects.Remove(component);
            }
        }

        private void TraverseScriptableObject(ScriptableObject scriptableObject, int depth, GameObject parentNode)
        {
            if (!scriptableObject || _context.CurrentNodeCount > _context.MaxNodes) return;

            if (_context.ProcessingObjects.Contains(scriptableObject))
            {
                ConnectToExistingNode(scriptableObject, parentNode, depth, false);
                return;
            }

            var needsTraversal = !_context.VisitedObjects.Contains(scriptableObject);
            _context.ProcessingObjects.Add(scriptableObject);

            try
            {
                var nodeObject = _nodeManager.CreateNode(scriptableObject, depth, parentNode);
                _context.CurrentNodeCount++;

                if (!needsTraversal) return;
                
                _context.VisitedObjects.Add(scriptableObject);

#if UNITY_EDITOR
                var serializedObject = new SerializedObject(scriptableObject);
                var property = serializedObject.GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && 
                        property.objectReferenceValue is GameObject go)
                    {
                        TraverseGameObject(go, depth + 1, nodeObject, true);
                    }
                }
#endif
            }
            finally
            {
                _context.ProcessingObjects.Remove(scriptableObject);
            }
        }

        private void ConnectToExistingNode(UnityEngine.Object obj, GameObject parentNode, int depth, bool isReference)
        {
            if (parentNode == null) return;

            var existingNode = _nodeManager.NodeLookup.Values
                .FirstOrDefault(node => node != null && 
                    node.GetComponent<NodeType>()?.reference == obj);

            if (existingNode == null) return;
            var connectionColor = isReference ? cols.ReferenceConnection : 
                obj is GameObject ? cols.ParentChildConnection : cols.ComponentConnection;
            var connectionType = isReference ? "referenceConnection" : 
                obj is GameObject ? "parentChildConnection" : "componentConnection";

            parentNode.ConnectNodes(existingNode, connectionColor, depth, connectionType, cols.MaxWidthHierarchy);
        }

        private bool ShouldIgnoreComponent(Component component)
        {
            if (_settings.IgnoreTransforms && component is Transform) return true;
            return _settings.IgnoredTypes.Contains(component.GetType());
        }

        private static IEnumerable<UnityEngine.Object> GetComponentReferences(Component component)
        {
            var fields = component.GetType().GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            return fields
                .Where(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                .Select(field => field.GetValue(component))
                .OfType<UnityEngine.Object>()
                .ToList();
        }

        private static bool IsAsset(UnityEngine.Object obj)
        {
            return obj && obj.GetInstanceID() < 0;
        }

#if UNITY_EDITOR
        private void ConnectUnityEventPersistentListeners(Component component, GameObject parentNode, int depth)
        {
            if (component == null) return;

            var type = component.GetType();
            var fields = type.GetFields(System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                if (!typeof(UnityEngine.Events.UnityEventBase).IsAssignableFrom(field.FieldType)) continue;

                var unityEvent = field.GetValue(component) as UnityEngine.Events.UnityEventBase;
                if (unityEvent == null) continue;

                int count = unityEvent.GetPersistentEventCount();
                for (int i = 0; i < count; i++)
                {
                    var target = unityEvent.GetPersistentTarget(i) as Component;
                    var method = unityEvent.GetPersistentMethodName(i);

                    if (target == null || string.IsNullOrEmpty(method)) continue;

                    var sourceNode = _nodeManager.FindNodeByType(type) ?? 
                        _nodeManager.CreateNode(component, depth, parentNode);
                    var targetNode = _nodeManager.FindNodeByType(target.GetType()) ?? 
                        _nodeManager.CreateNode(target, depth + 1, sourceNode);

                    if (!sourceNode || !targetNode) continue;
                    var color = new Color(1f, 0.8f, 0.2f, 0.9f);
                    sourceNode.ConnectNodes(targetNode, color, depth + 1, 
                        $"unityEvent_{field.Name}_{method}", 1, dashed: true);
                }
            }
        }
#endif

        public AnalysisContext GetContext() => _context;
    }

    [Serializable]
    public class TraversalSettings
    {
        public int MaxNodes = 1000;
        public bool SpawnRootNode = true;
        public bool IgnoreTransforms = false;
        public List<Type> IgnoredTypes = new();
    }
}
