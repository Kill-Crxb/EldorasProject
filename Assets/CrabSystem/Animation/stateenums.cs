/// <summary>
/// Template state enums for Third-Person Action RPG.
/// These are examples - you'll customize these per game.
/// </summary>

/// <summary>
/// Brain State - What is the entity's focus/intent?
/// High-level context that affects UI, camera, and available systems.
/// </summary>
public enum BrainState
{
    Idle,           // Nothing in particular, neutral state
    Combat,         // Engaged with enemies
    Exploration,    // Looking around, searching, looting
    Dialogue,       // Talking to NPC
    Inventory,      // Managing inventory (locks other actions)
    Crafting,       // Crafting interface
    Reading,        // Reading book/document
    Mounted,        // On mount/vehicle
    Swimming,       // In water
    Climbing,       // Climbing ladder/wall
    Dead            // Entity is dead
}

/// <summary>
/// Upper Body State - What are the arms/torso doing?
/// Action execution layer, often animation-driven.
/// </summary>
public enum UpperBodyState
{
    Idle,               // Hands free, relaxed

    // Melee Combat
    MeleeWindUp,        // Winding up melee attack
    MeleeSwing,         // Mid-swing (committed, can't cancel)
    MeleeRecovery,      // Recovering from swing
    MeleeCombo2,        // Second hit in combo
    MeleeCombo3,        // Third hit in combo

    // Defense
    Blocking,           // Holding block
    Parrying,           // Parry window active

    // Ranged Combat
    Aiming,             // Aiming bow/gun
    DrawingBow,         // Drawing bow back
    Shooting,           // Releasing projectile
    Reloading,          // Reloading weapon

    // Magic
    CastingWindUp,      // Preparing spell
    CastingChannel,     // Channeling spell (locked)
    CastingRelease,     // Releasing spell

    // Interaction
    Interacting,        // Opening door, pulling lever
    Carrying,           // Carrying object
    Climbing,           // Using arms to climb

    // Consumables
    Drinking,           // Using potion
    Eating,             // Eating food
    ThrowingItem,       // Throwing consumable

    // Special
    Stunned,            // Cannot act (stunned)
    KnockedDown,         // On ground, getting up

    // Parkour States
    Vaulting,
    Mantling,
    WallClimbing,


}

/// <summary>
/// Lower Body State - What are the legs doing?
/// Locomotion layer, affects velocity and physics.
/// </summary>
public enum LowerBodyState
{
    Idle,               // Standing still

    // Ground Movement
    Walking,            // Slow movement
    Running,            // Normal movement
    Sprinting,          // Fast movement (drains stamina)

    // Combat Movement
    Strafing,           // Side movement (target-locked)
    Backstepping,       // Defensive step backward

    // Evasive Movement
    Dodging,            // Evasive roll/dodge
    Dashing,            // Quick burst (may have i-frames)
    Sliding,            // Momentum slide

    // Airborne
    Jumping,            // In jump
    Falling,            // Freefall
    Landing,            // Landing recovery
    DoubleJump,         // Second jump (if applicable)

    // Special Movement
    Crawling,           // Moving while prone
    Swimming,           // Moving in water
    Climbing,           // Moving on ladder/wall

    // Forced States
    Stumbling,          // Lost balance
    KnockedDown,        // On ground

    //Parkour
    WallRunningLeft,
    WallRunningRight,
    WallClimbing,
    LedgeHanging,
    Vaulting,
    Mantling,
    AirStrafing,
    HardLanding

}

/// <summary>
/// Posture State - What is the body position?
/// Foundation layer, most restrictive, affects collision and capabilities.
/// </summary>
public enum PostureState
{
    Standing,           // Upright, full mobility
    Crouching,          // Half-height, slower, quieter
    Prone,              // Flat on ground, minimal profile

    // Environmental
    Swimming,           // Horizontal in water
    Climbing,           // Vertical on surface
    Mounted,            // On horse/vehicle

    // Seated/Resting
    Sitting,            // Seated (resting, dialogue)
    Kneeling,           // One knee (interacting)

    // Forced States
    KnockedDown,        // On ground (forced)
    Stunned,            // Locked in place
    Ragdoll,             // Physics-driven (dead/thrown)

    // Parkour States
    Sliding,            // PARKOUR: Crouched slide with momentum lock
    Airborne,
}