// InventoryGridData.cs - Pure data structure for sparse grid management
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages item placement in a sparse grid. No Unity dependencies.
/// Tracks which items occupy which areas, validates placement, and finds empty spaces.
/// </summary>
public class InventoryGridData
{
    private readonly int width;
    private readonly int height;
    private readonly Dictionary<string, GridArea> itemAreas; // itemInstanceId -> occupied area

    public int Width => width;
    public int Height => height;
    public int TotalCells => width * height;
    public int ItemCount => itemAreas.Count;

    public InventoryGridData(int width, int height)
    {
        this.width = width;
        this.height = height;
        this.itemAreas = new Dictionary<string, GridArea>();
    }

    #region Placement Validation

    public bool CanPlace(GridArea area, string excludeItemId = null)
    {
        if (!area.IsValid) return false;
        if (!area.IsWithinBounds(width, height)) return false;

        foreach (var kvp in itemAreas)
        {
            if (kvp.Key == excludeItemId) continue;
            if (area.Overlaps(kvp.Value)) return false;
        }
        return true;
    }

    public bool CanPlaceAt(GridPosition position, int width, int height, string excludeItemId = null)
    {
        GridArea area = new GridArea(position, width, height);
        return CanPlace(area, excludeItemId);
    }

    public bool CanPlaceAt(int x, int y, int width, int height, string excludeItemId = null)
    {
        return CanPlaceAt(new GridPosition(x, y), width, height, excludeItemId);
    }

    #endregion

    #region Item Management

    public bool PlaceItem(string itemId, GridArea area)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        if (!CanPlace(area, itemId)) return false;

        itemAreas[itemId] = area;
        return true;
    }

    public bool PlaceItem(string itemId, GridPosition position, int width, int height)
    {
        return PlaceItem(itemId, new GridArea(position, width, height));
    }

    public bool RemoveItem(string itemId)
    {
        return itemAreas.Remove(itemId);
    }

    public bool MoveItem(string itemId, GridPosition newPosition)
    {
        if (!itemAreas.TryGetValue(itemId, out GridArea currentArea))
            return false;

        GridArea newArea = new GridArea(newPosition, currentArea.width, currentArea.height);
        if (!CanPlace(newArea, itemId)) return false;

        itemAreas[itemId] = newArea;
        return true;
    }

    public void Clear()
    {
        itemAreas.Clear();
    }

    #endregion

    #region Query Methods

    public GridArea? GetItemArea(string itemId)
    {
        return itemAreas.TryGetValue(itemId, out GridArea area) ? area : null;
    }

    public string GetItemAtPosition(GridPosition position)
    {
        foreach (var kvp in itemAreas)
        {
            if (kvp.Value.Contains(position))
                return kvp.Key;
        }
        return null;
    }

    public string GetItemAtPosition(int x, int y)
    {
        return GetItemAtPosition(new GridPosition(x, y));
    }

    public bool HasItem(string itemId)
    {
        return itemAreas.ContainsKey(itemId);
    }

    #endregion

    #region Empty Space Finding

    public GridPosition FindEmptySpace(int itemWidth, int itemHeight)
    {
        for (int y = 0; y <= height - itemHeight; y++)
        {
            for (int x = 0; x <= width - itemWidth; x++)
            {
                GridArea testArea = new GridArea(x, y, itemWidth, itemHeight);
                if (CanPlace(testArea))
                {
                    return new GridPosition(x, y);
                }
            }
        }
        return GridPosition.Invalid;
    }

    public GridPosition FindClosestEmptySpace(GridPosition target, int itemWidth, int itemHeight)
    {
        GridPosition bestPosition = GridPosition.Invalid;
        int bestDistance = int.MaxValue;

        for (int y = 0; y <= height - itemHeight; y++)
        {
            for (int x = 0; x <= width - itemWidth; x++)
            {
                GridPosition testPos = new GridPosition(x, y);
                GridArea testArea = new GridArea(testPos, itemWidth, itemHeight);

                if (CanPlace(testArea))
                {
                    int distance = testPos.ManhattanDistance(target);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestPosition = testPos;
                    }
                }
            }
        }
        return bestPosition;
    }

    public int GetFreeCellCount()
    {
        int occupiedCells = itemAreas.Values.Sum(area => area.CellCount);
        return TotalCells - occupiedCells;
    }

    public float GetOccupancyPercentage()
    {
        if (TotalCells == 0) return 0f;
        int occupiedCells = itemAreas.Values.Sum(area => area.CellCount);
        return (float)occupiedCells / TotalCells;
    }

    #endregion

    #region Debug and Utility

    public string GetDebugString()
    {
        return $"Grid [{width}x{height}] - {ItemCount} items, {GetFreeCellCount()}/{TotalCells} free cells ({GetOccupancyPercentage():P0} occupied)";
    }

    public bool ValidateIntegrity()
    {
        // Check all items are within bounds
        foreach (var area in itemAreas.Values)
        {
            if (!area.IsWithinBounds(width, height))
                return false;
        }

        // Check no items overlap
        var areas = itemAreas.Values.ToList();
        for (int i = 0; i < areas.Count; i++)
        {
            for (int j = i + 1; j < areas.Count; j++)
            {
                if (areas[i].Overlaps(areas[j]))
                    return false;
            }
        }
        return true;
    }

    #endregion
}