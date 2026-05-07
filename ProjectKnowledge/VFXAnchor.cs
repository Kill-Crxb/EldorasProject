/// <summary>
/// Named anchor points on an entity for spawning visual effects.
/// Each anchor is a Transform child of the VFXSystem's Anchors GameObject.
/// </summary>
public enum VFXAnchor
{
    /// <summary>Above the entity's head. Damage numbers, status icons.</summary>
    Overhead,

    /// <summary>Hands / weapon origin. Cast effects, spell particles.</summary>
    CastOrigin,

    /// <summary>Centre of mass. Hit impact sparks, reaction effects.</summary>
    HitSparks,

    /// <summary>Around the entity's body. Persistent aura effects, buffs, debuffs.</summary>
    Aura,

    /// <summary>Behind the entity. Cape effects, back-mounted equipment VFX.</summary>
    Back,
}
