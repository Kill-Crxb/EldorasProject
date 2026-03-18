using UnityEngine;

/// <summary>
/// EquipmentSlotConfig - Configuration for a single equipment slot
/// 
/// 100% DATA-DRIVEN - No enums!
/// Uses EquipmentSlotDefinition ScriptableObject for all slot identity.
/// 
/// Example:
/// - Slot Definition: Helmet_EquipmentSlotDef SO
/// - Display Name: "Head"
/// - Background Icon: Helmet silhouette
/// - Allowed Categories: Armor
/// - Allowed Subtypes: Helmet, Circlet
/// 
/// Created: February 18, 2026
/// </summary>
[System.Serializable]
public class EquipmentSlotConfig
{
    [Header("Slot Identity")]
    [Tooltip("Equipment slot definition (data-driven ScriptableObject)")]
    public EquipmentSlotDefinition slotDefinition;

    [Tooltip("Display name shown in UI (optional override, uses slot definition if empty)")]
    public string displayNameOverride;

    [Header("Visual")]
    [Tooltip("Icon shown when slot is empty (helmet icon, weapon icon, etc.)")]
    public Sprite emptySlotIcon;

    [Tooltip("Background sprite for this slot (optional)")]
    public Sprite slotBackground;

    [Header("Item Restrictions")]
    [Tooltip("Allowed item categories (leave empty to use slot definition's restrictions)")]
    public ItemCategory[] allowedCategories;

    [Tooltip("Allowed item subtypes (leave empty to allow all from categories)")]
    public ItemSubType[] allowedSubTypes;

    [Header("Layout")]
    [Tooltip("Order in panel (lower = higher in list)")]
    public int displayOrder;

    [Tooltip("Is this a weapon slot? (uses 1x2 layout)")]
    public bool isWeaponSlot = false;

    /// <summary>
    /// Get display name (uses override if set, otherwise slot definition)
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(displayNameOverride))
                return displayNameOverride;

            if (slotDefinition != null)
                return slotDefinition.displayName;

            return "Unknown Slot";
        }
    }

    /// <summary>
    /// Get slot ID for dictionary/event keys
    /// </summary>
    public string SlotId
    {
        get
        {
            if (slotDefinition == null) return "";
            return slotDefinition.slotId;
        }
    }

    /// <summary>
    /// Check if an item can be equipped in this slot
    /// </summary>
    public bool CanEquipItem(ItemInstance item)
    {
        // Guard clause: null item
        if (item == null) return false;

        // Guard clause: no definition
        if (item.Definition == null) return false;

        // Guard clause: no slot definition
        if (slotDefinition == null) return false;

        // Primary check: Use slot definition's validation if available
        if (!slotDefinition.CanEquip(item.Definition))
        {
            return false; // Failed slot definition check
        }

        // Secondary check: Local category restrictions (if configured)
        if (allowedCategories != null && allowedCategories.Length > 0)
        {
            bool categoryMatch = false;
            foreach (var category in allowedCategories)
            {
                if (item.Definition.category == category)
                {
                    categoryMatch = true;
                    break;
                }
            }
            if (!categoryMatch) return false;
        }

        // Tertiary check: Local subtype restrictions (if configured)
        if (allowedSubTypes != null && allowedSubTypes.Length > 0)
        {
            bool subtypeMatch = false;
            foreach (var subtype in allowedSubTypes)
            {
                if (item.Definition.subType == subtype)
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
    /// Validate configuration
    /// </summary>
    public bool IsValid()
    {
        if (slotDefinition == null)
        {
            Debug.LogWarning($"[EquipmentSlotConfig] No slot definition assigned!");
            return false;
        }

        if (!slotDefinition.IsValid())
        {
            Debug.LogWarning($"[EquipmentSlotConfig] Slot definition {slotDefinition.name} is invalid!");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Get user-friendly name for inspector display
    /// </summary>
    public override string ToString()
    {
        if (slotDefinition != null)
            return $"{DisplayName} ({slotDefinition.slotId})";

        return "Unconfigured Slot";
    }
}