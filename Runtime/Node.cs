using System.Collections.Generic;
using _3DConnections.Runtime.Managers;
using UnityEngine;

namespace Runtime
{
    /// <summary>
    /// Internal representation of a Node used to compute layouts and keep track of all available nodes
    /// </summary>
    public class Node
    {
        
        public string name;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public Color Color { get; set; }
        public List<Node> Children { get; set; }
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
            Width = 2;
            Height = 1;
            this.name = name;
            Children = new List<Node>();
            relatedGameObject = null;
        }

        public Node(Transform relatedTransform)
        {
            X = 0;
            Y = 0;
            Width = 2;
            Height = 1;
            name = relatedTransform.name;
            Children = new List<Node>();
            relatedGameObject = relatedTransform.gameObject;
            
        }

        public void AttachLagProfiler()
        {
            var profiler = relatedGameObject.GetComponent<LagProfiler>();
            if (profiler == null)
            {
                relatedGameObject.AddComponent<LagProfiler>();
            }
        }

        public void RemoveLagProfiler()
        {
            var profiler = relatedGameObject.GetComponent<LagProfiler>();
            if (profiler != null)
            {
                Object.Destroy(profiler);
            }
        }

        public void ToggleLagProfiler()
        {
            var profiler = relatedGameObject.GetComponent<LagProfiler>();
            profiler?.ToggleIsMonitoring();
        }
    }
}