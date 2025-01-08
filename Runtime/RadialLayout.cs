using System.Collections.Generic;
using Runtime;
using UnityEngine;

namespace _3DConnections.Runtime
{
    public class RadialLayout
    {
        private float minNodeDistance = 7f; // Minimum distance between nodes
        private float radiusStep = 12f;
        private static float CalculateMinimumRadius(int childCount) 
        {
            // Calculate minimum radius needed to fit all children without overlap
            return (7f * childCount) / (2 * Mathf.PI);
        }

        private static bool IsOverlapping(Vector2 newPos, Node currentNode, List<Node> siblings) 
        {
            foreach (Node sibling in siblings) 
            {
                if (sibling == currentNode) continue;
            
                float distance = Vector2.Distance(newPos, sibling.position);
                if (distance < 7f) return true;
            }
            return false;
        }
        public static void LayoutChildrenRadially(Node parent, float startAngle) 
        {
            if (parent.GetChildren().Count == 0) return;

            float angleStep = 360f / parent.GetChildren().Count;
            float currentRadius = 12f;
        
            // Adjust radius if needed to prevent overlap
            float minRadius = CalculateMinimumRadius(parent.GetChildren().Count);
            currentRadius = Mathf.Max(currentRadius, minRadius);

            for (int i = 0; i < parent.GetChildren().Count; i++) 
            {
                float angle = startAngle + (i * angleStep);
                float radian = angle * Mathf.Deg2Rad;
            
                var newPos = (Vector2)parent.position + new Vector2(
                    Mathf.Cos(radian) * currentRadius,
                    Mathf.Sin(radian) * currentRadius
                );

                // Adjust position if overlapping with siblings
                while (IsOverlapping(newPos, parent.GetChildren()[i], parent.GetChildren())) 
                {
                    currentRadius += 7f * 0.5f;
                    newPos = (Vector2)parent.position + new Vector2(
                        Mathf.Cos(radian) * currentRadius,
                        Mathf.Sin(radian) * currentRadius
                    );
                }

                parent.GetChildren()[i].position = newPos;
                // Layout this node's children with offset angle
                LayoutChildrenRadially(parent.GetChildren()[i], angle);
            }
        }
    }
}