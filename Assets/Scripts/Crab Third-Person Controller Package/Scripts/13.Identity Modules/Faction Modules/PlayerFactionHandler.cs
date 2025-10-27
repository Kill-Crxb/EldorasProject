using UnityEngine;
using RPG.Factions;

/// <summary>
/// Simple handler that stores which faction the player belongs to.
/// This is the player equivalent of FactionAffiliationHandler on NPCs.
/// 
/// NO REPUTATION SYSTEM - Just faction membership for aggression checks.
/// 
/// Architecture:
/// - Player: Has PlayerFactionHandler (belongs to ONE faction)
/// - NPC: Has FactionAffiliationHandler (belongs to ONE faction)
/// - FactionManager: Defines relationships between factions
/// - AIModule: Checks FactionManager.GetRelationship() for detection
/// </summary>
public class PlayerFactionHandler : MonoBehaviour, IPlayerInfoHandler
{
    [Header("Faction Membership")]
    [SerializeField] private FactionType playerFaction = FactionType.Player;

    [Header("Settings")]
    [SerializeField] private bool canChangeFaction = false;
    [Tooltip("Show faction change notifications")]
    [SerializeField] private bool showDebugLogs = false;

    // Reference
    private PlayerInfoModule parent;

    #region Properties

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Which faction does the player belong to?
    /// </summary>
    public FactionType PlayerFaction => playerFaction;

    #endregion

    #region IPlayerInfoHandler Implementation

    public void Initialize(PlayerInfoModule parentModule)
    {
        parent = parentModule;

        if (showDebugLogs)
        {
            string factionName = GetFactionName();
            Debug.Log($"[PlayerFactionHandler] Player is a member of: {factionName}");
        }
    }

    public void UpdateHandler()
    {
        // No per-frame updates needed for static faction membership
    }

    public string GetHandlerSaveData()
    {
        var saveData = new PlayerFactionSaveData
        {
            playerFaction = playerFaction
        };
        return JsonUtility.ToJson(saveData);
    }

    public void LoadHandlerData(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogWarning("[PlayerFactionHandler] No save data to load");
            return;
        }

        var saveData = JsonUtility.FromJson<PlayerFactionSaveData>(json);
        playerFaction = saveData.playerFaction;

        if (showDebugLogs)
        {
            Debug.Log($"[PlayerFactionHandler] Loaded faction: {GetFactionName()}");
        }
    }

    public void ResetHandler()
    {
        playerFaction = FactionType.Player;

        if (showDebugLogs)
        {
            Debug.Log("[PlayerFactionHandler] Reset to default faction: Player");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get the player's current faction
    /// </summary>
    public FactionType GetFaction()
    {
        return playerFaction;
    }

    /// <summary>
    /// Change the player's faction (if allowed)
    /// </summary>
    public void SetFaction(FactionType newFaction)
    {
        if (!canChangeFaction)
        {
            Debug.LogWarning("[PlayerFactionHandler] Faction changes are disabled");
            return;
        }

        FactionType oldFaction = playerFaction;
        playerFaction = newFaction;

        if (showDebugLogs)
        {
            Debug.Log($"<color=yellow>[Faction Change] {oldFaction} → {newFaction}</color>");
        }
    }

    /// <summary>
    /// Get user-friendly faction name from FactionManager
    /// </summary>
    public string GetFactionName()
    {
        if (FactionManager.Instance != null)
        {
            return FactionManager.Instance.GetFactionName(playerFaction);
        }
        return playerFaction.ToString();
    }

    /// <summary>
    /// Get the relationship between player faction and another faction
    /// </summary>
    public FactionRelationship GetRelationshipWith(FactionType otherFaction)
    {
        return FactionManager.GetRelationship(playerFaction, otherFaction);
    }

    /// <summary>
    /// Check if player is hostile to a faction
    /// </summary>
    public bool IsHostileTo(FactionType otherFaction)
    {
        return GetRelationshipWith(otherFaction) == FactionRelationship.Hostile;
    }

    /// <summary>
    /// Check if player is friendly with a faction
    /// </summary>
    public bool IsFriendlyWith(FactionType otherFaction)
    {
        return GetRelationshipWith(otherFaction) == FactionRelationship.Friendly;
    }

    /// <summary>
    /// Check if player is neutral with a faction
    /// </summary>
    public bool IsNeutralWith(FactionType otherFaction)
    {
        return GetRelationshipWith(otherFaction) == FactionRelationship.Neutral;
    }

    /// <summary>
    /// Check if NPC should be hostile to player based on faction
    /// Called by NPCs via FactionAffiliationHandler
    /// </summary>
    public bool ShouldNPCBeHostile(FactionType npcFaction)
    {
        return FactionManager.IsHostile(npcFaction, playerFaction);
    }

    #endregion

    #region Context Menu Testing

    [ContextMenu("Debug: Print Current Faction")]
    private void DebugPrintFaction()
    {
        Debug.Log($"=== Player Faction Info ===");
        Debug.Log($"Current Faction: {playerFaction}");
        Debug.Log($"Display Name: {GetFactionName()}");
        Debug.Log($"Can Change: {canChangeFaction}");
    }

    [ContextMenu("Debug: Print All Relationships")]
    private void DebugPrintRelationships()
    {
        Debug.Log($"=== {playerFaction} Faction Relationships ===");

        var allFactions = (FactionType[])System.Enum.GetValues(typeof(FactionType));

        foreach (var faction in allFactions)
        {
            if (faction == FactionType.None || faction == playerFaction)
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

    [ContextMenu("Test: Change to Elves (Friendly)")]
    private void TestChangeFriendly()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Test] Must be in Play Mode");
            return;
        }

        bool oldSetting = canChangeFaction;
        canChangeFaction = true;

        SetFaction(FactionType.Elves);
        Debug.Log("<color=green>[Test] Changed to Elves faction (Friendly with most factions)</color>");

        canChangeFaction = oldSetting;
    }

    [ContextMenu("Test: Change to Warlocks (Hostile)")]
    private void TestChangeHostile()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Test] Must be in Play Mode");
            return;
        }

        bool oldSetting = canChangeFaction;
        canChangeFaction = true;

        SetFaction(FactionType.Warlocks);
        Debug.Log("<color=red>[Test] Changed to Warlocks faction (Hostile to most factions)</color>");

        canChangeFaction = oldSetting;
    }

    [ContextMenu("Test: Reset to Player Faction")]
    private void TestResetFaction()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Test] Must be in Play Mode");
            return;
        }

        bool oldSetting = canChangeFaction;
        canChangeFaction = true;

        SetFaction(FactionType.Player);
        Debug.Log("[Test] Reset to Player faction");

        canChangeFaction = oldSetting;
    }

    #endregion
}

#region Save Data Structure

[System.Serializable]
public class PlayerFactionSaveData
{
    public FactionType playerFaction;
}

#endregion