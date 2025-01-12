using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Runtime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using WhereClauseSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax.WhereClauseSyntax;

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
        public void Clear()
        {
            _nodesByGameObject.Clear();
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
                        Debug.Log("TryingToAdd Gameobject node " + node.Name + " with GameObject" + node.RelatedGameObject + " which was not successfully created");
                    return success;
                }
                
                // handling other node types
                var go = new GameObject(node.GetType() + " " + node.Name);
                return _nodesByGameObject.TryAdd(go, node) ? go : null;
            }

            if (!Contains(node.RelatedGameObject) && Contains(node))
            {
                Debug.Log("trying to add a node that was already present but has a different gameobject now: skipping");
            }

            return false;
        }

        public Node Add(GameObject gameObject)
        {
            if (gameObject == null)
            {
                Debug.Log("trying to add empty gameobject to node graph");
                return null;                
            }

            var newNode = new GameObjectNode(gameObject.name, null) { RelatedGameObject = gameObject };
            if (Contains(gameObject)) return _nodesByGameObject[gameObject];
            
            if (!_nodesByGameObject.TryAdd(gameObject, newNode))
            {
                Debug.Log("tried to add a gameobject to nodegraph which was not present before but also could not be added");               
            }
            return newNode;

        }

        /// <summary>
        /// Get the node using the 1 to 1 mapping 
        /// </summary>
        /// <param name="gameObject">GameObject that is on the OverlayScene representing a gameobject, component, etc.</param>
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

        private GameObject GetGameObject(Node node)
        {
            return _nodesByGameObject.FirstOrDefault(x => x.Value == node).Key;
        }
        
        
        
        // Contains using node as value
        private bool Contains(Node node)
        {
            return _nodesByGameObject.ContainsValue(node);
        }
        
        
        // Contains using the go as key
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
            return component != null && _nodesByGameObject.Keys.Any(go => go.name == component.name);
        }
        
        // Try to resolve Object
        public bool Contains(Object obj)
        {
            var isContained = obj switch
            {
                ScriptableObject so => Contains(so) ? 1 : 0,
                Component co => Contains(co) ? 1 : 0,
                GameObject go => Contains(go) ? 1 : 0,
                _ => 2
            };
            
            // cheap way to log whenever there is the wrong type passed
            if (isContained != 2) return isContained == 1;
            Debug.Log("executed nodegraph Contains on type of " + obj.GetType().Name);
            return false;

        }



        public void Remove(GameObject gameObject)
        {
            _nodesByGameObject.Remove(gameObject);
        }

        /// <summary>
        /// Smarter Add which deletes node/go if the matching node has the same name and location to ensure 1 to 1
        /// </summary>
        /// <param name="node"></param>
        public bool ReplaceRelatedGo(Node node)
        {
            // remove existing nodes and existing gameobjects
            foreach (var existingNode in GetNodes().Where(existingNode => existingNode.RelatedGameObject == node.RelatedGameObject))
            {
                if (_nodesByGameObject.ContainsKey(existingNode.RelatedGameObject))
                    _nodesByGameObject[existingNode.RelatedGameObject] = node;
                else
                {
                    _nodesByGameObject.Add(existingNode.RelatedGameObject, node);
                }
                return true;
            }

            return Add(node);
        }

        private Dictionary<GameObject, Node>.ValueCollection GetNodes()
        {
            return _nodesByGameObject.Values;
        }

        public Dictionary<GameObject, Node> GetRelations()
        {
            return _nodesByGameObject;
        }

        public void ApplyNodePositions()
        {
            foreach (var node in GetNodes())
            {
                GetGameObject(node).transform.localPosition = node.position;
            }
        }

        private List<GameObjectNode> GetGameObjectNodes()
        {
            return _nodesByGameObject.Values.OfType<GameObjectNode>().ToList();
        }

        /// <summary>
        /// Fill Children Attribute for GameObjectNodes using .GameObject.transform => children
        /// </summary>
        public void FillChildrenForGameObjectNodes()
        {
            var goNodes = GetGameObjectNodes();
            foreach (var node in goNodes)
            {
                if (node.GameObject == null)
                {
                    continue;
                }

                foreach (Transform childTransform in node.GameObject.transform)
                {
                    var childNode = goNodes.FirstOrDefault(goNode => goNode.GameObject == childTransform.gameObject);
                    if (childNode != null)
                    {
                        node.AddChild(childNode);
                    }
                }

                Debug.Log("filling children " + node.GetChildren().Count);
            }
        }

        [CanBeNull]
        private GameObjectNode TryGetGameObjectNodeByGameObject(GameObject gameObject)
        {
            return GetGameObjectNodes().FirstOrDefault(node => node.GameObject == gameObject);
        }

        public Node GetRootNode(GameObject[] gameObjects)
        {
            var nodes = new List<Node>();
            
            // lookup node
            foreach (var toCheckGameObject in gameObjects)
            {
                var node = TryGetGameObjectNodeByGameObject(toCheckGameObject);
                if (node != null)
                {
                    nodes.Add(node);
                }
                else
                {
                    // add to internal storage and also output
                    var newlyCreatedNode = Add(toCheckGameObject);
                    nodes.Add(newlyCreatedNode);
                }
            }

            var tfroot = new GameObjectNode("TF Root", null);
            tfroot.SetChildren(nodes);
            return tfroot;
        }
    }
}