using UnityEngine;

/// <summary>
/// Faction identity for NPCs
/// Determines appearance, behavior, and relationships
/// </summary>
public enum NPCFaction
{
    None = 0,

    // Civilized Factions
    Humans = 10,
    Elves = 11,
    Dwarves = 12,

    // Hostile Factions
    Undead = 20,
    Warlocks = 21,
    Demons = 22,

    // Wildlife
    Wildlife = 30,      // Wolves, bears, etc.
    Beasts = 31,        // More dangerous creatures

    // Special
    Neutral = 100,
    Player = 999        // For multiplayer - other players as NPCs
}

/// <summary>
/// NPC combat/behavior archetype
/// Determines combat style and abilities
/// </summary>
public enum NPCType
{
    None = 0,

    // Combat Types
    Warrior = 1,
    Rogue = 2,
    Mage = 3,
    Archer = 4,
    Cleric = 5,

    // Special Types
    Civilian = 10,      // Non-combatant
    Beast = 20,         // Animal AI
    Boss = 99           // Boss encounter
}

/// <summary>
/// NPC power tier
/// Determines stats, equipment quality, and threat level
/// </summary>
public enum NPCImportance
{
    Civilian = 0,       // Non-combatant (merchants, etc.)
    Soldier = 1,        // Generic combatant
    Elite = 2,          // Stronger variant
    Hero = 3,           // Named character
    Boss = 4            // Unique encounter
}