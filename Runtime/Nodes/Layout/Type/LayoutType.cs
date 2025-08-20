namespace _3DConnections.Runtime.Layout.Type
{
    /// <summary>
    /// Enumeration of available layout algorithms for node arrangement
    /// </summary>
    public enum LayoutType
    {
        /// <summary>
        /// Arranges nodes in a regular grid pattern
        /// Best for: Small to medium graphs (up to ~100 nodes), data visualization, uniform spacing requirements
        /// </summary>
        Grid = 0,
        
        /// <summary>
        /// Arranges nodes in circular/radial patterns with root nodes at centers
        /// Best for: Tree-like structures, small to medium graphs (up to ~200 nodes), network visualization
        /// </summary>
        Radial = 1,
        
        /// <summary>
        /// Hierarchical tree layout with clear parent-child relationships
        /// Best for: Organizational charts, decision trees, clear hierarchical data (up to ~100 nodes)
        /// </summary>
        Tree = 2,
        
        /// <summary>
        /// GRIP (Graph dRawing with Intelligent Placement) algorithm
        /// Best for: Medium graphs (50-500 nodes), general-purpose graph layout, balanced aesthetics
        /// </summary>
        GRIP = 3,
        
        /// <summary>
        /// Sugiyama hierarchical layout algorithm (layered graph drawing)
        /// Best for: Directed graphs, flowcharts, process diagrams, hierarchical networks (up to ~300 nodes)
        /// Minimizes edge crossings and creates clear layered structure
        /// </summary>
        Sugiyama = 4,
        
        /// <summary>
        /// Fast Multi-scale force-directed layout for large graphs
        /// Best for: Large graphs (500+ nodes), complex networks, when performance is critical
        /// Uses hierarchical coarsening for optimal performance on large datasets
        /// </summary>
        Multiscale = 5
    }

    /// <summary>
    /// Extension methods for LayoutType enum
    /// </summary>
    public static class LayoutTypeExtensions
    {
        /// <summary>
        /// Gets a human-readable description of the layout type
        /// </summary>
        /// <param name="layoutType">The layout type</param>
        /// <returns>Description string</returns>
        public static string GetDescription(this LayoutType layoutType)
        {
            return layoutType switch
            {
                LayoutType.Grid => "Regular grid arrangement with uniform spacing",
                LayoutType.Radial => "Circular arrangement with roots at centers",
                LayoutType.Tree => "Hierarchical tree with clear parent-child relationships",
                LayoutType.GRIP => "Intelligent placement with balanced aesthetics",
                LayoutType.Sugiyama => "Layered hierarchical layout with minimal crossings",
                LayoutType.Multiscale => "Multi-scale force-directed layout for large graphs",
                _ => "Unknown layout type"
            };
        }

        /// <summary>
        /// Gets the recommended maximum number of nodes for optimal performance
        /// </summary>
        /// <param name="layoutType">The layout type</param>
        /// <returns>Recommended maximum node count</returns>
        public static int GetRecommendedMaxNodes(this LayoutType layoutType)
        {
            return layoutType switch
            {
                LayoutType.Grid => 100,
                LayoutType.Radial => 200,
                LayoutType.Tree => 100,
                LayoutType.GRIP => 500,
                LayoutType.Sugiyama => 300,
                LayoutType.Multiscale => int.MaxValue,
                _ => 50
            };
        }

        /// <summary>
        /// Checks if the layout type is suitable for hierarchical data
        /// </summary>
        /// <param name="layoutType">The layout type</param>
        /// <returns>True if suitable for hierarchical data</returns>
        public static bool IsHierarchical(this LayoutType layoutType)
        {
            return layoutType switch
            {
                LayoutType.Tree => true,
                LayoutType.Sugiyama => true,
                LayoutType.Radial => true,
                _ => false
            };
        }

        /// <summary>
        /// Checks if the layout type performs well with large graphs
        /// </summary>
        /// <param name="layoutType">The layout type</param>
        /// <returns>True if suitable for large graphs</returns>
        public static bool IsScalable(this LayoutType layoutType)
        {
            return layoutType switch
            {
                LayoutType.Multiscale => true,
                LayoutType.GRIP => true,
                _ => false
            };
        }
    }
}