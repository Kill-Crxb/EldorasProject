using System;

/// <summary>
/// IEquipmentProvider - Legacy interface for backward compatibility
/// 
/// NOTE: This is a LEGACY interface kept for compatibility.
/// New code should use EquipmentSystem directly.
/// 
/// The EquipmentSlotType enum here is just a bridge to the new EquipmentSlot enum.
/// Eventually this entire file can be deleted once all systems are migrated.
/// 
/// Created: February 18, 2026 (Compatibility shim)
/// </summary>

/// <summary>
/// Legacy equipment slot enum - maps 1:1 to EquipmentSlot
/// </summary>
public enum EquipmentSlotType 
{ 
    Backpack = 0,
    Rig = 1,
    Belt = 2,
    Pouch = 3,
    Helmet = 4,
    Armor = 5,
    Gloves = 6,
    Boots = 7,
    Weapon1 = 8,
    Weapon2 = 9
}

/// <summary>
/// Legacy equipment provider interface
/// Use EquipmentSystem directly for new code
/// </summary>
public interface IEquipmentProvider
{
    string GetEquippedItem(EquipmentSlotType slot);
    bool EquipItem(string itemId, EquipmentSlotType slot);
    bool UnequipItem(EquipmentSlotType slot);
    bool IsSlotOccupied(EquipmentSlotType slot);

    event Action<EquipmentSlotType, string> OnItemEquipped;
    event Action<EquipmentSlotType> OnItemUnequipped;
}
