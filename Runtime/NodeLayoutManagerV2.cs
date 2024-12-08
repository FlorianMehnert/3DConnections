using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Runtime;
using UnityEngine;

public class NodeLayoutManagerV2
{
    /// <summary>
    /// Compact layout algorithm with fixed aspect ratio for all nodes
    /// </summary>
    /// <param name="nodes">List of nodes to be positioned</param>
    /// <param name="targetAspectRatio">Desired aspect ratio (width/height)</param>
    /// <param name="minimumPadding">Minimum spacing between nodes</param>
    public static void CompactFixedAspectRatioLayout(List<Node> nodes, float targetAspectRatio = 1.0f, float minimumPadding = 5f)
    {
        if (nodes == null || nodes.Count == 0) return;

        // Calculate total area needed
        float totalArea = nodes.Sum(n => n.Width * n.Height);

        // Determine layout grid dimensions
        int nodeCount = nodes.Count;
        int gridColumns = Mathf.CeilToInt(Mathf.Sqrt(nodeCount));
        int gridRows = Mathf.CeilToInt((float)nodeCount / gridColumns);

        // Normalize node sizes to a fixed aspect ratio while preserving total area
        List<Node> normalizedNodes = nodes.Select(node => 
        {
            // Calculate new dimensions maintaining the target aspect ratio
            float newWidth = Mathf.Sqrt(node.Width * node.Height * targetAspectRatio);
            float newHeight = newWidth / targetAspectRatio;

            return new Node(node.Name, 0, 0, newWidth, newHeight);
        }).ToList();

        // Determine max node dimensions in the grid
        float maxNodeWidth = normalizedNodes.Max(n => n.Width);
        float maxNodeHeight = normalizedNodes.Max(n => n.Height);

        // Parallel processing for positioning
        Parallel.For(0, normalizedNodes.Count, (int i) =>
        {
            // Calculate grid position
            int row = i / gridColumns;
            int col = i % gridColumns;

            // Position calculation with padding
            float x = col * (maxNodeWidth + minimumPadding);
            float y = row * (maxNodeHeight + minimumPadding);

            // Update node position and size
            Node originalNode = nodes[i];
            Node normalizedNode = normalizedNodes[i];

            // Preserve original scaling while positioning
            originalNode.X = x + (maxNodeWidth - originalNode.Width) / 2;
            originalNode.Y = y + (maxNodeHeight - originalNode.Height) / 2;
        });
    }

    /// <summary>
    /// Advanced layout with more precise aspect ratio and spacing control
    /// </summary>
    /// <param name="nodes">List of nodes to be positioned</param>
    /// <param name="targetAspectRatio">Desired aspect ratio (width/height)</param>
    /// <param name="minimumPadding">Minimum spacing between nodes</param>
    /// <param name="maxIterations">Maximum layout optimization iterations</param>
    public static void PreciseFixedAspectRatioLayout(List<Node> nodes, float targetAspectRatio = 1.0f, float minimumPadding = 5f, int maxIterations = 5)
    {
        // First, apply compact layout
        CompactFixedAspectRatioLayout(nodes, targetAspectRatio, minimumPadding);

        // Refinement pass to optimize spacing and maintain aspect ratio
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            Parallel.For(0, nodes.Count, (int i) =>
            {
                Node currentNode = nodes[i];

                // Enforce aspect ratio
                float currentAspect = currentNode.Width / currentNode.Height;
                if (Math.Abs(currentAspect - targetAspectRatio) > 0.01f)
                {
                    // Adjust dimensions to match target aspect ratio
                    if (currentAspect > targetAspectRatio)
                    {
                        // Width is too large, reduce width
                        currentNode.Width = currentNode.Height * targetAspectRatio;
                    }
                    else
                    {
                        // Height is too large, reduce height
                        currentNode.Height = currentNode.Width / targetAspectRatio;
                    }
                }
            });

            // Re-run layout to adjust positioning after dimension changes
            CompactFixedAspectRatioLayout(nodes, targetAspectRatio, minimumPadding);
        }
    }
}