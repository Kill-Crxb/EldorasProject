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

    [Header("Classification")]
    [Tooltip("Item category (Weapon, Armor, Consumable, etc.)")]
    public ItemCategory category;

    [Tooltip("Item subtype (Katana, Helmet, Potion, etc.)")]
    public ItemSubType subType;

    [Tooltip("Base type for moveset inheritance (weapons only, optional)")]
    public ItemBaseType baseType;

    [Header("Grid Properties")]
    [Tooltip("Width in inventory grid cells")]
    public int gridWidth = 1;

    [Tooltip("Height in inventory grid cells")]
    public int gridHeight = 1;

    [Header("Weapon Data")]
    [Tooltip("Dice damage and combat stats for this weapon (null for non-weapons)")]
    public WeaponData weaponData;

    [Header("Stats & Resources")]
    [Tooltip("Stat modifications (armor, attack power, etc.)")]
    public ItemStatModifier[] statModifiers;

    [Tooltip("Resource modifications (max health, mana regen, etc.)")]
    public ItemResourceModifier[] resourceModifiers;

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

    [Header("Upgrade System")]
    [Tooltip("Upgrade slots for this item (0-N slots, each with specific type)")]
    public ItemUpgradeSlot[] upgradeSlots;

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
    /// BRIDGE: Map new EquipmentSlotDefinition to old EquipmentSlot enum.
    /// Allows old inventory code to work during migration.
    /// </summary>
    public EquipmentSlot equipmentSlot
    {
        get
        {
            if (subType == null || !subType.isEquippable) return EquipmentSlot.Weapon1;
            if (subType.equipmentSlot == null) return EquipmentSlot.Weapon1;

            string slotId = subType.equipmentSlot.slotId.ToLower();

            switch (slotId)
            {
                case "head":
                case "helmet": return EquipmentSlot.Helmet;
                case "chest":
                case "armor":
                case "body": return EquipmentSlot.Armor;
                case "hands":
                case "gloves": return EquipmentSlot.Gloves;
                case "feet":
                case "boots": return EquipmentSlot.Boots;
                case "weapon1":
                case "mainhand":
                case "main_hand": return EquipmentSlot.Weapon1;
                case "weapon2":
                case "offhand":
                case "off_hand": return EquipmentSlot.Weapon2;
                case "backpack": return EquipmentSlot.Backpack;
                case "rig": return EquipmentSlot.Rig;
                case "belt": return EquipmentSlot.Belt;
                case "pouch": return EquipmentSlot.Pouch;
                default:
                    Debug.LogWarning($"[ItemDefinition] Unknown equipment slot ID: {slotId}, defaulting to Weapon1");
                    return EquipmentSlot.Weapon1;
            }
        }
    }

    /// <summary>BRIDGE: Archetype is deprecated — use tags or category instead.</summary>
    public ItemArchetype archetype => ItemArchetype.None;

    // ========================================
    // Helpers
    // ========================================

    public AbilityDefinition[] GetAllAbilities()
    {
        var allAbilities = new List<AbilityDefinition>();

        if (baseType != null)
        {
            if (customCombo != null && customCombo.Length > 0)
                allAbilities.AddRange(customCombo);
            else if (baseType.defaultCombo != null)
                allAbilities.AddRange(baseType.defaultCombo);

            if (customDefense != null)
                allAbilities.Add(customDefense);
            else if (baseType.defaultDefense != null)
                allAbilities.Add(baseType.defaultDefense);

            if (baseType.defaultSpecials != null)
                allAbilities.AddRange(baseType.defaultSpecials);
        }

        if (customSpecials != null)
            allAbilities.AddRange(customSpecials);

        if (grantedAbilities != null)
        {
            foreach (var granted in grantedAbilities)
            {
                if (granted.ability != null)
                    allAbilities.Add(granted.ability);
            }
        }

        return allAbilities.ToArray();
    }

    public AbilityDefinition[] GetComboAbilities()
    {
        if (customCombo != null && customCombo.Length > 0)
            return customCombo;

        if (baseType != null && baseType.defaultCombo != null)
            return baseType.defaultCombo;

        return new AbilityDefinition[0];
    }

    public AbilityDefinition GetDefenseAbility()
    {
        if (customDefense != null)
            return customDefense;

        if (baseType != null)
            return baseType.defaultDefense;

        return null;
    }

    public bool HasTag(string tag) => tags != null && tags.Contains(tag);

    public bool IsValid()
    {
        if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(displayName))
            return false;

        if (category == null || subType == null)
            return false;

        return true;
    }
}