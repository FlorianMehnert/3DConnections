using System;
using JetBrains.Annotations;
using UnityEngine;

public class ComponentNodeV1 : NodeV1
{
    protected sealed override Type NodeType => base.NodeType;

    public Component Component { get; }

    public ComponentNodeV1(string name, float x, float y, float width, float height, [CanBeNull] Component co) : base(
        name, x, y, width, height)
    {
        NodeType = typeof(ComponentNodeV1);
        Component = co;
    }

    public ComponentNodeV1(string name, [CanBeNull] Component co) : base(name)
    {
        NodeType = typeof(ComponentNodeV1);
    }

    public ComponentNodeV1(Transform position, [CanBeNull] Component co) : base(position)
    {
        NodeType = typeof(ComponentNodeV1);
    }

    public static ComponentNodeV1 GetOrCreateNode(Component component, NodeGraphScriptableObject nodegraph)
    {
        if (component == null)
            return null;

        // component exists with a name
        var newCo = new ComponentNodeV1(component.GetType().Name, component);
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