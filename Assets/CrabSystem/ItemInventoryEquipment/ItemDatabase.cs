using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ItemDatabase - DEPRECATED - Use ItemManager instead
/// 
/// DEPRECATION NOTICE (February 9, 2026):
/// This class has been replaced by ItemManager which follows the Manager pattern.
/// ItemManager integrates with ManagerBrain for proper initialization order.
/// 
/// Migration Path:
/// - Replace ItemDatabase.GetDefinition() with ItemManager.GetDefinition()
/// - Replace ItemDatabase.CreateItem() with ItemManager.CreateItem()
/// - All static methods have identical signatures
/// 
/// This class is kept for backward compatibility only and may be removed in future versions.
/// 
/// OLD Architecture:
/// - MonoBehaviour singleton (DontDestroyOnLoad)
/// - Scene-based initialization
/// - Manual GameObject placement required
/// 
/// NEW Architecture (ItemManager):
/// - IGameManager implementation
/// - ManagerBrain integration
/// - Guaranteed initialization order (Priority: 5)
/// - No DontDestroyOnLoad required
/// </summary>
[System.Obsolete("Use ItemManager instead. ItemDatabase is deprecated and will be removed in a future version.", false)]
public class ItemDatabase : MonoBehaviour
{
    #region Singleton

    private static ItemDatabase instance;
    public static ItemDatabase Instance => instance;

    #endregion

    #region Inspector Fields

    [Header("Item Definitions")]
    [Tooltip("Drag all ItemDefinition assets here. Items are indexed by itemId.")]
    [SerializeField] private ItemDefinition[] itemDefinitions;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region Private Fields

    // Fast lookup cache (itemId → definition)
    private Dictionary<string, ItemDefinition> definitionLookup;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(transform.root.gameObject);
            InitializeDatabase();
        }
        else
        {
            Debug.LogWarning("[ItemDatabase] Duplicate instance detected, destroying.");
            Destroy(gameObject);
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize database - build lookup cache
    /// Called automatically on Awake
    /// </summary>
    private void InitializeDatabase()
    {
        definitionLookup = new Dictionary<string, ItemDefinition>();

        // Guard clause - no items
        if (itemDefinitions == null || itemDefinitions.Length == 0)
        {
            Debug.LogError("[ItemDatabase] No items configured! Assign ItemDefinitions in the inspector.");
            return;
        }

        // Build lookup
        foreach (var definition in itemDefinitions)
        {
            // Guard clauses
            if (definition == null)
            {
                Debug.LogWarning("[ItemDatabase] Null ItemDefinition in array, skipping.");
                continue;
            }

            if (string.IsNullOrEmpty(definition.itemId))
            {
                Debug.LogWarning($"[ItemDatabase] ItemDefinition '{definition.name}' has no itemId, skipping.");
                continue;
            }

            // Check for duplicates
            if (definitionLookup.ContainsKey(definition.itemId))
            {
                Debug.LogError($"[ItemDatabase] Duplicate itemId '{definition.itemId}'! Items must have unique IDs.");
                continue;
            }

            // Add to lookup
            definitionLookup[definition.itemId] = definition;
        }

        if (debugLogging)
        {
            Debug.Log($"[ItemDatabase] Initialized with {definitionLookup.Count} items");
        }
    }

    #endregion

    #region Static Query Methods - CRITICAL: These MUST be static

    /// <summary>
    /// Get ItemDefinition by itemId
    /// STATIC METHOD - Called from anywhere as: ItemDatabase.GetDefinition("item_id")
    /// </summary>
    public static ItemDefinition GetDefinition(string itemId)
    {
        // Guard clause - no instance
        if (instance == null)
        {
            Debug.LogError("[ItemDatabase] No instance! Create ItemDatabase GameObject in scene.");
            return null;
        }

        // Guard clause - not initialized
        if (instance.definitionLookup == null)
        {
            Debug.LogError("[ItemDatabase] Not initialized!");
            return null;
        }

        // Guard clause - invalid ID
        if (string.IsNullOrEmpty(itemId))
        {
            Debug.LogWarning("[ItemDatabase] Cannot get definition: itemId is null or empty.");
            return null;
        }

        // Lookup
        if (instance.definitionLookup.TryGetValue(itemId, out var definition))
        {
            return definition;
        }

        if (instance.debugLogging)
            Debug.LogWarning($"[ItemDatabase] Item not found: {itemId}");

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
            Debug.LogError($"[ItemDatabase] Cannot create item: definition not found for '{itemId}'");
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
        if (instance == null || instance.definitionLookup == null)
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
            // Uses bridge property in ItemDefinition
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

    #endregion

    #region Editor Utilities

#if UNITY_EDITOR
    [ContextMenu("Refresh Database")]
    private void RefreshDatabase()
    {
        InitializeDatabase();
        Debug.Log("[ItemDatabase] Database refreshed!");
    }

    [ContextMenu("Print All Items")]
    private void PrintAllItems()
    {
        if (definitionLookup == null)
        {
            InitializeDatabase();
        }

        Debug.Log("=== ITEM DATABASE ===");
        Debug.Log($"Total Items: {definitionLookup.Count}");

        foreach (var kvp in definitionLookup)
        {
            Debug.Log($"  - {kvp.Key} ({kvp.Value.displayName})");
        }
    }

    [ContextMenu("Validate Database")]
    private void ValidateDatabase()
    {
        if (definitionLookup == null)
        {
            InitializeDatabase();
        }

        int errors = 0;
        int warnings = 0;

        // Check for nulls
        int nullCount = 0;
        foreach (var def in itemDefinitions)
        {
            if (def == null) nullCount++;
        }

        if (nullCount > 0)
        {
            Debug.LogWarning($"⚠️ {nullCount} null entries in item array");
            warnings++;
        }

        // Check for missing IDs
        int missingIds = 0;
        foreach (var def in itemDefinitions)
        {
            if (def != null && string.IsNullOrEmpty(def.itemId))
            {
                Debug.LogError($"❌ Item '{def.name}' has no itemId");
                errors++;
                missingIds++;
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
                Debug.LogError($"❌ Duplicate itemId: '{kvp.Key}' ({kvp.Value} instances)");
                errors++;
            }
        }

        Debug.Log("=== DATABASE VALIDATION ===");
        if (errors == 0 && warnings == 0)
        {
            Debug.Log("✅ Database is valid!");
        }
        else
        {
            Debug.Log($"Errors: {errors}, Warnings: {warnings}");
        }
    }
#endif

    #endregion
}