// PlayerItemsModule.cs - Clean data module for new three-layer system
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class PlayerItemsModule : MonoBehaviour, IPlayerModule, ISaveable
{
    [Header("Storage Configuration")]
    [SerializeField] private int maxInventorySlots = 80; // 8x10 grid

    [Header("Debug")]
    [SerializeField] private bool debugOperations = false;


    // Core storage - simple arrays
    private List<ItemInstance> inventoryItems = new List<ItemInstance>();
    private ItemInstance[] equippedItems; // Indexed by EquipmentSlot enum
    private Dictionary<EquipmentSlot, EquipmentSlotComponent> equipmentSlotComponents = new Dictionary<EquipmentSlot, EquipmentSlotComponent>();

    // System references
    private ControllerBrain brain;
    private RPGSecondaryStats statsSystem;


    // Properties
    public bool IsEnabled { get; set; } = true;

    // Events for UI systems to subscribe to
    public event Action OnInventoryChanged;
    public event Action<EquipmentSlot> OnEquipmentChanged;

    #region IPlayerModule Implementation
    private void Awake()
    {
        // Initialize equipment array early for registration
        int equipmentSlotCount = Enum.GetValues(typeof(EquipmentSlot)).Length;
        equippedItems = new ItemInstance[equipmentSlotCount];
    }

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        statsSystem = brain.GetModule<RPGSecondaryStats>();

        StartCoroutine(RegisterEquipmentSlotsDelayed());

        if (debugOperations)
            Debug.Log("PlayerItemsModule initialized - clean system");
    }

    private System.Collections.IEnumerator RegisterEquipmentSlotsDelayed()
    {
        // Wait one frame for UI to initialize
        yield return null;

        var slots = FindObjectsOfType<EquipmentSlotComponent>();
        foreach (var slot in slots)
        {
            RegisterEquipmentSlot(slot);
        }

        if (debugOperations)
            Debug.Log($"Registered {slots.Length} equipment slot components");
    }

    public void RegisterEquipmentSlot(EquipmentSlotComponent slotComponent)
    {
        if (!equipmentSlotComponents.ContainsKey(slotComponent.SlotType))
        {
            equipmentSlotComponents[slotComponent.SlotType] = slotComponent;
            slotComponent.SetEquipmentManager(this);

            // Update visual if slot already has item
            var currentItem = GetEquippedItem(slotComponent.SlotType);
            if (currentItem != null)
            {
                slotComponent.SetEquippedItemInstance(currentItem);
            }
        }
    }
    public void UpdateModule()
    {
        // Minimal per-frame logic if needed
    }

    #endregion

    #region Core Inventory Operations

    public bool AddItem(ItemInstance item)
    {
        if (item == null)
        {
            if (debugOperations) Debug.LogWarning("Attempted to add null item");
            return false;
        }

        if (inventoryItems.Count >= maxInventorySlots)
        {
            if (debugOperations) Debug.LogWarning("Inventory full");
            return false;
        }

        // CRITICAL FIX: Assign grid position if not already placed
        if (!item.IsPlaced)
        {
            // Find first available position for this item size
            GridPosition pos = FindEmptySpaceForItem(item);
            if (!pos.IsValid)
            {
                if (debugOperations) Debug.LogWarning($"No space in grid for {item.itemWidth}x{item.itemHeight} item");
                return false;
            }
            item.PlaceAtPosition(pos.x, pos.y);
        }

        inventoryItems.Add(item);
        OnInventoryChanged?.Invoke();

        if (debugOperations)
        {
            var def = ItemDatabase.GetDefinition(item.definitionId);
            Debug.Log($"Added {def?.displayName} to inventory at ({item.gridX}, {item.gridY})");
        }

        return true;
    }

    // Helper method to find empty space in grid
    private GridPosition FindEmptySpaceForItem(ItemInstance item)
    {
        int gridWidth = 8;
        int gridHeight = 10;

        // Try each position in the grid
        for (int y = 0; y <= gridHeight - item.itemHeight; y++)
        {
            for (int x = 0; x <= gridWidth - item.itemWidth; x++)
            {
                if (CanPlaceItemAt(x, y, item))
                {
                    return new GridPosition(x, y);
                }
            }
        }

        return GridPosition.Invalid;
    }

    // Check if item can be placed at position
    private bool CanPlaceItemAt(int x, int y, ItemInstance itemToPlace)
    {
        // Check bounds
        if (x < 0 || y < 0 || x + itemToPlace.itemWidth > 8 || y + itemToPlace.itemHeight > 10)
            return false;

        // Check for overlaps with other items
        foreach (var existingItem in inventoryItems)
        {
            if (existingItem == null || !existingItem.IsPlaced)
                continue;

            // Check if areas overlap
            bool overlapsX = x < existingItem.gridX + existingItem.itemWidth &&
                           x + itemToPlace.itemWidth > existingItem.gridX;
            bool overlapsY = y < existingItem.gridY + existingItem.itemHeight &&
                           y + itemToPlace.itemHeight > existingItem.gridY;

            if (overlapsX && overlapsY)
                return false;
        }

        return true;
    }

    public bool RemoveItem(string instanceId)
    {
        var item = inventoryItems.FirstOrDefault(i => i.instanceId == instanceId);
        if (item == null) return false;

        inventoryItems.Remove(item);
        OnInventoryChanged?.Invoke();

        if (debugOperations)
        {
            var def = ItemDatabase.GetDefinition(item.definitionId);
            Debug.Log($"Removed {def?.displayName} from inventory");
        }

        return true;
    }

    public ItemInstance GetItemInstance(string instanceId)
    {
        return inventoryItems.FirstOrDefault(i => i.instanceId == instanceId);
    }

    public IEnumerable<ItemInstance> GetAllInventoryItems()
    {
        return inventoryItems;
    }

    public int GetInventoryItemCount()
    {
        return inventoryItems.Count;
    }

    public bool HasInventorySpace()
    {
        return inventoryItems.Count < maxInventorySlots;
    }

    #endregion

    #region Equipment Operations

    public bool EquipItem(ItemInstance item, EquipmentSlot slot)
    {
        if (item == null || !CanEquipToSlot(item, slot))
        {
            if (debugOperations) Debug.LogWarning($"Cannot equip item to {slot}");
            return false;
        }

        int slotIndex = (int)slot;
        var previousItem = equippedItems[slotIndex];

        // Unequip old item if present
        if (previousItem != null)
        {
            RemoveItemStats(previousItem);
            AddItem(previousItem); // Return to inventory
        }

        // Remove from inventory if it's there
        inventoryItems.Remove(item);

        // Equip new item
        item.RemoveFromGrid(); // Clear grid position
        equippedItems[slotIndex] = item;
        ApplyItemStats(item);

        // NEW: Update slot visual
        if (equipmentSlotComponents.ContainsKey(slot))
        {
            equipmentSlotComponents[slot].SetEquippedItemInstance(item);
        }

        OnEquipmentChanged?.Invoke(slot);
        OnInventoryChanged?.Invoke();

        if (debugOperations)
        {
            var def = ItemDatabase.GetDefinition(item.definitionId);
            Debug.Log($"Equipped {def?.displayName} to {slot}");
        }

        return true;
    }


    public bool UnequipItem(EquipmentSlot slot)
    {
        int slotIndex = (int)slot;
        var item = equippedItems[slotIndex];

        if (item == null) return false;
        if (!HasInventorySpace()) return false;

        equippedItems[slotIndex] = null;
        RemoveItemStats(item);

        // Only auto-place if not already placed
        if (!item.IsPlaced)
        {
            AddItem(item);
        }
        else
        {
            // Already has position - just add to list
            inventoryItems.Add(item);
        }

        if (equipmentSlotComponents.ContainsKey(slot))
        {
            equipmentSlotComponents[slot].SetEmpty();
        }

        OnEquipmentChanged?.Invoke(slot);
        OnInventoryChanged?.Invoke();

        return true;
    }

    public ItemInstance GetEquippedItem(EquipmentSlot slot)
    {
        if (equippedItems == null) return null; // Add this line

        int index = (int)slot;
        if (index >= 0 && index < equippedItems.Length)
            return equippedItems[index];
        return null;
    }

    private bool CanEquipToSlot(ItemInstance item, EquipmentSlot slot)
    {
        var definition = ItemDatabase.GetDefinition(item.definitionId);
        return definition != null && definition.equipmentSlot == slot;
    }

    #endregion

    #region Stats Integration

    private void ApplyItemStats(ItemInstance item)
    {
        if (statsSystem != null && item != null)
        {
            item.ApplyToStatsSystem(statsSystem);
        }
    }

    private void RemoveItemStats(ItemInstance item)
    {
        if (statsSystem != null && item != null)
        {
            statsSystem.RemoveAllModifiersFromSource(item.instanceId);
        }
    }

    #endregion

    #region Save System (ISaveable)

    public string GetSaveId() => "PlayerItems";

    public string GetSaveData()
    {
        var saveData = new PlayerItemsSaveData
        {
            inventoryItems = inventoryItems.ToArray(),
            equippedItems = equippedItems,
            version = 4
        };
        return JsonUtility.ToJson(saveData, true);
    }

    public void LoadSaveData(string json)
    {
        var saveData = JsonUtility.FromJson<PlayerItemsSaveData>(json);
        if (saveData == null)
        {
            if (debugOperations) Debug.LogWarning("Failed to load save data");
            return;
        }

        // Clear existing data
        ClearAllItems();

        // Load inventory
        if (saveData.inventoryItems != null)
        {
            inventoryItems = new List<ItemInstance>(saveData.inventoryItems);
        }

        // Load equipment
        if (saveData.equippedItems != null)
        {
            for (int i = 0; i < saveData.equippedItems.Length && i < equippedItems.Length; i++)
            {
                if (saveData.equippedItems[i] != null)
                {
                    equippedItems[i] = saveData.equippedItems[i];
                    ApplyItemStats(saveData.equippedItems[i]);
                }
            }
        }

        OnInventoryChanged?.Invoke();
        for (int i = 0; i < equippedItems.Length; i++)
        {
            if (equippedItems[i] != null)
                OnEquipmentChanged?.Invoke((EquipmentSlot)i);
        }

        if (debugOperations)
            Debug.Log($"Loaded save: {inventoryItems.Count} inventory items, {equippedItems.Count(i => i != null)} equipped");
    }

    public int GetSaveVersion() => 4;

    private void ClearAllItems()
    {
        // Remove all equipped item stats
        foreach (var item in equippedItems)
        {
            if (item != null)
                RemoveItemStats(item);
        }

        inventoryItems.Clear();
        Array.Clear(equippedItems, 0, equippedItems.Length);
    }

    #endregion

    #region Public Utility Methods

    public void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    #endregion

    #region Context Menu Testing

    [ContextMenu("Add Test 1x1 Item")]
    public void AddTest1x1Item()
    {
        var item = ItemDatabase.CreateItem("headguard_common", ItemRarity.Common);
        if (item != null)
        {
            item.itemWidth = 1;
            item.itemHeight = 1;
            bool success = AddItem(item);
            Debug.Log($"Add 1x1 item: {(success ? "SUCCESS" : "FAILED")} - Now have {inventoryItems.Count} items");
        }
        else
        {
            Debug.LogError("Failed to create headguard_common - check ItemDatabase has this definition");
        }
    }

    [ContextMenu("Add Test 2x2 Item")]
    public void AddTest2x2Item()
    {
        var item = ItemDatabase.CreateItem("headguard_common", ItemRarity.Rare);
        if (item != null)
        {
            item.itemWidth = 2;
            item.itemHeight = 2;
            bool success = AddItem(item);
            Debug.Log($"Add 2x2 item: {(success ? "SUCCESS" : "FAILED")} - Now have {inventoryItems.Count} items");
        }
        else
        {
            Debug.LogError("Failed to create headguard_common - check ItemDatabase has this definition");
        }
    }

    [ContextMenu("Add Test 1x3 Item")]
    public void AddTest1x3Item()
    {
        var item = ItemDatabase.CreateItem("headband_common", ItemRarity.Epic);
        if (item != null)
        {
            item.itemWidth = 1;
            item.itemHeight = 3;
            bool success = AddItem(item);
            Debug.Log($"Add 1x3 item: {(success ? "SUCCESS" : "FAILED")} - Now have {inventoryItems.Count} items");
        }
        else
        {
            Debug.LogError("Failed to create headband_common - check ItemDatabase has this definition");
        }
    }
    [ContextMenu("Add One Of Each Item")]
    public void AddOneOfEachItem()
    {
        string[] itemIds = { "circlet_common", "boots_common", "cloak_common", "gloves_common" };
        int successCount = 0;

        foreach (string itemId in itemIds)
        {
            var item = ItemDatabase.CreateItem(itemId, ItemRarity.Common);
            if (item != null && AddItem(item))
            {
                successCount++;
            }
        }

        Debug.Log($"Added {successCount}/{itemIds.Length} items to inventory");
    }
    [ContextMenu("Clear All Items")]
    public void ClearAllItemsDebug()
    {
        ClearAllItems();
        OnInventoryChanged?.Invoke();
        Debug.Log("Cleared all items");
    }

    [ContextMenu("Debug Status")]
    public void DebugStatus()
    {
        Debug.Log($"=== PlayerItemsModule Status ===");
        Debug.Log($"Inventory: {inventoryItems.Count}/{maxInventorySlots} slots used");
        Debug.Log($"Equipment: {equippedItems.Count(i => i != null)}/{equippedItems.Length} slots equipped");
        Debug.Log($"Stats System: {(statsSystem != null ? "Connected" : "Missing")}");

        // Show placed items
        Debug.Log($"\nPlaced Items:");
        foreach (var item in inventoryItems)
        {
            if (item != null && item.IsPlaced)
            {
                var def = ItemDatabase.GetDefinition(item.definitionId);
                Debug.Log($"  - {def?.displayName ?? item.definitionId} at ({item.gridX},{item.gridY}) size {item.itemWidth}x{item.itemHeight}");
            }
        }
    }

    #endregion
}

[System.Serializable]
public class PlayerItemsSaveData
{
    public ItemInstance[] inventoryItems;
    public ItemInstance[] equippedItems;
    public int version;
}