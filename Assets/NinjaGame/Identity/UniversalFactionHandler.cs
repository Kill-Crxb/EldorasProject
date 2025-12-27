using UnityEngine;
using RPG.Factions;

/// <summary>
/// UNIVERSAL Faction Handler - works for ALL entity types (Player, NPC, Object)
/// Manages faction membership and relationship queries for any entity.
/// 
/// Replaces PlayerFactionHandler and FactionAffiliationHandler with single unified handler.
/// </summary>
public class UniversalFactionHandler : MonoBehaviour, IIdentityHandler
{
    [Header("Handler Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Faction Membership")]
    [Tooltip("Which faction does this entity belong to?")]
    [SerializeField] private FactionType affiliatedFaction = FactionType.None;

    [Header("Behavior Settings")]
    [Tooltip("Can this entity change factions? (Usually false for players/NPCs, true for convertible entities)")]
    [SerializeField] private bool canChangeFaction = false;

    [Tooltip("Automatically attack hostile factions on sight?")]
    [SerializeField] private bool aggressiveToHostileFactions = true;

    [Tooltip("Will assist allied faction members in combat?")]
    [SerializeField] private bool assistsAlliedFactions = false;

    [Tooltip("Will defend faction members when they're attacked?")]
    [SerializeField] private bool defendsFactionMembers = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Internal state
    private IdentitySystem parent;

    #region Properties (Public API)

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    /// <summary>
    /// Which faction does this entity belong to?
    /// </summary>
    public FactionType AffiliatedFaction => affiliatedFaction;

    /// <summary>
    /// Should this entity attack hostile factions automatically?
    /// </summary>
    public bool AggressiveToHostile => aggressiveToHostileFactions;

    /// <summary>
    /// Should this entity assist allied faction members?
    /// </summary>
    public bool AssistsAllies => assistsAlliedFactions;

    /// <summary>
    /// Should this entity defend faction members?
    /// </summary>
    public bool DefendsMembers => defendsFactionMembers;

    #endregion

    #region IIdentityHandler Implementation

    public void Initialize(IdentitySystem parentSystem)
    {
        parent = parentSystem;

        if (showDebugLogs)
        {
            string entityName = parent.GetEntityName();
            string factionName = GetFactionName();
            Debug.Log($"[FactionHandler] {entityName} is a member of: {factionName}");
        }
    }

    public void UpdateHandler()
    {
        // No per-frame updates needed for static faction membership
    }

    public string GetHandlerSaveData()
    {
        var saveData = new FactionSaveData
        {
            affiliatedFaction = affiliatedFaction,
            aggressiveToHostile = aggressiveToHostileFactions,
            assistsAllies = assistsAlliedFactions,
            defendsMembers = defendsFactionMembers
        };
        return JsonUtility.ToJson(saveData);
    }

    public void LoadHandlerData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var saveData = JsonUtility.FromJson<FactionSaveData>(json);

        affiliatedFaction = saveData.affiliatedFaction;
        aggressiveToHostileFactions = saveData.aggressiveToHostile;
        assistsAlliedFactions = saveData.assistsAllies;
        defendsFactionMembers = saveData.defendsMembers;
    }

    public void ResetHandler()
    {
        affiliatedFaction = FactionType.None;
        aggressiveToHostileFactions = true;
        assistsAlliedFactions = false;
        defendsFactionMembers = true;
    }

    #endregion

    #region Core API

    /// <summary>
    /// Get current faction
    /// </summary>
    public FactionType GetFaction() => affiliatedFaction;

    /// <summary>
    /// Change faction (if allowed)
    /// </summary>
    public void SetFaction(FactionType newFaction)
    {
        if (!canChangeFaction && affiliatedFaction != FactionType.None)
        {
            Debug.LogWarning($"[FactionHandler] {parent.GetEntityName()}: Faction changes disabled");
            return;
        }

        FactionType oldFaction = affiliatedFaction;
        affiliatedFaction = newFaction;

        if (showDebugLogs)
        {
            Debug.Log($"[FactionHandler] {parent.GetEntityName()}: {oldFaction} → {newFaction}");
        }
    }

    /// <summary>
    /// Get faction name (human-readable)
    /// </summary>
    public string GetFactionName()
    {
        if (FactionManager.Instance != null)
        {
            return FactionManager.GetFactionName(affiliatedFaction);
        }
        return affiliatedFaction.ToString();
    }

    #endregion

    #region Relationship Queries (Delegates to FactionManager)

    /// <summary>
    /// Get relationship with another faction
    /// </summary>
    public FactionRelationship GetRelationshipWith(FactionType otherFaction)
    {
        return FactionManager.GetRelationship(affiliatedFaction, otherFaction);
    }

    /// <summary>
    /// Is this entity hostile to another faction?
    /// </summary>
    public bool IsHostileTo(FactionType otherFaction)
    {
        return GetRelationshipWith(otherFaction) == FactionRelationship.Hostile;
    }

    /// <summary>
    /// Is this entity friendly with another faction?
    /// </summary>
    public bool IsFriendlyWith(FactionType otherFaction)
    {
        return GetRelationshipWith(otherFaction) == FactionRelationship.Friendly;
    }

    /// <summary>
    /// Is this entity neutral to another faction?
    /// </summary>
    public bool IsNeutralTo(FactionType otherFaction)
    {
        return GetRelationshipWith(otherFaction) == FactionRelationship.Neutral;
    }

    #endregion

    #region AI Helper Methods

    /// <summary>
    /// Should this entity attack the other faction on sight?
    /// </summary>
    public bool ShouldAttackOnSight(FactionType otherFaction)
    {
        return aggressiveToHostileFactions && IsHostileTo(otherFaction);
    }

    /// <summary>
    /// Should this entity assist members of the other faction?
    /// </summary>
    public bool ShouldAssist(FactionType otherFaction)
    {
        return assistsAlliedFactions && IsFriendlyWith(otherFaction);
    }

    /// <summary>
    /// Should this entity defend members of the other faction?
    /// </summary>
    public bool ShouldDefend(FactionType otherFaction)
    {
        return defendsFactionMembers && (otherFaction == affiliatedFaction);
    }

    /// <summary>
    /// Should NPC be hostile to this target? (AI targeting check)
    /// </summary>
    public bool ShouldNPCBeHostile(UniversalFactionHandler targetFaction)
    {
        if (targetFaction == null) return false;

        FactionRelationship relationship = GetRelationshipWith(targetFaction.AffiliatedFaction);
        return relationship == FactionRelationship.Hostile && aggressiveToHostileFactions;
    }

    #endregion
}

#region Save Data Structure

[System.Serializable]
public class FactionSaveData
{
    public FactionType affiliatedFaction;
    public bool aggressiveToHostile;
    public bool assistsAllies;
    public bool defendsMembers;
}

#endregion