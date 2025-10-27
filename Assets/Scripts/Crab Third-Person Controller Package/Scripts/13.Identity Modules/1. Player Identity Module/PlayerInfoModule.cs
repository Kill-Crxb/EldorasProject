using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using RPG.Factions;

/// <summary>
/// Central coordinator for player identity and world interaction systems.
/// Manages all IPlayerInfoHandler sub-modules (Faction, Identity, Quest, Location, NPC, etc.)
/// 
/// SIMPLIFIED VERSION: Uses PlayerFactionHandler (simple faction membership)
/// instead of FactionReputationHandler (complex reputation system)
/// 
/// Architecture:
/// - Implements IPlayerModule (integrates with ControllerBrain)
/// - Implements ISaveable (integrates with SaveSystemModule)
/// - Auto-discovers child handlers via GetComponentsInChildren
/// - Provides convenience API for accessing handler data
/// 
/// Follows same pattern as:
/// - MeleeModule → AttackModule, ComboModule, ActiveDefenseModule
/// </summary>
public class PlayerInfoModule : MonoBehaviour, IPlayerModule, ISaveable
{
    #region Serialized Fields

    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool showDebugInfo = false;

    [Header("Handler References")]
    [SerializeField] private PlayerFactionHandler factionHandler;  // ← SIMPLIFIED!
    [SerializeField] private IdentityHandler identityHandler;
    // TODO: Add other handlers as they're implemented:
    // [SerializeField] private QuestProgressHandler questHandler;
    // [SerializeField] private LocationDiscoveryHandler locationHandler;
    // [SerializeField] private NPCRelationshipHandler npcHandler;
    // [SerializeField] private WorldUnlocksHandler unlocksHandler;

    [Header("Network Settings (Future)")]
    [SerializeField] private bool isNetworked = false;
    [SerializeField] private bool isLocalPlayer = true;

    #endregion

    #region Private Fields

    private ControllerBrain brain;
    private List<IPlayerInfoHandler> handlers = new List<IPlayerInfoHandler>();
    private bool isInitialized = false;

    #endregion

    #region Properties

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public ControllerBrain Brain => brain;
    public PlayerFactionHandler FactionHandler => factionHandler;
    public IdentityHandler IdentityHandler => identityHandler;
    public int HandlerCount => handlers.Count;

    #endregion

    #region IPlayerModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Auto-discover all IPlayerInfoHandler components in children
        DiscoverHandlers();

        // Initialize each handler
        foreach (var handler in handlers)
        {
            try
            {
                handler.Initialize(this);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerInfoModule] Failed to initialize handler: {e.Message}");
            }
        }

        isInitialized = true;

        if (showDebugInfo)
        {
            Debug.Log($"[PlayerInfoModule] Initialized with {handlers.Count} handlers");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled || !isInitialized) return;

        // Update all handlers
        foreach (var handler in handlers)
        {
            try
            {
                handler.UpdateHandler();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerInfoModule] Handler update error: {e.Message}");
            }
        }
    }

    #endregion

    #region ISaveable Implementation

    public string GetSaveId()
    {
        return "PlayerInfo";
    }

    public string GetSaveData()
    {
        var saveData = new PlayerInfoSaveData
        {
            version = GetSaveVersion(),
            handlerData = new Dictionary<string, string>()
        };

        // Collect save data from all handlers
        foreach (var handler in handlers)
        {
            try
            {
                string handlerType = handler.GetType().Name;
                string handlerJson = handler.GetHandlerSaveData();
                saveData.handlerData[handlerType] = handlerJson;
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerInfoModule] Failed to save handler data: {e.Message}");
            }
        }

        return JsonUtility.ToJson(saveData, true);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[PlayerInfoModule] No save data to load");
            return;
        }

        try
        {
            var saveData = JsonUtility.FromJson<PlayerInfoSaveData>(json);

            // Load data into each handler
            foreach (var handler in handlers)
            {
                string handlerType = handler.GetType().Name;

                if (saveData.handlerData.TryGetValue(handlerType, out string handlerJson))
                {
                    try
                    {
                        handler.LoadHandlerData(handlerJson);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[PlayerInfoModule] Failed to load {handlerType}: {e.Message}");
                    }
                }
                else if (showDebugInfo)
                {
                    Debug.LogWarning($"[PlayerInfoModule] No save data found for {handlerType}");
                }
            }

            if (showDebugInfo)
            {
                Debug.Log($"[PlayerInfoModule] Loaded data for {saveData.handlerData.Count} handlers");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerInfoModule] Failed to load save data: {e.Message}");
        }
    }

    public int GetSaveVersion()
    {
        return 2; // Incremented from v1 (FactionReputationHandler) to v2 (PlayerFactionHandler)
    }

    #endregion

    #region Convenience API - Identity

    /// <summary>
    /// Get character name
    /// </summary>
    public string GetCharacterName()
    {
        return identityHandler != null ? identityHandler.CharacterName : "Unknown";
    }

    /// <summary>
    /// Get character ID
    /// </summary>
    public string GetCharacterId()
    {
        return identityHandler != null ? identityHandler.CharacterId : "";
    }

    /// <summary>
    /// Get character level
    /// </summary>
    public int GetCharacterLevel()
    {
        return identityHandler != null ? identityHandler.Level : 1;
    }

    /// <summary>
    /// Get total playtime in seconds
    /// </summary>
    public float GetTotalPlaytime()
    {
        return identityHandler != null ? identityHandler.TotalPlaytime : 0f;
    }

    #endregion

    #region Convenience API - Faction (SIMPLIFIED)

    /// <summary>
    /// Get player's current faction
    /// </summary>
    public FactionType GetPlayerFaction()
    {
        return factionHandler != null ? factionHandler.GetFaction() : FactionType.Player;
    }

    /// <summary>
    /// Get player's faction name (display)
    /// </summary>
    public string GetPlayerFactionName()
    {
        return factionHandler != null ? factionHandler.GetFactionName() : "Player";
    }

    /// <summary>
    /// Set player's faction (if allowed)
    /// </summary>
    public void SetPlayerFaction(FactionType newFaction)
    {
        if (factionHandler != null)
        {
            factionHandler.SetFaction(newFaction);
        }
        else
        {
            Debug.LogWarning("[PlayerInfoModule] PlayerFactionHandler not found");
        }
    }

    /// <summary>
    /// Check relationship with another faction
    /// </summary>
    public FactionRelationship GetRelationshipWith(FactionType otherFaction)
    {
        return factionHandler != null
            ? factionHandler.GetRelationshipWith(otherFaction)
            : FactionRelationship.Neutral;
    }

    /// <summary>
    /// Check if player is hostile to a faction
    /// </summary>
    public bool IsHostileTo(FactionType otherFaction)
    {
        return factionHandler != null && factionHandler.IsHostileTo(otherFaction);
    }

    /// <summary>
    /// Check if player is friendly with a faction
    /// </summary>
    public bool IsFriendlyWith(FactionType otherFaction)
    {
        return factionHandler != null && factionHandler.IsFriendlyWith(otherFaction);
    }

    /// <summary>
    /// Check if NPC should be hostile to player
    /// Called by NPCs checking player faction
    /// </summary>
    public bool ShouldNPCBeHostile(FactionType npcFaction)
    {
        return factionHandler != null && factionHandler.ShouldNPCBeHostile(npcFaction);
    }

    #endregion

    #region Convenience API - Future Handlers

    // TODO: Add convenience methods for other handlers as they're implemented

    // Example for QuestHandler:
    // public bool HasCompletedQuest(string questId) => questHandler?.IsQuestCompleted(questId) ?? false;
    // public void AcceptQuest(string questId) => questHandler?.AcceptQuest(questId);

    // Example for LocationHandler:
    // public bool HasDiscoveredLocation(string locationId) => locationHandler?.IsDiscovered(locationId) ?? false;
    // public void DiscoverLocation(string locationId) => locationHandler?.DiscoverLocation(locationId);

    #endregion

    #region Private Methods

    private void DiscoverHandlers()
    {
        handlers.Clear();

        // Find all IPlayerInfoHandler components in children
        var foundHandlers = GetComponentsInChildren<IPlayerInfoHandler>();

        foreach (var handler in foundHandlers)
        {
            handlers.Add(handler);
        }

        // Cache specific handler references for convenience
        factionHandler = GetComponentInChildren<PlayerFactionHandler>();
        identityHandler = GetComponentInChildren<IdentityHandler>();

        if (showDebugInfo)
        {
            Debug.Log($"[PlayerInfoModule] Discovered {handlers.Count} handlers:");
            foreach (var handler in handlers)
            {
                Debug.Log($"  - {handler.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// Reset all handlers to default state
    /// </summary>
    public void ResetAllHandlers()
    {
        foreach (var handler in handlers)
        {
            try
            {
                handler.ResetHandler();
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayerInfoModule] Failed to reset handler: {e.Message}");
            }
        }

        if (showDebugInfo)
        {
            Debug.Log("[PlayerInfoModule] Reset all handlers");
        }
    }

    #endregion

    #region Context Menu Testing

    [ContextMenu("Debug: Print Handler Count")]
    private void DebugPrintHandlerCount()
    {
        Debug.Log($"[PlayerInfoModule] Active Handlers: {handlers.Count}");
        foreach (var handler in handlers)
        {
            Debug.Log($"  - {handler.GetType().Name}");
        }
    }

    [ContextMenu("Debug: Test Save Data")]
    private void DebugTestSaveData()
    {
        string saveJson = GetSaveData();
        Debug.Log($"[PlayerInfoModule] Save Data:\n{saveJson}");
    }

    [ContextMenu("Debug: Reset All Handlers")]
    private void DebugResetHandlers()
    {
        ResetAllHandlers();
    }

    [ContextMenu("Debug: Print Faction Info")]
    private void DebugPrintFactionInfo()
    {
        if (factionHandler == null)
        {
            Debug.LogWarning("[Debug] FactionHandler not found");
            return;
        }

        Debug.Log($"=== Player Faction Info ===");
        Debug.Log($"Faction: {GetPlayerFaction()}");
        Debug.Log($"Display Name: {GetPlayerFactionName()}");

        // Print relationships
        var allFactions = (FactionType[])System.Enum.GetValues(typeof(FactionType));
        Debug.Log("\n=== Relationships ===");
        foreach (var faction in allFactions)
        {
            if (faction == FactionType.None || faction == GetPlayerFaction())
                continue;

            FactionRelationship rel = GetRelationshipWith(faction);
            string colorTag = rel switch
            {
                FactionRelationship.Friendly => "<color=green>",
                FactionRelationship.Hostile => "<color=red>",
                _ => "<color=yellow>"
            };

            Debug.Log($"{colorTag}{faction}: {rel}</color>");
        }
    }

    #endregion
}

#region Save Data Structure

/// <summary>
/// Container for all PlayerInfo save data
/// </summary>
[System.Serializable]
public class PlayerInfoSaveData
{
    public int version;
    public Dictionary<string, string> handlerData = new Dictionary<string, string>();
}

#endregion