using System.Linq;
using _3DConnections.Runtime.BurstPhysics;
using _3DConnections.Runtime.Managers;
using _3DConnections.Runtime.ScriptableObjects;
using UnityEditor;

namespace _3DConnections.Runtime.Scripts
{
    using System.Collections.Generic;
    using UnityEngine;

    public class SceneAnalyzer : MonoBehaviour
    {
        private readonly HashSet<Object> _visitedObjects = new();
        private readonly HashSet<Object> _processingObjects = new();
        private readonly Dictionary<int, GameObject> _instanceIdToNode = new();
        [SerializeField] private ToAnalyzeSceneScriptableObject toAnalyzeSceneScriptableObject;
        [SerializeField] private NodeGraphScriptableObject nodeGraph;

        // required for node spawning
        [SerializeField] private OverlaySceneScriptableObject overlay;
        private GameObject _parentNode;
        [SerializeField] private GameObject nodePrefab;
        [SerializeField] private int nodeWidth = 2;
        [SerializeField] private int nodeHeight = 1;
        [SerializeField] internal Color gameobjectColor = new(0.2f, 0.6f, 1f); // Blue
        [SerializeField] internal Color componentColor = new(0.4f, 0.8f, 0.4f); // Green
        [SerializeField] internal Color scriptableObjectColor = new(0.8f, 0.4f, 0.8f); // Purple
        [SerializeField] internal Color parentChildConnection = new(0.5f, 0.5f, 1f); // Light Blue
        [SerializeField] internal Color componentConnection = new(0.5f, 1f, 0.5f); // Light Green
        [SerializeField] internal Color referenceConnection = new(1f, 0f, 0.5f); // Light Yellow
        [SerializeField] private int maxNodes = 10000;
        private int _currentNodes;

        // TODO: add some editor only shading/monoBehaviour to visualize prefab
        [SerializeField] internal Color prefabColor = new(1f, 0.6f, 0.2f); // Orange


        public void AnalyzeScene()
        {
            _currentNodes = 0;
            _visitedObjects.Clear();
            _processingObjects.Clear();
            _instanceIdToNode.Clear();
            var rootGameObjects = toAnalyzeSceneScriptableObject.reference.scene.GetRootGameObjects();
            if (rootGameObjects.Length == 0)
            {
                Debug.Log("There are no gameObjects in the selected scene");
            }

            var rootNode = SpawnNode(null);
            foreach (var rootObject in rootGameObjects)
            {
                TraverseGameObject(rootObject, rootNode);
            }

            if (_instanceIdToNode != null && nodeGraph != null && nodeGraph.allNodes != null)
                nodeGraph.allNodes = _instanceIdToNode.Values.ToList();
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

            if (!_parentNode)
            {
                _parentNode = overlay.GetNodeGraph();
                if (!_parentNode)
                {
                    Debug.Log("In SpawnTestNodeOnSecondDisplay node graph game object was not found");
                }
            }

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

            var prefixNode = "" + type.nodeTypeName switch
            {
                "GameObject" => "go_",
                "Component" => "co_",
                "ScriptableObject" => "so_",
                _ => ""
            };
            if (type.reference == null)
            {
                nodeObject.name = "tfRoot";
            }
            else
            {
                var postfixNode = prefixNode != "go_" ? "_" + type.reference.GetType().Name : string.Empty;
                nodeObject.name = prefixNode + obj.name + postfixNode;    
            }
            
            SetNodeColor(nodeObject, obj);
            return nodeObject;
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
                    GameObject => gameobjectColor,
                    Component => componentColor,
                    ScriptableObject => scriptableObjectColor,
                    _ => Color.black,
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
                            GameObject => parentChildConnection,
                            Component => componentConnection,
                            _ => referenceConnection
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
                        GameObject => parentChildConnection,
                        Component => componentConnection,
                        _ => referenceConnection
                    });
            }

            return newNode;
        }


        /// <summary>
        /// Recursive function to Spawn a node for the given GameObject and Traverse Components/Children of the given gameObject
        /// </summary>
        /// <param name="gameObject">To Traverse gameObject</param>
        /// <param name="parentNodeObject">node object which should be the parent of the node that is spawned for the given gameObject</param>
        /// <param name="isReference"><b>True</b> if this function was called from TraverseComponent as reference, <b>False</b> if this was called from TraverseGameObject as parent-child connection</param>
        private void TraverseGameObject(GameObject gameObject, GameObject parentNodeObject = null, bool isReference = false)
        {
            if (gameObject == null || _currentNodes > maxNodes) return;

            var instanceId = gameObject.GetInstanceID();

            // Check if we're already processing this object (circular reference)
            if (_processingObjects.Contains(gameObject))
            {
                // If we're in a cycle, connect to the existing node if we have one
                if (_instanceIdToNode.TryGetValue(instanceId, out var existingNode) && parentNodeObject != null)
                {
                    ConnectNodes(parentNodeObject, existingNode, isReference ? referenceConnection : parentChildConnection);
                }

                return;
            }

            var needsTraversal = !_visitedObjects.Contains(gameObject);
            _processingObjects.Add(gameObject);

            try
            {
                var nodeObject = GetOrSpawnNode(gameObject, parentNodeObject);

                // Only traverse children and components if we haven't visited this object before
                if (!needsTraversal) return;
                _visitedObjects.Add(gameObject);

                // Traverse its components
                foreach (var component in gameObject.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        TraverseComponent(component, nodeObject);
                    }
                }

                // Traverse its children
                foreach (Transform child in gameObject.transform)
                {
                    if (child != null && child.gameObject != null)
                    {
                        TraverseGameObject(child.gameObject, nodeObject);
                    }
                }
            }
            finally
            {
                _processingObjects.Remove(gameObject);
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
                    ConnectNodes(parentNodeObject, existingNode, referenceConnection);
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
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null) continue;
                    Debug.Log($"'{scriptableObject.name}' references '{property.objectReferenceValue}' in field '{property.name}'");
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
            if (component == null || _currentNodes > maxNodes) return;

            var instanceId = component.GetInstanceID();

            // Check if we're already processing this component
            if (_processingObjects.Contains(component))
            {
                // If we're in a cycle, connect to the existing node if we have one
                if (_instanceIdToNode.TryGetValue(instanceId, out GameObject existingNode) && parentNodeObject != null)
                {
                    ConnectNodes(parentNodeObject, existingNode, componentConnection);
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
            var fields = component.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

            return (from field in fields where typeof(Object).IsAssignableFrom(field.FieldType) select field.GetValue(component)).OfType<Object>().ToList();
        }
    }
}