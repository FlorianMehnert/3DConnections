using System;
using System.Text;
using Runtime;

namespace _3DConnections.Runtime
{
    public static class NodeTreePrinter
    {
        public static string PrintTree(Node root, bool includePositionInfo = false)
        {
            var result = new StringBuilder();
            PrintNodeRecursive(root, "", true, result, includePositionInfo);
            return result.ToString();
        }

        private static void PrintNodeRecursive(Node node, string indent, bool isLast, StringBuilder result, bool includePositionInfo)
        {
            // Print current node
            result.Append(indent);
            
            // Print the appropriate connector
            if (isLast)
            {
                result.Append("└── ");
                indent += "    ";
            }
            else
            {
                result.Append("├── ");
                indent += "│   ";
            }

            // Print node information
            result.AppendLine(includePositionInfo ? $"{node.name} (X:{node.X}, Y:{node.Y}, W:{node.Width}, H:{node.Height})" : node.name);

            // Process children
            if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Count; i++)
                {
                    PrintNodeRecursive(node.Children[i], indent, i == node.Children.Count - 1, result, includePositionInfo);
                }
            }
        }

        // Method to print all connections as a list
        public static string PrintConnections(Node root)
        {
            StringBuilder result = new StringBuilder();
            PrintConnectionsRecursive(root, result);
            return result.ToString();
        }

        private static void PrintConnectionsRecursive(Node node, StringBuilder result)
        {
            if (node.Children is not { Count: > 0 }) return;
            foreach (var child in node.Children)
            {
                result.AppendLine($"{node.name} -> {child.name}");
                PrintConnectionsRecursive(child, result);
            }
        }
    }
}