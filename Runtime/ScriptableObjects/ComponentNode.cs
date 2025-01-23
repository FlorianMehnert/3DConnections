using System;
using JetBrains.Annotations;
using UnityEngine;

public class ComponentNode : Node
{
    protected sealed override Type NodeType => base.NodeType;

    public Component Component { get; }

    public ComponentNode(string name, float x, float y, float width, float height, [CanBeNull] Component co) : base(
        name, x, y, width, height)
    {
        NodeType = typeof(ComponentNode);
        Component = co;
    }

    public ComponentNode(string name, [CanBeNull] Component co) : base(name)
    {
        NodeType = typeof(ComponentNode);
    }

    public ComponentNode(Transform position, [CanBeNull] Component co) : base(position)
    {
        NodeType = typeof(ComponentNode);
    }

    public static ComponentNode GetOrCreateNode(Component component, NodeGraphScriptableObject nodegraph)
    {
        if (component == null)
            return null;

        // component exists with a name
        var newCo = new ComponentNode(component.GetType().Name, component);
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