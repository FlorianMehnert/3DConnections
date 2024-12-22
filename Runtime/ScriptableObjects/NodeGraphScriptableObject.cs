using System.Collections.Generic;
using System.Linq;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime.ScriptableObjects
{
    /// <summary>
    /// Represents all Nodes to gameObject data where only one to one relations exist
    /// </summary>
    
    [CreateAssetMenu(fileName = "Data", menuName = "3DConnections/ScriptableObjects/NodeGraph", order = 1)]
    public class NodeGraphScriptableObject : ScriptableObject
    {
        private readonly Dictionary<GameObject, Node> _nodesByGameObject = new();
        public void Clear()
        {
            _nodesByGameObject.Clear();
        }

        public void Add(Node node)
        {
            if (node.relatedGameObject == null)
            {
                Debug.Log("trying to add node that has no game object attached");
                return;                
            }

            if (!Contains(node))
            {
                _nodesByGameObject.TryAdd(node.relatedGameObject, node);
            }
            else if (!Contains(node.relatedGameObject) && Contains(node))
            {
                Debug.Log("trying to add a node that was already present but has a different gameobject now: skipping");
            }
        }

        public bool Contains(Node node)
        {
            return _nodesByGameObject.ContainsValue(node);
        }

        public bool Contains(GameObject gameObject)
        {
            return _nodesByGameObject.ContainsKey(gameObject);
        }



        public void Remove(GameObject gameObject)
        {
            _nodesByGameObject.Remove(gameObject);
        }

        /// <summary>
        /// Smarter Add which deletes node/go if the matching node has the same name and location to ensure 1 to 1
        /// </summary>
        /// <param name="node"></param>
        public void Replace(Node node)
        {
            // remove existing nodes and existing gameobjects
            foreach (var existingNode in GetNodes()){
                if (existingNode.relatedGameObject == node.relatedGameObject)
                {
                    _nodesByGameObject[existingNode.relatedGameObject] = node;
                    return;
                }
            }
            Add(node);
        }
        
        public List<Node> GetNodes()
        {
            return _nodesByGameObject.Values.ToList();
        }

        public Dictionary<GameObject, Node> GetRelations()
        {
            return _nodesByGameObject;
        }
    }
}