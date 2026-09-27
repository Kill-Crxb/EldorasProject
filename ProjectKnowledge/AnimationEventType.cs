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

        // Generic Effect Triggers
        Effect1,
        Effect2,
        Effect3,

        // State Machine Integration
        // NOTE: State transitions actually run through OnStateTransition(string) / OnStateTransitionEvent,
        // never through this enum — kept only for backward compatibility, do not use as effectTrigger.
        StateTransition,

        /// <summary>
        /// No animation event — the ability's effects run the instant it is used.
        ///
        /// AbilitySystem gates on `effectTrigger is Effect1/2/3`; anything else executes immediately
        /// in UseAbility. But it ALSO runs effects whenever an incoming event equals effectTrigger,
        /// so leaving a self-contained ability on the default (HitboxStart, 0) means a clip that
        /// happens to raise HitboxStart executes its effects a SECOND time. Nothing forwards None,
        /// so it is the only value that says "immediately, and only once" without lying.
        ///
        /// Use it for any ability whose effects do not need animation timing — movement impulses,
        /// instant buffs, teleports.
        ///
        /// ⚠ Appended, never inserted. These serialize by integer and every authored asset stores a
        /// number, so inserting a member silently repoints every ability past it.
        /// </summary>
        None
    }
}