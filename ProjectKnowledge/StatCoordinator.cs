using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central Stat Coordination System
/// 
/// Single-Player Mode:
/// - Tracks all entity stat systems
/// - Provides centralized queries
/// - Debug/profiling support
/// 
/// Multiplayer Mode (Future):
/// - Server: Authority for all stats
/// - Client: Routes to NetworkStatCache
/// - Syncs stat changes over network
/// 
/// Phase 1.8: Server-Ready Architecture
/// </summary>
public class StatCoordinator : MonoBehaviour
{
    #region Singleton

    public static StatCoordinator Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[StatCoordinator] Duplicate instance detected, destroying.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (debugLogging)
            Debug.Log("[StatCoordinator] Initialized");
    }

    #endregion

    #region Inspector Fields

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region Private Fields

    // All registered entities
    private Dictionary<string, IStatProvider> entityStats = new();

    // Track subscriptions for cleanup
    private Dictionary<string, System.Action<string, float, float>> statChangeHandlers = new();

    #endregion

    #region Properties

    /// <summary>
    /// Number of registered entities
    /// </summary>
    public int EntityCount => entityStats.Count;

    /// <summary>
    /// Is this manager running on a server? (Future multiplayer)
    /// </summary>
    public virtual bool IsServer => true;  // Single-player = always "server"

    #endregion

    #region Registration

    /// <summary>
    /// Register an entity's stat provider
    /// </summary>
    public void RegisterEntity(string entityId, IStatProvider statProvider)
    {
        if (string.IsNullOrEmpty(entityId))
        {
            Debug.LogError("[StatCoordinator] Cannot register entity with null/empty ID!");
            return;
        }

        if (statProvider == null)
        {
            Debug.LogError($"[StatCoordinator] Cannot register null stat provider for {entityId}!");
            return;
        }

        if (entityStats.ContainsKey(entityId))
        {
            Debug.LogWarning($"[StatCoordinator] Entity {entityId} already registered, replacing.");
            UnregisterEntity(entityId);
        }

        entityStats[entityId] = statProvider;

        // Subscribe to stat changes
        System.Action<string, float, float> handler = (statId, oldVal, newVal) =>
        {
            OnEntityStatChanged(entityId, statId, oldVal, newVal);
        };

        statChangeHandlers[entityId] = handler;
        statProvider.OnStatChanged += handler;

        if (debugLogging)
            Debug.Log($"[StatCoordinator] Registered entity: {entityId}");
    }

    /// <summary>
    /// Unregister an entity
    /// </summary>
    public void UnregisterEntity(string entityId)
    {
        if (!entityStats.TryGetValue(entityId, out var statProvider))
            return;

        // Unsubscribe from changes
        if (statChangeHandlers.TryGetValue(entityId, out var handler))
        {
            statProvider.OnStatChanged -= handler;
            statChangeHandlers.Remove(entityId);
        }

        entityStats.Remove(entityId);

        if (debugLogging)
            Debug.Log($"[StatCoordinator] Unregistered entity: {entityId}");
    }

    #endregion

    #region Queries

    /// <summary>
    /// Get stat provider for an entity
    /// </summary>
    public IStatProvider GetEntityStats(string entityId)
    {
        return entityStats.TryGetValue(entityId, out var stats) ? stats : null;
    }

    /// <summary>
    /// Check if entity is registered
    /// </summary>
    public bool HasEntity(string entityId)
    {
        return entityStats.ContainsKey(entityId);
    }

    /// <summary>
    /// Get stat value for specific entity
    /// </summary>
    public float GetEntityStatValue(string entityId, string statId, float defaultValue = 0f)
    {
        var stats = GetEntityStats(entityId);
        return stats?.GetValue(statId, defaultValue) ?? defaultValue;
    }

    #endregion

    #region Event Handling

    /// <summary>
    /// Called when any entity's stat changes
    /// Override in multiplayer subclass for network sync
    /// </summary>
    protected virtual void OnEntityStatChanged(string entityId, string statId, float oldValue, float newValue)
    {
        // Single-player: Do nothing (or log for debugging)
        if (debugLogging)
            Debug.Log($"[StatCoordinator] {entityId}.{statId}: {oldValue} → {newValue}");

        // Multiplayer Server (Future):
        // if (IsServer)
        //     NetworkManager.SendStatUpdate(entityId, statId, newValue);
    }

    #endregion

    #region Debug Utilities

    [ContextMenu("Print Registered Entities")]
    private void DebugPrintEntities()
    {
        Debug.Log($"=== StatCoordinator: {entityStats.Count} Entities ===");
        foreach (var kvp in entityStats)
        {
            Debug.Log($"  - {kvp.Key}: {kvp.Value.GetType().Name}");
        }
    }

    [ContextMenu("Clear All Entities")]
    private void DebugClearAll()
    {
        var ids = new List<string>(entityStats.Keys);
        foreach (var id in ids)
        {
            UnregisterEntity(id);
        }
        Debug.Log("[StatCoordinator] Cleared all entities");
    }

    #endregion

    #region Cleanup

    void OnDestroy()
    {
        // Clean up all subscriptions
        var ids = new List<string>(entityStats.Keys);
        foreach (var id in ids)
        {
            UnregisterEntity(id);
        }

        if (Instance == this)
            Instance = null;
    }

    #endregion
}