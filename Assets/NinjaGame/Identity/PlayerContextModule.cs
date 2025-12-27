using UnityEngine;
using RPG.Factions;

/// <summary>
/// Player Context Module - gathers player-specific identity data
/// 
/// Responsibilities:
/// - Track playtime
/// - Set default player faction
/// - Future: Achievement tracking, playstyle analysis, level sync with separate stat system
/// </summary>
public class PlayerContextModule : MonoBehaviour, IContextModule
{
    [Header("Player Settings")]
    [SerializeField] private bool trackAchievements = false; // Future feature

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // References
    private IdentitySystem parent;

    #region IContextModule Implementation

    public void Initialize(IdentitySystem parentSystem)
    {
        parent = parentSystem;

        // Set entity type to Player
        if (parent.Identity != null)
        {
            parent.Identity.Type = EntityType.Player;
        }

        // Set default player faction if not already set
        if (parent.Faction != null && parent.Faction.AffiliatedFaction == FactionType.None)
        {
            parent.Faction.SetFaction(FactionType.Player);
        }

        if (debugMode)
        {
            Debug.Log($"[PlayerContextModule] Initialized for {parent.GetEntityName()}");
        }
    }

    public void GatherContext()
    {
        // Update playtime (existence time for players = playtime)
        // This is already handled by IdentityHandler.UpdateHandler()

        // Future: Track achievements, analyze playstyle, etc.
        // Future: Sync level from external stat allocation system if needed
    }

    public string GetContextSaveData()
    {
        // Player context has no additional save data beyond core handlers
        // Playtime and level are saved by IdentityHandler
        // Faction is saved by FactionHandler
        return null;
    }

    public void LoadContextData(string json)
    {
        // No context-specific data to load
    }

    #endregion

    #region Player-Specific Helpers

    /// <summary>
    /// Get formatted playtime string
    /// </summary>
    public string GetFormattedPlaytime()
    {
        if (parent.Identity != null)
        {
            return parent.Identity.GetFormattedPlaytime();
        }
        return "0h 0m";
    }

    #endregion
}