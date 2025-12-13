using UnityEngine;

/// <summary>
/// Save data structure for StatAllocationSystem.
/// Stores player level and unallocated stat points.
/// </summary>
[System.Serializable]
public class StatAllocationData
{
    public int playerLevel;
    public int unallocatedPoints;
}