namespace Runtime
{
    public class Node
    {
        
        public string Name;
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public Node(string name, float x, float y, float width, float height)
        {
            Name = name;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}