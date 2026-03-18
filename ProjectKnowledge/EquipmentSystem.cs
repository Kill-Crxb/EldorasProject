using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// EquipmentSystem - Fully data-driven equipment management
///
/// NO ENUMS! Uses EquipmentSlotDefinition ScriptableObjects instead.
/// Equipment slots are identified by string slotId (e.g., "helmet", "ring", "mainwep")
///
/// Architecture:
/// - Dictionary instead of array (keyed by slotId)
/// - Events pass EquipmentSlotDefinition instead of enum
/// - Stat application works the same
/// - Fully flexible — add new slots without code changes
///
/// Created: February 18, 2026 (Refactored to eliminate enum dependency)
/// Updated: Phase 3 — ISaveable added
/// </summary>
public class EquipmentSystem : MonoBehaviour, IBrainModule, ISaveable
{
    [Header("Equipment Storage")]
    [Tooltip("Currently equipped items (visible in inspector for debugging)")]
    [SerializeField] private List<EquippedSlotData> equippedItems = new List<EquippedSlotData>();

    [Header("NPC Configuration")]
    [SerializeField] private bool hasNaturalWeapon = false;
    [SerializeField] private string naturalWeaponItemId;
    [SerializeField] private EquipmentSlotDefinition naturalWeaponSlot;

    [Header("Debug")]
    [SerializeField] private bool debugEquipment = false;

    // Runtime storage (fast dictionary lookup)
    private Dictionary<string, ItemInstance> equipment = new Dictionary<string, ItemInstance>();

    private ControllerBrain brain;
    public ControllerBrain Brain => brain;
    private StatSystem statSystem;
    private ResourceSystem resourceSystem;
    private bool isInitialized = false;

    public event Action<EquipmentSlotDefinition, ItemInstance> OnEquipmentChanged;

    #region IBrainModule

    public bool IsEnabled
    {
        get => enabled;
        set => enabled = value;
    }

    public void Initialize(ControllerBrain brain)
    {
        if (isInitialized) return;

        this.brain = brain;
        statSystem = brain.GetModule<StatSystem>();
        resourceSystem = brain.GetModule<ResourceSystem>();

        LoadSerializedData();

        if (hasNaturalWeapon && !string.IsNullOrEmpty(naturalWeaponItemId))
            EquipNaturalWeapon();

        isInitialized = true;

        if (debugEquipment)
            Debug.Log($"[EquipmentSystem] Initialized for {brain.name}");
    }

    public void LateInitialize() { }
    public void UpdateModule() { }
    public void Shutdown() { }

    #endregion

    #region ISaveable

    public string GetSaveId() => "equipment";
    public int GetSaveVersion() => 1;

    public string GetSaveData()
    {
        var saveData = new EquipmentSaveData
        {
            version = GetSaveVersion(),
            slots = new List<EquipmentSlotEntry>()
        };

        foreach (var kvp in equipment)
        {
            if (kvp.Value == null) continue;

            saveData.slots.Add(new EquipmentSlotEntry
            {
                slotId = kvp.Key,
                instanceId = kvp.Value.instanceId,
                definitionId = kvp.Value.definitionId,
                rarity = (int)kvp.Value.currentTier,
                durability = kvp.Value.durability
            });
        }

        if (debugEquipment)
            Debug.Log($"[EquipmentSystem] GetSaveData — {saveData.slots.Count} slots serialised");

        return JsonUtility.ToJson(saveData);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var saveData = JsonUtility.FromJson<EquipmentSaveData>(json);
        if (saveData?.slots == null) return;

        // Clear current equipment without firing events (load is a quiet restore)
        foreach (var kvp in equipment)
        {
            if (kvp.Value != null)
                RemoveItemStats(kvp.Value);
        }
        equipment.Clear();

        foreach (var entry in saveData.slots)
        {
            if (string.IsNullOrEmpty(entry.slotId) || string.IsNullOrEmpty(entry.definitionId))
                continue;

            var item = new ItemInstance(entry.definitionId, (ItemRarity)entry.rarity)
            {
                instanceId = entry.instanceId,
                durability = entry.durability
            };

            if (item.Definition == null)
            {
                if (debugEquipment)
                    Debug.LogWarning($"[EquipmentSystem] Skipping unknown item definition: {entry.definitionId}");
                continue;
            }

            equipment[entry.slotId] = item;
            ApplyItemStats(item);
        }

        UpdateSerializedData();

        if (debugEquipment)
            Debug.Log($"[EquipmentSystem] LoadSaveData — {saveData.slots.Count} slots restored for {brain.name}");
    }

    // ── Save Data Structures ──────────────────────────────────────────────

    [Serializable]
    private class EquipmentSaveData
    {
        public int version;
        public List<EquipmentSlotEntry> slots;
    }

    [Serializable]
    private class EquipmentSlotEntry
    {
        public string slotId;
        public string instanceId;
        public string definitionId;
        public int rarity;
        public float durability;
    }

    #endregion

    #region Equipment Operations

    public bool EquipItem(ItemInstance item, EquipmentSlotDefinition slot)
    {
        if (item == null)
        {
            Debug.LogWarning("[EquipmentSystem] Cannot equip null item!");
            return false;
        }

        if (item.Definition == null)
        {
            Debug.LogWarning("[EquipmentSystem] Item has no definition!");
            return false;
        }

        if (!slot.CanEquip(item.Definition))
        {
            if (debugEquipment)
                Debug.Log($"[EquipmentSystem] {item.Definition.displayName} cannot be equipped to {slot.displayName}");
            return false;
        }

        string slotId = slot.slotId;

        if (equipment.ContainsKey(slotId))
            UnequipItem(slot);

        equipment[slotId] = item;
        ApplyItemStats(item);
        OnEquipmentChanged?.Invoke(slot, item);
        UpdateSerializedData();

        if (debugEquipment)
            Debug.Log($"[EquipmentSystem] Equipped {item.Definition.displayName} to {slot.displayName}");

        return true;
    }

    public bool UnequipItem(EquipmentSlotDefinition slot)
    {
        if (slot == null) return false;

        string slotId = slot.slotId;

        if (!equipment.ContainsKey(slotId) || equipment[slotId] == null)
            return false;

        ItemInstance item = equipment[slotId];
        RemoveItemStats(item);
        equipment[slotId] = null;
        OnEquipmentChanged?.Invoke(slot, null);
        UpdateSerializedData();

        if (debugEquipment)
            Debug.Log($"[EquipmentSystem] Unequipped {item.Definition.displayName} from {slot.displayName}");

        return true;
    }

    public bool UnequipItemToInventory(EquipmentSlotDefinition slot)
    {
        if (slot == null) return false;

        string slotId = slot.slotId;

        if (!equipment.ContainsKey(slotId) || equipment[slotId] == null)
            return false;

        ItemInstance item = equipment[slotId];

        var inventorySystem = brain.GetModule<InventorySystem>();
        if (inventorySystem != null)
        {
            if (!inventorySystem.AddItem(item))
            {
                if (debugEquipment)
                    Debug.LogWarning("[EquipmentSystem] Inventory full, cannot unequip!");
                return false;
            }
        }

        RemoveItemStats(item);
        equipment[slotId] = null;
        OnEquipmentChanged?.Invoke(slot, null);
        UpdateSerializedData();

        if (debugEquipment)
            Debug.Log($"[EquipmentSystem] Unequipped {item.Definition.displayName} to inventory");

        return true;
    }

    public ItemInstance GetEquippedItem(EquipmentSlotDefinition slot)
    {
        if (slot == null) return null;
        equipment.TryGetValue(slot.slotId, out ItemInstance item);
        return item;
    }

    public ItemInstance GetEquippedItem(string slotId)
    {
        if (string.IsNullOrEmpty(slotId)) return null;
        equipment.TryGetValue(slotId, out ItemInstance item);
        return item;
    }

    public bool IsSlotOccupied(EquipmentSlotDefinition slot)
    {
        if (slot == null) return false;
        return equipment.ContainsKey(slot.slotId) && equipment[slot.slotId] != null;
    }

    public Dictionary<string, ItemInstance> GetAllEquippedItems()
        => new Dictionary<string, ItemInstance>(equipment);

    #endregion

    #region Stat Application

    private void ApplyItemStats(ItemInstance item)
    {
        if (item?.calculatedModifiers == null) return;
        if (statSystem == null) return;

        foreach (var modifier in item.calculatedModifiers)
            statSystem.Engine.AddFlatModifier(modifier.statName, item.instanceId, modifier.value);

        if (debugEquipment)
            Debug.Log($"[EquipmentSystem] Applied {item.calculatedModifiers.Length} stat modifiers");
    }

    private void RemoveItemStats(ItemInstance item)
    {
        if (item == null) return;
        if (statSystem == null) return;

        statSystem.Engine.RemoveAllModifiersFromSource(item.instanceId);

        if (debugEquipment)
            Debug.Log($"[EquipmentSystem] Removed stat modifiers from {item.Definition.displayName}");
    }

    #endregion

    #region Natural Weapons (NPCs)

    private void EquipNaturalWeapon()
    {
        var weaponInstance = new ItemInstance(naturalWeaponItemId, ItemRarity.Common);

        if (weaponInstance.Definition == null)
        {
            Debug.LogError($"[EquipmentSystem] Failed to create natural weapon: {naturalWeaponItemId}");
            return;
        }

        if (naturalWeaponSlot == null)
        {
            Debug.LogError("[EquipmentSystem] No slot specified for natural weapon!");
            return;
        }

        EquipItem(weaponInstance, naturalWeaponSlot);

        if (debugEquipment)
            Debug.Log($"[EquipmentSystem] Equipped natural weapon: {weaponInstance.Definition.displayName}");
    }

    #endregion

    #region Serialization (Inspector Visibility)

    [Serializable]
    public class EquippedSlotData
    {
        public string slotId;
        public ItemInstance item;
    }

    private void LoadSerializedData()
    {
        equipment.Clear();

        foreach (var slotData in equippedItems)
        {
            if (slotData != null && !string.IsNullOrEmpty(slotData.slotId) && slotData.item != null)
                equipment[slotData.slotId] = slotData.item;
        }
    }

    private void UpdateSerializedData()
    {
        equippedItems.Clear();

        foreach (var kvp in equipment)
        {
            equippedItems.Add(new EquippedSlotData
            {
                slotId = kvp.Key,
                item = kvp.Value
            });
        }
    }

    #endregion

    #region Debug

    [ContextMenu("Debug: Print Equipment")]
    private void DebugPrintEquipment()
    {
        Debug.Log($"=== EQUIPMENT ({brain.name}) ===");
        foreach (var kvp in equipment)
            Debug.Log($"  [{kvp.Key}] {(kvp.Value != null ? kvp.Value.Definition.displayName : "(empty)")}");
    }

    #endregion
}