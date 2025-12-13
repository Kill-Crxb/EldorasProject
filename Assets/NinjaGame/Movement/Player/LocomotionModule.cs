using UnityEngine;
using System.Collections;

/// <summary>
/// LocomotionModule - Complete movement system
/// Integrates: ThirdPersonController movement + AdvancedMovementModule features
/// Features: Smooth movement, strafe, lock-on, jump+coyote time, dash, input buffering
/// </summary>
public class LocomotionModule : MonoBehaviour, IBrainModule, IMovementProvider
{
    [Header("Module Config")]
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    [Header("Required Components")]
    [SerializeField] private CharacterController characterControllerRef;
    [SerializeField] private Rigidbody rbRef;

    [Header("Movement Settings")]
    [SerializeField] private MovementStyle movementStyle = MovementStyle.Dynamic;
    [SerializeField] private bool sprintForcesFreeMovement = true;
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 4f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 10f;

    [Header("Lock-On Movement")]
    [SerializeField] private float strafeWalkSpeed = 1.5f;
    [SerializeField] private float strafeRunSpeed = 3f;
    [SerializeField] private float strafeSprintSpeed = 4.5f;
    [SerializeField] private float lockOnRotationSpeed = 15f;

    [Header("Physics")]
    [SerializeField] private float gravity = -9.81f;  // Standard Unity gravity

    [Header("Jump Settings")]
    [SerializeField] private bool canJump = true;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float coyoteTime = 0.1f;        // Grace period after leaving ground
    [SerializeField] private float jumpBufferTime = 0.1f;    // Input buffering for responsive jumps
    [SerializeField] private bool canDoubleJump = false;
    [SerializeField] private int maxAirJumps = 0;

    [Header("Dash Settings")]
    [SerializeField] private bool dashEnabled = true;
    [SerializeField] private float dashForce = 15f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.5f;

    [Header("Animation Parameters")]
    [SerializeField] private string movementStateParam = "MovementState";
    [SerializeField] private string isLockedOnParam = "IsLockedOn";
    [SerializeField] private string strafeXParam = "StrafeX";
    [SerializeField] private string strafeYParam = "StrafeY";
    [SerializeField] private string movementSpeedParam = "MovementSpeed";
    [SerializeField] private string jumpTriggerParam = "JumpTrigger";
    [SerializeField] private string dashTriggerParam = "DashTrigger";
    [SerializeField] private string isDashingParam = "IsDashing";

    [Header("Debug")]
    [SerializeField] private bool debugMovement = false;

    // Movement style enum (from ThirdPersonController)
    public enum MovementStyle
    {
        Dynamic,      // Switches based on lock-on state
        AlwaysStrafe, // Always uses strafe movement
        AlwaysFree    // Always uses free movement
    }

    // Movement state enum for animations (from ThirdPersonController)
    public enum MovementState
    {
        Idle = 0,
        Walking = 1,
        Running = 2,
        Sprinting = 3,
        StrafeIdle = 4,
        StrafeWalk = 5,
        StrafeRun = 6,
        StrafeSprint = 7
    }

    // Components
    private ControllerBrain brain;
    private CharacterController characterController;
    private Rigidbody rb;
    private IInputProvider inputProvider;
    private IAnimationProvider animationProvider;
    private AbilityModule abilityModule;

    // Root transforms (CRITICAL - like ThirdPersonController)
    private Transform playerRoot;  // The actual Player GameObject
    private Transform modelRoot;

    // Movement variables (from ThirdPersonController)
    private Vector3 currentVelocity;   // Current velocity (smoothly lerped)
    private Vector3 targetVelocity;    // Desired velocity
    private Vector3 verticalVelocity;  // Gravity/jump velocity (separate)
    private MovementState currentMovementState;
    private bool isSprinting;

    // Dash system (from AdvancedMovementModule)
    private bool isDashing;
    private bool dashAvailable = true;
    private Vector3 dashVelocity;
    private float lastDashTime;

    // Strafe variables (for lock-on support)
    private Vector2 strafeInput;
    private Vector2 normalizedStrafeInput;
    private float currentStrafeSpeed;

    // Input
    private Vector2 movementInput;

    // Target lock
    private bool isLockedOn;
    private Transform lockedTarget;

    // Jump tracking (from AdvancedMovementModule)
    private int airJumpCount;
    private float lastGroundedTime;
    private float lastGroundExitTime;
    private bool jumpInputPressed;
    private float jumpInputTime;

    // Cached camera transform
    private Transform cachedCameraTransform;

    // IMovementProvider implementation
    public Vector3 Velocity => currentVelocity;
    public bool IsGrounded => characterController != null && characterController.isGrounded;
    public bool IsSprinting => isSprinting;
    public bool IsMoving => currentVelocity.magnitude > 0.1f;
    public Vector3 MoveDirection => currentVelocity.normalized;

    // Additional properties
    public bool IsDashing => isDashing;
    public int CurrentAirJumps => airJumpCount;

    // Effective strafe state (from ThirdPersonController)
    private bool EffectiveStrafeMode
    {
        get
        {
            // Sprint always forces free movement if enabled
            if (sprintForcesFreeMovement && isSprinting)
                return false;

            return movementStyle switch
            {
                MovementStyle.AlwaysStrafe => true,
                MovementStyle.AlwaysFree => false,
                MovementStyle.Dynamic => isLockedOn,
                _ => isLockedOn
            };
        }
    }

    // Coyote time support (from AdvancedMovementModule)
    private bool CanGroundJump => canJump && (IsGrounded || Time.time <= lastGroundExitTime + coyoteTime);
    private bool CanAirJump => canJump && !IsGrounded && airJumpCount < maxAirJumps;
    private bool CanDash => dashEnabled && dashAvailable && !isDashing && Time.time >= lastDashTime + dashCooldown;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;


        // Setup player references (CRITICAL)
        SetupPlayerReferences();

        // Get CharacterController
        characterController = characterControllerRef != null ? characterControllerRef : GetComponent<CharacterController>();
        if (characterController == null)
            characterController = GetComponentInParent<CharacterController>();

        if (characterController == null)
        {
            Debug.LogError($"[LocomotionModule] CharacterController not found on {gameObject.name}!");
            return;
        }

        // Get Rigidbody
        rb = rbRef != null ? rbRef : GetComponent<Rigidbody>();
        if (rb == null)
            rb = GetComponentInParent<Rigidbody>();

        if (rb != null && !rb.isKinematic)
        {
            Debug.LogWarning($"[LocomotionModule] Rigidbody on {gameObject.name} should be kinematic!");
            rb.isKinematic = true;
        }

        // Get providers
        inputProvider = brain.GetModuleImplementing<IInputProvider>();
        animationProvider = brain.GetModuleImplementing<IAnimationProvider>();
        abilityModule = brain.GetModule<AbilityModule>();

        // Subscribe to feet events for jump reset
        brain.OnFeetEnter += HandleFeetEnter;
        brain.OnFeetExit += HandleFeetExit;

        if (debugMovement)
            Debug.Log($"[LocomotionModule] Initialized for {gameObject.name}");
    }

    void SetupPlayerReferences()
    {
        // Structure: Player (root) -> Component_Brain -> Component_Locomotion (this)
        var brainTransform = transform.parent;
        playerRoot = brainTransform?.parent;

        if (playerRoot == null)
        {
            Debug.LogError("[LocomotionModule] Could not find Player root!");
            playerRoot = transform;
        }

        modelRoot = brain?.ModelRoot;

        if (debugMovement)
            Debug.Log($"[LocomotionModule] PlayerRoot: {playerRoot?.name}, ModelRoot: {modelRoot?.name}");
    }

    public void UpdateModule()
    {
        if (!IsEnabled || characterController == null) return;

        // Check if movement is locked by animation events
        bool isMovementLocked = abilityModule != null && abilityModule.IsMovementLocked;

        // Get input
        movementInput = inputProvider?.MoveInput ?? Vector2.zero;

        // Handle movement - skip if locked
        if (!isMovementLocked)
        {
            HandleMovement();
        }
        else
        {
            // Movement is locked - zero out horizontal velocity but keep vertical (gravity)
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
            ApplyGravity();
            Vector3 totalVelocity = currentVelocity + verticalVelocity;
            characterController.Move(totalVelocity * Time.deltaTime);
        }

        HandleJumpInput();
        UpdateMovementState();
        UpdateAnimationParameters();
    }

    #region Movement System (from ThirdPersonController)

    void HandleMovement()
    {
        ApplyGravity();

        // Dash takes priority over normal movement
        if (isDashing)
        {
            currentVelocity = dashVelocity;
        }
        else if (EffectiveStrafeMode && (movementStyle == MovementStyle.AlwaysStrafe || lockedTarget != null))
        {
            HandleLockedOnMovement();
        }
        else
        {
            HandleFreeMovement();
        }

        // Apply movement
        Vector3 totalVelocity = currentVelocity + verticalVelocity;
        characterController.Move(totalVelocity * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (characterController.isGrounded && verticalVelocity.y <= 0f)
        {
            verticalVelocity.y = -2f; // Small downward force to stay grounded
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }
    }

    void HandleFreeMovement()
    {
        // Get camera direction
        Transform cameraTransform = GetCameraTransform();
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        // Flatten to horizontal plane
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();

        // Calculate movement direction
        Vector3 moveDirection = cameraForward * movementInput.y + cameraRight * movementInput.x;

        if (moveDirection.magnitude > 0.1f)
        {
            // Set target velocity
            float targetSpeed = GetTargetSpeed();
            targetVelocity = moveDirection.normalized * targetSpeed;

            // Rotate player to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            playerRoot.rotation = Quaternion.Lerp(playerRoot.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            targetVelocity = Vector3.zero;
        }

        // Smooth velocity change
        float lerpSpeed = targetVelocity.magnitude > currentVelocity.magnitude ? acceleration : deceleration;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, lerpSpeed * Time.deltaTime);

        // Clear strafe input
        strafeInput = Vector2.zero;
        normalizedStrafeInput = Vector2.zero;
    }

    void HandleLockedOnMovement()
    {
        Vector3 forwardVector;
        Vector3 rightVector;

        // Determine facing direction
        if (lockedTarget != null)
        {
            Vector3 directionToTarget = (lockedTarget.position - playerRoot.position);
            directionToTarget.y = 0;

            if (directionToTarget.magnitude < 0.1f)
            {
                directionToTarget = playerRoot.forward;
            }
            else
            {
                directionToTarget.Normalize();
            }

            if (directionToTarget.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                playerRoot.rotation = Quaternion.Lerp(playerRoot.rotation, targetRotation, lockOnRotationSpeed * Time.deltaTime);
            }

            forwardVector = directionToTarget;
            rightVector = Vector3.Cross(Vector3.up, directionToTarget).normalized;
        }
        else
        {
            // No target - use camera facing
            Transform cameraTransform = GetCameraTransform();
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();

            forwardVector = cameraForward;
            rightVector = Vector3.Cross(Vector3.up, cameraForward).normalized;

            if (forwardVector.magnitude > 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(forwardVector);
                playerRoot.rotation = Quaternion.Lerp(playerRoot.rotation, targetRotation, lockOnRotationSpeed * Time.deltaTime);
            }
        }

        // Store strafe input for animations
        strafeInput.x = movementInput.x;
        strafeInput.y = movementInput.y;

        // Normalize for animation blend tree
        normalizedStrafeInput = strafeInput.normalized * Mathf.Min(strafeInput.magnitude, 1f);

        // Calculate world space movement direction
        Vector3 moveDirection = forwardVector * strafeInput.y + rightVector * strafeInput.x;

        if (moveDirection.magnitude > 0.1f)
        {
            float targetSpeed = GetStrafeSpeed();
            targetVelocity = moveDirection.normalized * targetSpeed;
            currentStrafeSpeed = targetSpeed;
        }
        else
        {
            targetVelocity = Vector3.zero;
            currentStrafeSpeed = 0f;
        }

        // Faster acceleration for snappy strafe movement
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, acceleration * 1.5f * Time.deltaTime);
    }

    Transform GetCameraTransform()
    {
        if (cachedCameraTransform == null)
        {
            Camera mainCam = Camera.main;
            cachedCameraTransform = mainCam != null ? mainCam.transform : transform;
        }
        return cachedCameraTransform;
    }

    float GetTargetSpeed()
    {
        bool sprintInput = inputProvider?.SprintHeld ?? false;

        if (sprintInput && movementInput.y > 0.1f)
        {
            isSprinting = true;
            return sprintSpeed;
        }
        else if (movementInput.magnitude > 0.5f)
        {
            isSprinting = false;
            return runSpeed;
        }
        else
        {
            isSprinting = false;
            return walkSpeed;
        }
    }

    float GetStrafeSpeed()
    {
        bool sprintInput = inputProvider?.SprintHeld ?? false;

        if (sprintInput && movementInput.y > 0.1f)
        {
            isSprinting = true;
            return strafeSprintSpeed;
        }
        else if (movementInput.magnitude > 0.5f)
        {
            isSprinting = false;
            return strafeRunSpeed;
        }
        else
        {
            isSprinting = false;
            return strafeWalkSpeed;
        }
    }

    #endregion

    #region Jump System (from AdvancedMovementModule with coyote time)

    void HandleJumpInput()
    {
        if (!canJump) return;

        // Get jump input from provider
        bool jumpPressed = inputProvider?.JumpPressed ?? false;

        // Input buffering
        if (jumpPressed)
        {
            jumpInputPressed = true;
            jumpInputTime = Time.time;
        }

        // Check if we have recent jump input (buffering)
        bool hasRecentJumpInput = jumpInputPressed || (Time.time <= jumpInputTime + jumpBufferTime);
        if (!hasRecentJumpInput) return;

        // Check if we can jump
        bool canGroundJump = CanGroundJump;
        bool canAirJump = CanAirJump;

        if (canGroundJump || canAirJump)
        {
            PerformJump();
        }
    }

    void PerformJump()
    {
        // Consume input
        jumpInputPressed = false;
        jumpInputTime = 0f;

        // Apply jump force
        verticalVelocity.y = jumpForce;

        bool wasGrounded = IsGrounded || (Time.time <= lastGroundExitTime + coyoteTime);

        if (!wasGrounded)
        {
            // Air jump
            airJumpCount++;
            if (debugMovement)
                Debug.Log($"[LocomotionModule] Air jump {airJumpCount}/{maxAirJumps}");
        }
        else
        {
            // Ground jump - reset air jumps
            airJumpCount = 0;
            if (debugMovement)
                Debug.Log("[LocomotionModule] Ground jump");
        }

        // Trigger animation
        if (animationProvider != null && !string.IsNullOrEmpty(jumpTriggerParam))
        {
            animationProvider.SetTrigger(jumpTriggerParam);
        }
    }

    void HandleFeetEnter(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground)
        {
            lastGroundedTime = Time.time;
            airJumpCount = 0;

            if (debugMovement)
                Debug.Log("[LocomotionModule] Landed on ground");
        }
    }

    void HandleFeetExit(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground && !IsGrounded)
        {
            lastGroundExitTime = Time.time;

            if (debugMovement)
                Debug.Log("[LocomotionModule] Left ground - coyote time active");
        }
    }

    #endregion

    #region Dash API (called by Dash Abilities)

    /// <summary>
    /// Start a dash in the specified direction with override
    /// Called by Dash abilities through MovementEffect
    /// </summary>
    public void StartDash(Vector3 direction, float force, float duration)
    {
        if (isDashing)
        {
            if (debugMovement)
                Debug.LogWarning("[LocomotionModule] Already dashing, ignoring new dash");
            return;
        }

        // Start dash
        isDashing = true;
        dashAvailable = false;
        dashVelocity = direction.normalized * force;
        lastDashTime = Time.time;

        // Trigger animation
        if (animationProvider != null)
        {
            if (!string.IsNullOrEmpty(dashTriggerParam))
                animationProvider.SetTrigger(dashTriggerParam);

            if (!string.IsNullOrEmpty(isDashingParam))
                animationProvider.SetBool(isDashingParam, true);
        }

        // Start dash coroutine
        StartCoroutine(DashCoroutine(duration));

        if (debugMovement)
            Debug.Log($"[LocomotionModule] Dash started: direction={direction}, force={force}, duration={duration}");
    }

    /// <summary>
    /// Check if a dash can be performed (for ability validation)
    /// </summary>
    public bool CanPerformDash()
    {
        return dashEnabled && dashAvailable && !isDashing && Time.time >= lastDashTime + dashCooldown;
    }

    IEnumerator DashCoroutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // End dash
        isDashing = false;
        dashVelocity = Vector3.zero;

        // Clear dash animation
        if (animationProvider != null && !string.IsNullOrEmpty(isDashingParam))
        {
            animationProvider.SetBool(isDashingParam, false);
        }

        // Cooldown
        yield return new WaitForSeconds(dashCooldown);
        dashAvailable = true;

        if (debugMovement)
            Debug.Log("[LocomotionModule] Dash ended, cooldown complete");
    }

    #endregion

    #region Animation & State Updates

    void UpdateMovementState()
    {
        MovementState newState;

        if (currentVelocity.magnitude < 0.1f)
        {
            newState = EffectiveStrafeMode ? MovementState.StrafeIdle : MovementState.Idle;
        }
        else if (EffectiveStrafeMode)
        {
            // Strafe states
            if (isSprinting && currentVelocity.magnitude > strafeRunSpeed + 0.1f)
            {
                newState = MovementState.StrafeSprint;
            }
            else if (currentVelocity.magnitude > strafeWalkSpeed + 0.1f)
            {
                newState = MovementState.StrafeRun;
            }
            else
            {
                newState = MovementState.StrafeWalk;
            }
        }
        else
        {
            // Free movement states
            if (isSprinting && currentVelocity.magnitude > runSpeed + 0.1f)
            {
                newState = MovementState.Sprinting;
            }
            else if (currentVelocity.magnitude > walkSpeed + 0.1f)
            {
                newState = MovementState.Running;
            }
            else
            {
                newState = MovementState.Walking;
            }
        }

        currentMovementState = newState;
    }

    void UpdateAnimationParameters()
    {
        if (animationProvider == null) return;

        // Movement state
        animationProvider.SetInteger(movementStateParam, (int)currentMovementState);

        // Strafe mode
        animationProvider.SetBool(isLockedOnParam, EffectiveStrafeMode);

        // Strafe input
        animationProvider.SetFloat(strafeXParam, normalizedStrafeInput.x);
        animationProvider.SetFloat(strafeYParam, normalizedStrafeInput.y);

        // Movement speed (normalized 0-1)
        float normalizedSpeed = currentVelocity.magnitude / sprintSpeed;
        animationProvider.SetFloat(movementSpeedParam, normalizedSpeed);

        // Grounded state
        animationProvider.SetBool("IsGrounded", IsGrounded);

        // Vertical velocity
        animationProvider.SetFloat("VerticalVelocity", verticalVelocity.y);
    }

    #endregion

    #region IMovementProvider Methods

    public void Move(Vector3 direction, float speed)
    {
        targetVelocity = direction.normalized * speed;
    }

    public void Rotate(Quaternion rotation)
    {
        if (playerRoot != null)
            playerRoot.rotation = rotation;
    }

    public void Stop()
    {
        targetVelocity = Vector3.zero;
        currentVelocity = Vector3.zero;
    }

    public void ApplyImpulse(Vector3 direction, float force)
    {
        // Add impulse to current velocity (for knockback, etc)
        currentVelocity += direction.normalized * force;

        if (debugMovement)
            Debug.Log($"[LocomotionModule] Impulse applied: {direction.normalized * force}");
    }

    public void TeleportTo(Vector3 position)
    {
        if (characterController == null) return;

        characterController.enabled = false;
        playerRoot.position = position;
        characterController.enabled = true;

        currentVelocity = Vector3.zero;
        targetVelocity = Vector3.zero;
        verticalVelocity = Vector3.zero;

        if (debugMovement)
            Debug.Log($"[LocomotionModule] Teleported to {position}");
    }

    #endregion

    #region Jump/Dash API (for external control)

    public void ApplyJumpVelocity(float force)
    {
        verticalVelocity.y = force;
    }

    public Vector3 GetVerticalVelocity() => verticalVelocity;

    public void SetVerticalVelocity(Vector3 velocity)
    {
        verticalVelocity = velocity;
    }

    public void SetDashVelocity(Vector3 velocity)
    {
        isDashing = true;
        dashVelocity = velocity;
    }

    public void ClearDashOverride()
    {
        isDashing = false;
        dashVelocity = Vector3.zero;
    }

    public void ForceDash(Vector3 direction)
    {
        dashVelocity = direction.normalized * dashForce;
        isDashing = true;
        StartCoroutine(DashCoroutine(dashDuration));
    }

    public void ForceJump(float customForce = -1f)
    {
        verticalVelocity.y = customForce > 0 ? customForce : jumpForce;
    }

    #endregion

    #region Target Lock Methods

    public void SetLockOnTarget(Transform target)
    {
        lockedTarget = target;
        isLockedOn = (target != null);

        if (debugMovement)
            Debug.Log($"[LocomotionModule] Lock-on target: {(target != null ? target.name : "None")}");
    }

    #endregion

    #region Debug Visualization

    void OnDrawGizmosSelected()
    {
        if (!debugMovement || !Application.isPlaying || playerRoot == null) return;

        // Draw current velocity
        Gizmos.color = Color.green;
        Gizmos.DrawRay(playerRoot.position + Vector3.up * 0.5f, currentVelocity);

        // Draw target velocity
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(playerRoot.position + Vector3.up * 0.7f, targetVelocity);

        // Draw dash velocity
        if (isDashing)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(playerRoot.position + Vector3.up * 0.9f, dashVelocity);
        }

        // Draw lock-on target
        if (EffectiveStrafeMode && lockedTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(playerRoot.position + Vector3.up * 1f, lockedTarget.position);

            Vector3 directionToTarget = (lockedTarget.position - playerRoot.position).normalized;
            Vector3 rightVector = Vector3.Cross(Vector3.up, directionToTarget);

            Gizmos.color = Color.magenta;
            Vector3 strafeDir = (directionToTarget * normalizedStrafeInput.y + rightVector * normalizedStrafeInput.x) * 2f;
            Gizmos.DrawRay(playerRoot.position + Vector3.up * 1.5f, strafeDir);
        }
    }

    #endregion

    void OnDestroy()
    {
        if (brain != null)
        {
            brain.OnFeetEnter -= HandleFeetEnter;
            brain.OnFeetExit -= HandleFeetExit;
        }
    }
}