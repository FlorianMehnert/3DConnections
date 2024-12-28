using System;
using _3DConnections.Runtime.ScriptableObjects;
using Runtime;
using Unity.VisualScripting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _3DConnections.Runtime
{
    public class ScriptableObjectNode : Node
    {
        protected sealed override Type NodeType => base.NodeType;

        public ScriptableObjectNode(string name, float x, float y, float width, float height) : base(name, x, y, width, height)
        {
            NodeType = typeof(ScriptableObject);
        }

        public ScriptableObjectNode(string name) : base(name)
        {
            NodeType = typeof(ScriptableObject);
        }

        public ScriptableObjectNode(Transform relatedTransform) : base(relatedTransform)
        {
            NodeType = typeof(ScriptableObject);
        }

        public static Node GetOrCreateNode(ScriptableObject so, NodeGraphScriptableObject nodegraph)
        {
            var newSo = new ScriptableObjectNode(so.name);
            return nodegraph.Contains(so) ? nodegraph.GetNode(so) : nodegraph.Add(newSo) ? newSo : null;
        }
    }
}