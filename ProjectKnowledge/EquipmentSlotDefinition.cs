using UnityEngine;

/// <summary>
/// EquipmentSlotDefinition - Defines where items can be equipped on character
/// 
/// Purpose: Replace hardcoded EquipmentSlot enum with data-driven system
/// 
/// Examples:
/// - "Head" (helmet, circlet, headband)
/// - "Weapon1" (main hand weapon)
/// - "Weapon2" (off-hand weapon/shield)
/// - "Chest" (armor, robes, shirts)
/// - "Ring1", "Ring2" (jewelry slots)
/// 
/// Usage:
/// - Create assets: Right-click → Items/Equipment Slot
/// - Configure slot properties
/// - Reference in ItemSubType for equipment binding
/// 
/// Benefits:
/// - Add new equipment slots without code changes
/// - Configure UI layout via displayOrder
/// - Support multi-slot categories (Ring1, Ring2)
/// - Visual customization per slot
/// </summary>
[CreateAssetMenu(fileName = "New Equipment Slot", menuName = "Items/Equipment Slot")]
public class EquipmentSlotDefinition : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique identifier (e.g., 'head', 'weapon1', 'chest')")]
    public string slotId;

    [Tooltip("Display name shown in UI")]
    public string displayName;

    [Header("UI Configuration")]
    [Tooltip("Order in equipment panel (lower = higher priority)")]
    public int displayOrder;

    [Tooltip("Icon representing this slot in UI")]
    public Sprite slotIcon;

    [Tooltip("Background sprite for slot (optional)")]
    public Sprite slotBackground;

    [Header("Behavior")]
    [Tooltip("Can this slot hold multiple items? (e.g., ammo pouch)")]
    public bool allowsMultipleItems = false;

    [Tooltip("Maximum items if allowsMultipleItems is true")]
    public int maxItems = 1;

    [Header("Restrictions")]
    [Tooltip("Item categories allowed in this slot")]
    public ItemCategory[] allowedCategories;

    [Tooltip("Item subtypes allowed in this slot (if empty, allows all from categories)")]
    public ItemSubType[] allowedSubTypes;

    [Header("Socket")]
    [Tooltip("Name of the bone socket to attach equipped item to (must match ModelSocketConfig, e.g. 'weapon', 'shield', 'helmet')")]
    public string socketName;

    [Header("Layout")]
    [Tooltip("Is this a weapon/tall slot? Uses 1x2 grid area instead of 1x1")]
    public bool isWeaponSlot = false;

    [Tooltip("Preserve sprite aspect ratio in slot icon. Enable for tall or wide items (weapons, 2x2 chest pieces). Disable for square slots.")]
    public bool preserveAspect = false;

    [Header("Visual Feedback")]
    [Tooltip("Highlight color when slot is valid drop target")]
    public Color validDropColor = Color.green;

    [Tooltip("Highlight color when slot is invalid drop target")]
    public Color invalidDropColor = Color.red;

    [Header("Description")]
    [TextArea(2, 3)]
    public string description;

    /// <summary>
    /// Check if an item can be equipped in this slot
    /// </summary>
    public bool CanEquip(ItemDefinition item)
    {
        // Guard clause - null check
        if (item == null || item.subType == null) return false;

        // Check if item's subtype is configured for this slot
        if (item.subType.equipmentSlot != this) return false;

        // Check category restrictions
        if (allowedCategories != null && allowedCategories.Length > 0)
        {
            bool categoryMatch = false;
            foreach (var category in allowedCategories)
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
        if (allowedSubTypes != null && allowedSubTypes.Length > 0)
        {
            bool subtypeMatch = false;
            foreach (var subtype in allowedSubTypes)
            {
                if (item.subType == subtype)
                {
                    subtypeMatch = true;
                    break;
                }
            }
            if (!subtypeMatch) return false;
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