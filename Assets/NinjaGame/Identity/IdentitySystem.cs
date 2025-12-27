using UnityEngine;
using System.Collections.Generic;
using RPG.Factions;
// Note: ModelModule is in CrabThirdPerson.Character namespace, but we avoid importing
// that namespace to prevent FactionType ambiguity (both namespaces have FactionType)

/// <summary>
/// UNIVERSAL Identity System - works for ALL entity types (Player, NPC, Object)
/// 
/// Coordinates identity handlers and context modules to provide unified identity management.
/// Replaces PlayerInfoModule and NPCModule with single universal system.
/// 
/// Architecture:
/// - Core Handlers (universal): IdentityHandler, FactionHandler, ModelHandler, SaveHandler
/// - Context Module (entity-specific): PlayerContextModule, NPCContextModule, ObjectContextModule
/// </summary>
public class IdentitySystem : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool debugMode = false;

    [Header("Core Handlers - Universal")]
    [Tooltip("Manages entity name, ID, level, and existence time")]
    [SerializeField] private UniversalIdentityHandler identityHandler;

    [Tooltip("Manages faction membership and relationships")]
    [SerializeField] private UniversalFactionHandler factionHandler;

    [Tooltip("Manages visual model and sockets (assign your ModelModule if you have one)")]
    [SerializeField] private MonoBehaviour modelHandler; // ModelModule

    [Tooltip("Manages save/load (optional for non-persistent entities)")]
    [SerializeField] private UniversalSaveHandler saveHandler;

    [Header("Context Module - Entity-Specific")]
    [Tooltip("Assign ONE: PlayerContextModule, NPCContextModule, or ObjectContextModule")]
    [SerializeField] private MonoBehaviour contextModule; // IContextModule

    // Cached references
    private ControllerBrain brain;
    private IContextModule cachedContext;
    private List<IIdentityHandler> handlers = new List<IIdentityHandler>();

    #region Properties (Public API)

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public ControllerBrain Brain => brain;

    // Handler accessors
    public UniversalIdentityHandler Identity => identityHandler;
    public UniversalFactionHandler Faction => factionHandler;
    public MonoBehaviour Model => modelHandler; // Cast to ModelModule if needed
    public UniversalSaveHandler Save => saveHandler;
    public IContextModule Context => cachedContext;

    // Entity type detection (based on context module)
    public bool IsPlayer => cachedContext is PlayerContextModule;
    public bool IsNPC => cachedContext is NPCContextModule;
    public bool IsObject => cachedContext is ObjectContextModule;

    #endregion

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Discover all identity handlers
        DiscoverHandlers();

        // Cache context module interface
        cachedContext = contextModule as IContextModule;
        if (cachedContext == null && contextModule != null)
        {
            Debug.LogError($"[IdentitySystem] Context module {contextModule.GetType().Name} " +
                          "does not implement IContextModule!", this);
            return;
        }

        // Initialize all handlers first (they set up their state)
        foreach (var handler in handlers)
        {
            if (handler != null && handler.IsEnabled)
            {
                handler.Initialize(this);
            }
        }

        // Then initialize context module (it configures handlers based on entity type)
        cachedContext?.Initialize(this);

        if (debugMode)
        {
            string entityType = IsPlayer ? "Player" : IsNPC ? "NPC" : IsObject ? "Object" : "Unknown";
            Debug.Log($"[IdentitySystem] Initialized as {entityType}: {GetEntityName()}");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        // Update all handlers (core maintenance)
        foreach (var handler in handlers)
        {
            if (handler != null && handler.IsEnabled)
            {
                handler.UpdateHandler();
            }
        }

        // Gather entity-specific context (updates handlers with fresh data)
        cachedContext?.GatherContext();
    }

    #endregion

    #region Handler Discovery

    private void DiscoverHandlers()
    {
        handlers.Clear();

        // Add explicitly assigned handlers
        if (identityHandler != null) handlers.Add(identityHandler);
        if (factionHandler != null) handlers.Add(factionHandler);
        if (saveHandler != null) handlers.Add(saveHandler);

        // ModelModule doesn't implement IIdentityHandler (it's its own thing)
        // But we still want to initialize it if it implements IBrainModule
        if (modelHandler != null && modelHandler is IBrainModule modelBrain)
        {
            modelBrain.Initialize(brain);
        }

        // Also search for any additional IIdentityHandler components in children
        var foundHandlers = GetComponentsInChildren<IIdentityHandler>();
        foreach (var handler in foundHandlers)
        {
            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }

        if (debugMode)
        {
            Debug.Log($"[IdentitySystem] Discovered {handlers.Count} handlers");
        }
    }

    #endregion

    #region Convenience API (Delegates to Handlers)

    /// <summary>
    /// Get entity name (universal - works for all entity types)
    /// </summary>
    public string GetEntityName() => identityHandler?.DisplayName ?? "Unknown";

    /// <summary>
    /// Get entity unique ID
    /// </summary>
    public new string GetEntityId() => identityHandler?.EntityId ?? "";

    /// <summary>
    /// Get entity level (0 for objects)
    /// </summary>
    public int GetLevel() => identityHandler?.Level ?? 0;

    /// <summary>
    /// Get entity type (Player, NPC, Prop, etc.)
    /// </summary>
    public EntityType GetEntityType() => identityHandler?.Type ?? EntityType.Entity;

    /// <summary>
    /// Get existence time (playtime for players, lifetime for NPCs/objects)
    /// </summary>
    public float GetExistenceTime() => identityHandler?.ExistenceTime ?? 0f;

    /// <summary>
    /// Get faction (universal - works for all entity types)
    /// </summary>
    public FactionType GetFaction() => factionHandler?.GetFaction() ?? FactionType.None;

    /// <summary>
    /// Is this entity hostile to another faction?
    /// </summary>
    public bool IsHostileTo(FactionType other) => factionHandler?.IsHostileTo(other) ?? false;

    /// <summary>
    /// Is this entity friendly with another faction?
    /// </summary>
    public bool IsFriendlyWith(FactionType other) => factionHandler?.IsFriendlyWith(other) ?? false;

    /// <summary>
    /// Get relationship with another faction
    /// </summary>
    public FactionRelationship GetRelationshipWith(FactionType other) =>
        factionHandler?.GetRelationshipWith(other) ?? FactionRelationship.Neutral;

    /// <summary>
    /// Get current visual model (if modelHandler is ModelModule)
    /// </summary>
    public GameObject GetCurrentModel()
    {
        // Use fully qualified type to avoid namespace ambiguity
        if (modelHandler is CrabThirdPerson.Character.ModelModule mm)
            return mm.CurrentModel;
        return null;
    }

    /// <summary>
    /// Get model ID (if modelHandler is ModelModule)
    /// </summary>
    public string GetModelId()
    {
        if (modelHandler is CrabThirdPerson.Character.ModelModule mm)
            return mm.CurrentModelId ?? "";
        return "";
    }

    /// <summary>
    /// Swap to different model (if modelHandler is ModelModule)
    /// </summary>
    public bool SwapModel(string modelId)
    {
        if (modelHandler is CrabThirdPerson.Character.ModelModule mm)
            return mm.SwapModel(modelId);
        return false;
    }

    #endregion

    #region Save/Load (Optional - only if SaveHandler present)

    /// <summary>
    /// Get complete save data for this entity
    /// </summary>
    public string GetSaveData()
    {
        if (saveHandler != null && saveHandler.IsPersistent)
        {
            return saveHandler.GetCompleteSaveData();
        }
        return null;
    }

    /// <summary>
    /// Load save data for this entity
    /// </summary>
    public void LoadSaveData(string json)
    {
        saveHandler?.LoadCompleteSaveData(json);
    }

    /// <summary>
    /// Get save ID for this entity
    /// </summary>
    public string GetSaveId() => saveHandler?.GetSaveId() ?? GetEntityId();

    #endregion

    #region Debug Helpers

    [ContextMenu("Debug: Print Identity Info")]
    private void DebugPrintInfo()
    {
        Debug.Log("=== IDENTITY SYSTEM INFO ===");
        Debug.Log($"Name: {GetEntityName()}");
        Debug.Log($"ID: {GetEntityId()}");
        Debug.Log($"Type: {GetEntityType()}");
        Debug.Log($"Level: {GetLevel()}");
        Debug.Log($"Faction: {GetFaction()}");
        Debug.Log($"Existence Time: {GetExistenceTime()}s");
        Debug.Log($"Is Player: {IsPlayer}");
        Debug.Log($"Is NPC: {IsNPC}");
        Debug.Log($"Is Object: {IsObject}");
        Debug.Log($"Context Module: {cachedContext?.GetType().Name ?? "None"}");
    }

    [ContextMenu("Debug: Test Save/Load")]
    private void DebugTestSaveLoad()
    {
        string saveData = GetSaveData();
        if (saveData != null)
        {
            Debug.Log($"[IdentitySystem] Save Data:\n{saveData}");
        }
        else
        {
            Debug.Log("[IdentitySystem] No save data (entity not persistent or no SaveHandler)");
        }
    }

    #endregion
}