using System;

public enum EquipmentSlotType { Head, Chest, Legs, Feet, Hands, MainHand, OffHand, Accessory1, Accessory2 }

public interface IEquipmentProvider
{
    string GetEquippedItem(EquipmentSlotType slot);
    bool EquipItem(string itemId, EquipmentSlotType slot);
    bool UnequipItem(EquipmentSlotType slot);
    bool IsSlotOccupied(EquipmentSlotType slot);

    event Action<EquipmentSlotType, string> OnItemEquipped;
    event Action<EquipmentSlotType> OnItemUnequipped;
}