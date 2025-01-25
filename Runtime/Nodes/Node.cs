using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Internal representation of a Node used to compute layouts and keep track of all available nodes
/// </summary>
public abstract class Node
{
    public readonly string Name;
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    /// <summary>
    /// Property for displaying hierarchy among nodes. Mostly influenced by transform hierarchies
    /// </summary>
    private List<Node> _children = new();

    // physical node
    public GameObject RelatedGameObject;

    // the respective object in the analyzed scene
    protected virtual Type NodeType { get; set; }

    protected Node(string name, float x, float y, float width, float height)
    {
        Name = name;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    protected Node(string name)
    {
        X = 0;
        Y = 0;
        Width = 2;
        Height = 1;
        Name = name;
        RelatedGameObject = null;
    }

    protected Node(Transform position)
    {
        X = 0;
        Y = 0;
        Width = 2;
        Height = 1;
        Name = position.name;
        RelatedGameObject = position.gameObject;
    }

    public Vector3 position
    {
        get { return new Vector3(X, Y, 0); }
        set
        {
            X = value.x;
            Y = value.y;
        }
    }

    public override string ToString()
    {
        return "Name: " + Name + " NodeType: " + NodeType;
    }

    public bool AddChild(Node child)
    {
        if (_children.Contains(child)) return false;
        _children.Add(child);
        return true;
    }

    public bool RemoveChild(Node child)
    {
        return _children.Remove(child);
    }

    public List<Node> GetChildren()
    {
        return _children;
    }

    public void SetChildren(List<Node> children)
    {
        _children = children;
    }
}