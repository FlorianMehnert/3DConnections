using System;
using JetBrains.Annotations;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime.ScriptableObjects
{
    public class GameObjectNode : Node
    {
        protected sealed override Type NodeType => base.NodeType;


        public GameObjectNode(string name, float x, float y, float width, float height, [CanBeNull] GameObject go) : base(name, x, y, width, height)
        {
            NodeType = typeof(GameObject);
            GameObject = go;
        }

        /// <summary>
        /// Construct a GameObjectNode
        /// </summary>
        /// <param name="name">Name of the GameObjectNode</param>
        /// <param name="go">GameObject that will be represented by this GameObjectNode</param>
        public GameObjectNode(string name, [CanBeNull] GameObject go) : base(name)
        {
            NodeType = typeof(GameObject);
            GameObject = go;
        }

        public GameObject GameObject { get; }
    }
}