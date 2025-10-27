// InventoryEnums.cs - Centralized enum definitions for inventory system
using UnityEngine;

/// <summary>
/// Item rarity levels affecting visual appearance and gameplay value
/// </summary>
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Basic item type categories for organization and filtering
/// </summary>
public enum ItemType
{
    Backpack,
    Rig,
    Belt,
    Pouch,
    Helmet,
    Armor,
    Gloves,
    Boots,
    Weapon1,
    Weapon2
}

/// <summary>
/// Equipment slot types for paperdoll/character equipment
/// </summary>
public enum EquipmentSlot
{
    Backpack,
    Rig,
    Belt,
    Pouch,
    Helmet,
    Armor,
    Gloves,
    Boots,
    Weapon1,
    Weapon2
}



/// <summary>
/// Weapon categories for combat system integration
/// </summary>
public enum WeaponType
{
    None,
    Sword,
    Axe,
    Mace,
    Dagger,
    Bow,
    Staff,
    Shield,
    TwoHanded,
    Unarmed
}

/// <summary>
/// Armor categories for defense calculations
/// </summary>
public enum ArmorType
{
    None,
    Light,
    Medium,
    Heavy,
    Cloth,
    Leather,
    Mail,
    Plate
}