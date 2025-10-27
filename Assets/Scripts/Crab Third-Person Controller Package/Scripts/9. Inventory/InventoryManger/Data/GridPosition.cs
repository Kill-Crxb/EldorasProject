// GridPosition.cs - Simple value type for grid coordinates
using System;

/// <summary>
/// Represents a position in a 2D grid using integer coordinates.
/// Bottom-left is (0,0), X increases right, Y increases up.
/// </summary>
[Serializable]
public struct GridPosition : IEquatable<GridPosition>
{
    public int x;
    public int y;

    public GridPosition(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    /// <summary>
    /// Returns true if this position has valid (non-negative) coordinates.
    /// </summary>
    public bool IsValid => x >= 0 && y >= 0;

    /// <summary>
    /// Represents an invalid/uninitialized position.
    /// </summary>
    public static GridPosition Invalid => new GridPosition(-1, -1);

    /// <summary>
    /// Returns true if this position is within the specified grid bounds.
    /// </summary>
    public bool IsWithinBounds(int gridWidth, int gridHeight)
    {
        return x >= 0 && x < gridWidth && y >= 0 && y < gridHeight;
    }

    /// <summary>
    /// Calculates Manhattan distance to another position.
    /// </summary>
    public int ManhattanDistance(GridPosition other)
    {
        return Math.Abs(x - other.x) + Math.Abs(y - other.y);
    }

    // Equality and operators
    public bool Equals(GridPosition other)
    {
        return x == other.x && y == other.y;
    }

    public override bool Equals(object obj)
    {
        return obj is GridPosition other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (x * 397) ^ y;
        }
    }

    public static bool operator ==(GridPosition left, GridPosition right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GridPosition left, GridPosition right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"({x}, {y})";
    }
}
