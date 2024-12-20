using System;
using UnityEngine;
using UnityEngine.Serialization;
using Color = System.Drawing.Color;

namespace Runtime
{
    /// <summary>
    /// Internal representation of a Node used to compute layouts and keep track of all available nodes
    /// </summary>
    [Serializable]
    public class Node
    {
        
        public string name;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public Color Color { get; set; }
        public Node[] Children { get; set; }
        public GameObject relatedGameObject;

        public Node(string name, float x, float y, float width, float height)
        {
            this.name = name;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Node(string name)
        {
            X = 0;
            Y = 0;
            Width = 150;
            Height = 30;
            this.name = name;
            Children = Array.Empty<Node>();
            relatedGameObject = null;
        }
    }
}