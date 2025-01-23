using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Organizing overlay nodes in a circular layout
/// </summary>
public class RadialLayout
{
    private static float CalculateMinimumRadius(int childCount)
    {
        // Calculate the minimum radius needed to fit all children without overlap
        return (7f * childCount) / (2 * Mathf.PI);
    }

    private static bool IsOverlapping(Vector2 newPos, Node currentNode, List<Node> siblings)
    {
        return (from sibling in siblings where sibling != currentNode select Vector2.Distance(newPos, sibling.position))
            .Any(distance => distance < 7f);
    }

    public static void LayoutChildrenRadially(Node parent, float startAngle)
    {
        if (parent.GetChildren().Count == 0) return;

        var angleStep = 360f / parent.GetChildren().Count;
        var currentRadius = 12f;

        // Adjust radius if needed to prevent overlap
        var minRadius = CalculateMinimumRadius(parent.GetChildren().Count);
        currentRadius = Mathf.Max(currentRadius, minRadius);

        for (var i = 0; i < parent.GetChildren().Count; i++)
        {
            var angle = startAngle + (i * angleStep);
            var radian = angle * Mathf.Deg2Rad;

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
            // Layout this node's children with an offset angle
            LayoutChildrenRadially(parent.GetChildren()[i], angle);
        }
    }
}