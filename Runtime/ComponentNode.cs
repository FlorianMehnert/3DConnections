using System;
using _3DConnections.Runtime.ScriptableObjects;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime
{
    public class ComponentNode : Node
    {
        protected sealed override Type NodeType => base.NodeType;
        
        public ComponentNode(string name, float x, float y, float width, float height) : base(name, x, y, width, height)
        {
            NodeType = typeof(ComponentNode);
        }

        public ComponentNode(string name) : base(name)
        {
            NodeType = typeof(ComponentNode);
        }

        public ComponentNode(Transform relatedTransform) : base(relatedTransform)
        {
            NodeType = typeof(ComponentNode);
        }

        public static Node GetOrCreateNode(Component co, NodeGraphScriptableObject nodegraph)
        {
            var newCo = new ScriptableObjectNode(co.name);
            return nodegraph.Contains(co) ? nodegraph.GetNode(co) : nodegraph.Add(newCo) ? newCo : null;
        }
    }
}