using System;
using System.Collections.Generic;
using UnityEngine;
using RPG.Factions;

[CreateAssetMenu(fileName = "NPCArchetype", menuName = "RPG/NPC/Archetype")]
public class NPCArchetype : ScriptableObject
{
    [Header("Archetype Identity")]
    public string archetypeId;
    public string archetypeName;

    [Header("Configuration")]
    [Tooltip("This archetype's faction. Drag a FactionDefinition asset. Empty = unaffiliated (neutral to everyone).")]
    public FactionDefinition faction;
    public NPCType npcType;
    public NPCImportance importance;

    [Header("Base Stats (Level 1 Overrides)")]
    public StatBaseOverride[] baseStatOverrides = new StatBaseOverride[0];

    [Header("Level")]
    [Range(1, 30)]
    public int baseLevel = 1;

    [Header("Equipment")]
    public string mainHandWeaponId;
    public string offHandWeaponId;

    [Header("Abilities")]
    public List<AbilityDefinition> abilities = new List<AbilityDefinition>();

    [Header("Ability Loadout")]
    public AbilityLoadoutConfiguration loadoutConfig;

    [Header("Combat")]
    public string combatBehaviorClassName;
    public AISystemType aiSystemType = AISystemType.GOAP;

    [Header("GOAP Configuration")]
    public List<GOAPGoal> goapGoals = new List<GOAPGoal>();
    public GoalSelectionMode goalSelectionMode = GoalSelectionMode.WeightedRandom;
    public bool useGoalCommitment = true;
    public float minimumGoalDuration = 0.5f;

    [Header("AI Configuration")]
    public CombatPersonality combatPersonality = CombatPersonality.Balanced;
    public HiddenStats hiddenStats;

    [Header("Patrol")]
    public PatrolMode patrolMode = PatrolMode.Static;
    public List<Transform> patrolWaypoints = new List<Transform>();
    public float wanderRadius = 10f;
    public float wanderCheckInterval = 30f;
    public PatrolResourceType[] requiredResources;

    [Header("Perception")]
    public float visionRange = 15f;
    public float visionAngle = 90f;
    public bool requireLineOfSight = true;

    [Header("Model")]
    public List<string> modelPool;
    public bool randomizeModel = true;

    [Header("Names")]
    public bool useGenericName = true;
    public string genericName;
    public List<string> namePrefix;
    public List<string> nameSuffix;

    [Header("Legacy")]
    public string aiBehaviorProfileId;
    public bool aggressiveToHostileFactions = true;
    public bool assistsAlliedFactions = false;
    public bool defendsFactionMembers = true;

    #region Helpers

    /// <summary>String id for CharacterConfigData / persistence. Empty if no faction assigned.</summary>
    public string FactionId => faction != null ? faction.FactionId : "";

    public bool HasGOAPGoals => goapGoals != null && goapGoals.Count > 0;
    public bool UsesGOAP => aiSystemType == AISystemType.GOAP;

    #endregion

    #region Validation

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(archetypeId))
            archetypeId = $"archetype_{npcType}_{importance}".ToLower();
    }

    #endregion
}

[Serializable]
public class StatBaseOverride
{
    public string statId;
    public float baseValue;
}

public enum AISystemType
{
    Legacy,
    FSM,
    GOAP,
    Hybrid
}

public enum CombatPersonality
{
    Aggressive,
    Defensive,
    Balanced,
    Opportunist,
    Berserker
}

public enum PatrolMode
{
    Static,
    FixedRoute,
    RandomWander,
    ResourceBased
}

public enum PatrolResourceType
{
    Food,
    Water,
    Shelter
}

[Serializable]
public class HiddenStats
{
    [Range(1, 10)] public int perception = 5;
    [Range(1, 10)] public int reflexes = 5;
    [Range(1, 10)] public int intelligence = 5;
    [Range(1, 10)] public int courage = 5;

    public float GetReactionTime() => Mathf.Lerp(0.8f, 0.2f, reflexes / 10f);
    public float GetVisionRange() => Mathf.Lerp(10f, 25f, perception / 10f);
    public float GetFlankingChance() => intelligence / 10f;
    public bool WillFightWhenOutnumbered() => UnityEngine.Random.value < (courage / 10f);
}