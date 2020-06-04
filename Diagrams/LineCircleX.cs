namespace Diagrams
{
    /// <summary>
    /// Enumerates all possible solutions to the intersection event of a circle and an infinite line segment.
    /// </summary>
    /// <exclude />
    public enum LineCircleX
    {
        /// <summary>No intersection, line is completely outside the circle.</summary>
        None,
        /// <summary>Line is tangent to circle.</summary>
        Tangent,
        /// <summary>Line and circle intersect in 2 points</summary>
        Secant,
    }
}