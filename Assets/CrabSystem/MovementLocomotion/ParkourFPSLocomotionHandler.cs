using UnityEngine;

/// <summary>
/// Parkour FPS Locomotion Handler - State-Reactive Rigidbody Movement
/// 
/// ARCHITECTURE:
/// - StateMachineModule owns ALL gameplay state
/// - This handler is PURELY REACTIVE to state
/// - Applies forces, velocities, and physics based on current state
/// - NEVER maintains its own state machine
/// 
/// RESPONSIBILITIES:
/// - Apply movement forces based on LowerBodyState
/// - Apply gravity modifications based on PostureState
/// - Apply adhesion forces during wall-runs
/// - Apply impulse forces for jumps/vaults
/// - Adjust drag, gravity scale, constraints per state
/// 
/// DOES NOT:
/// - Decide if wall-run is allowed (that's ParkourStateController)
/// - Track timers for gameplay state (that's StateMachineModule)
/// - Change state without permission (requests only)
/// </summary>
public class ParkourFPSLocomotionHandler : LocomotionHandler
{
    #region Physics Settings

    [Header("Ground Movement")]
    [SerializeField] private new float walkSpeed = 2f;
    [SerializeField] private new float runSpeed = 5f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float groundAcceleration = 10f;
    [SerializeField] private float groundDrag = 6f;
    [SerializeField] private float airAcceleration = 2f;
    [SerializeField] private float airDrag = 0.1f;
    [SerializeField] private float sprintMultiplier = 1.5f;

    [Header("Jump Forces")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float doubleJumpForce = 7f;
    [SerializeField] private float wallJumpUpForce = 7f;
    [SerializeField] private float wallJumpOutForce = 5f;

    [Header("Wall Running Physics")]
    [SerializeField] private float wallRunForce = 200f;
    [SerializeField] private float wallRunGravityScale = 0.3f;
    [SerializeField] private float wallAdhesionForce = 100f;
    [SerializeField] private float wallClimbUpSpeed = 3f;

    [Header("Sliding Physics")]
    [SerializeField] private float slideForce = 400f;
    [SerializeField] private float slideGravityMultiplier = 2f;
    [SerializeField] private float slopeSlideBonusForce = 200f;

    [Header("Vault/Mantle Forces")]
    [SerializeField] private float vaultUpForce = 6f;
    [SerializeField] private float vaultForwardForce = 8f;
    [SerializeField] private float mantleUpForce = 10f;
    [SerializeField] private float mantleForwardForce = 3f;

    [Header("Gravity")]
    [SerializeField] private float normalGravityScale = 1f;
    [SerializeField] private float fallingGravityScale = 1.5f;
    [SerializeField] private float groundedGravityForce = -2f;

    [Header("Collision Shape Adjustments")]
    [SerializeField] private float slideCapsuleHeightScale = 0.5f;
    [SerializeField] private Transform playerModel;

    #endregion

    #region Components

    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;
    private StateMachineModule stateMachine;
    private IAnimationProvider animationProvider;

    private float originalCapsuleHeight;
    private float originalModelScaleY;

    #endregion

    #region State Cache

    // Cache state queries to avoid repeated lookups per frame
    private LowerBodyState cachedLowerBodyState;
    private PostureState cachedPostureState;
    private bool cachedIsGrounded;

    #endregion

    #region Initialization

    public override void Initialize(MovementSystem system)
    {
        base.Initialize(system);

        // Get Rigidbody (REQUIRED)
        rb = GetComponentInParent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"[ParkourFPSLocomotionHandler] Rigidbody not found! This handler requires Rigidbody physics.");
            enabled = false;
            return;
        }

        rb.freezeRotation = true;
        rb.useGravity = true;

        // Get capsule collider for shape adjustments
        capsuleCollider = GetComponentInParent<CapsuleCollider>();
        if (capsuleCollider != null)
        {
            originalCapsuleHeight = capsuleCollider.height;
        }

        // Get model for visual scaling
        if (playerModel != null)
        {
            originalModelScaleY = playerModel.localScale.y;
        }

        // Get StateMachineModule (REQUIRED)
        stateMachine = system.Brain.GetModule<StateMachineModule>();
        if (stateMachine == null)
        {
            Debug.LogError($"[ParkourFPSLocomotionHandler] StateMachineModule not found! This handler requires centralized state.");
            enabled = false;
            return;
        }

        // Get animation provider
        animationProvider = system.Brain.GetModuleImplementing<IAnimationProvider>();

        if (showDebugInfo)
            Debug.Log($"[ParkourFPSLocomotionHandler] Initialized with state-reactive architecture");
    }

    #endregion

    #region Main Execution

    public override void ExecuteMovement(MovementInput input)
    {
        // Cache state queries for this frame
        CacheStateQueries();

        // Apply collision shape based on posture
        ApplyPostureShape();

        // Apply movement based on lower body state
        ApplyMovementForces(input);

        // Apply gravity based on state
        ApplyStateGravity();

        // Apply drag based on state
        ApplyStateDrag();

        // Update velocity cache
        currentVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        verticalVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        // Update animations
        UpdateAnimations();
    }

    #endregion

    #region State Caching

    private void CacheStateQueries()
    {
        if (stateMachine == null) return;

        cachedLowerBodyState = stateMachine.GetLowerBodyState();
        cachedPostureState = stateMachine.GetPostureState();
        cachedIsGrounded = stateMachine.IsGrounded;
    }

    #endregion

    #region Posture Shape Application

    /// <summary>
    /// Adjust collision shape based on posture state
    /// This is a REACTIVE response to posture changes
    /// </summary>
    private void ApplyPostureShape()
    {
        if (capsuleCollider == null) return;

        switch (cachedPostureState)
        {
            case PostureState.Sliding:
                // Shrink capsule for slide
                capsuleCollider.height = originalCapsuleHeight * slideCapsuleHeightScale;
                if (playerModel != null)
                {
                    playerModel.localScale = new Vector3(
                        playerModel.localScale.x,
                        originalModelScaleY * slideCapsuleHeightScale,
                        playerModel.localScale.z
                    );
                }
                break;

            default:
                // Restore normal height
                capsuleCollider.height = originalCapsuleHeight;
                if (playerModel != null)
                {
                    playerModel.localScale = new Vector3(
                        playerModel.localScale.x,
                        originalModelScaleY,
                        playerModel.localScale.z
                    );
                }
                break;
        }
    }

    #endregion

    #region Movement Force Application

    /// <summary>
    /// Apply movement forces based on LowerBodyState
    /// This is the core "state-reactive" logic
    /// </summary>
    private void ApplyMovementForces(MovementInput input)
    {
        Vector3 moveDirection = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);

        switch (cachedLowerBodyState)
        {
            case LowerBodyState.Walking:
            case LowerBodyState.Running:
                ApplyGroundMovement(moveDirection, walkSpeed);
                break;

            case LowerBodyState.Sprinting:
                ApplyGroundMovement(moveDirection, runSpeed * sprintMultiplier);
                break;

            case LowerBodyState.AirStrafing:
            case LowerBodyState.Jumping:
            case LowerBodyState.DoubleJump:
            case LowerBodyState.Falling:
                ApplyAirMovement(moveDirection);
                break;

            case LowerBodyState.WallRunningLeft:
            case LowerBodyState.WallRunningRight:
                ApplyWallRunMovement(input);
                break;

            case LowerBodyState.WallClimbing:
                ApplyWallClimbMovement();
                break;

            case LowerBodyState.Sliding:
                ApplySlidingMovement(moveDirection);
                break;

            case LowerBodyState.Vaulting:
                ApplyVaultForce();
                break;

            case LowerBodyState.Mantling:
                ApplyMantleForce();
                break;

            case LowerBodyState.Idle:
            case LowerBodyState.Landing:
            case LowerBodyState.HardLanding:
                // No active forces, let drag slow down
                break;
        }
    }

    private void ApplyGroundMovement(Vector3 direction, float speed)
    {
        if (direction.magnitude < 0.01f) return;

        Vector3 targetVelocity = direction.normalized * speed;
        Vector3 velocityDiff = targetVelocity - new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        rb.AddForce(velocityDiff * groundAcceleration, ForceMode.Acceleration);
    }

    private void ApplyAirMovement(Vector3 direction)
    {
        if (direction.magnitude < 0.01f) return;

        Vector3 targetVelocity = direction.normalized * moveSpeed;
        Vector3 velocityDiff = targetVelocity - new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        rb.AddForce(velocityDiff * airAcceleration, ForceMode.Acceleration);
    }

    private void ApplyWallRunMovement(MovementInput input)
    {
        // Wall-run movement is handled by ParkourStateController
        // This just applies adhesion force to keep player on wall

        // Get wall normal from ParkourStateController (would be injected or stored)
        // For now, approximate from velocity
        Vector3 wallNormal = Vector3.Cross(rb.linearVelocity.normalized, Vector3.up);

        // Apply adhesion force toward wall
        rb.AddForce(-wallNormal * wallAdhesionForce, ForceMode.Force);
    }

    private void ApplyWallClimbMovement()
    {
        // Override vertical velocity to climb up
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, wallClimbUpSpeed, rb.linearVelocity.z);
    }

    private void ApplySlidingMovement(Vector3 direction)
    {
        // Sliding maintains momentum with friction
        rb.AddForce(direction.normalized * slideForce, ForceMode.Force);

        // Bonus force on slopes
        if (Physics.Raycast(rootTransform.position, Vector3.down, out RaycastHit hit, 2f))
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            if (slopeAngle > 10f)
            {
                Vector3 slopeDirection = Vector3.ProjectOnPlane(Vector3.down, hit.normal);
                rb.AddForce(slopeDirection * slopeSlideBonusForce, ForceMode.Force);
            }
        }
    }

    private void ApplyVaultForce()
    {
        // Vault is a quick impulse up and forward
        // This would typically be called once on state entry via animation event
        Vector3 force = rootTransform.forward * vaultForwardForce + Vector3.up * vaultUpForce;
        rb.linearVelocity = force;
    }

    private void ApplyMantleForce()
    {
        // Mantle is a pull-up
        Vector3 force = rootTransform.forward * mantleForwardForce + Vector3.up * mantleUpForce;
        rb.linearVelocity = force;
    }

    #endregion

    #region Gravity Application

    /// <summary>
    /// Apply gravity modifications based on state
    /// </summary>
    private void ApplyStateGravity()
    {
        switch (cachedLowerBodyState)
        {
            case LowerBodyState.WallRunningLeft:
            case LowerBodyState.WallRunningRight:
                // Reduced gravity during wall-run
                rb.AddForce(Physics.gravity * (wallRunGravityScale - 1f), ForceMode.Acceleration);
                break;

            case LowerBodyState.WallClimbing:
                // No gravity during climb
                rb.AddForce(-Physics.gravity, ForceMode.Acceleration);
                break;

            case LowerBodyState.Falling:
                // Increased gravity for snappy fall
                rb.AddForce(Physics.gravity * (fallingGravityScale - 1f), ForceMode.Acceleration);
                break;
        }

        // Posture-based gravity
        if (cachedPostureState == PostureState.Sliding)
        {
            rb.AddForce(Physics.gravity * (slideGravityMultiplier - 1f), ForceMode.Acceleration);
        }

        // Grounded gravity snap
        if (cachedIsGrounded && rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, groundedGravityForce, rb.linearVelocity.z);
        }
    }

    #endregion

    #region Drag Application

    private void ApplyStateDrag()
    {
        if (cachedIsGrounded)
        {
            rb.linearDamping = groundDrag;
        }
        else
        {
            rb.linearDamping = airDrag;
        }
    }

    #endregion

    #region Animation Updates

    private void UpdateAnimations()
    {
        if (animationProvider == null) return;

        // Movement speed
        float normalizedSpeed = rb.linearVelocity.magnitude / (runSpeed * sprintMultiplier);
        animationProvider.SetFloat("MovementSpeed", normalizedSpeed);

        // Grounded
        animationProvider.SetBool("IsGrounded", cachedIsGrounded);

        // Vertical velocity
        animationProvider.SetFloat("VerticalVelocity", rb.linearVelocity.y);

        // State flags
        animationProvider.SetBool("IsWallRunning",
            cachedLowerBodyState == LowerBodyState.WallRunningLeft ||
            cachedLowerBodyState == LowerBodyState.WallRunningRight);

        animationProvider.SetBool("IsClimbing",
            cachedLowerBodyState == LowerBodyState.WallClimbing);

        animationProvider.SetBool("IsSliding",
            cachedPostureState == PostureState.Sliding);
    }

    #endregion

    #region Public API for Jump/Vault Impulses

    /// <summary>
    /// Apply jump impulse (called by ParkourStateController when jump is approved)
    /// </summary>
    public void ApplyJumpImpulse()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    /// <summary>
    /// Apply double jump impulse
    /// </summary>
    public void ApplyDoubleJumpImpulse()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * doubleJumpForce, ForceMode.Impulse);
    }

    /// <summary>
    /// Apply wall jump impulse (called when jumping off wall)
    /// </summary>
    public void ApplyWallJumpImpulse(Vector3 wallNormal)
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 force = Vector3.up * wallJumpUpForce + wallNormal * wallJumpOutForce;
        rb.AddForce(force, ForceMode.Impulse);
    }

    #endregion

    #region Properties

    public override bool IsGrounded => cachedIsGrounded;
    public override bool IsMoving => rb.linearVelocity.magnitude > 0.1f;
    public override Vector3 Velocity => rb.linearVelocity;
    public override Vector3 VerticalVelocity => new Vector3(0f, rb.linearVelocity.y, 0f);

    #endregion

    #region Debug Visualization

    protected override void OnDrawGizmos()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        base.OnDrawGizmos();

        // Draw state label
        if (stateMachine != null)
        {
            Vector3 labelPos = rootTransform.position + Vector3.up * 3f;

#if UNITY_EDITOR
            string stateText = $"Lower: {cachedLowerBodyState}\nPosture: {cachedPostureState}";
            UnityEditor.Handles.Label(labelPos, stateText);
#endif
        }
    }

    #endregion
}