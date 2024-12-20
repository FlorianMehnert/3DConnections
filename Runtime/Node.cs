using System.Drawing;

namespace Runtime
{
    /// <summary>
    /// Internal representation of a Node used to compute layouts and keep track of all available nodes
    /// </summary>
    public class Node
    {
        
        public string Name;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public Color Color { get; set; }

        public Node(string name, float x, float y, float width, float height)
        {
            Name = name;
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
            Name = name;
        }
    }
}