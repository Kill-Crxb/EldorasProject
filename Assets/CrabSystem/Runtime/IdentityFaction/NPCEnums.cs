using UnityEngine;

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