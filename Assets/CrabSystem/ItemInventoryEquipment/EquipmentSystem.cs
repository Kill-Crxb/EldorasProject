using UnityEngine;
using System;
using System.Collections.Generic;

public class EquipmentSystem : MonoBehaviour, IBrainModule, ISaveable
{
    [Header("Equipment Storage")]
    [Tooltip("Currently equipped items (visible in inspector for debugging)")]
    [SerializeField] private List<EquippedSlotData> equippedItems = new List<EquippedSlotData>();

    [Header("Slot Definitions")]
    [Tooltip("All EquipmentSlotDefinition SOs this entity uses. Required for visual restore on login. " +
             "Assign the same SOs the equipment sockets are tagged with.")]
    [SerializeField] private List<EquipmentSlotDefinition> slotDefinitions = new List<EquipmentSlotDefinition>();

    [Header("NPC Configuration")]
    [SerializeField] private bool hasNaturalWeapon = false;
    [SerializeField] private string naturalWeaponItemId;
    [SerializeField] private EquipmentSlotDefinition naturalWeaponSlot;

    private Dictionary<string, ItemInstance> equipment = new Dictionary<string, ItemInstance>();

    // Built from slotDefinitions on Initialize — slotId → SO reference
    private Dictionary<string, EquipmentSlotDefinition> slotLookup = new Dictionary<string, EquipmentSlotDefinition>();

    private ControllerBrain brain;
    public ControllerBrain Brain => brain;
    private IStatProvider statSystem;
    private ResourceSystem resourceSystem;
    private bool isInitialized = false;

    public event Action<EquipmentSlotDefinition, ItemInstance> OnEquipmentChanged;
    public event Action<EquipmentSlotDefinition, ItemInstance> OnEquipmentVisual;

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

        BuildSlotLookup();
        LoadSerializedData();

        if (hasNaturalWeapon && !string.IsNullOrEmpty(naturalWeaponItemId))
            EquipNaturalWeapon();

        isInitialized = true;
    }

    // LateInitialize runs in the brain's Awake, before the async save load, so the equipment is
    // still empty here. The brain's OnLoaded comes after this character's data is restored.
    public void LateInitialize()
    {
        brain.OnLoaded += HandleLoaded;
    }

    public void UpdateModule() { }

    public void Shutdown()
    {
        if (brain != null) brain.OnLoaded -= HandleLoaded;
    }

    private void HandleLoaded()
    {
        brain.OnLoaded -= HandleLoaded;

        try
        {
            BroadcastVisuals();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EquipmentSystem] Exception in BroadcastVisuals: {ex.Message}\n{ex.StackTrace}");
        }
    }

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

            equipment[entry.slotId] = item;
            ApplyItemStats(item);
        }

        UpdateSerializedData();
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

        if (!slot.CanEquip(item.Definition)) return false;

        string slotId = slot.slotId;

        if (equipment.ContainsKey(slotId))
            UnequipItem(slot);

        equipment[slotId] = item;
        ApplyItemStats(item);
        OnEquipmentChanged?.Invoke(slot, item);
        OnEquipmentVisual?.Invoke(slot, item);
        UpdateSerializedData();

        return true;
    }

    /// <summary>
    /// Equips an item that is currently in this entity's inventory, taking it out of the bag
    /// and putting anything already in the slot back. Use this for anything player-driven —
    /// plain EquipItem leaves the item in the inventory as well, which duplicates it.
    /// </summary>
    public bool EquipFromInventory(ItemInstance item, EquipmentSlotDefinition slot)
    {
        if (item == null || slot == null) return false;
        if (!slot.CanEquip(item.Definition)) return false;

        var inventory = brain != null ? brain.GetModule<InventorySystem>() : null;
        if (inventory == null) return EquipItem(item, slot);

        // Take the incoming item out first so the outgoing one has room to land.
        if (!inventory.RemoveItem(item.instanceId)) return false;

        ItemInstance displaced = GetEquippedItem(slot);

        if (displaced != null && !inventory.AddItem(displaced))
        {
            Debug.LogWarning($"[EquipmentSystem] No room to unequip {displaced.Definition?.displayName}");
            inventory.AddItem(item);
            return false;
        }

        if (displaced != null) UnequipItem(slot);

        if (EquipItem(item, slot)) return true;

        // Should not happen — CanEquip passed above. Put everything back rather than lose it.
        inventory.AddItem(item);
        if (displaced != null)
        {
            inventory.RemoveItem(displaced.instanceId);
            EquipItem(displaced, slot);
        }
        return false;
    }

    /// <summary>Empties a slot. The item goes nowhere — callers that want it back use UnequipItemToInventory.</summary>
    public bool UnequipItem(EquipmentSlotDefinition slot)
    {
        if (slot == null) return false;

        ItemInstance item = GetEquippedItem(slot);
        if (item == null) return false;

        RemoveItemStats(item);
        equipment[slot.slotId] = null;
        OnEquipmentChanged?.Invoke(slot, null);
        OnEquipmentVisual?.Invoke(slot, null);
        UpdateSerializedData();

        return true;
    }

    /// <summary>Moves the equipped item back into the bag. Stays equipped if there is no room.</summary>
    public bool UnequipItemToInventory(EquipmentSlotDefinition slot)
    {
        if (slot == null) return false;

        ItemInstance item = GetEquippedItem(slot);
        if (item == null) return false;

        var inventory = brain != null ? brain.GetModule<InventorySystem>() : null;

        if (inventory != null && !inventory.AddItem(item))
        {
            Debug.LogWarning($"[EquipmentSystem] No room in the bag for {item.Definition?.displayName}");
            return false;
        }

        return UnequipItem(slot);
    }

    /// <summary>Resolve a slot id ("helmet", "mainwep") to its definition.</summary>
    public EquipmentSlotDefinition GetSlotDefinition(string slotId)
    {
        if (string.IsNullOrEmpty(slotId)) return null;
        return slotLookup.GetValueOrDefault(slotId);
    }

    /// <summary>Slot ids this entity has definitions for. Useful when a socket id does not match.</summary>
    public IEnumerable<string> GetSlotIds() => slotLookup.Keys;

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

    /// <summary>
    /// Returns the DiceProfile for the item in the given slot, or null if the slot
    /// is empty or the equipped item has no DiceProfile assigned.
    /// Used by DamageEffect to resolve dice damage at hit time.
    /// </summary>
    public DiceProfile GetEquippedWeapon(string slotId)
    {
        var item = GetEquippedItem(slotId);
        return item?.Definition?.weaponData;
    }

    public bool IsSlotOccupied(EquipmentSlotDefinition slot)
    {
        if (slot == null) return false;
        return equipment.ContainsKey(slot.slotId) && equipment[slot.slotId] != null;
    }

    public Dictionary<string, ItemInstance> GetAllEquippedItems()
        => new Dictionary<string, ItemInstance>(equipment);

    #endregion

    #region Visual Broadcast

    /// <summary>
    /// Fires OnEquipmentVisual for every occupied slot.
    /// Called from LateInitialize so all brain modules are subscribed first.
    /// Slots with no matching SO in slotLookup are skipped with a warning.
    /// </summary>
    private void BroadcastVisuals()
    {
        foreach (var kvp in equipment)
        {
            if (kvp.Value == null) continue;

            if (!slotLookup.TryGetValue(kvp.Key, out EquipmentSlotDefinition slotDef))
            {
                Debug.LogWarning($"[EquipmentSystem] No slot definition found for '{kvp.Key}' — visual not broadcast. Add the SO to the Slot Definitions list.");
                continue;
            }

            OnEquipmentVisual?.Invoke(slotDef, kvp.Value);
        }
    }

    /// <summary>
    /// Builds a slotId → EquipmentSlotDefinition lookup from the inspector list.
    /// Called once on Initialize.
    /// </summary>
    private void BuildSlotLookup()
    {
        slotLookup.Clear();

        foreach (var slotDef in slotDefinitions)
        {
            if (slotDef == null || string.IsNullOrEmpty(slotDef.slotId)) continue;
            slotLookup[slotDef.slotId] = slotDef;
        }
    }

    #endregion

    #region Stat Application

    // Item stat modifiers are not applied in this pass. The stat store holds base
    // values only; item.* stats also need lifting out of the entity schema before
    // equipment can feed them. Kept as seams so the call sites stay correct.
    private void ApplyItemStats(ItemInstance item) { }

    private void RemoveItemStats(ItemInstance item) { }

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

}
