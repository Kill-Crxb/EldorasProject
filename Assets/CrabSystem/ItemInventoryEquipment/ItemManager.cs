using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Item Manager - Global item definition registry (Singleton)
/// 
/// Architecture Pattern:
/// ItemManager (Global) → ItemSystem (Per-Entity)
/// 
/// Mirrors:
/// - StatsManager → StatSystem
/// - ResourceManager → ResourceSystem
/// - DamageManager → DamageSystem
/// 
/// Responsibilities:
/// - Hold all ItemDefinition assets
/// - Provide item lookup by ID
/// - Validate item references at startup
/// - Single source of truth for "what items exist"
/// - Support filtering by category, slot, subtype, tags
/// 
/// Benefits vs MonoBehaviour Singleton:
/// - ManagerBrain integration (guaranteed init order)
/// - No DontDestroyOnLoad required
/// - Proper dependency management
/// - Consistent with other managers
/// - Better testability
/// 
/// Priority: 5 (After Stats/Resources, before Damage)
/// 
/// Phase 2: UI Window System Integration
/// Created: February 9, 2026
/// </summary>
public class ItemManager : MonoBehaviour, IGameManager
{
    #region Singleton (Simplified)

    private static ItemManager instance;
    public static ItemManager Instance => instance;

    #endregion

    #region IGameManager Implementation

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

        if (debugLogging)
        {
            Debug.Log($"[{ManagerName}] Initialized with {definitionLookup.Count} items");
        }
    }

    public void LateInitialize()
    {
        // Final validation after all managers loaded
        ValidateAllItems();
    }

    public void Shutdown()
    {
        if (debugLogging)
            Debug.Log($"[{ManagerName}] Shutdown complete");
    }

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();

        if (itemDefinitions == null || itemDefinitions.Length == 0)
        {
            result.Warnings.Add("No items configured");
            return result;
        }

        // Check for nulls
        int nullCount = 0;
        foreach (var def in itemDefinitions)
        {
            if (def == null) nullCount++;
        }

        if (nullCount > 0)
        {
            result.Warnings.Add($"{nullCount} null entries in item array");
        }

        // Check for missing IDs
        foreach (var def in itemDefinitions)
        {
            if (def != null && string.IsNullOrEmpty(def.itemId))
            {
                result.Errors.Add($"Item '{def.name}' has no itemId");
            }
        }

        // Check for duplicates
        var idCounts = new Dictionary<string, int>();
        foreach (var def in itemDefinitions)
        {
            if (def != null && !string.IsNullOrEmpty(def.itemId))
            {
                if (!idCounts.ContainsKey(def.itemId))
                    idCounts[def.itemId] = 0;
                idCounts[def.itemId]++;
            }
        }

        foreach (var kvp in idCounts)
        {
            if (kvp.Value > 1)
            {
                result.Errors.Add($"Duplicate itemId: '{kvp.Key}' ({kvp.Value} instances)");
            }
        }

        result.Info.Add($"Loaded {definitionLookup.Count} valid items");
        return result;
    }

    #endregion

    #region Inspector Fields

    [Header("Item Definitions")]
    [Tooltip("All item definitions to load at startup. These define what items exist in the game.")]
    [SerializeField] private ItemDefinition[] itemDefinitions;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region Private Fields

    // Fast lookup cache (itemId → definition)
    private Dictionary<string, ItemDefinition> definitionLookup = new Dictionary<string, ItemDefinition>();

    #endregion

    #region Initialization

    /// <summary>
    /// Load all item definitions and build lookup cache
    /// Called automatically by Initialize()
    /// </summary>
    private void LoadItems()
    {
        definitionLookup.Clear();

        // Guard clause - no items
        if (itemDefinitions == null || itemDefinitions.Length == 0)
        {
            Debug.LogWarning("[ItemManager] No items configured! Assign ItemDefinitions in the inspector.");
            return;
        }

        int loadedCount = 0;

        // Build lookup
        foreach (var definition in itemDefinitions)
        {
            // Guard clauses
            if (definition == null)
            {
                Debug.LogWarning("[ItemManager] Null ItemDefinition in array, skipping.");
                continue;
            }

            if (string.IsNullOrEmpty(definition.itemId))
            {
                Debug.LogWarning($"[ItemManager] ItemDefinition '{definition.name}' has no itemId, skipping.");
                continue;
            }

            // Check for duplicates
            if (definitionLookup.ContainsKey(definition.itemId))
            {
                Debug.LogError($"[ItemManager] Duplicate itemId '{definition.itemId}'! Items must have unique IDs.");
                continue;
            }

            // Add to lookup
            definitionLookup[definition.itemId] = definition;
            loadedCount++;

            if (debugLogging)
                Debug.Log($"[ItemManager] Loaded item: {definition.itemId} ({definition.displayName})");
        }

        if (debugLogging)
        {
            Debug.Log($"[ItemManager] Item loading complete: {loadedCount} items");
        }
    }

    /// <summary>
    /// Validate all items (called in LateInitialize)
    /// </summary>
    private void ValidateAllItems()
    {
        // Additional validation can be added here
        // For now, just confirm loaded state

        if (debugLogging)
        {
            Debug.Log($"[{ManagerName}] Item validation complete - {definitionLookup.Count} items loaded");
        }
    }

    #endregion

    #region Public API - Static Methods

    /// <summary>
    /// Get ItemDefinition by itemId
    /// STATIC METHOD - Called from anywhere as: ItemManager.GetDefinition("item_id")
    /// </summary>
    public static ItemDefinition GetDefinition(string itemId)
    {
        // Guard clause - no instance
        if (instance == null)
        {
            Debug.LogError("[ItemManager] No instance! ManagerBrain should initialize ItemManager.");
            return null;
        }

        // Guard clause - not initialized
        if (!instance.IsInitialized)
        {
            Debug.LogError("[ItemManager] Not initialized! ManagerBrain should call Initialize().");
            return null;
        }

        // Guard clause - invalid ID
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

        if (instance.debugLogging)
            Debug.LogWarning($"[ItemManager] Item not found: {itemId}");

        return null;
    }

    /// <summary>
    /// Create new ItemInstance from definition
    /// STATIC METHOD
    /// </summary>
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

    /// <summary>
    /// Check if an item exists
    /// STATIC METHOD
    /// </summary>
    public static bool HasItem(string itemId)
    {
        if (instance == null || !instance.IsInitialized)
            return false;

        if (string.IsNullOrEmpty(itemId))
            return false;

        return instance.definitionLookup.ContainsKey(itemId);
    }

    /// <summary>
    /// Get all items by equipment slot
    /// STATIC METHOD
    /// </summary>
    public static ItemDefinition[] GetItemsBySlot(EquipmentSlot slot)
    {
        if (instance?.itemDefinitions == null)
            return new ItemDefinition[0];

        var results = new List<ItemDefinition>();

        foreach (var definition in instance.itemDefinitions)
        {
            if (definition != null && definition.equipmentSlot == slot)
            {
                results.Add(definition);
            }
        }

        return results.ToArray();
    }

    /// <summary>
    /// Get items by category
    /// STATIC METHOD
    /// </summary>
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

    /// <summary>
    /// Get items by subtype
    /// STATIC METHOD
    /// </summary>
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

    /// <summary>
    /// Get items by tag
    /// STATIC METHOD
    /// </summary>
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

    /// <summary>
    /// Get all items (for debug/admin tools)
    /// STATIC METHOD
    /// </summary>
    public static ItemDefinition[] GetAllItems()
    {
        if (instance?.itemDefinitions == null)
            return new ItemDefinition[0];

        return instance.itemDefinitions;
    }

    /// <summary>
    /// Get all item IDs (for debugging)
    /// STATIC METHOD
    /// </summary>
    public static string[] GetAllItemIds()
    {
        if (instance?.definitionLookup == null)
            return new string[0];

        var ids = new string[instance.definitionLookup.Count];
        instance.definitionLookup.Keys.CopyTo(ids, 0);
        return ids;
    }

    /// <summary>
    /// Get count of loaded items
    /// </summary>
    public static int GetItemCount()
    {
        if (instance?.definitionLookup == null)
            return 0;

        return instance.definitionLookup.Count;
    }

    #endregion

    #region Hot-Reload Support

    /// <summary>
    /// Reload all items from inspector array (for hot-reloading during development)
    /// </summary>
    [ContextMenu("Hot Reload Items")]
    public void HotReload()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[ItemManager] Cannot hot-reload: not initialized");
            return;
        }

        LoadItems();
        Debug.Log($"[ItemManager] Hot-reloaded {definitionLookup.Count} items");
    }

    #endregion

    #region Editor Utilities

#if UNITY_EDITOR
    [ContextMenu("Print All Items")]
    private void PrintAllItems()
    {
        if (definitionLookup == null || definitionLookup.Count == 0)
        {
            if (!IsInitialized)
            {
                LoadItems();
            }
        }

        Debug.Log("=== ITEM MANAGER ===");
        Debug.Log($"Total Items: {definitionLookup.Count}");

        foreach (var kvp in definitionLookup)
        {
            Debug.Log($"  - {kvp.Key} ({kvp.Value.displayName})");
        }
    }

    [ContextMenu("Validate All Items")]
    private void ValidateInEditor()
    {
        var result = Validate();
        result.LogResult(ManagerName);
    }
#endif

    #endregion
}