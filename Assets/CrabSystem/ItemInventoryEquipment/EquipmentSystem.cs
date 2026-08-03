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
             "Assign the same SOs used in EquipmentWindow's slot configs.")]
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

        BuildSlotLookup();
        LoadSerializedData();

        if (hasNaturalWeapon && !string.IsNullOrEmpty(naturalWeaponItemId))
            EquipNaturalWeapon();

        isInitialized = true;

       
    }

    /// <summary>
    /// Subscribe to OnLoadCompleted here rather than broadcasting immediately.
    /// LateInitialize fires synchronously during ControllerBrain.Awake — before the
    /// async save load runs — so the equipment dictionary is still empty at that point.
    /// OnLoadCompleted fires after all ISaveable modules have finished loading their data,
    /// which is the correct moment to rebuild visuals.
    /// </summary>
    public void LateInitialize()
    {
        GameEvents.OnLoadCompleted += HandleLoadCompleted;
    }

    public void UpdateModule() { }

    public void Shutdown()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;
    }

    private void HandleLoadCompleted()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

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

        if (!slot.CanEquip(item.Definition))
        {

                Debug.Log($"[EquipmentSystem] {item.Definition.displayName} cannot be equipped to {slot.displayName}");
            return false;
        }

        string slotId = slot.slotId;

        if (equipment.ContainsKey(slotId))
            UnequipItem(slot);

        equipment[slotId] = item;
        ApplyItemStats(item);
        OnEquipmentChanged?.Invoke(slot, item);
        GameEvents.ItemEquipped(slot, item);
        UpdateSerializedData();

        
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
        GameEvents.ItemEquipped(slot, null);
        UpdateSerializedData();

        
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
               
                    Debug.LogWarning("[EquipmentSystem] Inventory full, cannot unequip!");
                return false;
            }
        }

        RemoveItemStats(item);
        equipment[slotId] = null;
        OnEquipmentChanged?.Invoke(slot, null);
        GameEvents.ItemEquipped(slot, null);
        UpdateSerializedData();

        
            

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

    /// <summary>
    /// Returns the WeaponData for the item in the given slot, or null if the slot
    /// is empty or the equipped item has no WeaponData assigned.
    /// Used by DamageEffect to resolve dice damage at hit time.
    /// </summary>
    public WeaponData GetEquippedWeapon(string slotId)
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
    /// Fires GameEvents.ItemEquipped for every occupied slot.
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

            GameEvents.ItemEquipped(slotDef, kvp.Value);

       
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

    private void ApplyItemStats(ItemInstance item)
    {
        if (item?.calculatedModifiers == null) return;
        if (statSystem == null) return;

        foreach (var modifier in item.calculatedModifiers)
            statSystem.Engine.AddFlatModifier(modifier.statName, item.instanceId, modifier.value);

      
    }

    private void RemoveItemStats(ItemInstance item)
    {
        if (item == null) return;
        if (statSystem == null) return;

        statSystem.Engine.RemoveAllModifiersFromSource(item.instanceId);

      
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