using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A set of equipped items as plain data — what is worn, not who wears it or when it
/// changes. Slots are keyed by the same slotId strings EquipmentSlotDefinition uses, so
/// adding a slot is authoring, not code.
///
/// Useful for NPC loadout templates and gear sets. Live player equipment is owned by
/// EquipmentSystem, which persists itself through ISaveable.
/// </summary>
[CreateAssetMenu(fileName = "New Equipment Loadout", menuName = "Items/Equipment Loadout")]
public class EquipmentLoadout : ScriptableObject
{
    [Header("Loadout Info")]
    public string loadoutName = "Default Loadout";

    [Header("Equipment Slots")]
    public List<EquipmentSlotData> slots = new List<EquipmentSlotData>();

    [Header("Metadata")]
    public long lastModified;
    public string ownerId;

    public int EquippedCount => slots.Count;

    public void SetSlot(string slotId, string itemId, ItemRarity rarity)
    {
        if (string.IsNullOrEmpty(slotId) || string.IsNullOrEmpty(itemId)) return;

        var existing = GetSlot(slotId);

        if (existing == null)
        {
            existing = new EquipmentSlotData { slotId = slotId };
            slots.Add(existing);
        }

        existing.itemId = itemId;
        existing.rarity = rarity;
        existing.equipTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        UpdateTimestamp();
    }

    public void ClearSlot(string slotId)
    {
        slots.RemoveAll(s => s.slotId == slotId);
        UpdateTimestamp();
    }

    public EquipmentSlotData GetSlot(string slotId) => slots.Find(s => s.slotId == slotId);

    public void ClearAll()
    {
        slots.Clear();
        UpdateTimestamp();
    }

    public void UpdateTimestamp() => lastModified = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public void SaveToDisk(string path)
    {
        var wrapper = new EquipmentLoadoutWrapper { loadout = this };
        System.IO.File.WriteAllText(path, JsonUtility.ToJson(wrapper, true));
    }

    public static EquipmentLoadout LoadFromDisk(string path)
    {
        if (!System.IO.File.Exists(path)) return null;

        var wrapper = JsonUtility.FromJson<EquipmentLoadoutWrapper>(System.IO.File.ReadAllText(path));
        return wrapper.loadout;
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(ownerId))
            ownerId = $"loadout_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
    }
}

[System.Serializable]
public class EquipmentSlotData
{
    [Tooltip("Slot id, matching EquipmentSlotDefinition — 'helmet', 'mainwep', 'ring2'.")]
    public string slotId;

    [Tooltip("Item ID from ItemManager")]
    public string itemId;

    public ItemRarity rarity = ItemRarity.Common;

    [Tooltip("When this item was equipped (Unix timestamp)")]
    public long equipTimestamp;
}

[System.Serializable]
public class EquipmentLoadoutWrapper
{
    public EquipmentLoadout loadout;
}
