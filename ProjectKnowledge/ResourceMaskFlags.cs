using System;

/// <summary>
/// Flags for masking (disabling) specific resources.
/// Use this to optimize performance and UI by disabling resources that an entity doesn't use.
/// Example: Bears don't use mana, so mask it out.
/// </summary>
[Flags]
public enum ResourceMaskFlags
{
    None = 0,

    Health = 1 << 0,
    Mana = 1 << 1,
    Stamina = 1 << 2,

    // Convenience Masks
    /// <summary>Physical entity: Health + Stamina only</summary>
    PhysicalEntity = Health | Stamina,

    /// <summary>Magic entity: Health + Mana only</summary>
    MagicalEntity = Health | Mana,

    /// <summary>Hybrid entity: All resources</summary>
    AllResources = Health | Mana | Stamina,

    /// <summary>Minimal entity: Health only (for simple enemies or objects)</summary>
    HealthOnly = Health
}