using UnityEngine;

// State Provider Interface - Exposes state-derived gameplay flags
//
// This interface allows reactive modules (camera, UI, audio) to query
// gameplay state without duplicating logic or inferring rules.
//
// StateMachineModule implements this to provide derived flags from
// its authoritative state.
//
// KEY PRINCIPLE:
// - IStateProvider reports FACTS (what state is)
// - IStateProvider does NOT report physics (how fast, how much)
// - Reactive modules use state flags for "should" decisions
// - Reactive modules use physics providers for "how much" data
//
// Examples of derived flags:
// - AllowsCameraInput (false during stun, ragdoll, cutscenes)
// - AllowsHeadBob (false during wall-run, slide, hard landing)
// - CameraRoll (tilt during wall-running)
// - HeadBobSpeed (multiplier based on walk/run/sprint state)

namespace CrabSystem
{
    public interface IStateProvider
    {
        // ===== State Queries =====
    
        BrainState BrainState { get; }
    
        UpperBodyState UpperBodyState { get; }
    
        LowerBodyState LowerBodyState { get; }
    
        PostureState PostureState { get; }
    
        // ===== Derived Flags (Semantic Convenience) =====
    
        // Is entity grounded? (derived from posture)
        // True for: Standing, Crouching, Prone, Sitting, Kneeling
        bool IsGrounded { get; }
    
        // Is entity airborne? (derived from lower body)
        // True for: Jumping, Falling, DoubleJump, AirStrafing
        // True for: WallRunningLeft, WallRunningRight (airborne but on wall)
        // False for: Standing, Walking, Running, Sprinting
        bool IsAirborne { get; }
    
        // Is entity in any parkour state?
        // True for: WallRunning, WallClimbing, Vaulting, Mantling, LedgeHanging
        bool IsParkour { get; }
    
        // ===== Camera-Specific Flags =====
    
        // Should camera process player input?
        // False during: Stunned, Ragdoll, KnockedDown, Dialogue, Inventory, etc.
        bool AllowsCameraInput { get; }
    
        // Should head bob be active?
        // False during: Sliding, WallRunning, Airborne states, HardLanding, Parkour
        // True only during: Standing/Crouching + Walking/Running/Sprinting
        bool AllowsHeadBob { get; }
    
        // Head bob speed multiplier based on movement state
        // 0.0 = stationary/no bob
        // 0.5 = walking
        // 1.0 = running
        // 1.5 = sprinting
        // Only non-zero when AllowsHeadBob is true
        float HeadBobSpeed { get; }
    
        // Camera roll angle (in degrees) for wall-run tilt
        // Negative = left wall-run, Positive = right wall-run, Zero = normal
        float CameraRoll { get; }
    
        // Camera pitch clamp override for specific states
        // Returns null if no override, otherwise (min, max) angles
        // Use case: Tighter clamp during mantling, wider during wall-climbing
        Vector2? CameraPitchClampOverride { get; }
    
        // ===== Movement-Specific Flags =====
    
        // Should horizontal movement input be processed?
        // False during: Mantling, forced climbs, certain vaults
        bool AllowsHorizontalInput { get; }
    
        // Should vertical movement input (jump) be processed?
        // False during: Stunned, KnockedDown, certain animations
        bool AllowsVerticalInput { get; }
    
        // Does entity have air control?
        // Depends on jump type, wall-run exit, or falling context
        bool AllowsAirControl { get; }
    }
}
