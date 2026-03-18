using UnityEngine;

/// <summary>
/// Serializable item entry in container
/// Lightweight - just IDs and position, not full ItemInstance
/// </summary>
[System.Serializable]
public class ContainerItem
{
    [Tooltip("Item ID from ItemManager")]
    public string itemId;

    [Tooltip("Item rarity/tier")]
    public ItemRarity rarity = ItemRarity.Common;

    [Tooltip("Grid X position")]
    public int gridX;

    [Tooltip("Grid Y position")]
    public int gridY;

    [Tooltip("When this item was added (Unix timestamp)")]
    public long timestamp;
}