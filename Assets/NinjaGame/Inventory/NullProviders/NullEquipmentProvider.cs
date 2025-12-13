using System;
using UnityEngine;

public class NullEquipmentProvider : MonoBehaviour, IEquipmentProvider
{
    public string GetEquippedItem(EquipmentSlotType slot) => null;
    public bool EquipItem(string itemId, EquipmentSlotType slot) => false;
    public bool UnequipItem(EquipmentSlotType slot) => false;
    public bool IsSlotOccupied(EquipmentSlotType slot) => false;

    public event Action<EquipmentSlotType, string> OnItemEquipped;
    public event Action<EquipmentSlotType> OnItemUnequipped;
}