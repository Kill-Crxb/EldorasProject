// GridArea.cs - Represents a rectangular area in the grid
using System;

/// <summary>
/// Represents a rectangular area in a grid, defined by bottom-left position and size.
/// Used for multi-slot items that occupy multiple grid cells.
/// </summary>
[Serializable]
public struct GridArea : IEquatable<GridArea>
{
    public GridPosition position;
    public int width;
    public int height;

    public GridArea(GridPosition position, int width, int height)
    {
        this.position = position;
        this.width = width;
        this.height = height;
    }

    public GridArea(int x, int y, int width, int height)
    {
        this.position = new GridPosition(x, y);
        this.width = width;
        this.height = height;
    }

    /// <summary>
    /// Returns the top-right corner position (exclusive).
    /// </summary>
    public GridPosition TopRight => new GridPosition(position.x + width, position.y + height);

    /// <summary>
    /// Returns the center position of the area (may be fractional).
    /// </summary>
    public (float x, float y) Center => (position.x + width / 2f, position.y + height / 2f);

    /// <summary>
    /// Returns true if this area contains the specified position.
    /// </summary>
    public bool Contains(GridPosition point)
    {
        return point.x >= position.x && point.x < position.x + width &&
               point.y >= position.y && point.y < position.y + height;
    }

    /// <summary>
    /// Returns true if this area overlaps with another area.
    /// Adjacent areas (touching edges) do NOT count as overlapping.
    /// </summary>
    public bool Overlaps(GridArea other)
    {
        // Areas don't overlap if one is completely to the left, right, above, or below the other
        return !(position.x + width <= other.position.x ||      // This is completely left of other
                 other.position.x + other.width <= position.x || // Other is completely left of this
                 position.y + height <= other.position.y ||      // This is completely below other
                 other.position.y + other.height <= position.y); // Other is completely below this
    }

    /// <summary>
    /// Returns true if this area is completely within the specified grid bounds.
    /// </summary>
    public bool IsWithinBounds(int gridWidth, int gridHeight)
    {
        return position.x >= 0 &&
               position.y >= 0 &&
               position.x + width <= gridWidth &&
               position.y + height <= gridHeight;
    }

    /// <summary>
    /// Returns true if the area has valid dimensions (positive width and height).
    /// </summary>
    public bool IsValid => width > 0 && height > 0 && position.IsValid;

    /// <summary>
    /// Returns all grid positions occupied by this area.
    /// </summary>
    public GridPosition[] GetOccupiedPositions()
    {
        GridPosition[] positions = new GridPosition[width * height];
        int index = 0;

        for (int y = position.y; y < position.y + height; y++)
        {
            for (int x = position.x; x < position.x + width; x++)
            {
                positions[index++] = new GridPosition(x, y);
            }
        }

        return positions;
    }

    /// <summary>
    /// Returns the total number of grid cells this area occupies.
    /// </summary>
    public int CellCount => width * height;

    // Equality
    public bool Equals(GridArea other)
    {
        return position.Equals(other.position) &&
               width == other.width &&
               height == other.height;
    }

    public override bool Equals(object obj)
    {
        return obj is GridArea other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = position.GetHashCode();
            hash = (hash * 397) ^ width;
            hash = (hash * 397) ^ height;
            return hash;
        }
    }

    public static bool operator ==(GridArea left, GridArea right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GridArea left, GridArea right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"Area[{position}, {width}x{height}]";
    }
}
