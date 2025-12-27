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

    [Header("Animation Parameters")]
    [SerializeField] private string movementSpeedParam = "MovementSpeed";
    [SerializeField] private string isGroundedParam = "IsGrounded";
    [SerializeField] private string isLockedOnParam = "IsLockedOn";
    [SerializeField] private string strafeXParam = "StrafeX";
    [SerializeField] private string strafeYParam = "StrafeY";
    [SerializeField] private string jumpTriggerParam = "JumpTrigger";

    // References
    private IAnimationProvider animationProvider;

    // State
    private Vector3 targetVelocity;
    private Vector2 movementInput;
    private Vector2 normalizedStrafeInput;
    private bool isSprinting;
    private bool isLockedOn;
    private bool abilityDashing; // Tracks if ability dash is active

    // Jump
    private float lastGroundedTime;
    private float lastJumpInputTime;
    private int airJumpsUsed;

    public override void Initialize(MovementSystem system)
    {
        base.Initialize(system);
        animationProvider = system.Brain.GetModuleImplementing<IAnimationProvider>();
    }

    public override void ExecuteMovement(MovementInput input)
    {
        movementInput = input.MoveDirection;
        isSprinting = input.Sprint;
        isLockedOn = input.HasLookInput;

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
        if (isSprinting) return sprintSpeed;
        if (movementInput.magnitude > 0.5f) return runSpeed;
        return walkSpeed;
    }

    float GetStrafeSpeed()
    {
        if (isSprinting) return strafeSprintSpeed;
        if (movementInput.magnitude > 0.5f) return strafeRunSpeed;
        return strafeWalkSpeed;
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
            airJumpsUsed = 0;
            lastJumpInputTime = 0f;

            // Trigger jump animation
            if (animationProvider != null && !string.IsNullOrEmpty(jumpTriggerParam))
            {
                animationProvider.SetTrigger(jumpTriggerParam);
            }
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

    protected override void ApplyGravity()
    {
        if (movementSystem.IsGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = groundedGravity;
            lastGroundedTime = Time.time;
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
        if (animationProvider == null) return;

        // Movement speed (normalized 0-1, clamped to prevent overshoot)
        float normalizedSpeed = currentVelocity.magnitude / sprintSpeed;
        normalizedSpeed = Mathf.Clamp01(normalizedSpeed);
        animationProvider.SetFloat(movementSpeedParam, normalizedSpeed);

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

    /// <summary>
    /// Apply an impulse force (for knockback, pushback effects)
    /// </summary>
    public void ApplyImpulse(Vector3 direction, float force)
    {
        Vector3 impulse = direction.normalized * force;
        currentVelocity += impulse;
    }

    /// <summary>
    /// Start a dash with specific direction, speed, and duration
    /// Called by dash abilities via MovementEffect
    /// </summary>
    public void StartDash(Vector3 direction, float speed, float duration)
    {
        // Ability dashes override current velocity for duration
        StartCoroutine(AbilityDashCoroutine(direction.normalized * speed, duration));
    }

    /// <summary>
    /// Teleport to a position (for teleport abilities)
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        if (characterController != null)
        {
            characterController.enabled = false;
            rootTransform.position = position;
            characterController.enabled = true;

            // Reset velocity to prevent slide/fall on arrival
            currentVelocity = Vector3.zero;
            verticalVelocity = Vector3.zero;
        }
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
}