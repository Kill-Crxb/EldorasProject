using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

// Grid-based inventory system for containers.
// Optional module - not all entities need inventory (e.g., wolves).

namespace CrabSystem
{
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
                items = new List<ItemRecord>()
            };

            foreach (var item in inventoryItems)
                if (item != null) saveData.items.Add(ItemRecord.From(item));

            return JsonUtility.ToJson(saveData);
        }

        public void LoadSaveData(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var saveData = JsonUtility.FromJson<InventorySaveData>(json);
            if (saveData?.items == null) return;

            ClearInventory();
            restoring = true;
            foreach (var record in saveData.items) Restore(record);
            EndRestore();
        }

        private void Restore(ItemRecord record)
        {
            ItemInstance item = record.ToInstance();
            if (item == null) return;
            if (!AddItem(item))
                Debug.LogWarning($"[InventorySystem] Failed to restore item {record.definitionId} at ({record.gridX},{record.gridY})", this);
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
            public List<ItemRecord> items;
        }

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
            foreach (var record in contents.items) Restore(record);
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
            currentContents.items = new List<ItemRecord>();
        }

        public ContainerContents SaveToContents()
        {
            if (currentContents == null) return null;

            currentContents.items.Clear();

            foreach (var item in inventoryItems)
                if (item != null && item.IsPlaced) currentContents.items.Add(ItemRecord.From(item));
            currentContents.UpdateTimestamp();

            return currentContents;
        }

        public ContainerContents GetCurrentContents() => currentContents;

        // Whose inventory this is — a vendor's shelf is found through it.
        public ControllerBrain Brain => brain;

        // Authored layout used before any contents exist. Grids read their size from it.
        public ContainerData DefaultContainer => containers != null && containers.Length > 0 ? containers[0] : null;

        public void SetContents(ContainerContents contents) { currentContents = contents; }

        // By-id calls count and move stacks: a stackable item (maxStackSize > 1) fills existing stacks
        // first, then new instances each holding up to a full stack. Coins arrive as one stack.
        bool IInventoryProvider.HasItem(string itemId)
            => inventoryItems.Any(i => i.definitionId == itemId);

        int IInventoryProvider.GetItemCount(string itemId)
        {
            int total = 0;
            foreach (var item in inventoryItems)
                if (item.definitionId == itemId) total += item.stackCount;
            return total;
        }

        bool IInventoryProvider.AddItem(string itemId, int quantity)
        {
            int left = FillStacks(itemId, quantity);
            while (left > 0)
            {
                var instance = new ItemInstance(itemId, ItemRarity.Common);
                if (instance.Definition == null) return false;
                instance.stackCount = Mathf.Min(left, StackLimit(instance.Definition));
                if (!AddItem(instance)) return false;
                left -= instance.stackCount;
            }
            return true;
        }

        bool IInventoryProvider.RemoveItem(string itemId, int quantity)
        {
            if (((IInventoryProvider)this).GetItemCount(itemId) < quantity) return false;

            int left = quantity;
            for (int i = inventoryItems.Count - 1; i >= 0 && left > 0; i--)
            {
                ItemInstance item = inventoryItems[i];
                if (item.definitionId != itemId) continue;

                int taken = Mathf.Min(left, item.stackCount);
                left -= taken;
                item.stackCount -= taken;
                if (item.stackCount == 0) RemoveItem(item.instanceId);
            }
            RaiseChanged();
            return true;
        }

        // Tops up existing stacks of the item and returns what didn't fit.
        private int FillStacks(string itemId, int quantity)
        {
            int left = quantity;
            foreach (var item in inventoryItems)
            {
                if (left <= 0) break;
                if (item.definitionId != itemId || item.Definition == null) continue;

                int room = StackLimit(item.Definition) - item.stackCount;
                if (room <= 0) continue;

                int added = Mathf.Min(room, left);
                item.stackCount += added;
                left -= added;
            }
            if (left != quantity) RaiseChanged();
            return left;
        }

        private static int StackLimit(ItemDefinition definition) => Mathf.Max(1, definition.maxStackSize);

        void IInventoryProvider.ClearInventory() => ClearInventory();
    }
}
