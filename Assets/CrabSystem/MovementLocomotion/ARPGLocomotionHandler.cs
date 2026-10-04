using UnityEngine;

public enum MovementStyle
{
    Dynamic,      // Switches based on lock-on state
    AlwaysStrafe, // Always uses strafe movement
    AlwaysFree    // Always uses free movement
}

public class ARPGLocomotionHandler : LocomotionHandler
{
    [Header("Movement Settings")]
    [SerializeField] private MovementStyle movementStyle = MovementStyle.Dynamic;
    [SerializeField] private bool sprintForcesFreeMovement = true;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 10f;

    [Header("Gait")]
    [Tooltip("Start in walk mode. The gait toggle swaps walk and run; sprint is never input — it " +
             "is the run gait while SprintGranted is up and CannotSprint is not.")]
    [SerializeField] private bool startInWalkMode = false;

    [Header("Lock-On / Strafe")]
    [SerializeField] private float strafeWalkSpeed = 1.5f;
    [SerializeField] private float strafeRunSpeed = 3f;
    [SerializeField] private float strafeSprintSpeed = 4.5f;
    [SerializeField] private float lockOnRotationSpeed = 15f;

    [Header("Jump")]
    [SerializeField] private bool canJump = true;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private int maxAirJumps = 0;

    [Header("Wall Jump")]
    [Tooltip("Needs a ParkourAssistant on the brain. Without one this is silently unavailable.")]
    [SerializeField] private bool canWallJump = true;

    [Tooltip("Wall jumps allowed per airtime. Reset on landing, NOT per wall — touching a new " +
             "wall does not hand one back, or a corner becomes an infinite ladder.")]
    [SerializeField] private int maxWallJumps = 2;

    [Tooltip("Upward force. Left at 0 it uses jumpForce.")]
    [SerializeField] private float wallJumpForce = 0f;

    [Tooltip("Horizontal shove away from the wall. Zero means you climb straight up the face, " +
             "which lets you scale any wall by mashing jump.")]
    [SerializeField] private float wallJumpPush = 5f;

    [Header("Animation Parameters")]
    [IdRef(IdKind.AnimatorParam)] [SerializeField] private string movementSpeedParam = "MovementSpeed";
    [IdRef(IdKind.AnimatorParam)] [SerializeField] private string isGroundedParam = "IsGrounded";
    [IdRef(IdKind.AnimatorParam)] [SerializeField] private string isLockedOnParam = "IsLockedOn";
    [IdRef(IdKind.AnimatorParam)] [SerializeField] private string strafeXParam = "StrafeX";
    [IdRef(IdKind.AnimatorParam)] [SerializeField] private string strafeYParam = "StrafeY";
    [IdRef(IdKind.AnimatorParam)] [SerializeField] private string jumpTriggerParam = "JumpTrigger";
    [IdRef(IdKind.AnimatorParam)] [SerializeField] private string movementStateParam = "MovementState";

    // References
    private IAnimationProvider animationProvider;
    private TargetLockModule targetLock;

    // State
    private Vector3 targetVelocity;
    private Vector2 movementInput;
    private Vector2 normalizedStrafeInput;
    private bool isSprinting;
    private bool isLockedOn;
    private bool abilityDashing; // Tracks if ability dash is active

    private bool isInWalkMode;

    // Jump
    private float lastGroundedTime;
    private float lastJumpInputTime;
    private int airJumpsUsed;
    private int wallJumpsUsed;

    private ParkourAssistant parkour;

    public override void Initialize(MovementSystem system)
    {
        base.Initialize(system);
        animationProvider = system.Brain.GetProvider<IAnimationProvider>();
        targetLock = system.Brain.GetModule<TargetLockModule>();
        parkour = system.Brain.GetModule<ParkourAssistant>();

        // Initialize walk/run toggle state
        isInWalkMode = startInWalkMode;

        if (showDebugInfo)
        {
            Debug.Log($"[ARPGLocomotion] Initialized - " +
                     $"TargetLock: {(targetLock != null ? "Found" : "None")}, " +
                     $"Parkour: {(parkour != null ? "Found" : "None — wall jump disabled")}, " +
                     $"Speeds [W:{walkSpeed} R:{runSpeed} S:{sprintSpeed}]");
        }
    }

    public override void ExecuteMovement(MovementInput input)
    {
        movementInput = input.MoveDirection;

        // Check actual target lock state (not just look input)
        isLockedOn = targetLock != null && targetLock.IsLockedOn;

        if (input.ToggleGait)
            isInWalkMode = !isInWalkMode;

        isSprinting = CurrentGait == Gait.Sprint;

        ApplyGravity();

        // Short-circuit if ability dash is active (prevents movement fighting)
        if (abilityDashing)
        {
            Vector3 total = currentVelocity + verticalVelocity;
            MoveCharacterController(total);
            UpdateAnimations();
            return;
        }

        bool useStrafeMode = ShouldUseStrafe();

        if (useStrafeMode)
            HandleStrafeMovement(input);
        else
            HandleFreeMovement(input);

        HandleJump(input.Jump);

        Vector3 totalMovement = currentVelocity + verticalVelocity;
        MoveCharacterController(totalMovement);

        UpdateAnimations();
    }

    bool ShouldUseStrafe()
    {
        switch (movementStyle)
        {
            case MovementStyle.AlwaysStrafe:
                return true;
            case MovementStyle.AlwaysFree:
                return false;
            case MovementStyle.Dynamic:
                // Sprint forces free movement (even when locked on)
                if (sprintForcesFreeMovement && isSprinting)
                    return false;
                return isLockedOn;
            default:
                return false;
        }
    }

    // Same rule as ParkourLocomotionHandler: the walk/run toggle is the entity's, sprint is granted.
    public override Gait CurrentGait
    {
        get
        {
            if (isInWalkMode) return Gait.Walk;
            return SprintAvailable() ? Gait.Sprint : Gait.Run;
        }
    }

    // ============================
    // Movement
    // ============================

    void HandleFreeMovement(MovementInput input)
    {
        Vector3 moveDir = new Vector3(movementInput.x, 0f, movementInput.y);

        if (moveDir.magnitude > 0.1f)
        {
            float speed = GetMoveSpeed();
            targetVelocity = moveDir.normalized * speed;

            if (showDebugInfo)
            {
                Debug.Log($"[HandleFreeMovement] MoveDir: {moveDir.magnitude:F2}, " +
                         $"TargetSpeed: {speed:F2}, TargetVelocity: {targetVelocity.magnitude:F2}");
            }

            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            rootTransform.rotation = Quaternion.Lerp(
                rootTransform.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            );
        }
        else
        {
            targetVelocity = Vector3.zero;
        }

        float lerp = targetVelocity.magnitude > currentVelocity.magnitude
            ? acceleration
            : deceleration;

        currentVelocity = Vector3.Lerp(
            currentVelocity,
            targetVelocity,
            lerp * Time.deltaTime
        );

        if (showDebugInfo && Time.frameCount % 30 == 0) // Log every 30 frames
        {
            Debug.Log($"[Velocity] Current: {currentVelocity.magnitude:F2}, " +
                     $"Target: {targetVelocity.magnitude:F2}, " +
                     $"Lerp: {lerp:F2}");
        }

        normalizedStrafeInput = Vector2.zero;
    }

    void HandleStrafeMovement(MovementInput input)
    {
        Vector3 forward = new Vector3(input.LookDirection.x, 0f, input.LookDirection.y);
        if (forward.magnitude < 0.1f)
            forward = rootTransform.forward;

        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Quaternion targetRot = Quaternion.LookRotation(forward);
        rootTransform.rotation = Quaternion.Lerp(
            rootTransform.rotation,
            targetRot,
            lockOnRotationSpeed * Time.deltaTime
        );

        float fwd = Vector3.Dot(new Vector3(movementInput.x, 0, movementInput.y), forward);
        float side = Vector3.Dot(new Vector3(movementInput.x, 0, movementInput.y), right);

        normalizedStrafeInput = new Vector2(side, fwd).normalized *
                                Mathf.Min(new Vector2(side, fwd).magnitude, 1f);

        Vector3 moveDir = forward * fwd + right * side;

        if (moveDir.magnitude > 0.1f)
        {
            targetVelocity = moveDir.normalized * GetStrafeSpeed();
        }
        else
        {
            targetVelocity = Vector3.zero;
        }

        currentVelocity = Vector3.Lerp(
            currentVelocity,
            targetVelocity,
            acceleration * 1.5f * Time.deltaTime
        );

    }

    float GetMoveSpeed()
    {
        float baseSpeed;
        string speedType;

        // Intent-based speed selection (not magnitude-based)
        if (isSprinting)
        {
            baseSpeed = sprintSpeed;
            speedType = "SPRINT";
        }
        else if (isInWalkMode)
        {
            // Walk mode toggled on
            baseSpeed = walkSpeed;
            speedType = "WALK (toggled)";
        }
        else
        {
            // Default running
            baseSpeed = runSpeed;
            speedType = "RUN";
        }

        if (showDebugInfo)
        {
            Debug.Log($"[GetMoveSpeed] Type: {speedType}, Speed: {baseSpeed:F2}, " +
                     $"InputMag: {movementInput.magnitude:F2}, isSprinting: {isSprinting}, " +
                     $"isInWalkMode: {isInWalkMode}");
        }

        return baseSpeed;
    }

    float GetStrafeSpeed()
    {
        float baseSpeed;

        // Intent-based speed selection (same as GetMoveSpeed)
        if (isSprinting)
        {
            baseSpeed = strafeSprintSpeed;
        }
        else if (isInWalkMode)
        {
            // Walk mode toggled on
            baseSpeed = strafeWalkSpeed;
        }
        else
        {
            // Default running
            baseSpeed = strafeRunSpeed;
        }

        return baseSpeed;
    }

    // ============================
    // Jump
    // ============================

    void HandleJump(bool jumpPressed)
    {
        if (!canJump) return;

        if (jumpPressed)
            lastJumpInputTime = Time.time;

        bool buffered = Time.time <= lastJumpInputTime + jumpBufferTime;
        bool grounded = movementSystem.IsGrounded;
        bool coyote = Time.time <= lastGroundedTime + coyoteTime;

        if (!buffered) return;

        if (grounded || coyote)
        {
            verticalVelocity.y = jumpForce;
            lastJumpInputTime = 0f;

            // Trigger jump animation
            if (animationProvider != null && !string.IsNullOrEmpty(jumpTriggerParam))
            {
                animationProvider.SetTrigger(jumpTriggerParam);
            }
        }
        else if (TryWallJump())
        {
            lastJumpInputTime = 0f;
        }
        else if (airJumpsUsed < maxAirJumps)
        {
            verticalVelocity.y = jumpForce;
            airJumpsUsed++;
            lastJumpInputTime = 0f;

            // Trigger jump animation (air jump)
            if (animationProvider != null && !string.IsNullOrEmpty(jumpTriggerParam))
            {
                animationProvider.SetTrigger(jumpTriggerParam);
            }
        }
        else
        {
            // Clear buffer when jump rejected (prevents late jumps on landing)
            lastJumpInputTime = 0f;
        }
    }

    /// <summary>
    /// Kick off a wall, if there is one and the budget allows.
    ///
    /// Ordered ABOVE the air jump in HandleJump on purpose: a wall jump is the more specific
    /// move and should not silently burn an air jump charge when a wall was right there.
    ///
    /// ParkourAssistant only reports — it has no idea what a wall jump is. All the policy
    /// (how many, how hard, which way) lives here, with the rest of the jump rules.
    /// </summary>
    bool TryWallJump()
    {
        if (!canWallJump || parkour == null) return false;
        if (wallJumpsUsed >= maxWallJumps) return false;
        if (movementSystem.IsGrounded) return false;

        // Facing comes from the entity root, not from the brain object — the brain is a child
        // and does not necessarily rotate with the character.
        Transform body = movementSystem.Brain.EntityRoot != null
            ? movementSystem.Brain.EntityRoot
            : movementSystem.Brain.transform;

        // A wall on either side will do. A wall on the right shoves you left, and vice versa.
        Vector3 push;

        if (parkour.HasWallLeft)
            push = body.right;
        else if (parkour.HasWallRight)
            push = -body.right;
        else
            return false;

        verticalVelocity.y = wallJumpForce > 0f ? wallJumpForce : jumpForce;

        // Overwrite horizontal velocity rather than adding to it. Adding lets a player running
        // at the wall keep their inbound speed and stick to the face instead of leaving it.
        currentVelocity = push * wallJumpPush;

        wallJumpsUsed++;

        if (animationProvider != null && !string.IsNullOrEmpty(jumpTriggerParam))
            animationProvider.SetTrigger(jumpTriggerParam);

        if (showDebugInfo)
            Debug.Log($"[WallJump] {wallJumpsUsed}/{maxWallJumps} — pushed {push}");

        return true;
    }

    protected override void ApplyGravity()
    {
        if (movementSystem.IsGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = groundedGravity;
            lastGroundedTime = Time.time;

            // Reset here, on contact with the ground — NOT in the grounded branch of
            // HandleJump, which only runs when you actually jump. Walking off a ledge, using
            // an air jump, landing and walking off again used to leave the counter spent.
            airJumpsUsed = 0;
            wallJumpsUsed = 0;
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }
    }

    // ============================
    // Animations
    // ============================

    void UpdateAnimations()
    {

        if (animationProvider == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[UpdateAnimations] AnimationProvider is NULL!");
            return;
        }

        // VELOCITY-BASED ANIMATION (for smooth blending)
        // Send actual velocity magnitude to animator
        // The blend tree will smoothly interpolate between animation states
        float speed = currentVelocity.magnitude;
        int movementState;
        if (speed < 0.1f)
            movementState = 0; // Idle
        else if (isSprinting)
            movementState = 3; // Sprint
        else if (isInWalkMode)
            movementState = 1; // Walk
        else
            movementState = 2; // Run

        animationProvider.SetInteger(movementStateParam, movementState);
        if (showDebugInfo)
        {
            Debug.Log($"[UpdateAnimations] Velocity: {speed:F2} → Sent to animator '{movementSpeedParam}'");
        }

        animationProvider.SetFloat(movementSpeedParam, speed);

        // Grounded state
        animationProvider.SetBool(isGroundedParam, movementSystem.IsGrounded);

        // Lock-on / strafe mode
        animationProvider.SetBool(isLockedOnParam, ShouldUseStrafe());

        // Strafe input (for blend trees)
        animationProvider.SetFloat(strafeXParam, normalizedStrafeInput.x);
        animationProvider.SetFloat(strafeYParam, normalizedStrafeInput.y);
    }


    // ============================
    // Ability Support (called by MovementEffect)
    // ============================

    // ApplyImpulse and TeleportTo now live on LocomotionHandler. The base implementations ARE the
    // two that used to sit here, moved up so callers stop casting to this class to reach them —
    // that cast is what made every movement ability a silent no-op on the parkour handler.

    /// <summary>
    /// Start a dash with specific direction, speed, and duration.
    ///
    /// ⚠ This coroutine is the bool-soup the movement rework replaces, kept only because NPCs still
    /// run this handler. It is not cancellable, and the velocity it writes is invisible to friction,
    /// gravity and collision for its whole duration. Do not copy it into a new handler.
    /// </summary>
    public override bool BeginDash(Vector3 direction, float speed, float duration)
    {
        StartCoroutine(AbilityDashCoroutine(direction.normalized * speed, duration));
        return true;
    }

    private System.Collections.IEnumerator AbilityDashCoroutine(Vector3 velocity, float duration)
    {
        abilityDashing = true;
        Vector3 originalVelocity = currentVelocity;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            currentVelocity = velocity;
            elapsed += Time.deltaTime;
            yield return null;
        }

        abilityDashing = false;

        // Restore some velocity to avoid abrupt stop
        currentVelocity = originalVelocity * 0.5f;
    }

    // ============================
    // Public Properties (for other systems)
    // ============================

    /// <summary>
    /// Is the character currently sprinting?
    /// </summary>
    public bool IsSprinting => isSprinting;

    /// <summary>
    /// Is the character in strafe mode?
    /// </summary>
    public bool IsStrafing => ShouldUseStrafe();

    /// <summary>
    /// Is an ability dash currently active?
    /// </summary>
    public bool IsAbilityDashing => abilityDashing;

    /// <summary>
    /// Is walk mode currently active?
    /// </summary>
    public bool IsInWalkMode => isInWalkMode;

    // ============================
    // Debug / Testing
    // ============================

    [ContextMenu("Toggle Walk/Run Mode")]
    void DebugToggleWalkRun()
    {
        isInWalkMode = !isInWalkMode;
        Debug.Log($"[ARPGLocomotion] Manually toggled to: {(isInWalkMode ? "WALK" : "RUN")}");
    }
}