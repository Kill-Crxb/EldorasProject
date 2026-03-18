using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ItemDefinition - Data-driven item template (ScriptableObject)
/// 
/// Architecture:
/// - Replaces hardcoded enums with ScriptableObject references
/// - Supports formula-based stat scaling via StatSystem
/// - Integrates with ResourceSystem for max/regen bonuses
/// - Phase 3: Grants abilities via RuntimeAbilityManager
/// 
/// Backward Compatibility:
/// - Provides bridge properties (equipmentSlot, baseStats, tierScaling)
/// - Allows old inventory code to work during Phase 3 migration
/// - New items should use: category, subType, statModifiers, upgradeSlots
/// - Legacy items can use: baseStats, tierScaling until migrated
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    // ========================================
    // Basic Info
    // ========================================

    [Header("Basic Info")]
    [Tooltip("Unique identifier (e.g., 'steel_katana', 'iron_circlet')")]
    public string itemId;

    [Tooltip("Display name shown in UI")]
    public string displayName;

    [TextArea(2, 4)]
    [Tooltip("Item description for tooltips")]
    public string description;

    [Tooltip("Item icon for UI")]
    public Sprite icon;

    [Tooltip("Prefab for dropped/world items")]
    public GameObject worldPrefab;

    [Tooltip("Prefab instantiated in character hand/socket when equipped (3D model)")]
    public GameObject equippedPrefab;

    // ========================================
    // Classification (Data-Driven)
    // ========================================

    [Header("Classification")]
    [Tooltip("Item category (Weapon, Armor, Consumable, etc.)")]
    public ItemCategory category;

    [Tooltip("Item subtype (Katana, Helmet, Potion, etc.)")]
    public ItemSubType subType;

    [Tooltip("Base type for moveset inheritance (weapons only, optional)")]
    public ItemBaseType baseType;

    // ========================================
    // Grid Properties
    // ========================================

    [Header("Grid Properties")]
    [Tooltip("Width in inventory grid cells")]
    public int gridWidth = 1;

    [Tooltip("Height in inventory grid cells")]
    public int gridHeight = 1;

    // ========================================
    // Stats & Resources (StatSystem Integration)
    // ========================================

    [Header("Stats & Resources")]
    [Tooltip("Stat modifications (armor, attack power, etc.)")]
    public ItemStatModifier[] statModifiers;

    [Tooltip("Resource modifications (max health, mana regen, etc.)")]
    public ItemResourceModifier[] resourceModifiers;

    // ========================================
    // Abilities (Phase 3)
    // ========================================

    [Header("Abilities")]
    [Tooltip("Abilities granted by this item (equipment/consumables)")]
    public GrantedAbilityData[] grantedAbilities;

    [Header("Moveset Override (Weapons)")]
    [Tooltip("Override base type's default combo (null = use base type)")]
    public AbilityDefinition[] customCombo;

    [Tooltip("Override base type's default defense (null = use base type)")]
    public AbilityDefinition customDefense;

    [Tooltip("Additional special abilities unique to this weapon")]
    public AbilityDefinition[] customSpecials;

    // ========================================
    // Upgrade System
    // ========================================

    [Header("Upgrade System")]
    [Tooltip("Upgrade slots for this item (0-N slots, each with specific type)")]
    public ItemUpgradeSlot[] upgradeSlots;

    // ========================================
    // Advanced Properties
    // ========================================

    [Header("Advanced")]
    [Tooltip("Item rarity level")]
    public ItemRarity rarity = ItemRarity.Common;

    [Tooltip("Maximum stack size (1 = non-stackable)")]
    public int maxStackSize = 1;

    [Tooltip("Tags for filtering/sorting (e.g., 'legendary_set', 'fire_damage')")]
    public List<string> tags = new List<string>();

    [Tooltip("Value in currency (for buying/selling)")]
    public int baseValue;

    [Tooltip("Can this item be dropped on death?")]
    public bool dropsOnDeath = true;

    [Tooltip("Can this item be traded with other players?")]
    public bool isTradeable = true;

    [Tooltip("Can this item be destroyed/deleted?")]
    public bool isDestructible = true;

    // ========================================
    // Backward Compatibility (Phase 3 Migration)
    // ========================================


    /// <summary>
    /// BRIDGE: Map new EquipmentSlotDefinition to old EquipmentSlot enum
    /// Allows old inventory code to work during migration
    /// </summary>
    public EquipmentSlot equipmentSlot
    {
        get
        {
            // Guard clause - not equippable
            if (subType == null || !subType.isEquippable) return EquipmentSlot.Weapon1;
            if (subType.equipmentSlot == null) return EquipmentSlot.Weapon1;

            // Map from new EquipmentSlotDefinition to old enum
            string slotId = subType.equipmentSlot.slotId.ToLower();

            switch (slotId)
            {
                case "head":
                case "helmet":
                    return EquipmentSlot.Helmet;

                case "chest":
                case "armor":
                case "body":
                    return EquipmentSlot.Armor;

                case "hands":
                case "gloves":
                    return EquipmentSlot.Gloves;

                case "feet":
                case "boots":
                    return EquipmentSlot.Boots;

                case "weapon1":
                case "mainhand":
                case "main_hand":
                    return EquipmentSlot.Weapon1;

                case "weapon2":
                case "offhand":
                case "off_hand":
                    return EquipmentSlot.Weapon2;

                case "backpack":
                    return EquipmentSlot.Backpack;

                case "rig":
                    return EquipmentSlot.Rig;

                case "belt":
                    return EquipmentSlot.Belt;

                case "pouch":
                    return EquipmentSlot.Pouch;

                default:
                    Debug.LogWarning($"[ItemDefinition] Unknown equipment slot ID: {slotId}, defaulting to Weapon1");
                    return EquipmentSlot.Weapon1;
            }
        }
    }

    /// <summary>
    /// BRIDGE: Archetype is deprecated, always returns None
    /// Use tags or category system instead
    /// </summary>
    public ItemArchetype archetype => ItemArchetype.None;

    // ========================================
    // Helpers
    // ========================================

    /// <summary>
    /// Get all abilities this item grants (base type + custom + granted)
    /// </summary>
    public AbilityDefinition[] GetAllAbilities()
    {
        List<AbilityDefinition> allAbilities = new List<AbilityDefinition>();

        // Add base type abilities if no custom override
        if (baseType != null)
        {
            // Use custom combo if defined, else base type default
            if (customCombo != null && customCombo.Length > 0)
            {
                allAbilities.AddRange(customCombo);
            }
            else if (baseType.defaultCombo != null)
            {
                allAbilities.AddRange(baseType.defaultCombo);
            }

            // Use custom defense if defined, else base type default
            if (customDefense != null)
            {
                allAbilities.Add(customDefense);
            }
            else if (baseType.defaultDefense != null)
            {
                allAbilities.Add(baseType.defaultDefense);
            }

            // Add base type specials
            if (baseType.defaultSpecials != null)
            {
                allAbilities.AddRange(baseType.defaultSpecials);
            }
        }

        // Add custom specials
        if (customSpecials != null)
        {
            allAbilities.AddRange(customSpecials);
        }

        // Add granted abilities
        if (grantedAbilities != null)
        {
            foreach (var granted in grantedAbilities)
            {
                if (granted.ability != null)
                {
                    allAbilities.Add(granted.ability);
                }
            }
        }

        return allAbilities.ToArray();
    }

    /// <summary>
    /// Get combo abilities (base type or custom)
    /// </summary>
    public AbilityDefinition[] GetComboAbilities()
    {
        // Use custom combo if defined
        if (customCombo != null && customCombo.Length > 0)
            return customCombo;

        // Fall back to base type
        if (baseType != null && baseType.defaultCombo != null)
            return baseType.defaultCombo;

        return new AbilityDefinition[0];
    }

    /// <summary>
    /// Get defense ability (custom or base type)
    /// </summary>
    public AbilityDefinition GetDefenseAbility()
    {
        // Use custom defense if defined
        if (customDefense != null)
            return customDefense;

        // Fall back to base type
        if (baseType != null)
            return baseType.defaultDefense;

        return null;
    }

    /// <summary>
    /// Check if this item has a specific tag
    /// </summary>
    public bool HasTag(string tag)
    {
        return tags != null && tags.Contains(tag);
    }

    /// <summary>
    /// Validation helper
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(displayName))
            return false;

        if (category == null || subType == null)
            return false;

        return true;
    }
}