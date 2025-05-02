using System;
using JetBrains.Annotations;
using UnityEngine;

public class GameObjectNodeV1 : NodeV1
{
    protected sealed override Type NodeType => base.NodeType;


    public GameObjectNodeV1(string name, float x, float y, float width, float height, [CanBeNull] GameObject go) : base(
        name, x, y, width, height)
    {
        NodeType = typeof(GameObject);
        GameObject = go;
    }

    /// <summary>
    /// Construct a GameObjectNode
    /// </summary>
    /// <param name="name">Name of the GameObjectNode</param>
    /// <param name="go">GameObject that will be represented by this GameObjectNode</param>
    public GameObjectNodeV1(string name, [CanBeNull] GameObject go) : base(name)
    {
        NodeType = typeof(GameObject);
        GameObject = go;
    }

    public GameObject GameObject { get; }
}