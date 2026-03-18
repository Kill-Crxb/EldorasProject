using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Equipment Loadout - ScriptableObject representing equipped items
/// 
/// Philosophy:
/// - This is JUST data - "here's what items are equipped"
/// - No behavior, no lifecycle logic
/// - Can be created at runtime or pre-configured in editor
/// - Can be saved to disk or kept in memory
/// 
/// Use Cases:
/// 1. Player Equipment State
///    - Save/load equipped items
///    - Persist between sessions
/// 
/// 2. NPC Equipment Templates
///    - Pre-configure NPC loadouts in editor
///    - Reuse across multiple NPCs
/// 
/// 3. Loadout Swapping (Future)
///    - PvE vs PvP gear sets
///    - Quick equipment changes
/// 
/// Lifecycle Responsibility:
/// - EquipmentSystem: Decides WHEN to save/load
/// - EquipmentLoadout: Just holds the data
/// 
/// Created: February 11, 2026
/// </summary>
[CreateAssetMenu(fileName = "New Equipment Loadout", menuName = "Items/Equipment Loadout")]
public class EquipmentLoadout : ScriptableObject
{
    [Header("Loadout Info")]
    [Tooltip("Loadout name (e.g., 'PvE Tank Build', 'PvP DPS Setup')")]
    public string loadoutName = "Default Loadout";

    [Header("Equipment Slots")]
    [Tooltip("Equipped items per slot (editable in inspector)")]
    public List<EquipmentSlotData> slots = new List<EquipmentSlotData>();

    [Header("Metadata")]
    [Tooltip("Last modified timestamp (runtime updated)")]
    public long lastModified;

    [Tooltip("Owner ID (for player-specific loadouts)")]
    public string ownerId;

    #region Runtime API

    /// <summary>
    /// Set equipped item for a specific slot
    /// </summary>
    public void SetSlot(EquipmentSlot slotType, string itemId, ItemRarity rarity)
    {
        // Guard clause: invalid item
        if (string.IsNullOrEmpty(itemId)) return;

        // Find existing slot
        var existing = slots.Find(s => s.slotType == slotType);
        if (existing != null)
        {
            // Update existing
            existing.itemId = itemId;
            existing.rarity = rarity;
            existing.equipTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        else
        {
            // Add new slot
            slots.Add(new EquipmentSlotData
            {
                slotType = slotType,
                itemId = itemId,
                rarity = rarity,
                equipTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }

        UpdateTimestamp();
    }

    /// <summary>
    /// Clear equipped item from specific slot
    /// </summary>
    public void ClearSlot(EquipmentSlot slotType)
    {
        slots.RemoveAll(s => s.slotType == slotType);
        UpdateTimestamp();
    }

    /// <summary>
    /// Get equipped item for specific slot
    /// </summary>
    public EquipmentSlotData GetSlot(EquipmentSlot slotType)
    {
        return slots.Find(s => s.slotType == slotType);
    }

    /// <summary>
    /// Clear all equipped items
    /// </summary>
    public void ClearAll()
    {
        slots.Clear();
        UpdateTimestamp();
    }

    /// <summary>
    /// Get count of equipped items
    /// </summary>
    public int EquippedCount => slots.Count;

    /// <summary>
    /// Update last modified timestamp
    /// </summary>
    public void UpdateTimestamp()
    {
        lastModified = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    #endregion

    #region Serialization

    /// <summary>
    /// Save this loadout to disk as JSON
    /// </summary>
    public void SaveToDisk(string path)
    {
        var wrapper = new EquipmentLoadoutWrapper { loadout = this };
        string json = JsonUtility.ToJson(wrapper, true);
        System.IO.File.WriteAllText(path, json);
    }

    /// <summary>
    /// Load loadout from disk
    /// </summary>
    public static EquipmentLoadout LoadFromDisk(string path)
    {
        // Guard clause: file doesn't exist
        if (!System.IO.File.Exists(path))
            return null;

        string json = System.IO.File.ReadAllText(path);
        var wrapper = JsonUtility.FromJson<EquipmentLoadoutWrapper>(json);
        return wrapper.loadout;
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        // Auto-generate ownerId if empty
        if (string.IsNullOrEmpty(ownerId))
        {
            ownerId = $"loadout_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        }
    }

    #endregion

    #region Debug

    [ContextMenu("Clear All Slots")]
    private void DebugClearAll()
    {
        ClearAll();
        Debug.Log($"[EquipmentLoadout] Cleared all slots from {loadoutName}");
    }

    [ContextMenu("Print Loadout")]
    private void DebugPrintLoadout()
    {
        Debug.Log($"=== {loadoutName} ({ownerId}) ===");
        Debug.Log($"Equipped Items: {slots.Count}");
        foreach (var slot in slots)
        {
            Debug.Log($"  - {slot.slotType}: {slot.itemId} ({slot.rarity})");
        }
    }

    #endregion
}

/// <summary>
/// Serializable equipment slot entry
/// Lightweight - just item ID, rarity, and timestamp
/// </summary>
[System.Serializable]
public class EquipmentSlotData
{
    [Tooltip("Equipment slot type")]
    public EquipmentSlot slotType;

    [Tooltip("Item ID from ItemManager")]
    public string itemId;

    [Tooltip("Item rarity/tier")]
    public ItemRarity rarity = ItemRarity.Common;

    [Tooltip("When this item was equipped (Unix timestamp)")]
    public long equipTimestamp;
}

/// <summary>
/// Wrapper for JSON serialization
/// </summary>
[System.Serializable]
public class EquipmentLoadoutWrapper
{
    public EquipmentLoadout loadout;
}