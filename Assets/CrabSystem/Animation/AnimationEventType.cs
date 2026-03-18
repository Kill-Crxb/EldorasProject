namespace NinjaGame.Animation
{
    /// <summary>
    /// Defines all possible animation events that can be triggered during combat animations.
    /// These events allow precise timing control for hitboxes, movement, cancels, and effects.
    /// </summary>
    public enum AnimationEventType
    {
        // Hitbox Control
        HitboxStart,
        HitboxEnd,

        // Cancel Windows
        AnimLocked,
        AnimUnlocked,

        // Parry System
        ParryStart,
        ParryMax,

        // Movement Control
        MovementLocked,
        MovementUnlocked,
        RootMotionStart,
        RootMotionEnd,

        // VFX/SFX
        PlayEffect,
        WeaponTrailStart,
        WeaponTrailEnd,

        // Combo System
        ComboWindowStart,
        ComboWindowEnd,

        // Special Movement
        TeleportFrame,

        // Invincibility Frames
        IFrameStart,
        IFrameEnd,

        // AI Decision Points
        Feint,

        // State Machine Integration
        StateTransition,

        // Generic Effect Triggers
        Effect1,
        Effect2,
        Effect3
    }
}