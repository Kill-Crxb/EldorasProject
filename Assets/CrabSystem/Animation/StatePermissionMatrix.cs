using UnityEngine;

/// <summary>
/// Permission matrix that defines what states can coexist.
/// Enforces physical constraints: Posture -> LowerBody -> UpperBody -> Brain
/// 
/// This is a ScriptableObject so you can:
/// 1. Create different permission profiles per enemy type
/// 2. Override specific rules in derived classes
/// 3. Configure in inspector
/// </summary>
[CreateAssetMenu(fileName = "StatePermissionMatrix", menuName = "StateMachine/Permission Matrix")]
public class StatePermissionMatrix : ScriptableObject
{
    [Header("Debug")]
    [SerializeField] protected bool debugPermissions = false;

    // ===== POSTURE CONSTRAINS MOVEMENT =====

    /// <summary>
    /// Check if current posture allows a desired lower body movement.
    /// Posture is the foundation - it has most restrictive power.
    /// </summary>
    public virtual bool CanPerformMovement(PostureState posture, LowerBodyState desiredMovement)
    {
        switch (posture)
        {
            case PostureState.Prone:
                // Can only crawl when prone
                bool proneAllowed = desiredMovement == LowerBodyState.Crawling ||
                                   desiredMovement == LowerBodyState.Idle;

                if (!proneAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Prone blocks {desiredMovement}");

                return proneAllowed;

            case PostureState.Crouching:
                // Can't sprint or dash while crouching
                bool crouchBlocked = desiredMovement == LowerBodyState.Sprinting ||
                                    desiredMovement == LowerBodyState.Dashing;

                if (crouchBlocked && debugPermissions)
                    Debug.Log($"[Permissions] Crouching blocks {desiredMovement}");

                return !crouchBlocked;

            case PostureState.Standing:
                // Full mobility when standing
                return true;

            case PostureState.Swimming:
                // Only swimming movement
                bool swimAllowed = desiredMovement == LowerBodyState.Swimming ||
                                  desiredMovement == LowerBodyState.Idle;

                if (!swimAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Swimming blocks {desiredMovement}");

                return swimAllowed;

            case PostureState.Climbing:
                // Only climbing movement
                bool climbAllowed = desiredMovement == LowerBodyState.Climbing ||
                                   desiredMovement == LowerBodyState.Idle;

                if (!climbAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Climbing blocks {desiredMovement}");

                return climbAllowed;

            case PostureState.Mounted:
                // Limited movement on mount
                bool mountAllowed = desiredMovement == LowerBodyState.Walking ||
                                   desiredMovement == LowerBodyState.Running ||
                                   desiredMovement == LowerBodyState.Sprinting ||
                                   desiredMovement == LowerBodyState.Idle;

                if (!mountAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Mounted blocks {desiredMovement}");

                return mountAllowed;

            case PostureState.KnockedDown:
            case PostureState.Stunned:
            case PostureState.Ragdoll:
                // No movement allowed in these states
                bool forcedBlocked = desiredMovement != LowerBodyState.Idle;

                if (forcedBlocked && debugPermissions)
                    Debug.Log($"[Permissions] {posture} blocks all movement");

                return !forcedBlocked;

            default:
                return true;
        }
    }

    // ===== MOVEMENT CONSTRAINS ACTIONS =====

    /// <summary>
    /// Check if current movement allows a desired upper body action.
    /// Some movements restrict what you can do with your arms.
    /// </summary>
    public virtual bool CanPerformAction_MovementCheck(LowerBodyState movement, UpperBodyState desiredAction)
    {
        switch (movement)
        {
            case LowerBodyState.Dashing:
            case LowerBodyState.Dodging:
                // Can't attack mid-dash/dodge (evasive maneuvers)
                bool evasiveBlocked = desiredAction != UpperBodyState.Idle;

                if (evasiveBlocked && debugPermissions)
                    Debug.Log($"[Permissions] {movement} blocks {desiredAction}");

                return !evasiveBlocked;

            case LowerBodyState.Climbing:
                // Can only use arms for climbing
                bool climbAllowed = desiredAction == UpperBodyState.Climbing ||
                                   desiredAction == UpperBodyState.Idle;

                if (!climbAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Climbing blocks {desiredAction}");

                return climbAllowed;

            case LowerBodyState.Sprinting:
                // Can aim/shoot while sprinting, but can't melee
                bool sprintBlocked = desiredAction == UpperBodyState.MeleeWindUp ||
                                    desiredAction == UpperBodyState.MeleeSwing ||
                                    desiredAction == UpperBodyState.MeleeRecovery ||
                                    desiredAction == UpperBodyState.Blocking;

                if (sprintBlocked && debugPermissions)
                    Debug.Log($"[Permissions] Sprinting blocks {desiredAction}");

                return !sprintBlocked;

            case LowerBodyState.Stumbling:
            case LowerBodyState.KnockedDown:
                // Can't act while stumbling/knocked down
                bool stumblingBlocked = desiredAction != UpperBodyState.Idle &&
                                       desiredAction != UpperBodyState.KnockedDown;

                if (stumblingBlocked && debugPermissions)
                    Debug.Log($"[Permissions] {movement} blocks {desiredAction}");

                return !stumblingBlocked;

            default:
                // Most movements allow upper body actions
                return true;
        }
    }

    // ===== ACTIONS CONSTRAIN MOVEMENT =====

    /// <summary>
    /// Check if current upper body action allows a desired movement.
    /// Some actions lock you in place or restrict mobility.
    /// </summary>
    public virtual bool CanPerformMovement_ActionCheck(UpperBodyState action, LowerBodyState desiredMovement)
    {
        switch (action)
        {
            case UpperBodyState.MeleeSwing:
                // Committed to swing - can't move
                bool swingLocked = desiredMovement != LowerBodyState.Idle;

                if (swingLocked && debugPermissions)
                    Debug.Log($"[Permissions] MeleeSwing locks movement");

                return !swingLocked;

            case UpperBodyState.MeleeWindUp:
            case UpperBodyState.MeleeRecovery:
                // Can walk during windup/recovery, but can't dash
                bool windupRestricted = desiredMovement == LowerBodyState.Dashing ||
                                       desiredMovement == LowerBodyState.Dodging ||
                                       desiredMovement == LowerBodyState.Sprinting;

                if (windupRestricted && debugPermissions)
                    Debug.Log($"[Permissions] {action} blocks {desiredMovement}");

                return !windupRestricted;

            case UpperBodyState.Blocking:
                // Can move slowly while blocking
                bool blockingAllowed = desiredMovement == LowerBodyState.Walking ||
                                      desiredMovement == LowerBodyState.Backstepping ||
                                      desiredMovement == LowerBodyState.Strafing ||
                                      desiredMovement == LowerBodyState.Idle;

                if (!blockingAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Blocking restricts to slow movement");

                return blockingAllowed;

            case UpperBodyState.Aiming:
                // Can move while aiming (but slower)
                bool aimingAllowed = desiredMovement == LowerBodyState.Walking ||
                                    desiredMovement == LowerBodyState.Strafing ||
                                    desiredMovement == LowerBodyState.Backstepping ||
                                    desiredMovement == LowerBodyState.Idle;

                if (!aimingAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Aiming restricts to slow movement");

                return aimingAllowed;

            case UpperBodyState.CastingChannel:
                // Can't move while channeling
                bool channelLocked = desiredMovement != LowerBodyState.Idle;

                if (channelLocked && debugPermissions)
                    Debug.Log($"[Permissions] Channeling locks movement");

                return !channelLocked;

            case UpperBodyState.Carrying:
                // Can walk while carrying, but slowly
                bool carryingAllowed = desiredMovement == LowerBodyState.Walking ||
                                      desiredMovement == LowerBodyState.Idle;

                if (!carryingAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Carrying restricts to walking");

                return carryingAllowed;

            case UpperBodyState.Stunned:
            case UpperBodyState.KnockedDown:
                // Can't move while stunned/knocked down
                bool forcedLocked = desiredMovement != LowerBodyState.Idle;

                if (forcedLocked && debugPermissions)
                    Debug.Log($"[Permissions] {action} locks movement");

                return !forcedLocked;

            default:
                // Most actions allow free movement
                return true;
        }
    }

    // ===== POSTURE CONSTRAINS ACTIONS =====

    /// <summary>
    /// Check if current posture allows a desired upper body action.
    /// Body position limits what you can do with your arms.
    /// </summary>
    public virtual bool CanPerformAction_PostureCheck(PostureState posture, UpperBodyState desiredAction)
    {
        switch (posture)
        {
            case PostureState.Prone:
                // Limited to pistol/thrown items while prone
                bool proneAllowed = desiredAction == UpperBodyState.Aiming ||
                                   desiredAction == UpperBodyState.Shooting ||
                                   desiredAction == UpperBodyState.ThrowingItem ||
                                   desiredAction == UpperBodyState.Idle;

                if (!proneAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Prone blocks {desiredAction}");

                return proneAllowed;

            case PostureState.Crouching:
                // Can't use some heavy weapons or actions requiring full height
                // Most actions available
                return true;

            case PostureState.Swimming:
                // Very limited actions in water
                bool swimAllowed = desiredAction == UpperBodyState.Idle ||
                                  desiredAction == UpperBodyState.ThrowingItem;

                if (!swimAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Swimming blocks {desiredAction}");

                return swimAllowed;

            case PostureState.Climbing:
                // Only climbing actions
                bool climbAllowed = desiredAction == UpperBodyState.Climbing ||
                                   desiredAction == UpperBodyState.Idle;

                if (!climbAllowed && debugPermissions)
                    Debug.Log($"[Permissions] Climbing blocks {desiredAction}");

                return climbAllowed;

            case PostureState.KnockedDown:
            case PostureState.Stunned:
            case PostureState.Ragdoll:
                // No actions allowed
                bool forcedBlocked = desiredAction != UpperBodyState.Idle &&
                                    desiredAction != UpperBodyState.KnockedDown &&
                                    desiredAction != UpperBodyState.Stunned;

                if (forcedBlocked && debugPermissions)
                    Debug.Log($"[Permissions] {posture} blocks all actions");

                return !forcedBlocked;

            default:
                return true;
        }
    }

    // ===== POSTURE CHANGE RESTRICTIONS =====

    /// <summary>
    /// Check if posture can be changed based on current states.
    /// Some states prevent changing body position.
    /// </summary>
    public virtual bool CanChangePosture(UpperBodyState upperBody, LowerBodyState lowerBody, PostureState desiredPosture)
    {
        // Can't change posture while mid-swing
        if (upperBody == UpperBodyState.MeleeSwing)
        {
            if (debugPermissions)
                Debug.Log("[Permissions] Can't change posture during MeleeSwing");
            return false;
        }

        // Can't change posture while dashing
        if (lowerBody == LowerBodyState.Dashing || lowerBody == LowerBodyState.Dodging)
        {
            if (debugPermissions)
                Debug.Log($"[Permissions] Can't change posture during {lowerBody}");
            return false;
        }

        // Can't change posture while channeling
        if (upperBody == UpperBodyState.CastingChannel)
        {
            if (debugPermissions)
                Debug.Log("[Permissions] Can't change posture while channeling");
            return false;
        }

        return true;
    }
}