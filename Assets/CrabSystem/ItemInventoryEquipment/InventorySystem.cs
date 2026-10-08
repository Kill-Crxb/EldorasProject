using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Grid-based inventory system for containers.
/// Optional module - not all entities need inventory (e.g., wolves).
/// </summary>
public class InventorySystem : MonoBehaviour, IBrainModule, IInventoryProvider, ISaveable
{
    public int InitOrder => 130;

    [Header("Container Configuration")]
    [SerializeField] private ContainerData[] containers;

    [Header("Container Contents")]
    [Tooltip("Optional: Pre-configured contents to load on initialization (inspector or runtime)")]
    [SerializeField] private ContainerContents initialContents;

    [Header("Capacity")]
    [SerializeField] private int maxItems = 100;

    private ControllerBrain brain;
    private List<ItemInstance> inventoryItems = new List<ItemInstance>();
    private ContainerContents currentContents;
    private bool isInitialized = false;

    public event Action OnInventoryChanged;
    public event Action<ISaveable> Dirty;
    public event Action<ItemInstance> OnItemAdded;

    // True while a save or a container's contents are being put back. OnItemAdded means "this
    // character just got an item" (pickups, quest Find objectives), so restoring must not raise it;
    // listeners get one OnInventoryChanged when the restore ends instead (B18).
    private bool restoring;
    public event Action<ItemInstance> OnItemRemoved;

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
        isInitialized = true;
    }

    public void LateInitialize()
    {
        // Load initial contents if assigned (non-player entities / pre-configured chests)
        // Players skip this — SaveManager loads their state via LoadSaveData()
        if (initialContents != null)
            LoadFromContents(initialContents);
        else
            CreateDefaultContents();
    }

    public void UpdateModule() { }

    public void Shutdown() { }

    #endregion

    #region ISaveable

    public string GetSaveId() => "inventory";
    public int GetSaveVersion() => 1;
    public int LoadOrder => 40;

    private void RaiseChanged()
    {
        OnInventoryChanged?.Invoke();
        Dirty?.Invoke(this);
    }

    public string GetSaveData()
    {
        var saveData = new InventorySaveData
        {
            version = GetSaveVersion(),
            items = new List<ItemSaveEntry>()
        };

        foreach (var item in inventoryItems)
        {
            if (item == null) continue;

            saveData.items.Add(new ItemSaveEntry
            {
                instanceId = item.instanceId,
                definitionId = item.definitionId,
                rarity = (int)item.currentTier,
                gridX = item.gridX,
                gridY = item.gridY,
                stackCount = item.stackCount,
                durability = item.durability
            });
        }

        return JsonUtility.ToJson(saveData);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var saveData = JsonUtility.FromJson<InventorySaveData>(json);
        if (saveData?.items == null) return;

        ClearInventory();
        restoring = true;

        foreach (var entry in saveData.items)
        {
            if (string.IsNullOrEmpty(entry.definitionId)) continue;

            var item = new ItemInstance(entry.definitionId, (ItemRarity)entry.rarity)
            {
                instanceId = entry.instanceId,
                stackCount = entry.stackCount,
                durability = entry.durability
            };

            item.PlaceAtPosition(entry.gridX, entry.gridY);

            if (!AddItem(item))
                Debug.LogWarning($"[InventorySystem] Failed to restore item {entry.definitionId} at ({entry.gridX},{entry.gridY})");
        }

        EndRestore();
    }

    private void EndRestore()
    {
        restoring = false;
        RaiseChanged();
    }

    // ── Save Data Structures ──────────────────────────────────────────────

    [Serializable]
    private class InventorySaveData
    {
        public int version;
        public List<ItemSaveEntry> items;
    }

    [Serializable]
    private class ItemSaveEntry
    {
        public string instanceId;
        public string definitionId;
        public int rarity;
        public int gridX;
        public int gridY;
        public int stackCount;
        public float durability;
    }

    #endregion

    #region Item Management

    public bool AddItem(ItemInstance item)
    {
        if (item == null) return false;
        if (item.Definition == null) return false;
        if (inventoryItems.Count >= maxItems) return false;

        if (!TryPlaceInAnyContainer(item))
        {
            Debug.LogWarning($"[InventorySystem] No space for {item.Definition.displayName}");
            return false;
        }

        inventoryItems.Add(item);
        if (restoring) return true;

        OnItemAdded?.Invoke(item);
        RaiseChanged();

        return true;
    }

    public bool RemoveItem(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return false;

        var item = inventoryItems.FirstOrDefault(i => i.instanceId == instanceId);
        if (item == null) return false;

        item.RemoveFromGrid();
        inventoryItems.Remove(item);
        OnItemRemoved?.Invoke(item);
        RaiseChanged();

        return true;
    }

    public ItemInstance GetItemInstance(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return null;
        return inventoryItems.FirstOrDefault(i => i.instanceId == instanceId);
    }

    public ItemInstance[] GetAllItems() => inventoryItems.ToArray();

    public bool HasItem(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return false;
        return inventoryItems.Any(i => i.instanceId == instanceId);
    }

    public int GetItemCount() => inventoryItems.Count;

    public void ClearInventory()
    {
        foreach (var item in inventoryItems)
            item?.RemoveFromGrid();

        inventoryItems.Clear();
        RaiseChanged();
    }

    #endregion

    #region Grid Placement

    private bool TryPlaceInAnyContainer(ItemInstance item)
    {
        // If item already has a valid position, honour it
        if (item.IsPlaced && IsPositionValid(item, item.gridX, item.gridY))
            return true;

        // Otherwise find the first open slot
        if (currentContents == null) return false;

        int gridW = currentContents.gridWidth;
        int gridH = currentContents.gridHeight;

        for (int y = 0; y <= gridH - item.itemHeight; y++)
        {
            for (int x = 0; x <= gridW - item.itemWidth; x++)
            {
                if (IsPositionValid(item, x, y))
                {
                    item.PlaceAtPosition(x, y);
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPositionValid(ItemInstance newItem, int x, int y)
    {
        if (currentContents == null) return false;

        int gridW = currentContents.gridWidth;
        int gridH = currentContents.gridHeight;

        // Bounds check
        if (x < 0 || y < 0) return false;
        if (x + newItem.itemWidth > gridW) return false;
        if (y + newItem.itemHeight > gridH) return false;

        // Overlap check
        foreach (var existing in inventoryItems)
        {
            if (existing == null || !existing.IsPlaced) continue;
            if (existing == newItem) continue;

            bool overlapX = x < existing.gridX + existing.itemWidth && x + newItem.itemWidth > existing.gridX;
            bool overlapY = y < existing.gridY + existing.itemHeight && y + newItem.itemHeight > existing.gridY;

            if (overlapX && overlapY) return false;
        }

        return true;
    }

    #endregion

    #region Contents Management

    public void LoadFromContents(ContainerContents contents)
    {
        if (contents == null)
        {
            Debug.LogWarning("[InventorySystem] Tried to load null ContainerContents");
            return;
        }

        ClearInventory();
        currentContents = contents;
        restoring = true;

        foreach (var containerItem in contents.items)
        {
            var itemInstance = ItemManager.CreateItem(containerItem.itemId, containerItem.rarity);
            if (itemInstance == null) continue;

            itemInstance.PlaceAtPosition(containerItem.gridX, containerItem.gridY);

            if (!AddItem(itemInstance))
                Debug.LogWarning($"[InventorySystem] Failed to load item {containerItem.itemId} at ({containerItem.gridX},{containerItem.gridY})");
        }

        EndRestore();
    }

    private void CreateDefaultContents()
    {
        if (containers == null || containers.Length == 0) return;

        currentContents = ScriptableObject.CreateInstance<ContainerContents>();
        currentContents.gridWidth = containers[0].gridWidth;
        currentContents.gridHeight = containers[0].gridHeight;
        currentContents.containerId = brain?.name ?? "unknown";
        currentContents.displayName = "Inventory";
        currentContents.items = new List<ContainerItem>();
    }

    public ContainerContents SaveToContents()
    {
        if (currentContents == null) return null;

        currentContents.items.Clear();

        foreach (var item in inventoryItems)
        {
            if (item == null || !item.IsPlaced) continue;
            currentContents.AddItem(item.definitionId, item.currentTier, item.gridX, item.gridY);
        }

        return currentContents;
    }

    public ContainerContents GetCurrentContents() => currentContents;

    /// <summary>Whose inventory this is — a vendor's shelf is found through it.</summary>
    public ControllerBrain Brain => brain;

    /// <summary>Authored layout used before any contents exist. Grids read their size from it.</summary>
    public ContainerData DefaultContainer => containers != null && containers.Length > 0 ? containers[0] : null;

    public void SetContents(ContainerContents contents) { currentContents = contents; }

    #endregion

    #region IInventoryProvider

    bool IInventoryProvider.HasItem(string itemId)
        => inventoryItems.Any(i => i.definitionId == itemId);

    int IInventoryProvider.GetItemCount(string itemId)
        => inventoryItems.Count(i => i.definitionId == itemId);

    bool IInventoryProvider.AddItem(string itemId, int quantity)
    {
        for (int i = 0; i < quantity; i++)
        {
            var instance = new ItemInstance(itemId, ItemRarity.Common);
            if (!AddItem(instance)) return false;
        }
        return true;
    }

    bool IInventoryProvider.RemoveItem(string itemId, int quantity)
    {
        int removed = 0;
        for (int i = inventoryItems.Count - 1; i >= 0 && removed < quantity; i--)
        {
            if (inventoryItems[i].definitionId != itemId) continue;
            RemoveItem(inventoryItems[i].instanceId);
            removed++;
        }
        return removed == quantity;
    }

    void IInventoryProvider.ClearInventory() => ClearInventory();

    #endregion
}