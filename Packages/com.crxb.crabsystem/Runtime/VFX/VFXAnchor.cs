// Named anchor points on an entity for spawning visual effects.
// Each anchor is a Transform child of the VFXSystem's Anchors GameObject.

namespace CrabSystem
{
    public enum VFXAnchor
    {
        // Above the entity's head. Damage numbers, status icons.
        Overhead,

        // Hands / weapon origin. Cast effects, spell particles.
        CastOrigin,

        // Centre of mass. Hit impact sparks, reaction effects.
        HitSparks,

        // Around the entity's body. Persistent aura effects, buffs, debuffs.
        Aura,

        // Behind the entity. Cape effects, back-mounted equipment VFX.
        Back,
    }
}
