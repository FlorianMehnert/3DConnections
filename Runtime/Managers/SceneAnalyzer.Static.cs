using _3DConnections.Runtime.Nodes.Extensions;

namespace _3DConnections.Runtime.Managers
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using cols = ScriptableObjects.NodeColorsScriptableObject;
    public partial class SceneAnalyzer

    {
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
                if (_instanceIdToNodeLookup.TryGetValue(toTraverseGameObjectID, out var existingNode) &&
                    parentNodeObject)
                    parentNodeObject.ConnectNodes(existingNode,
                        isReference ? cols.ReferenceConnection : cols.ParentChildConnection, depth,
                        isReference ? "referenceConnection" : "parentChildConnection",
                        cols.MaxWidthHierarchy);
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
                    if (component)
                        TraverseComponent(component, depth + 1, nodeObject);

                // Traverse its children
                foreach (Transform child in toTraverseGameObject.transform)
                    if (child && child.gameObject)
                        TraverseGameObject(child.gameObject, depth + 1, nodeObject);
            }
            finally
            {
                _processingObjects.Remove(toTraverseGameObject);
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
                (ignoreTransforms && component.GetType() == typeof(Transform))) return;

            // Track MonoBehaviour types for dynamic analysis
            if (component is MonoBehaviour && analyzeDynamicReferences)
            {
                _discoveredMonoBehaviours.Add(component.GetType());
            }

            var instanceId = component.GetInstanceID();

            // Check if we're already processing this component
            if (_processingObjects.Contains(component))
            {
                // If we're in a cycle, connect to the existing node if we have one
                if (_instanceIdToNodeLookup.TryGetValue(instanceId, out var existingNode) && parentNodeObject)
                    parentNodeObject.ConnectNodes(existingNode, cols.ComponentConnection, depth, "componentConnection",
                        cols.MaxWidthHierarchy);

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
                            if (_instanceIdToNodeLookup.TryGetValue(idOfAssetObject, out var existingNode) &&
                                parentNodeObject)
                                parentNodeObject.ConnectNodes(existingNode, cols.ReferenceConnection, depth,
                                    "referenceConnection",
                                    cols.MaxWidthHierarchy);
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

        private static IEnumerable<Object> GetComponentReferences(Component component)
        {
            var fields = component.GetType().GetFields(System.Reflection.BindingFlags.Instance |
                                                       System.Reflection.BindingFlags.Public |
                                                       System.Reflection.BindingFlags.NonPublic);

            return (from field in fields
                where typeof(Object).IsAssignableFrom(field.FieldType)
                select field.GetValue(component)).OfType<Object>().ToList();
        }

        private void FindReferencesInScriptableObject(ScriptableObject scriptableObject, GameObject parentNodeObject,
            int depth)
        {
            if (!scriptableObject || _currentNodes > maxNodes) return;
            var instanceId = scriptableObject.GetInstanceID();
            if (_processingObjects.Contains(scriptableObject))
            {
                if (_instanceIdToNodeLookup.TryGetValue(instanceId, out var existingNode) && parentNodeObject)
                    parentNodeObject.ConnectNodes(existingNode, cols.ReferenceConnection, depth, "referenceConnection",
                        cols.MaxWidthHierarchy);

                return;
            }

            var needsTraversal = !_visitedObjects.Contains(scriptableObject);
            _processingObjects.Add(scriptableObject);
            try
            {
                var nodeObject = GetOrSpawnNode(scriptableObject, depth, parentNodeObject);

                // Detect delegate-based events (UnityAction, Action, etc.) in ScriptableObject
                var delegateEvents = FindDelegateFields(scriptableObject);
                if (delegateEvents.Count > 0)
                {
                    if (!_eventPublishers.ContainsKey(scriptableObject.GetType()))
                        _eventPublishers[scriptableObject.GetType()] = new List<string>();

                    foreach (var (fieldName, _) in delegateEvents)
                    {
                        if (!_eventPublishers[scriptableObject.GetType()].Contains(fieldName))
                            _eventPublishers[scriptableObject.GetType()].Add(fieldName);
                    }
                }


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

    }
}