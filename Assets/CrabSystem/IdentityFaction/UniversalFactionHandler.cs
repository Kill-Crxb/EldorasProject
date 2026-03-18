using UnityEngine;
using RPG.Factions;

public class UniversalFactionHandler : MonoBehaviour, IIdentityHandler
{
    [Header("Handler Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Faction Membership")]
    [SerializeField] private FactionType affiliatedFaction = FactionType.None;

    [Header("Behavior Settings")]
    [SerializeField] private bool canChangeFaction = false;
    [SerializeField] private bool aggressiveToHostileFactions = true;
    [SerializeField] private bool assistsAlliedFactions = false;
    [SerializeField] private bool defendsFactionMembers = true;

    private IdentitySystem parent;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public FactionType AffiliatedFaction => affiliatedFaction;
    public bool AggressiveToHostile => aggressiveToHostileFactions;
    public bool AssistsAllies => assistsAlliedFactions;
    public bool DefendsMembers => defendsFactionMembers;

    public void Initialize(IdentitySystem parentSystem) => parent = parentSystem;
    public void UpdateHandler() { }

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

    public FactionType GetFaction() => affiliatedFaction;

    public void SetFaction(FactionType newFaction)
    {
        if (!canChangeFaction && affiliatedFaction != FactionType.None)
        {
            Debug.LogWarning($"[FactionHandler] {parent.GetEntityName()}: Faction changes disabled");
            return;
        }
        affiliatedFaction = newFaction;
    }

    public string GetFactionName() => FactionManager.Instance != null
        ? FactionManager.GetFactionName(affiliatedFaction)
        : affiliatedFaction.ToString();

    public FactionRelationship GetRelationshipWith(FactionType otherFaction)
        => FactionManager.GetRelationship(affiliatedFaction, otherFaction);

    public bool IsHostileTo(FactionType otherFaction) => GetRelationshipWith(otherFaction) == FactionRelationship.Hostile;
    public bool IsFriendlyWith(FactionType otherFaction) => GetRelationshipWith(otherFaction) == FactionRelationship.Friendly;
    public bool IsNeutralTo(FactionType otherFaction) => GetRelationshipWith(otherFaction) == FactionRelationship.Neutral;

    public bool ShouldAttackOnSight(FactionType otherFaction) => aggressiveToHostileFactions && IsHostileTo(otherFaction);
    public bool ShouldAssist(FactionType otherFaction) => assistsAlliedFactions && IsFriendlyWith(otherFaction);
    public bool ShouldDefend(FactionType otherFaction) => defendsFactionMembers && otherFaction == affiliatedFaction;

    public bool ShouldNPCBeHostile(UniversalFactionHandler target)
    {
        if (target == null) return false;
        return aggressiveToHostileFactions && IsHostileTo(target.AffiliatedFaction);
    }
}

[System.Serializable]
public class FactionSaveData
{
    public FactionType affiliatedFaction;
    public bool aggressiveToHostile;
    public bool assistsAllies;
    public bool defendsMembers;
}