namespace Runtime
{
    public class Node
    {
        private string _name;
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public Node (float x, float y, float width, float height){
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}