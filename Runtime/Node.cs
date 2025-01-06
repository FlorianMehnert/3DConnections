using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Runtime
{
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
        public List<Node> Children { get; set; }
        
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
            Children = new List<Node>();
            RelatedGameObject = null;
        }

        protected Node(Transform position)
        {
            X = 0;
            Y = 0;
            Width = 2;
            Height = 1;
            Name = position.name;
            Children = new List<Node>();
            RelatedGameObject = position.gameObject;
        }

        public Vector3 GetPosition()
        {
            return new Vector3(X, Y, 0);
        }
    }
}