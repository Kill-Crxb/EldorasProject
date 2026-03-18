using UnityEngine;
using System.Collections.Generic;
using NinjaGame.Stats;
using System;

/// <summary>
/// Global Stat Schema Manager (Singleton)
/// 
/// Updated with ManagerBrain integration (IGameManager)
/// 
/// Responsibilities:
/// - Load stat schemas once at game startup
/// - Provide schema definitions to all entities
/// - Act as single source of truth for "what stats exist"
/// - Validate cross-manager stat references
/// 
/// Priority: 0 (FIRST - all other managers depend on stats)
/// 
/// Phase 1.7b: System Consolidation + Manager Coordination
/// Created: January 09, 2026
/// Updated: January 11, 2026
/// </summary>
public class StatsManager : MonoBehaviour, IGameManager
{
    #region Singleton (Simplified)

    private static StatsManager instance;
    public static StatsManager Instance => instance;

    #endregion

    #region IGameManager Implementation

    public string ManagerName => "Stats Manager";
    public int InitializationPriority => 0; // FIRST - everything depends on stats
    public bool IsEnabled => enabled;
    public bool IsInitialized { get; private set; }

    public void Initialize()
    {
        if (IsInitialized) return;

        instance = this;

        LoadSchemas();

        IsInitialized = true;

        if (debugLogging)
        {
            Debug.Log($"[{ManagerName}] Initialized with {schemaCache.Count} schemas, {allStatIds.Count} total stats");
        }
    }

    public void LateInitialize()
    {
        // Final validation after all managers loaded
        ValidateAllSchemas();
    }

    public void Shutdown()
    {
        if (debugLogging)
            Debug.Log($"[{ManagerName}] Shutdown complete");
    }

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();

        if (globalSchemas == null || globalSchemas.Count == 0)
        {
            result.IsFatal = true;
            result.Errors.Add("No stat schemas configured");
            return result;
        }

        foreach (var schema in globalSchemas)
        {
            if (schema == null)
            {
                result.IsFatal = true;
                result.Errors.Add("Null schema in list");
                return result;
            }
        }

        result.Info.Add($"Loaded {globalSchemas.Count} stat schemas, {allStatIds.Count} total stats");
        return result;
    }

    #endregion

    #region Inspector Fields

    [Header("Schema Configuration")]
    [Tooltip("All stat schemas to load at startup. These define what stats exist in the game.")]
    [SerializeField] private List<StatSchema> globalSchemas = new List<StatSchema>();

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region Private Fields

    // Schema cache for fast lookup
    private Dictionary<string, StatSchema> schemaCache = new Dictionary<string, StatSchema>();

    // All stat definitions across all schemas (for quick "does this stat exist?" checks)
    private HashSet<string> allStatIds = new HashSet<string>();

    // StatHandle support (fast lookups)
    private Dictionary<int, StatDefinition> handleLookup = new Dictionary<int, StatDefinition>();
    private Dictionary<int, string> handleToString = new Dictionary<int, string>();

    #endregion

    #region Initialization & Coordination

    /// <summary>
    /// Load all schemas from inspector list into cache
    /// </summary>
    private void LoadSchemas()
    {
        schemaCache.Clear();
        allStatIds.Clear();

        if (globalSchemas == null || globalSchemas.Count == 0)
        {
            Debug.LogWarning("[StatsManager] No schemas assigned! Assign StatSchema ScriptableObjects in the inspector.");
            return;
        }

        int loadedCount = 0;
        int totalStats = 0;

        foreach (var schema in globalSchemas)
        {
            if (schema == null)
            {
                Debug.LogWarning("[StatsManager] Null schema in list, skipping.");
                continue;
            }

            // Use asset name as schema ID (e.g., "RPGCoreStats")
            string schemaId = schema.name;

            // Check for duplicate schema IDs
            if (schemaCache.ContainsKey(schemaId))
            {
                Debug.LogError($"[StatsManager] Duplicate schema ID '{schemaId}'! Each schema must have a unique name.");
                continue;
            }

            // Cache the schema
            schemaCache[schemaId] = schema;
            loadedCount++;

            // Cache all stat IDs for quick existence checks
            foreach (var statDef in schema.stats)
            {
                if (allStatIds.Contains(statDef.statId))
                {
                    Debug.LogWarning($"[StatsManager] Duplicate stat ID '{statDef.statId}' found in schema '{schemaId}'. This may cause conflicts.");
                }

                allStatIds.Add(statDef.statId);
                totalStats++;
            }

            if (debugLogging)
                Debug.Log($"[StatsManager] Loaded schema: {schemaId} ({schema.stats.Count} stats)");
        }

        if (debugLogging)
        {
            Debug.Log($"[StatsManager] Schema loading complete:");
            Debug.Log($"  - Schemas loaded: {loadedCount}");
            Debug.Log($"  - Total stats: {totalStats}");
        }

        // Build handle lookup cache
        BuildHandleLookup();
    }

    /// <summary>
    /// Validate all schemas (called in LateInitialize)
    /// </summary>
    private void ValidateAllSchemas()
    {
        // Note: StatSchema.ValidateFormulas() is private, so we can't call it here
        // Individual schemas validate themselves when loaded

        if (debugLogging)
        {
            Debug.Log($"[{ManagerName}] Schema validation complete - {schemaCache.Count} schemas loaded");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get a schema by its unique ID
    /// </summary>
    /// <param name="schemaId">The schema's unique identifier (e.g., "RPGCoreStats")</param>
    /// <returns>The schema, or null if not found</returns>
    public StatSchema GetSchema(string schemaId)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[StatsManager] Not initialized! ManagerBrain should call Initialize().");
            return null;
        }

        if (string.IsNullOrEmpty(schemaId))
        {
            Debug.LogError("[StatsManager] Cannot get schema: schemaId is null or empty.");
            return null;
        }

        if (schemaCache.TryGetValue(schemaId, out var schema))
        {
            return schema;
        }

        if (debugLogging)
            Debug.LogWarning($"[StatsManager] Schema '{schemaId}' not found. Available schemas: {string.Join(", ", schemaCache.Keys)}");

        return null;
    }

    /// <summary>
    /// Get all loaded schemas
    /// </summary>
    public List<StatSchema> GetAllSchemas()
    {
        return new List<StatSchema>(schemaCache.Values);
    }

    /// <summary>
    /// COORDINATION: Check if a stat ID exists in any schema
    /// Used by other managers to validate stat references
    /// </summary>
    /// <param name="statId">Full stat ID (e.g., "character.strength")</param>
    /// <returns>True if the stat exists</returns>
    public bool HasStat(string statId)
    {
        if (!IsInitialized)
        {
            Debug.LogError("[StatsManager] Not initialized! Cannot check stat existence.");
            return false;
        }

        if (string.IsNullOrEmpty(statId))
            return false;

        return allStatIds.Contains(statId);
    }

    /// <summary>
    /// Check if a stat ID exists (alias for backward compatibility)
    /// </summary>
    public bool StatExists(string statId)
    {
        return HasStat(statId);
    }

    /// <summary>
    /// Check if a schema exists
    /// </summary>
    public bool HasSchema(string schemaId)
    {
        if (!IsInitialized)
            return false;

        return schemaCache.ContainsKey(schemaId);
    }

    /// <summary>
    /// Get list of all stat IDs across all schemas
    /// </summary>
    public HashSet<string> GetAllStatIds()
    {
        return new HashSet<string>(allStatIds);
    }

    /// <summary>
    /// Get all schema IDs (for debugging)
    /// </summary>
    public List<string> GetSchemaIds()
    {
        if (!IsInitialized)
        {
            Debug.LogError("[StatsManager] Not initialized! Cannot get schema IDs.");
            return new List<string>();
        }

        return new List<string>(schemaCache.Keys);
    }

    /// <summary>
    /// STATHANDLE SUPPORT: Resolve stat ID string to fast handle
    /// Call once at initialization, cache the handle
    /// </summary>
    public StatHandle Resolve(string statId)
    {
        if (string.IsNullOrEmpty(statId))
        {
            Debug.LogError("[StatsManager] Cannot resolve null/empty stat ID");
            return StatHandle.Invalid;
        }

        if (!HasStat(statId))
        {
            Debug.LogError($"[StatsManager] Cannot resolve unknown stat: {statId}");
            return StatHandle.Invalid;
        }

        int id = GetDeterministicHash(statId);
        return new StatHandle(id);
    }

    /// <summary>
    /// Get stat ID from handle (for debugging and StatEngine lookups)
    /// </summary>
    public string GetStatIdByHandle(StatHandle handle)
    {
        if (!handle.IsValid)
            return "<invalid>";

        if (handleToString.TryGetValue(handle.Id, out string statId))
            return statId;

        return "<unknown>";
    }

    /// <summary>
    /// Deterministic hash function (FNV-1a algorithm)
    /// Guarantees same hash on all platforms/builds
    /// </summary>
    private int GetDeterministicHash(string str)
    {
        unchecked
        {
            const uint FNV_PRIME = 16777619;
            const uint FNV_OFFSET = 2166136261;

            uint hash = FNV_OFFSET;
            foreach (char c in str)
            {
                hash ^= c;
                hash *= FNV_PRIME;
            }
            return (int)hash;
        }
    }

    /// <summary>
    /// Build handle lookup cache
    /// Called after loading schemas
    /// </summary>
    private void BuildHandleLookup()
    {
        handleLookup.Clear();
        handleToString.Clear();

        foreach (var statId in allStatIds)
        {
            int id = GetDeterministicHash(statId);

            // Find the stat definition
            foreach (var schema in schemaCache.Values)
            {
                var stat = schema.stats.Find(s => s.statId == statId);
                if (stat != null)
                {
                    handleLookup[id] = stat;
                    handleToString[id] = statId;
                    break;
                }
            }
        }

        if (debugLogging)
        {
            Debug.Log($"[StatsManager] Built handle lookup: {handleLookup.Count} stats");
        }
    }

    #endregion

    #region Hot-Reload Support

    /// <summary>
    /// Reload all schemas from disk (for hot-reloading during development)
    /// </summary>
    [ContextMenu("Hot Reload Schemas")]
    public void HotReloadSchemas()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StatsManager] Hot reload only works in Play Mode");
            return;
        }

        Debug.Log("[StatsManager] Hot reloading schemas...");
        LoadSchemas();
        Debug.Log("[StatsManager] Hot reload complete");
    }

    #endregion

    #region Debug Utilities

    [ContextMenu("Print All Schemas")]
    private void PrintAllSchemas()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[StatsManager] Not initialized yet");
            return;
        }

        Debug.Log("=== StatsManager: All Schemas ===");
        foreach (var kvp in schemaCache)
        {
            Debug.Log($"Schema: {kvp.Key} ({kvp.Value.stats.Count} stats)");
        }
        Debug.Log($"Total Stats: {allStatIds.Count}");
    }

    [ContextMenu("Print All Stat IDs")]
    private void PrintAllStatIds()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[StatsManager] Not initialized yet");
            return;
        }

        Debug.Log("=== StatsManager: All Stat IDs ===");
        foreach (var statId in allStatIds)
        {
            Debug.Log($"  - {statId}");
        }
        Debug.Log($"Total: {allStatIds.Count} stats");
    }

    #endregion
}