using System;
using _3DConnections.Runtime.Managers;
using JetBrains.Annotations;
using Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

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

        public GameObjectNode(Transform position, [CanBeNull] GameObject go) : base(position)
        {
            NodeType = typeof(GameObject);
            GameObject = go;
        }

        public GameObject GameObject { get; }


        public void AttachLagProfiler()
        {
            var profiler = RelatedGameObject.GetComponent<LagProfiler>();
            if (profiler == null)
            {
                RelatedGameObject.AddComponent<LagProfiler>();
            }
        }

        public void RemoveLagProfiler()
        {
            var profiler = RelatedGameObject.GetComponent<LagProfiler>();
            if (profiler != null)
            {
                Object.Destroy(profiler);
            }
        }

        public void ToggleLagProfiler()
        {
            var profiler = RelatedGameObject.GetComponent<LagProfiler>();
            profiler?.ToggleIsMonitoring();
        }

        public static Node GetOrCreateNode(GameObject go, NodeGraphScriptableObject nodegraph)
        {
            return nodegraph.Contains(go) ? nodegraph.GetNode(go) : nodegraph.Add(go);
        }
    }
}