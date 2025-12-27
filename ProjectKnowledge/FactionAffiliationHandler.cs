using UnityEngine;
using RPG.Factions;

public class FactionAffiliationHandler : MonoBehaviour, INPCHandler
{
    [Header("Handler Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Faction Membership")]
    [SerializeField] private FactionType affiliatedFaction = FactionType.None;

    [Header("Behavior Settings")]
    [SerializeField] private bool aggressiveToHostileFactions = true;
    [SerializeField] private bool assistsAlliedFactions = false;
    [SerializeField] private bool defendsFactionMembers = true;

    private NPCModule parentNPC;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public FactionType AffiliatedFaction => affiliatedFaction;
    public bool AggressiveToHostile => aggressiveToHostileFactions;
    public bool AssistsAllies => assistsAlliedFactions;
    public bool DefendsMembers => defendsFactionMembers;

    public void Initialize(NPCModule parent)
    {
        parentNPC = parent;

        if (affiliatedFaction == FactionType.None && parent.AppliedArchetype != null)
        {
            SetFactionFromArchetype(parent.AppliedArchetype);
        }
    }

    public void UpdateHandler()
    {
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
        if (parentNPC.AppliedArchetype != null)
        {
            SetFactionFromArchetype(parentNPC.AppliedArchetype);
        }
    }

    private void SetFactionFromArchetype(NPCArchetype archetype)
    {
        string factionString = archetype.faction.ToString().ToLower();

        if (factionString.Contains("wildlife") || factionString.Contains("beast"))
            affiliatedFaction = FactionType.Wildlife;
        else if (factionString.Contains("human"))
            affiliatedFaction = FactionType.Humans;
        else if (factionString.Contains("elf"))
            affiliatedFaction = FactionType.Elves;
        else if (factionString.Contains("dwarf"))
            affiliatedFaction = FactionType.Dwarves;
        else if (factionString.Contains("warlock"))
            affiliatedFaction = FactionType.Warlocks;
        else if (factionString.Contains("undead") || factionString.Contains("skeleton"))
            affiliatedFaction = FactionType.Undead;
        else if (factionString.Contains("bandit") || factionString.Contains("raider"))
            affiliatedFaction = FactionType.Bandits;
        else if (factionString.Contains("monster"))
            affiliatedFaction = FactionType.Monsters;
        else
            affiliatedFaction = FactionType.Neutral;

        aggressiveToHostileFactions = archetype.aggressiveToHostileFactions;
        assistsAlliedFactions = archetype.assistsAlliedFactions;
        defendsFactionMembers = archetype.defendsFactionMembers;
    }

    public void SetFaction(FactionType newFaction)
    {
        affiliatedFaction = newFaction;
    }

    public FactionRelationship GetRelationshipToPlayer()
    {
        return FactionManager.GetRelationship(affiliatedFaction, FactionType.Player);
    }

    public bool CanInteractWithPlayer(PlayerInfoModule playerInfo)
    {
        if (affiliatedFaction == FactionType.Wildlife ||
            affiliatedFaction == FactionType.Monsters)
        {
            return false;
        }

        FactionRelationship relationship = GetRelationshipToPlayer();
        if (relationship == FactionRelationship.Hostile)
        {
            return false;
        }

        return true;
    }

    public bool IsHostileToPlayer(PlayerInfoModule playerInfo = null)
    {
        if (!aggressiveToHostileFactions)
        {
            return false;
        }

        FactionRelationship relationship = GetRelationshipToPlayer();
        return relationship == FactionRelationship.Hostile;
    }

    public bool IsFriendlyToPlayer(PlayerInfoModule playerInfo = null)
    {
        FactionRelationship relationship = GetRelationshipToPlayer();
        return relationship == FactionRelationship.Friendly;
    }

    public bool IsNeutralToPlayer(PlayerInfoModule playerInfo = null)
    {
        FactionRelationship relationship = GetRelationshipToPlayer();
        return relationship == FactionRelationship.Neutral;
    }

    public Color GetNameplateColor()
    {
        FactionRelationship relationship = GetRelationshipToPlayer();
        return FactionColors.GetRelationshipColor(relationship);
    }

    public FactionRelationship GetRelationshipToNPC(NPCModule otherNPC)
    {
        if (otherNPC == null) return FactionRelationship.Neutral;

        var otherFaction = otherNPC.GetHandler<FactionAffiliationHandler>();
        if (otherFaction == null) return FactionRelationship.Neutral;

        return FactionManager.GetRelationship(affiliatedFaction, otherFaction.AffiliatedFaction);
    }

    public bool IsAllyOf(NPCModule otherNPC)
    {
        var otherFaction = otherNPC.GetHandler<FactionAffiliationHandler>();
        if (otherFaction == null) return false;

        if (affiliatedFaction == otherFaction.AffiliatedFaction)
        {
            return true;
        }

        FactionRelationship relationship = GetRelationshipToNPC(otherNPC);
        return relationship == FactionRelationship.Friendly;
    }

    public bool IsEnemyOf(NPCModule otherNPC)
    {
        if (!aggressiveToHostileFactions) return false;

        var otherFaction = otherNPC.GetHandler<FactionAffiliationHandler>();
        if (otherFaction == null) return false;

        if (affiliatedFaction == otherFaction.AffiliatedFaction)
        {
            return false;
        }

        FactionRelationship relationship = GetRelationshipToNPC(otherNPC);
        return relationship == FactionRelationship.Hostile;
    }

    public bool ShouldAssist(NPCModule otherNPC)
    {
        if (!assistsAlliedFactions) return false;

        return IsAllyOf(otherNPC);
    }

    public bool ShouldDefend(NPCModule otherNPC)
    {
        if (!defendsFactionMembers) return false;

        var otherFaction = otherNPC.GetHandler<FactionAffiliationHandler>();
        if (otherFaction == null) return false;

        return affiliatedFaction == otherFaction.AffiliatedFaction;
    }

    [System.Serializable]
    private class FactionSaveData
    {
        public FactionType affiliatedFaction;
        public bool aggressiveToHostile;
        public bool assistsAllies;
        public bool defendsMembers;
    }
}