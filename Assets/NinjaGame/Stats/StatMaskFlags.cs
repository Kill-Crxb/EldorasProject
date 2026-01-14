using System;

/// <summary>
/// Flags for masking (disabling) specific secondary stat calculations.
/// Use this to optimize performance by not calculating stats that an entity doesn't use.
/// Example: Bears don't need magical stats, so mask them out.
/// </summary>
[Flags]
public enum StatMaskFlags
{
    None = 0,

    // Physical Combat Stats
    PhysicalPower = 1 << 0,
    PhysicalSpeed = 1 << 1,
    PhysicalEfficiency = 1 << 2,
    PhysicalReach = 1 << 3,
    PhysicalDuration = 1 << 4,
    PhysicalPenetration = 1 << 5,

    // Magical Combat Stats
    MagicalPower = 1 << 6,
    MagicalSpeed = 1 << 7,
    MagicalEfficiency = 1 << 8,
    MagicalReach = 1 << 9,
    MagicalDuration = 1 << 10,
    MagicalPenetration = 1 << 11,

    // Defense Stats
    Armor = 1 << 12,
    Resistance = 1 << 13,
    Evasion = 1 << 14,

    // Resource Stats
    MaxHealth = 1 << 15,
    Regeneration = 1 << 16,
    MaxStamina = 1 << 17,
    Recovery = 1 << 18,
    MaxMana = 1 << 19,
    Recollection = 1 << 20,

    // Convenience Masks
    AllPhysicalStats = PhysicalPower | PhysicalSpeed | PhysicalEfficiency | PhysicalReach | PhysicalDuration | PhysicalPenetration,
    AllMagicalStats = MagicalPower | MagicalSpeed | MagicalEfficiency | MagicalReach | MagicalDuration | MagicalPenetration,
    AllDefenseStats = Armor | Resistance | Evasion,
    AllResourceStats = MaxHealth | Regeneration | MaxStamina | Recovery | MaxMana | Recollection,

    // Common Entity Presets
    /// <summary>Physical-only entity: Uses physical combat + defense + basic resources (no mana)</summary>
    PhysicalOnlyEntity = AllPhysicalStats | AllDefenseStats | MaxHealth | Regeneration | MaxStamina | Recovery,

    /// <summary>Magic-only entity: Uses magical combat + defense + mana resources (no stamina)</summary>
    MagicalOnlyEntity = AllMagicalStats | AllDefenseStats | MaxHealth | Regeneration | MaxMana | Recollection,

    /// <summary>Hybrid entity: Uses both physical and magical combat + all resources</summary>
    HybridEntity = AllPhysicalStats | AllMagicalStats | AllDefenseStats | AllResourceStats,

    /// <summary>Natural enemy (bear, wolf): Physical combat only, no magical stats, no mana</summary>
    NaturalEnemy = AllPhysicalStats | Armor | MaxHealth | Regeneration | MaxStamina | Recovery,

    /// <summary>Calculate everything (default for players)</summary>
    CalculateAll = ~0
}