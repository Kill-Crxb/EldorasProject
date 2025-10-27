using UnityEngine;
using RPG.Factions;

/// <summary>
/// Handles which faction this NPC belongs to.
/// This is the NPC equivalent of FactionReputationHandler in PlayerInfo.
/// 
/// Player: Tracks reputation WITH all factions
/// NPC: Declares membership IN one faction
/// 
/// PHASE 2 ENHANCEMENTS:
/// - Now uses FactionType enum instead of string IDs
/// - Integrates with FactionManager for relationship queries
/// - Provides nameplate color integration
/// - Safe conversion from NPCFaction to FactionType
/// </summary>
public class FactionAffiliationHandler : MonoBehaviour, INPCHandler
{
    [Header("Handler Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Faction Membership")]
    [SerializeField] private FactionType affiliatedFaction = FactionType.None;

    [Header("Behavior Settings")]
    [Tooltip("Attack players/NPCs from hostile factions on sight")]
    [SerializeField] private bool aggressiveToHostileFactions = true;

    [Tooltip("Help nearby allies in combat")]
    [SerializeField] private bool assistsAlliedFactions = false;

    [Tooltip("Defend other members of same faction")]
    [SerializeField] private bool defendsFactionMembers = true;

    [Header("Visual Indicator (Optional)")]
    [SerializeField] private bool showFactionIndicator = false;
    [SerializeField] private GameObject factionIndicatorPrefab;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // References
    private NPCModule parentNPC;
    private GameObject factionIndicator;

    // Properties
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public FactionType AffiliatedFaction => affiliatedFaction;
    public bool AggressiveToHostile => aggressiveToHostileFactions;
    public bool AssistsAllies => assistsAlliedFactions;
    public bool DefendsMembers => defendsFactionMembers;

    // ========================================
    // INPCHandler Implementation
    // ========================================

    public void Initialize(NPCModule parent)
    {
        parentNPC = parent;

        // Auto-set faction from archetype if not manually set
        if (affiliatedFaction == FactionType.None && parent.AppliedArchetype != null)
        {
            SetFactionFromArchetype(parent.AppliedArchetype);
        }

        // Set up visual indicator if enabled
        if (showFactionIndicator && factionIndicatorPrefab != null)
        {
            CreateFactionIndicator();
        }

        if (showDebugLogs)
        {
            Debug.Log($"[FactionAffiliationHandler] {parent.NPCName} affiliated with {affiliatedFaction}");
        }
    }

    public void UpdateHandler()
    {
        // Faction affiliation is typically static
        // No per-frame updates needed
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
        var saveData = JsonUtility.FromJson<FactionSaveData>(json);

        affiliatedFaction = saveData.affiliatedFaction;
        aggressiveToHostileFactions = saveData.aggressiveToHostile;
        assistsAlliedFactions = saveData.assistsAllies;
        defendsFactionMembers = saveData.defendsMembers;
    }

    public void ResetHandler()
    {
        // Reset to archetype defaults
        if (parentNPC.AppliedArchetype != null)
        {
            SetFactionFromArchetype(parentNPC.AppliedArchetype);
        }
    }

    // ========================================
    // Faction Configuration
    // ========================================

    /// <summary>
    /// Set faction from archetype configuration
    /// Converts NPCFaction enum to FactionType using string matching (safe approach)
    /// </summary>
    private void SetFactionFromArchetype(NPCArchetype archetype)
    {
        // Convert NPCFaction to FactionType using string matching (safe for any enum values)
        affiliatedFaction = ConvertNPCFactionToFactionType(archetype.faction);

        // Apply archetype behavior settings
        aggressiveToHostileFactions = archetype.aggressiveToHostileFactions;
        assistsAlliedFactions = archetype.assistsAlliedFactions;
        defendsFactionMembers = archetype.defendsFactionMembers;
    }

    /// <summary>
    /// Convert NPCFaction enum to FactionType enum using string matching
    /// This is safe even if enum values don't match exactly
    /// </summary>
    private FactionType ConvertNPCFactionToFactionType(NPCFaction npcFaction)
    {
        // Convert to string for comparison
        string factionString = npcFaction.ToString().ToLower();

        // Match common patterns
        if (factionString.Contains("wildlife") || factionString.Contains("beast"))
            return FactionType.Wildlife;

        if (factionString.Contains("human"))
            return FactionType.Humans;

        if (factionString.Contains("elf") || factionString.Contains("elves"))
            return FactionType.Elves;

        if (factionString.Contains("dwarf") || factionString.Contains("dwarves"))
            return FactionType.Dwarves;

        if (factionString.Contains("warlock"))
            return FactionType.Warlocks;

        if (factionString.Contains("undead") || factionString.Contains("skeleton") || factionString.Contains("zombie"))
            return FactionType.Undead;

        if (factionString.Contains("bandit") || factionString.Contains("raider"))
            return FactionType.Bandits;

        if (factionString.Contains("monster"))
            return FactionType.Monsters;

        // Default to Neutral for unknown factions
        if (showDebugLogs)
        {
            Debug.LogWarning($"[FactionAffiliationHandler] Unknown NPCFaction: {npcFaction}. Defaulting to Neutral.");
        }
        return FactionType.Neutral;
    }

    /// <summary>
    /// Manually set faction (for dynamic faction changes)
    /// </summary>
    public void SetFaction(FactionType newFaction)
    {
        affiliatedFaction = newFaction;

        if (showDebugLogs)
        {
            Debug.Log($"[FactionAffiliationHandler] Faction changed to {newFaction}");
        }
    }

    // ========================================
    // Relationship Queries (PHASE 2 ENHANCED)
    // ========================================

    /// <summary>
    /// Get the relationship between this NPC's faction and the player's faction
    /// </summary>
    public FactionRelationship GetRelationshipToPlayer()
    {
        return FactionManager.GetRelationship(affiliatedFaction, FactionType.Player);
    }

    /// <summary>
    /// Check if this NPC can interact with a player
    /// Based on player's reputation with this NPC's faction
    /// </summary>
    public bool CanInteractWithPlayer(PlayerInfoModule playerInfo)
    {
        // Wildlife/monsters can't interact
        if (affiliatedFaction == FactionType.Wildlife ||
            affiliatedFaction == FactionType.Monsters)
        {
            return false;
        }

        // Check if relationship is hostile
        FactionRelationship relationship = GetRelationshipToPlayer();
        if (relationship == FactionRelationship.Hostile)
        {
            return false; // Can't interact with hostile NPCs
        }

        // TODO: Check player's reputation when fully integrated
        // For now, allow interaction if not hostile
        return true;
    }

    /// <summary>
    /// Check if this NPC is hostile to a player
    /// </summary>
    public bool IsHostileToPlayer(PlayerInfoModule playerInfo = null)
    {
        if (!aggressiveToHostileFactions)
        {
            return false; // Passive NPC
        }

        // Use FactionManager to determine hostility
        FactionRelationship relationship = GetRelationshipToPlayer();
        return relationship == FactionRelationship.Hostile;
    }

    /// <summary>
    /// Check if this NPC is friendly to the player
    /// </summary>
    public bool IsFriendlyToPlayer(PlayerInfoModule playerInfo = null)
    {
        FactionRelationship relationship = GetRelationshipToPlayer();
        return relationship == FactionRelationship.Friendly;
    }

    /// <summary>
    /// Check if this NPC is neutral to the player
    /// </summary>
    public bool IsNeutralToPlayer(PlayerInfoModule playerInfo = null)
    {
        FactionRelationship relationship = GetRelationshipToPlayer();
        return relationship == FactionRelationship.Neutral;
    }

    /// <summary>
    /// Get the color that should be used for this NPC's nameplate
    /// </summary>
    public Color GetNameplateColor()
    {
        FactionRelationship relationship = GetRelationshipToPlayer();
        return FactionColors.GetRelationshipColor(relationship);
    }

    // ========================================
    // NPC vs NPC Relationships (PHASE 2 ENHANCED)
    // ========================================

    /// <summary>
    /// Get the relationship between this NPC and another NPC
    /// </summary>
    public FactionRelationship GetRelationshipToNPC(NPCModule otherNPC)
    {
        if (otherNPC == null) return FactionRelationship.Neutral;

        var otherFaction = otherNPC.GetHandler<FactionAffiliationHandler>();
        if (otherFaction == null) return FactionRelationship.Neutral;

        return FactionManager.GetRelationship(affiliatedFaction, otherFaction.AffiliatedFaction);
    }

    /// <summary>
    /// Check if this NPC is allied with another NPC
    /// </summary>
    public bool IsAllyOf(NPCModule otherNPC)
    {
        var otherFaction = otherNPC.GetHandler<FactionAffiliationHandler>();
        if (otherFaction == null) return false;

        // Same faction = always allies
        if (affiliatedFaction == otherFaction.AffiliatedFaction)
        {
            return true;
        }

        // Check FactionManager for allied relationships
        FactionRelationship relationship = GetRelationshipToNPC(otherNPC);
        return relationship == FactionRelationship.Friendly;
    }

    /// <summary>
    /// Check if this NPC is enemy with another NPC
    /// </summary>
    public bool IsEnemyOf(NPCModule otherNPC)
    {
        if (!aggressiveToHostileFactions) return false;

        var otherFaction = otherNPC.GetHandler<FactionAffiliationHandler>();
        if (otherFaction == null) return false;

        // Same faction = never enemies
        if (affiliatedFaction == otherFaction.AffiliatedFaction)
        {
            return false;
        }

        // Check FactionManager for hostile relationships
        FactionRelationship relationship = GetRelationshipToNPC(otherNPC);
        return relationship == FactionRelationship.Hostile;
    }

    /// <summary>
    /// Should this NPC help another NPC in combat?
    /// </summary>
    public bool ShouldAssist(NPCModule otherNPC)
    {
        if (!assistsAlliedFactions) return false;

        return IsAllyOf(otherNPC);
    }

    /// <summary>
    /// Should this NPC defend another NPC when attacked?
    /// </summary>
    public bool ShouldDefend(NPCModule otherNPC)
    {
        if (!defendsFactionMembers) return false;

        // Only defend same faction members
        var otherFaction = otherNPC.GetHandler<FactionAffiliationHandler>();
        if (otherFaction == null) return false;

        return affiliatedFaction == otherFaction.AffiliatedFaction;
    }

    // ========================================
    // Visual Indicator
    // ========================================

    private void CreateFactionIndicator()
    {
        factionIndicator = Instantiate(factionIndicatorPrefab, transform);

        // Position above NPC
        var indicatorTransform = factionIndicator.transform;
        indicatorTransform.localPosition = Vector3.up * 2.5f;

        // Apply faction color
        Color factionColor = FactionColors.GetFactionColor(affiliatedFaction);

        // Apply to renderer if available
        var renderer = factionIndicator.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = factionColor;
        }
    }

    // ========================================
    // Debug
    // ========================================

    [ContextMenu("Debug: Print Faction Info")]
    private void DebugPrintInfo()
    {
        Debug.Log($"=== Faction Affiliation ===");
        Debug.Log($"Faction: {affiliatedFaction}");
        Debug.Log($"Relationship to Player: {GetRelationshipToPlayer()}");
        Debug.Log($"Hostile to Player: {IsHostileToPlayer()}");
        Debug.Log($"Friendly to Player: {IsFriendlyToPlayer()}");
        Debug.Log($"Nameplate Color: {GetNameplateColor()}");
        Debug.Log($"Aggressive: {aggressiveToHostileFactions}");
        Debug.Log($"Assists Allies: {assistsAlliedFactions}");
        Debug.Log($"Defends Members: {defendsFactionMembers}");
    }

    [ContextMenu("Debug: Change to Hostile Faction (Warlocks)")]
    private void DebugSetHostile()
    {
        SetFaction(FactionType.Warlocks);
        Debug.Log($"<color=red>[Faction] Changed to HOSTILE: {affiliatedFaction}</color>");
    }

    [ContextMenu("Debug: Change to Friendly Faction (Elves)")]
    private void DebugSetFriendly()
    {
        SetFaction(FactionType.Elves);
        Debug.Log($"<color=green>[Faction] Changed to FRIENDLY: {affiliatedFaction}</color>");
    }

    [ContextMenu("Debug: Change to Neutral Faction")]
    private void DebugSetNeutral()
    {
        SetFaction(FactionType.Neutral);
        Debug.Log($"<color=yellow>[Faction] Changed to NEUTRAL: {affiliatedFaction}</color>");
    }

    // ========================================
    // Save Data Structure
    // ========================================

    [System.Serializable]
    private class FactionSaveData
    {
        public FactionType affiliatedFaction;
        public bool aggressiveToHostile;
        public bool assistsAllies;
        public bool defendsMembers;
    }
}