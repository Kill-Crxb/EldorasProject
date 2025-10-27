using UnityEngine;

/// <summary>
/// Provides combat statistics for damage calculations.
/// Implemented by adapters that bridge to specific stat systems (RPG, simple, etc.).
/// 
/// Design:
/// - Decouples damage system from specific stat implementations
/// - Allows any stat system to work with universal damage system
/// - Adapters translate between specific systems and this interface
/// </summary>
public interface ICombatStatsProvider
{
    /// <summary>Base attack power for damage calculations</summary>
    float GetAttackPower();

    /// <summary>Chance to land a critical hit (0.0 to 1.0)</summary>
    float GetCriticalChance();

    /// <summary>Damage multiplier on critical hit (typically 1.5 to 3.0)</summary>
    float GetCriticalMultiplier();

    /// <summary>Reduces target's armor effectiveness (0.0 to 1.0)</summary>
    float GetArmorPenetration();

    /// <summary>Physical damage reduction stat</summary>
    float GetArmor();

    /// <summary>Magical damage reduction stat</summary>
    float GetMagicResistance();
}