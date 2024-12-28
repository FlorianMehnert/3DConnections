using System;
using _3DConnections.Runtime.Managers;
using Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _3DConnections.Runtime.ScriptableObjects
{
    public class GameObjectNode : Node
    {
        protected sealed override Type NodeType => base.NodeType;

        public GameObjectNode(string name, float x, float y, float width, float height) : base(name, x, y, width, height)
        {
            NodeType = typeof(GameObject);
        }

        public GameObjectNode(string name) : base(name)
        {
            NodeType = typeof(GameObject);
        }

        public GameObjectNode(Transform relatedTransform) : base(relatedTransform)
        {
            NodeType = typeof(GameObject);
        }

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