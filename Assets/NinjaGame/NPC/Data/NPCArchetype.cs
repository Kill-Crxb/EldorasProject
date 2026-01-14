using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines a complete NPC configuration based on Faction + Type + Importance.
/// Created as ScriptableObject or loaded from JSON.
/// Example: "Wildlife + Beast + Soldier = Wolf"
/// 
/// Phase 1.7b: Added level scaling support for universal RPGSystem
/// </summary>
[CreateAssetMenu(fileName = "NPCArchetype", menuName = "RPG/NPC/Archetype")]
public class NPCArchetype : ScriptableObject
{
    [Header("Archetype Identity")]
    public string archetypeId;          // "archetype_wildlife_bear_soldier"
    public string archetypeName;        // "Bear"

    [Header("Three-Dimensional Configuration")]
    public NPCFaction faction;          // Wildlife
    public NPCType npcType;             // Beast
    public NPCImportance importance;    // Soldier

    [Header("Stat Allocation")]
    public StatAllocation baseStats;

    [Header("Level & Scaling")]
    [Tooltip("Starting level for this archetype (can be overridden per-NPC via RPGSystem)")]
    [Range(1, 30)]
    public int baseLevel = 1;

    [Tooltip("How much each core stat grows per level (0 = no scaling, 2 = +2 per level)")]
    [Range(0f, 5f)]
    public float statGrowthPerLevel = 2f;

    [Tooltip("Use scaled growth curve (more realistic) vs linear growth")]
    public bool useScaledGrowth = true;

    [Header("Equipment & Weapons")]
    public string mainHandWeaponId;     // "weapon_wolf_bite"
    public string offHandWeaponId;      // null for wolves

    [Header("Abilities")]
    [Tooltip("Abilities this NPC can use (for natural weapons or spells)")]
    public List<AbilityDefinition> abilities = new List<AbilityDefinition>();

    [Header("Ability Loadout (For Humanoids)")]
    [Tooltip("Optional: Configure ability slots for humanoid NPCs that use loadout system. Leave empty for creatures with natural weapons.")]
    public AbilityLoadoutConfiguration loadoutConfig;

    [Header("Combat Behavior")]
    [Tooltip("Name of the combat behavior class (e.g., 'BearCombatBehavior', 'MeleeCombatBehavior')")]
    public string combatBehaviorClassName;

    [Header("AI System Selection")]
    [Tooltip("Which AI system to use for this NPC")]
    public AISystemType aiSystemType = AISystemType.GOAP;

    [Header("GOAP Configuration (NEW)")]
    [Tooltip("Goals that this NPC can execute (approach, attack, retreat, etc.)")]
    public List<GOAPGoal> goapGoals = new List<GOAPGoal>();

    [Tooltip("Combat actions this NPC can perform (Fate system - attack sequences with RNG branches)")]
    public List<GOAPAction> goapActions = new List<GOAPAction>();

    [Tooltip("Goal selection mode: Deterministic (highest weight) or random")]
    public GoalSelectionMode goalSelectionMode = GoalSelectionMode.WeightedRandom;

    [Tooltip("Use goal commitment (prevents rapid goal switching)")]
    public bool useGoalCommitment = true;

    [Tooltip("Minimum time to stay in a goal before switching")]
    public float minimumGoalDuration = 0.5f;

    [Header("AI Configuration")]
    [Tooltip("Combat personality: Aggressive, Defensive, Balanced, Opportunist, Berserker")]
    public CombatPersonality combatPersonality = CombatPersonality.Balanced;

    [Tooltip("Hidden stats affect AI behavior (perception, reflexes, intelligence, courage)")]
    public HiddenStats hiddenStats;

    [Tooltip("Patrol mode for this NPC type")]
    public PatrolMode patrolMode = PatrolMode.Static;

    [Tooltip("For FixedRoute patrol: list of waypoint transforms")]
    public List<Transform> patrolWaypoints = new List<Transform>();

    [Tooltip("For RandomWander: radius and frequency")]
    public float wanderRadius = 10f;
    public float wanderCheckInterval = 30f;

    [Tooltip("For ResourceBased (animals): required resources")]
    public PatrolResourceType[] requiredResources;

    [Header("Perception Configuration")]
    [Tooltip("How far this NPC can see (uses PerceptionModule)")]
    public float visionRange = 15f;

    [Tooltip("Field of view angle in degrees")]
    public float visionAngle = 90f;

    [Tooltip("Require line of sight to detect targets")]
    public bool requireLineOfSight = true;

    [Header("Model Configuration")]
    public List<string> modelPool;      // ["wolf_grey", "wolf_brown", "wolf_black"]
    public bool randomizeModel = true;

    [Header("Name Generation")]
    public bool useGenericName = true;
    public string genericName;          // "Wolf"
    public List<string> namePrefix;     // For named NPCs
    public List<string> nameSuffix;

    [Header("AI Behavior (Legacy - will be deprecated)")]
    public string aiBehaviorProfileId;  // "aggressive_melee" (reference to AIBehaviorProfile)

    [Header("Faction Settings")]
    public bool aggressiveToHostileFactions = true;
    public bool assistsAlliedFactions = false;
    public bool defendsFactionMembers = true;

    // Validation
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(archetypeId))
        {
            archetypeId = $"archetype_{faction}_{npcType}_{importance}".ToLower();
        }
    }

    // Helper methods
    public bool HasGOAPGoals => goapGoals != null && goapGoals.Count > 0;
    public bool HasGOAPActions => goapActions != null && goapActions.Count > 0;
    public bool UsesGOAP => aiSystemType == AISystemType.GOAP;
}


[System.Serializable]
public class StatAllocation
{
    [Header("Primary Stats")]
    public int mind = 10;           // Magical power
    public int body = 10;           // Physical power
    public int spirit = 10;         // Magical defense
    public int resilience = 10;     // Physical defense
    public int endurance = 10;      // Resource pools
    public int insight = 10;        // Regeneration

    [Header("Derived Modifiers")]
    [Range(0.5f, 2f)]
    public float healthMultiplier = 1f;     // Modify calculated max health

    [Range(0.5f, 2f)]
    public float damageMultiplier = 1f;     // Modify calculated damage
}

// AI System Type Selection
public enum AISystemType
{
    Legacy,         // Old AIModule state machine (deprecated)
    FSM,            // Finite State Machine with combat behaviors
    GOAP,           // Goal-Oriented Action Planning (new default)
    Hybrid          // FSM for high-level states, GOAP for combat decisions
}

// New enums for AI system
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

/// <summary>
/// Hidden stats affect AI behavior (perception, reflexes, intelligence, courage)
/// </summary>
[System.Serializable]
public class HiddenStats
{
    [Header("Perception")]
    [Range(1, 10)] public int perception = 5;

    [Header("Reflexes")]
    [Range(1, 10)] public int reflexes = 5;

    [Header("Intelligence")]
    [Range(1, 10)] public int intelligence = 5;

    [Header("Courage")]
    [Range(1, 10)] public int courage = 5;

    // Derived values
    public float GetReactionTime() => UnityEngine.Mathf.Lerp(0.8f, 0.2f, reflexes / 10f);
    public float GetVisionRange() => UnityEngine.Mathf.Lerp(10f, 25f, perception / 10f);
    public float GetFlankingChance() => intelligence / 10f;
    public bool WillFightWhenOutnumbered() => UnityEngine.Random.value < (courage / 10f);
}