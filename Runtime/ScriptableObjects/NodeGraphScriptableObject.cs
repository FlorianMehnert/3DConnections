using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime.ScriptableObjects
{
    /// <summary>
    /// Manager for Node class containing utilities and data to keep track of nodes and their game object counterpart
    /// </summary>
    [CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/NodeGraph", order = 1)]
    public class NodeGraphScriptableObject : ScriptableObject
    {
        // connections of Node objects and their visually representing GameObjects
        private readonly Dictionary<GameObject, Node> _nodesByGameObject = new();
        public GameObject currentlySelectedGameObject;
        public List<GameObject> allNodes = new();

        public void Clear()
        {
            _nodesByGameObject.Clear();
        }

        public Transform[] AllNodeTransforms2D
        {
            get
            {
                return allNodes.Select(n => n.transform).ToArray();
            }
        }

        /// <summary>
        /// Try to register node to internal tracking but only if not already present - in this case use <see cref="ReplaceRelatedGo"/>
        /// </summary>
        /// <param name="node"></param>
        /// <returns>True for successful adding, False for unsuccessful</returns>
        public bool Add(Node node)
        {
            if (node.RelatedGameObject == null && node is GameObjectNode)
            {
                Debug.Log("trying to add node that has no game object attached");
                return false;
            }

            if (!Contains(node))
            {
                if (node is GameObjectNode)
                {
                    var success = _nodesByGameObject.TryAdd(node.RelatedGameObject, node);
                    if (!success)
                        Debug.Log("TryingToAdd GameObject node " + node.Name + " with GameObject" + node.RelatedGameObject + " which was not successfully created");
                    return success;
                }

                // handling other node types
                var go = new GameObject(node.GetType() + " " + node.Name);
                return _nodesByGameObject.TryAdd(go, node) ? go : null;
            }

            if (!Contains(node.RelatedGameObject) && Contains(node))
            {
                Debug.Log("trying to add a node that was already present but has a different gameObject now: skipping");
            }

            return false;
        }

        public Node Add(GameObject gameObject)
        {
            if (gameObject == null)
            {
                Debug.Log("trying to add empty gameObject to node graph");
                return null;
            }

            var newNode = new GameObjectNode(gameObject.name, null) { RelatedGameObject = gameObject };
            if (Contains(gameObject)) return _nodesByGameObject[gameObject];

            if (!_nodesByGameObject.TryAdd(gameObject, newNode))
            {
                Debug.Log("tried to add a gameObject to nodeGraph which was not present before but also could not be added");
            }

            return newNode;
        }

        /// <summary>
        /// Get the node using the 1 to 1 mapping 
        /// </summary>
        /// <param name="gameObject">GameObject that is on the OverlayScene representing a gameObject, component, etc.</param>
        /// <returns>Node that is representing the given gameObject which is on the overlay</returns>
        public Node GetNode(GameObject gameObject)
        {
            return _nodesByGameObject[gameObject];
        }

        public ScriptableObjectNode GetNode(ScriptableObject scriptableObject)
        {
            return _nodesByGameObject.Values.OfType<ScriptableObjectNode>().FirstOrDefault(x => x.Name == scriptableObject.name);
        }

        public ComponentNode GetNode(Component component)
        {
            return _nodesByGameObject.Values.OfType<ComponentNode>().FirstOrDefault(x => x.Name == component.name);
        }


        // Contains using node as value
        private bool Contains(Node node)
        {
            return _nodesByGameObject.ContainsValue(node);
        }


        // Contains using the go as a key
        public bool Contains(GameObject gameObject)
        {
            return gameObject != null && _nodesByGameObject.ContainsKey(gameObject);
        }

        // Contains using SO name
        public bool Contains(ScriptableObject scriptableObject)
        {
            return scriptableObject != null && _nodesByGameObject.Keys.Any(go => go.name == scriptableObject.name);
        }


        // Contains using Component name
        public bool Contains(Component component)
        {
            foreach (var node in _nodesByGameObject.Values)
            {
                if (node is not ComponentNode componentNode) continue;
                if (componentNode.Component && componentNode.Component == component)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Smarter Add which deletes node/go if the matching node has the same name and location to ensure 1 to 1
        /// </summary>
        /// <param name="node"></param>
        public bool ReplaceRelatedGo(Node node)
        {
            // remove existing nodes and existing gameObjects
            foreach (var existingNode in GetNodes().Where(existingNode => existingNode.RelatedGameObject == node.RelatedGameObject))
            {
                _nodesByGameObject[existingNode.RelatedGameObject] = node;

                return true;
            }

            return Add(node);
        }

        private Dictionary<GameObject, Node>.ValueCollection GetNodes()
        {
            return _nodesByGameObject.Values;
        }

        private List<GameObjectNode> GetGameObjectNodes()
        {
            return _nodesByGameObject.Values.OfType<GameObjectNode>().ToList();
        }

        /// <summary>
        /// Get all GameObjectNodes but also check for existing GameObject
        /// </summary>
        /// <returns></returns>
        private List<GameObjectNode> GetGameObjectNodesWithGameObject()
        {
            return GetGameObjectNodes().Where(gameObjectNode => gameObjectNode.GameObject != null).ToList();
        }

        [CanBeNull]
        private GameObjectNode TryGetGameObjectNodeByGameObject(GameObject gameObject)
        {
            return GetGameObjectNodes().FirstOrDefault(node => node.GameObject == gameObject);
        }

        /// <summary>
        /// Returns the first gameObject node that has the gameObject with the given id
        /// </summary>
        /// <param name="id">id of the searched gameObject</param>
        /// <returns></returns>
        public GameObjectNode ContainsGameObjectNodeByID(int id)
        {
            return GetGameObjectNodesWithGameObject().FirstOrDefault(goNode => goNode.GameObject.GetInstanceID() == id);
        }

        /// <summary>
        /// Add the given Component to all nodes. Sets the gravityScale to 0 and freezeRotation if set to Rigidbody2D
        /// </summary>
        /// <param name="componentType">Type of Component to be added to all nodes</param>
        public void NodesAddComponent(System.Type componentType)
        {
            if (allNodes.Count == 0) return;
            foreach (var node in from node in allNodes
                     let existingComponent = node.GetComponent(componentType)
                     where existingComponent == null
                     select node)
            {
                var newComponent = node.AddComponent(componentType);
                if (newComponent is not Rigidbody2D rigidbody2D) continue;
                rigidbody2D.gravityScale = 0;
                rigidbody2D.freezeRotation = true;
            }
        }


        /// <summary>
        /// Remove component of a given type from all nodes
        /// </summary>
        /// <param name="componentType">Component type to remove</param>
        private void NodesRemoveComponent(System.Type componentType)
        {
            // Check if the target object already has the component
            foreach (var node in allNodes)
            {
                if (node != null && componentType != null)
                {
                    var components = node.GetComponents(componentType);
                    foreach (var component in components)
                    {
                        DestroyImmediate(component);
                    }
                }
            }

        }

        public void NodesRemoveComponents(List<System.Type> componentTypes)
        {
            var orderedTypes = componentTypes.OrderBy(t => t != typeof(SpringJoint2D)).ToList();
            foreach (var orderedType in orderedTypes)
            {
                Debug.Log(orderedType);
            }

            foreach (var componentType in orderedTypes)
            {
                NodesRemoveComponent(componentType);
            }
        }
    }
}