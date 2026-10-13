using UnityEngine;
using System.Collections.Generic;

// Global registry of every item that exists. Definitions come from the inspector array
// and from any Resources/ItemDatabase folder; everything else in the game resolves items
// through GetDefinition/CreateItem by id.

namespace CrabSystem
{
    public class ItemManager : MonoBehaviour, IGameManager
    {
        private static ItemManager instance;
        public static ItemManager Instance => instance;

        public string ManagerName => "Item Manager";
        public int InitializationPriority => 5; // After Stats (0) and Resources (10)
        public bool IsEnabled => enabled;
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) return;

            instance = this;

            LoadItems();

            IsInitialized = true;
        }

        public void LateInitialize() { }

        public void Shutdown() { }

        public ValidationResult Validate()
        {
            var result = ValidationResult.Success();

            if (!IsInitialized) return result;

            if (definitionLookup.Count == 0)
            {
                result.Warnings.Add($"No items loaded — inspector array is empty and Resources/{DatabaseFolder} found nothing");
                return result;
            }

            result.Info.Add($"{definitionLookup.Count} items registered");
            return result;
        }

        // Any folder named "Resources" anywhere under Assets is a resources root, so the
        // database folder is found by this name alone, wherever it sits in the project.
        private const string DatabaseFolder = CrabResources.Items;

        [Header("Item Definitions")]
        [Tooltip("All item definitions to load at startup. These define what items exist in the game.")]
        [SerializeField] private ItemDefinition[] itemDefinitions;

        // Fast lookup cache (itemId → definition)
        private Dictionary<string, ItemDefinition> definitionLookup = new Dictionary<string, ItemDefinition>();

        private void LoadItems()
        {
            definitionLookup.Clear();

            int count = Register(itemDefinitions);
            count += Register(Resources.LoadAll<ItemDefinition>(DatabaseFolder));

            if (count == 0)
            {
                Debug.LogWarning($"[ItemManager] No items loaded. Assign definitions in the inspector, " +
                                 $"or drop them in a Resources/{DatabaseFolder} folder.");
            }
        }

        // Adds definitions to the lookup, skipping anything unusable. Returns how many landed.
        // Called once for the inspector array and once for the database folder, so an asset
        // listed in both is silently ignored the second time rather than reported as a clash.
        private int Register(ItemDefinition[] definitions)
        {
            if (definitions == null) return 0;

            int count = 0;

            foreach (var definition in definitions)
            {
                if (definition == null) continue;

                if (string.IsNullOrEmpty(definition.itemId))
                {
                    Debug.LogWarning($"[ItemManager] '{definition.name}' has no itemId, skipping.");
                    continue;
                }

                if (definitionLookup.TryGetValue(definition.itemId, out var existing))
                {
                    if (existing != definition)
                        Debug.LogError($"[ItemManager] Duplicate itemId '{definition.itemId}' on " +
                                       $"'{definition.name}' and '{existing.name}'. Ids must be unique.");
                    continue;
                }

                definitionLookup[definition.itemId] = definition;
                count++;
            }

            return count;
        }

        // Get ItemDefinition by itemId
        // STATIC METHOD - Called from anywhere as: ItemManager.GetDefinition("item_id")
        public static ItemDefinition GetDefinition(string itemId)
        {
            if (instance == null)
            {
                Debug.LogError("[ItemManager] No instance! ManagerBrain should initialize ItemManager.");
                return null;
            }

            if (!instance.IsInitialized)
            {
                Debug.LogError("[ItemManager] Not initialized! ManagerBrain should call Initialize().");
                return null;
            }

            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning("[ItemManager] Cannot get definition: itemId is null or empty.");
                return null;
            }

            // Lookup
            if (instance.definitionLookup.TryGetValue(itemId, out var definition))
            {
                return definition;
            }

            Debug.LogWarning($"[ItemManager] Item not found: {itemId}");
            return null;
        }

        // Create new ItemInstance from definition
        // STATIC METHOD
        public static ItemInstance CreateItem(string itemId, ItemRarity tier = ItemRarity.Common)
        {
            var definition = GetDefinition(itemId);

            if (definition == null)
            {
                Debug.LogError($"[ItemManager] Cannot create item: definition not found for '{itemId}'");
                return null;
            }

            return new ItemInstance(itemId, tier);
        }

        // Check if an item exists
        // STATIC METHOD
        public static bool HasItem(string itemId)
        {
            if (instance == null || !instance.IsInitialized)
                return false;

            if (string.IsNullOrEmpty(itemId))
                return false;

            return instance.definitionLookup.ContainsKey(itemId);
        }

        // Get items by category
        // STATIC METHOD
        public static ItemDefinition[] GetItemsByCategory(ItemCategory category)
        {
            if (instance?.itemDefinitions == null)
                return new ItemDefinition[0];

            if (category == null)
                return new ItemDefinition[0];

            var results = new List<ItemDefinition>();

            foreach (var definition in instance.itemDefinitions)
            {
                if (definition != null && definition.category == category)
                {
                    results.Add(definition);
                }
            }

            return results.ToArray();
        }

        // Get items by subtype
        // STATIC METHOD
        public static ItemDefinition[] GetItemsBySubType(ItemSubType subType)
        {
            if (instance?.itemDefinitions == null)
                return new ItemDefinition[0];

            if (subType == null)
                return new ItemDefinition[0];

            var results = new List<ItemDefinition>();

            foreach (var definition in instance.itemDefinitions)
            {
                if (definition != null && definition.subType == subType)
                {
                    results.Add(definition);
                }
            }

            return results.ToArray();
        }

        // Get items by tag
        // STATIC METHOD
        public static ItemDefinition[] GetItemsByTag(string tag)
        {
            if (instance?.itemDefinitions == null)
                return new ItemDefinition[0];

            if (string.IsNullOrEmpty(tag))
                return new ItemDefinition[0];

            var results = new List<ItemDefinition>();

            foreach (var definition in instance.itemDefinitions)
            {
                if (definition != null && definition.HasTag(tag))
                {
                    results.Add(definition);
                }
            }

            return results.ToArray();
        }

        // Get all items (for debug/admin tools)
        // STATIC METHOD
        public static ItemDefinition[] GetAllItems()
        {
            if (instance?.itemDefinitions == null)
                return new ItemDefinition[0];

            return instance.itemDefinitions;
        }

        // Get all item IDs (for debugging)
        // STATIC METHOD
        public static string[] GetAllItemIds()
        {
            if (instance?.definitionLookup == null)
                return new string[0];

            var ids = new string[instance.definitionLookup.Count];
            instance.definitionLookup.Keys.CopyTo(ids, 0);
            return ids;
        }

        // Get count of loaded items
        public static int GetItemCount()
        {
            if (instance?.definitionLookup == null)
                return 0;

            return instance.definitionLookup.Count;
        }
    }
}
