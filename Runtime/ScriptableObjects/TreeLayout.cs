using System;
using System.Collections.Generic;
using System.Linq;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime
{
    public static class TreeLayout
    {
        private const float LevelSeparation = 10f;
        private const float SiblingSeparation = 1f;

        /// <summary>
        /// Single use to follow a tree structure and align all the node positions, so they do not overlap using Reingold-Tilford algorithm. Takes width and height of nodes into account
        /// </summary>
        /// <param name="root"></param>
        public static void LayoutTree(Node root)
        {
            if (root == null) return;

            var visited = new HashSet<Node>();
            // First pass: Assign initial y coordinates based on level
            // and calculate preliminary x coordinates
            AssignYCoordinates(root, 0, visited);
            visited.Clear();
            CalculateInitialX(root, visited);

            // Second pass: Adjust for conflicts between subtrees
            var modsums = new float[1000]; // Adjust size based on expected tree depth
            visited.Clear();
            AdjustTreePositions(root, 0, 0, modsums, visited);

            // Final pass: Apply the modifications
            visited.Clear();
            ApplyMods(root, 0, visited);
        }

        private static void AssignYCoordinates(Node node, float level, HashSet<Node> visited)
        {
            if (!visited.Add(node)) return;

            node.Y = level * LevelSeparation;
            if (node.RelatedGameObject == null) return;
            foreach (var child in node.GetChildren())
            {
                AssignYCoordinates(child, level + 1, visited);
            }
        }

        private static float CalculateInitialX(Node node, HashSet<Node> visited)
        {
            if (!visited.Add(node)) return node.X;

            if (node.GetChildren() == null || node.GetChildren().Count == 0)
            {
                return 0;
            }

            var leftmost = float.MaxValue;
            var rightmost = float.MinValue;

            foreach (var childX in from child in node.GetChildren() where !visited.Contains(child) select CalculateInitialX(child, visited))
            {
                leftmost = Math.Min(leftmost, childX);
                rightmost = Math.Max(rightmost, childX);
            }

            // If no unvisited children were processed, return current X
            if (Mathf.Approximately(leftmost, float.MaxValue))
            {
                return node.X;
            }

            // Position node at the center of its unvisited children
            var centerX = (leftmost + rightmost) / 2;
            node.X = centerX;

            return centerX;
        }

        private static void AdjustTreePositions(Node node, int level, float modsum, float[] modsums, HashSet<Node> visited)
        {
            if (!visited.Add(node)) return;

            var prevMod = modsums[level];
            modsums[level] = modsum;

            if (node.GetChildren() == null || node.GetChildren().Count == 0)
            {
                return;
            }

            // Create a list of unvisited children
            var unvisitedChildren = node.GetChildren().Where(child => !visited.Contains(child)).ToList();

            if (unvisitedChildren.Count > 0)
            {
                var leftmost = unvisitedChildren[0];
                var previous = leftmost;

                for (var i = 1; i < unvisitedChildren.Count; i++)
                {
                    var current = unvisitedChildren[i];

                    // Check for conflicts and adjust if needed
                    var minDistance = previous.Width + SiblingSeparation;
                    if (current.X - previous.X < minDistance)
                    {
                        var shift = minDistance - (current.X - previous.X);
                        current.X += shift;

                        // Propagate the shift to all right siblings
                        for (var j = i + 1; j < unvisitedChildren.Count; j++)
                        {
                            unvisitedChildren[j].X += shift;
                        }
                    }

                    previous = current;
                }

                // Recursively process unvisited children
                foreach (var child in unvisitedChildren)
                {
                    AdjustTreePositions(child, level + 1, modsum, modsums, visited);
                }
            }

            modsums[level] = prevMod;
        }

        private static void ApplyMods(Node node, float modsum, HashSet<Node> visited)
        {
            if (!visited.Add(node)) return;

            node.X += modsum;

            if (node.GetChildren() == null) return;
            foreach (var child in node.GetChildren())
            {
                ApplyMods(child, modsum, visited);
            }
        }
    }
}