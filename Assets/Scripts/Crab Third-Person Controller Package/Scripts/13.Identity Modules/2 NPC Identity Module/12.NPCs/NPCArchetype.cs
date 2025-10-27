using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines a complete NPC configuration based on Faction + Type + Importance.
/// Created as ScriptableObject or loaded from JSON.
/// Example: "Wildlife + Beast + Soldier = Wolf"
/// </summary>
[CreateAssetMenu(fileName = "NPCArchetype", menuName = "RPG/NPC/Archetype")]
public class NPCArchetype : ScriptableObject
{
    [Header("Archetype Identity")]
    public string archetypeId;          // "archetype_wildlife_wolf_soldier"
    public string archetypeName;        // "Wolf"

    [Header("Three-Dimensional Configuration")]
    public NPCFaction faction;          // Wildlife
    public NPCType npcType;             // Beast
    public NPCImportance importance;    // Soldier

    [Header("Stat Allocation")]
    public StatAllocation baseStats;

    [Header("Equipment & Weapons")]
    public string mainHandWeaponId;     // "weapon_wolf_bite"
    public string offHandWeaponId;      // null for wolves

    [Header("Model Configuration")]
    public List<string> modelPool;      // ["wolf_grey", "wolf_brown", "wolf_black"]
    public bool randomizeModel = true;

    [Header("Name Generation")]
    public bool useGenericName = true;
    public string genericName;          // "Wolf"
    public List<string> namePrefix;     // For named NPCs
    public List<string> nameSuffix;

    [Header("AI Behavior")]
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
}

/// <summary>
/// Base stat allocation for NPCs
/// Maps to RPGCoreStats
/// </summary>
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