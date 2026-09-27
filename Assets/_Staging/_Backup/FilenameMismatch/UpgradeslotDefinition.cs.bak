using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UpgradeSlotDefinition - Defines what types of upgrades a slot can accept
/// 
/// Purpose: Replace hardcoded UpgradeSlotType enum with flexible data-driven system
/// 
/// Examples:
/// - "Reinforcement" → Accepts: Armor plating, weapon sharpening
/// - "Underlay" → Accepts: Stat-boosting gems, enchantments
/// - "Trinket" → Accepts: Special effect items, charms
/// - "Rune" → Accepts: Magic runes (jewelry slots)
/// - "Socket" → Accepts: Gemstones (ring slots)
/// 
/// Usage:
/// - Create assets: Right-click → Items/Upgrade Slot Definition
/// - Configure what categories/types can be inserted
/// - Reference in ItemUpgradeSlot to define item's upgrade slots
/// 
/// Benefits:
/// - Add new upgrade types without code changes
/// - Flexible filtering (by category, subtype, or tags)
/// - Visual customization per slot type
/// </summary>
[CreateAssetMenu(fileName = "New Upgrade Slot", menuName = "Items/Upgrade Slot Definition")]
public class UpgradeSlotDefinition : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique identifier (e.g., 'reinforcement', 'underlay', 'trinket')")]
    public string slotId;

    [Tooltip("Display name shown in UI")]
    public string displayName;

    [Header("Accepted Upgrades")]
    [Tooltip("Item categories that can be inserted (e.g., 'Upgrade' category)")]
    public List<ItemCategory> acceptedCategories = new List<ItemCategory>();

    [Tooltip("Specific item subtypes that can be inserted (if empty, allows all from categories)")]
    public List<ItemSubType> acceptedSubTypes = new List<ItemSubType>();

    [Tooltip("Tag-based filtering (e.g., 'offensive_gem', 'defensive_rune')")]
    public List<string> acceptedTags = new List<string>();

    [Header("Visual")]
    [Tooltip("Icon representing this slot type in UI")]
    public Sprite slotIcon;

    [Tooltip("Background sprite for slot")]
    public Sprite slotBackground;

    [Tooltip("Color tint for slot in UI")]
    public Color slotColor = Color.white;

    [Header("Behavior")]
    [Tooltip("Can this slot be left empty?")]
    public bool isOptional = true;

    [Tooltip("Is this slot locked by default? (requires unlock item/progression)")]
    public bool isLockedByDefault = false;

    [Header("Description")]
    [TextArea(2, 4)]
    public string description;

    /// <summary>
    /// Check if an item can be inserted into this slot type
    /// </summary>
    public bool CanAccept(ItemDefinition item)
    {
        // Guard clause - null check
        if (item == null) return false;

        // Check category restrictions
        if (acceptedCategories != null && acceptedCategories.Count > 0)
        {
            bool categoryMatch = false;
            foreach (var category in acceptedCategories)
            {
                if (item.category == category)
                {
                    categoryMatch = true;
                    break;
                }
            }
            if (!categoryMatch) return false;
        }

        // Check subtype restrictions
        if (acceptedSubTypes != null && acceptedSubTypes.Count > 0)
        {
            bool subtypeMatch = false;
            foreach (var subtype in acceptedSubTypes)
            {
                if (item.subType == subtype)
                {
                    subtypeMatch = true;
                    break;
                }
            }
            if (!subtypeMatch) return false;
        }

        // Check tag restrictions
        if (acceptedTags != null && acceptedTags.Count > 0)
        {
            bool tagMatch = false;
            if (item.tags != null)
            {
                foreach (var requiredTag in acceptedTags)
                {
                    foreach (var itemTag in item.tags)
                    {
                        if (requiredTag == itemTag)
                        {
                            tagMatch = true;
                            break;
                        }
                    }
                    if (tagMatch) break;
                }
            }
            if (!tagMatch) return false;
        }

        return true;
    }

    /// <summary>
    /// Validation helper
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(slotId) && !string.IsNullOrEmpty(displayName);
    }
}