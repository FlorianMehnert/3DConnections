using System;
using _3DConnections.Runtime.ScriptableObjects;
using Runtime;
using UnityEngine;
using UnityEngine.TextCore;

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

        public ComponentNode(Transform position) : base(position)
        {
            NodeType = typeof(ComponentNode);
        }

        public static Node GetOrCreateNode(Component co, NodeGraphScriptableObject nodegraph)
        {
            // also broken/null components need to return a node
            if (co == null)
            {
                var nullCo = new ComponentNode("null");
                nodegraph.Add(nullCo);
                return nullCo;
            }
            
            // component exists with a name
            var newCo = new ComponentNode(co.name);
            if (nodegraph.Contains(co))
            {
                return nodegraph.GetNode(co);
            }

            nodegraph.Add(newCo);
            return newCo;
        }
    }
}