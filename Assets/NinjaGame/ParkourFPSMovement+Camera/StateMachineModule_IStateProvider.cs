using UnityEngine;

/// <summary>
/// StateMachineModule - IStateProvider Implementation
/// 
/// This is a PARTIAL CLASS extension to add IStateProvider interface
/// to your existing StateMachineModule.
/// 
/// INSTALLATION:
/// 1. Add this file to your project alongside StateMachineModule.cs
/// 2. Add "IStateProvider" to the StateMachineModule class declaration:
///    public class StateMachineModule : MonoBehaviour, IStateProvider
/// 3. Done! No other changes needed to existing code.
/// 
/// FIXES APPLIED:
/// - Proper enum usage (PostureState vs LowerBodyState separation)
/// - IsAirborne includes wall-running
/// - Head bob speed based on state, not physics
/// - All flags use correct enum types
/// 
/// EDGE CASES ADDRESSED:
/// - IsGrounded vs IsAirborne mutual exclusivity documented
/// - AllowsAirControl uses whitelist pattern (safer for future states)
/// - Forced state checks centralized for maintainability
/// - Strafe head bob speed documented as design dial
/// </summary>
public partial class StateMachineModule : IStateProvider
{
    #region IStateProvider - State Queries

    BrainState IStateProvider.BrainState => GetBrainState();
    UpperBodyState IStateProvider.UpperBodyState => GetUpperBodyState();
    LowerBodyState IStateProvider.LowerBodyState => GetLowerBodyState();
    PostureState IStateProvider.PostureState => GetPostureState();

    #endregion

    #region IStateProvider - Core Derived Flags

    /// <summary>
    /// Is entity grounded?
    /// Derived from PostureState (Standing, Crouching, Prone, etc.)
    /// 
    /// MUTUAL EXCLUSIVITY:
    /// IsGrounded and IsAirborne are MUTUALLY EXCLUSIVE.
    /// - Wall-running: IsAirborne = true, IsGrounded = false
    /// - Sliding: IsGrounded = true, IsAirborne = false
    /// - Jumping: IsAirborne = true, IsGrounded = false
    /// 
    /// This prevents downstream issues with IK, footsteps, landing detection.
    /// </summary>
    bool IStateProvider.IsGrounded => IsGrounded; // Uses base StateMachineModule implementation

    /// <summary>
    /// Is entity airborne?
    /// Derived from LowerBodyState.
    /// 
    /// INCLUDES:
    /// - Jumping, Falling, DoubleJump, AirStrafing
    /// - WallRunningLeft, WallRunningRight (airborne on wall)
    /// 
    /// EXCLUDES:
    /// - Standing, Walking, Running, Sprinting, Sliding
    /// 
    /// MUTUAL EXCLUSIVITY:
    /// IsAirborne and IsGrounded are MUTUALLY EXCLUSIVE.
    /// Wall-running is airborne (not grounded).
    /// </summary>
    bool IStateProvider.IsAirborne
    {
        get
        {
            LowerBodyState lower = GetLowerBodyState();
            
            return lower == LowerBodyState.Jumping ||
                   lower == LowerBodyState.Falling ||
                   lower == LowerBodyState.DoubleJump ||
                   lower == LowerBodyState.AirStrafing ||
                   lower == LowerBodyState.WallRunningLeft ||  // PARKOUR: Wall-running is airborne
                   lower == LowerBodyState.WallRunningRight;   // PARKOUR: Wall-running is airborne
        }
    }

    /// <summary>
    /// Is entity in any parkour state?
    /// Parkour states are special movement modes requiring environmental interaction.
    /// </summary>
    bool IStateProvider.IsParkour
    {
        get
        {
            LowerBodyState lower = GetLowerBodyState();
            
            return lower == LowerBodyState.WallRunningLeft ||
                   lower == LowerBodyState.WallRunningRight ||
                   lower == LowerBodyState.WallClimbing ||
                   lower == LowerBodyState.Vaulting ||
                   lower == LowerBodyState.Mantling ||
                   lower == LowerBodyState.LedgeHanging;
        }
    }

    #endregion

    #region IStateProvider - Camera Flags

    /// <summary>
    /// Should camera process player input?
    /// False during states where player doesn't control view.
    /// </summary>
    bool IStateProvider.AllowsCameraInput
    {
        get
        {
            // Block camera input during forced postures
            PostureState posture = GetPostureState();
            if (posture == PostureState.Stunned ||
                posture == PostureState.Ragdoll ||
                posture == PostureState.KnockedDown)
                return false;

            // Block camera input during UI/interaction brain states
            BrainState brain = GetBrainState();
            if (brain == BrainState.Dialogue ||
                brain == BrainState.Inventory ||
                brain == BrainState.Crafting ||
                brain == BrainState.Reading)
                return false;

            // Block camera input during upper body forced states
            UpperBodyState upper = GetUpperBodyState();
            if (upper == UpperBodyState.Stunned ||
                upper == UpperBodyState.KnockedDown)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Should head bob be active?
    /// Only during normal grounded movement (standing/crouching + walking/running/sprinting).
    /// 
    /// DESIGN DIAL:
    /// Strafing currently uses walking-speed bob.
    /// Some FPS games reduce bob during strafing for aim clarity.
    /// Adjust in HeadBobSpeed if needed.
    /// </summary>
    bool IStateProvider.AllowsHeadBob
    {
        get
        {
            LowerBodyState lower = GetLowerBodyState();
            PostureState posture = GetPostureState();

            // No head bob during sliding
            if (lower == LowerBodyState.Sliding)
                return false;

            // No head bob during parkour
            if (((IStateProvider)this).IsParkour)
                return false;

            // No head bob during airborne states
            if (((IStateProvider)this).IsAirborne)
                return false;

            // No head bob during hard landing
            if (lower == LowerBodyState.HardLanding)
                return false;

            // No head bob during evasive moves
            if (lower == LowerBodyState.Dodging ||
                lower == LowerBodyState.Dashing)
                return false;

            // No head bob during forced states
            if (IsForcedLowerBodyState(lower))
                return false;

            // Head bob only during normal standing/crouching posture
            if (posture != PostureState.Standing && 
                posture != PostureState.Crouching)
                return false;

            // Must be in a movement state
            if (lower == LowerBodyState.Walking ||
                lower == LowerBodyState.Running ||
                lower == LowerBodyState.Sprinting ||
                lower == LowerBodyState.Strafing)
                return true;

            return false;
        }
    }

    /// <summary>
    /// Head bob speed multiplier based on LowerBodyState.
    /// STATE-DRIVEN: Speed comes from state, not physics velocity.
    /// 
    /// DESIGN DIAL:
    /// - Strafing uses walk speed (0.5x) for aim stability
    /// - Some games use 0.3x for strafing or disable it entirely
    /// - Tweak these values to match your game feel
    /// </summary>
    float IStateProvider.HeadBobSpeed
    {
        get
        {
            // Only return speed if head bob is allowed
            if (!((IStateProvider)this).AllowsHeadBob)
                return 0f;

            LowerBodyState lower = GetLowerBodyState();

            // Return multiplier based on movement state
            switch (lower)
            {
                case LowerBodyState.Walking:
                    return 0.5f;

                case LowerBodyState.Strafing:
                    // DESIGN DIAL: Strafing uses walk speed for aim clarity
                    // Consider 0.3f or 0.0f if you want less bob during strafe
                    return 0.5f;

                case LowerBodyState.Running:
                    return 1.0f;

                case LowerBodyState.Sprinting:
                    return 1.5f;

                case LowerBodyState.Idle:
                default:
                    return 0f;
            }
        }
    }

    /// <summary>
    /// Camera roll angle for wall-run tilt (degrees).
    /// Negative = left wall, Positive = right wall, Zero = normal.
    /// </summary>
    float IStateProvider.CameraRoll
    {
        get
        {
            LowerBodyState lower = GetLowerBodyState();

            // Wall-run tilt (will be interpolated by camera)
            if (lower == LowerBodyState.WallRunningLeft)
                return -15f; // Tilt left

            if (lower == LowerBodyState.WallRunningRight)
                return 15f; // Tilt right

            // No tilt
            return 0f;
        }
    }

    /// <summary>
    /// Camera pitch clamp override for specific states.
    /// Returns null if no override needed.
    /// </summary>
    Vector2? IStateProvider.CameraPitchClampOverride
    {
        get
        {
            LowerBodyState lower = GetLowerBodyState();

            // Tighter pitch clamp during mantle (looking mostly forward)
            if (lower == LowerBodyState.Mantling)
                return new Vector2(-30f, 30f); // Restricted

            // Wider pitch clamp during wall climbing (need to look up)
            if (lower == LowerBodyState.WallClimbing)
                return new Vector2(-10f, 85f); // Can look up more

            // No override - use default camera settings
            return null;
        }
    }

    #endregion

    #region IStateProvider - Movement Flags

    /// <summary>
    /// Should horizontal movement input be processed?
    /// False during states that lock player control.
    /// </summary>
    bool IStateProvider.AllowsHorizontalInput
    {
        get
        {
            LowerBodyState lower = GetLowerBodyState();

            // No horizontal input during mantling (committed animation)
            if (lower == LowerBodyState.Mantling)
                return false;

            // No horizontal input during forced states
            if (IsForcedLowerBodyState(lower))
                return false;

            // No horizontal input during certain vaults (if animation-locked)
            // Uncomment if vaults should lock input:
            // if (lower == LowerBodyState.Vaulting)
            //     return false;

            return true;
        }
    }

    /// <summary>
    /// Should vertical input (jump) be processed?
    /// False during states that prevent jumping.
    /// </summary>
    bool IStateProvider.AllowsVerticalInput
    {
        get
        {
            PostureState posture = GetPostureState();
            LowerBodyState lower = GetLowerBodyState();

            // No jump during forced postures
            if (posture == PostureState.Stunned ||
                posture == PostureState.Ragdoll ||
                posture == PostureState.KnockedDown)
                return false;

            // No jump during forced lower body states
            if (IsForcedLowerBodyState(lower))
                return false;

            // Jump is allowed (specific jump logic handled elsewhere)
            return true;
        }
    }

    /// <summary>
    /// Does entity have air control?
    /// 
    /// SAFER PATTERN:
    /// Uses WHITELIST approach (explicit states that have control).
    /// Adding new forced states (knockback, cinematic falls, grapple)
    /// will default to NO control, which is safer.
    /// </summary>
    bool IStateProvider.AllowsAirControl
    {
        get
        {
            LowerBodyState lower = GetLowerBodyState();

            // WHITELIST: Explicitly allow air control for these states
            switch (lower)
            {
                // Normal air states
                case LowerBodyState.Jumping:
                case LowerBodyState.DoubleJump:
                case LowerBodyState.AirStrafing:
                case LowerBodyState.Falling:
                    return true;

                // Parkour air states
                case LowerBodyState.WallRunningLeft:
                case LowerBodyState.WallRunningRight:
                    return true;

                // NO CONTROL (forced states, future-proof)
                case LowerBodyState.Stumbling:
                case LowerBodyState.KnockedDown:
                    return false;

                // Default: NO control
                // This is safer - new states must explicitly opt-in
                // Future states like knockback, zipline, grapple will default to no control
                default:
                    return false;
            }
        }
    }

    #endregion

    #region Helper Methods (Centralized State Checks)

    /// <summary>
    /// Is this a forced lower body state?
    /// Centralized check for states that lock player control.
    /// 
    /// MAINTAINABILITY:
    /// Add new forced states here instead of repeating checks everywhere.
    /// Examples: Knockback, Zipline, Grappled, CinematicFall
    /// </summary>
    private bool IsForcedLowerBodyState(LowerBodyState state)
    {
        switch (state)
        {
            case LowerBodyState.KnockedDown:
            case LowerBodyState.Stumbling:
                return true;

            // Add new forced states here as needed:
            // case LowerBodyState.Knockback:
            // case LowerBodyState.Grappled:
            // case LowerBodyState.CinematicFall:
            //     return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Is this a forced posture state?
    /// Centralized check for postures that lock control.
    /// </summary>
    private bool IsForcedPostureState(PostureState state)
    {
        switch (state)
        {
            case PostureState.Stunned:
            case PostureState.Ragdoll:
            case PostureState.KnockedDown:
                return true;

            default:
                return false;
        }
    }

    #endregion

    #region Future-Proofing Notes

    // ========================================
    // FUTURE OPTIMIZATION (Not needed now):
    // ========================================
    // If profiling shows derived flag recomputation is expensive,
    // you can cache flags and recompute only on state changes:
    //
    // private bool cachedIsAirborne;
    // private bool cachedIsParkour;
    // private float cachedHeadBobSpeed;
    // 
    // void OnLowerBodyStateChanged(LowerBodyState old, LowerBodyState newState) {
    //     cachedIsAirborne = ComputeIsAirborne();
    //     cachedIsParkour = ComputeIsParkour();
    //     cachedHeadBobSpeed = ComputeHeadBobSpeed();
    // }
    //
    // But current approach is clearer during development.

    // ========================================
    // FUTURE REFACTOR (6 months from now):
    // ========================================
    // Consider grouping camera flags into a CameraProfile struct:
    //
    // struct CameraProfile {
    //     float Roll;
    //     Vector2? PitchClamp;
    //     bool AllowsHeadBob;
    //     float HeadBobSpeed;
    // }
    //
    // CameraProfile ActiveCameraProfile { get; }
    //
    // But don't do this yet - current approach is easier to understand
    // while the system is still growing.

    #endregion
}
