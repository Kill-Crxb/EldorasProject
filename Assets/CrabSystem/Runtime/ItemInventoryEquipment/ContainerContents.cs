using UnityEngine;
using System.Collections.Generic;

// Container Contents - ScriptableObject representing what's inside a container
//
// Philosophy:
// - This is JUST data - "here's what items are in this container"
// - No behavior, no lifecycle logic
// - Can be created at runtime or pre-configured in editor
// - Can be saved to disk or kept in memory
//
// Use Cases:
// 1. Pre-configured Loot (Inspector)
//    - Designer creates asset with specific items
//    - InventorySystem loads this on spawn
//    - Example: Tutorial chest with starting gear
//
// 2. Runtime Generated Loot (LootSystem)
//    - LootSystem rolls loot, creates ContainerContents instance
//    - InventorySystem uses this data
//    - Example: Boss dies, generates loot, stores in ContainerContents
//
// 3. Player-Modified Containers (Extraction Shooter)
//    - Player loots chest, leaves items behind
//    - InventorySystem updates ContainerContents
//    - Other players see modified contents
//    - End of raid: LootSystem decides to respawn or clear
//
// 4. Persistent Storage (Stash)
//    - InventorySystem saves ContainerContents to disk
//    - On load: InventorySystem reads from disk, applies to container
//    - Same ContainerContents loaded across multiple play sessions
//
// Lifecycle Responsibility:
// - LootSystem: Decides WHEN to generate/clear/respawn contents
// - InventorySystem: Manages WHAT items are in the container
// - ContainerContents: Just holds the data

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "New Container Contents", menuName = "Items/Container Contents")]
    public class ContainerContents : ScriptableObject
    {
        [Header("Container Info")]
        [Tooltip("Unique identifier (auto-generated or set manually)")]
        public string containerId;

        [Tooltip("Display name for UI")]
        public string displayName = "Container";

        [Header("Grid Configuration")]
        [Tooltip("Grid width in cells")]
        [Range(1, 20)]
        public int gridWidth = 8;

        [Tooltip("Grid height in cells")]
        [Range(1, 20)]
        public int gridHeight = 10;

        [Header("Items")]
        [Tooltip("Items currently in this container (editable in inspector)")]
        public List<ItemRecord> items = new List<ItemRecord>();

        [Header("Metadata")]
        [Tooltip("Last modified timestamp (runtime updated)")]
        public long lastModified;

        [Tooltip("Original creator (for player-placed containers)")]
        public string creatorId;

        public bool RemoveItem(int index)
        {
            if (index < 0 || index >= items.Count)
                return false;

            items.RemoveAt(index);
            UpdateTimestamp();
            return true;
        }

        // Remove specific item by item ID
        public bool RemoveItem(string itemId)
        {
            int index = items.FindIndex(i => i.definitionId == itemId);
            if (index >= 0)
            {
                items.RemoveAt(index);
                UpdateTimestamp();
                return true;
            }
            return false;
        }

        // Clear all items
        public void ClearAll()
        {
            items.Clear();
            UpdateTimestamp();
        }

        public int ItemCount => items.Count;

        // Update last modified timestamp
        public void UpdateTimestamp()
        {
            lastModified = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // Convert to ContainerData for network/save serialization
        public ContainerData ToContainerData()
        {
            var itemInstances = new List<ItemInstance>();

            foreach (var record in items)
            {
                ItemInstance instance = record.ToInstance();
                if (instance != null) itemInstances.Add(instance);
            }

            return new ContainerData
            {
                containerId = containerId,
                type = ContainerType.Chest,
                gridWidth = gridWidth,
                gridHeight = gridHeight,
                items = itemInstances.ToArray(),
                lastModified = lastModified
            };
        }

        // Create ContainerContents from ContainerData
        public static ContainerContents FromContainerData(ContainerData data)
        {
            var contents = CreateInstance<ContainerContents>();
            contents.containerId = data.containerId;
            contents.gridWidth = data.gridWidth;
            contents.gridHeight = data.gridHeight;
            contents.lastModified = data.lastModified;
            contents.items = new List<ItemRecord>();

            if (data.items != null)
                foreach (var instance in data.items) contents.items.Add(ItemRecord.From(instance));

            return contents;
        }

        // Save this container contents to disk as JSON
        public void SaveToDisk(string path)
        {
            var wrapper = new ContainerContentsWrapper { contents = this };
            string json = JsonUtility.ToJson(wrapper, true);
            System.IO.File.WriteAllText(path, json);
        }

        public static ContainerContents LoadFromDisk(string path)
        {
            if (!System.IO.File.Exists(path))
                return null;

            string json = System.IO.File.ReadAllText(path);
            var wrapper = JsonUtility.FromJson<ContainerContentsWrapper>(json);
            return wrapper.contents;
        }

        private void OnValidate()
        {
            // Auto-generate containerId if empty
            if (string.IsNullOrEmpty(containerId))
            {
                containerId = $"container_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            }

            // Ensure grid is valid
            gridWidth = Mathf.Max(1, gridWidth);
            gridHeight = Mathf.Max(1, gridHeight);
        }

        [ContextMenu("Clear All Items")]
        private void DebugClearAll()
        {
            ClearAll();
            Debug.Log($"[ContainerContents] Cleared all items from {containerId}");
        }

        [ContextMenu("Print Items")]
        private void DebugPrintItems()
        {
            Debug.Log($"=== {displayName} ({containerId}) ===");
            Debug.Log($"Grid: {gridWidth}x{gridHeight}");
            Debug.Log($"Items: {items.Count}");
            foreach (var item in items)
            {
                Debug.Log($"  - {item.definitionId} ({item.rarity}) x{item.stackCount} at ({item.gridX}, {item.gridY})");
            }
        }
    }

    // Wrapper for JSON serialization
    [System.Serializable]
    public class ContainerContentsWrapper
    {
        public ContainerContents contents;
    }
}
