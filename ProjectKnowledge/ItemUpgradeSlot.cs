using UnityEngine;

/// <summary>
/// ItemUpgradeSlot - Individual upgrade slot configuration for an item
/// 
/// Purpose: Define specific upgrade slots for each item (0-N slots)
/// 
/// Examples:
/// - Helmet with 3 slots: [Reinforcement, Underlay, Trinket]
/// - Unique weapon with 1 slot: [Legendary Rune]
/// - Basic armor with 0 slots: []
/// 
/// Usage:
/// - Add to ItemDefinition.upgradeSlots array
/// - Each slot references UpgradeSlotDefinition (what it accepts)
/// - ItemInstance tracks what's actually slotted
/// 
/// Benefits:
/// - Each item can have unique slot configuration
/// - Slots define what upgrades they accept
/// - Supports locked/unlockable slots
/// - UI display order via slotIndex
/// </summary>
[System.Serializable]
public class ItemUpgradeSlot
{
    [Header("Slot Configuration")]
    [Tooltip("What types of upgrades this slot accepts")]
    public UpgradeSlotDefinition slotDefinition;

    [Tooltip("Display order in UI (lower = higher priority)")]
    public int slotIndex;

    [Header("Slot State")]
    [Tooltip("Is this slot locked by default? (requires unlock progression)")]
    public bool isLockedByDefault;

    [Tooltip("Custom unlock requirement description (e.g., 'Complete Quest: Blacksmith Master')")]
    [TextArea(1, 2)]
    public string unlockRequirement;

    [Header("Visual Override")]
    [Tooltip("Override slot icon (uses slotDefinition.slotIcon if null)")]
    public Sprite customSlotIcon;

    [Tooltip("Override slot color (uses slotDefinition.slotColor if default)")]
    public Color customSlotColor = Color.white;

    /// <summary>
    /// Check if this slot can accept an upgrade item
    /// </summary>
    public bool CanAcceptUpgrade(ItemDefinition upgradeItem)
    {
        // Guard clause - no definition
        if (slotDefinition == null) return false;

        // Guard clause - locked
        if (isLockedByDefault) return false;

        // Delegate to slot definition for acceptance logic
        return slotDefinition.CanAccept(upgradeItem);
    }

    /// <summary>
    /// Get the icon to display for this slot
    /// </summary>
    public Sprite GetDisplayIcon()
    {
        // Use custom icon if provided
        if (customSlotIcon != null) return customSlotIcon;

        // Fall back to slot definition icon
        if (slotDefinition != null) return slotDefinition.slotIcon;

        // No icon available
        return null;
    }

    /// <summary>
    /// Get the color to display for this slot
    /// </summary>
    public Color GetDisplayColor()
    {
        // Use custom color if not default white
        if (customSlotColor != Color.white) return customSlotColor;

        // Fall back to slot definition color
        if (slotDefinition != null) return slotDefinition.slotColor;

        // Default white
        return Color.white;
    }

    /// <summary>
    /// Validation helper
    /// </summary>
    public bool IsValid()
    {
        return slotDefinition != null && slotDefinition.IsValid();
    }
}