using UnityEngine;

/// <summary>
/// ItemSubType - Specific item types under categories
/// 
/// Purpose: Replace hardcoded EquipmentSlot/ItemType enums with data-driven system
/// 
/// Examples:
/// - Category: Weapon → SubTypes: Katana, Longsword, Bow, Staff
/// - Category: Armor → SubTypes: Helmet, Chestplate, Gloves, Boots
/// - Category: Consumable → SubTypes: Health Potion, Mana Potion, Buff Scroll
/// 
/// Usage:
/// - Create assets: Right-click → Items/SubType
/// - Assign parent category
/// - Configure equipment binding if applicable
/// 
/// Benefits:
/// - Add new item types without code changes
/// - Each subtype can have unique properties
/// - Flexible equipment slot assignment
/// </summary>
[CreateAssetMenu(fileName = "New Item SubType", menuName = "Items/SubType")]
public class ItemSubType : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique identifier (e.g., 'katana', 'helmet', 'health_potion')")]
    public string subTypeId;

    [Tooltip("Display name shown in UI")]
    public string displayName;

    [Header("Category")]
    [Tooltip("Parent category this subtype belongs to")]
    public ItemCategory parentCategory;

    [Header("Equipment Binding")]
    [Tooltip("Can items of this subtype be equipped?")]
    public bool isEquippable;

    [Tooltip("Equipment slot this subtype occupies (if equippable)")]
    public EquipmentSlotDefinition equipmentSlot;

    [Header("Visual")]
    [Tooltip("Default icon for items of this subtype (can be overridden per item)")]
    public Sprite defaultIcon;

    [Header("Description")]
    [TextArea(2, 4)]
    public string description;

    [Header("Advanced")]
    [Tooltip("Tags for filtering/sorting (e.g., 'two_handed', 'magical', 'heavy_armor')")]
    public string[] tags;

    /// <summary>
    /// Validation helper
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(subTypeId) || string.IsNullOrEmpty(displayName))
            return false;

        if (parentCategory == null)
            return false;

        if (isEquippable && equipmentSlot == null)
            return false;

        return true;
    }
}