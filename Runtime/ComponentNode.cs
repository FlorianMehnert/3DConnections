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

        public static ComponentNode GetOrCreateNode(Component component, NodeGraphScriptableObject nodegraph)
        {
            // also broken/null components need to return a node
            if (component == null)
            {
                var nullCo = new ComponentNode("null");
                nodegraph.Add(nullCo);
                return nullCo;
            }
            
            // component exists with a name
            var newCo = new ComponentNode(component.name);
            if (nodegraph.Contains(component))
            {
                var componentNode = nodegraph.GetNode(component);
                if (componentNode == null)
                {
                    componentNode = newCo;
                }
                return componentNode;
            }

            nodegraph.Add(newCo);
            return newCo;
        }
    }
}