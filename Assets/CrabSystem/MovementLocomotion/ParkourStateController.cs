using UnityEngine;

/// <summary>
/// Parkour State Controller - Detects parkour conditions and requests state transitions
/// 
/// ARCHITECTURE:
/// - This module DETECTS conditions for parkour states (wall contact, ledges, etc.)
/// - REQUESTS transitions from StateMachineModule
/// - StateMachineModule validates and commits transitions
/// - Locomotion handler reacts to state changes
/// 
/// RESPONSIBILITIES:
/// - Detect wall contact (left/right/front)
/// - Detect ledges and vaultable obstacles
/// - Request wall-run entry/exit
/// - Request climb/vault/mantle transitions
/// - Request slide entry/exit
/// - Track parkour-specific timers (wall-run duration, slide duration)
/// 
/// DOES NOT:
/// - Apply movement forces (that's ParkourFPSLocomotionHandler)
/// - Own gameplay state (that's StateMachineModule)
/// - Bypass permissions (uses Try methods only)
/// </summary>
public class ParkourStateController : MonoBehaviour, IBrainModule
{
    #region Module Config

    [Header("Module Config")]
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    #endregion

    #region Detection Settings

    [Header("Wall Detection")]
    [SerializeField] private LayerMask wallLayers;
    [SerializeField] private float wallCheckDistance = 0.7f;
    [SerializeField] private float wallRunMinSpeed = 3f;
    [SerializeField] private float wallRunMaxTime = 2f;
    [SerializeField] private float minHeightAboveGround = 1.5f;

    [Header("Climb Detection")]
    [SerializeField] private float climbCheckDistance = 0.7f;
    [SerializeField] private float climbSphereCastRadius = 0.25f;
    [SerializeField] private float maxWallLookAngle = 40f;
    [SerializeField] private float climbMaxTime = 1f;

    [Header("Slide Settings")]
    [SerializeField] private float slideMinSpeed = 4f;
    [SerializeField] private float slideMaxTime = 1.5f;

    [Header("Vault/Mantle Detection")]
    [SerializeField] private float vaultHeight = 1.2f;
    [SerializeField] private float mantleHeight = 2f;
    [SerializeField] private float ledgeCheckDistance = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    #endregion

    #region Components

    private ControllerBrain brain;
    private StateMachineModule stateMachine;
    private MovementSystem movementSystem;
    private ParkourFPSLocomotionHandler locomotionHandler;
    private IInputProvider inputProvider;
    private Transform rootTransform;
    private Rigidbody rb;

    #endregion

    #region Wall Detection State

    private bool wallLeft;
    private bool wallRight;
    private bool wallFront;
    private RaycastHit leftWallHit;
    private RaycastHit rightWallHit;
    private RaycastHit frontWallHit;

    // Last wall tracking for climb
    private Transform lastWall;
    private Vector3 lastWallNormal;
    private float minWallNormalAngleChange = 20f;

    #endregion

    #region Parkour Timers

    private float wallRunTimer;
    private float climbTimer;
    private float slideTimer;

    #endregion

    #region Initialization

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        rootTransform = brain.EntityRoot;

        // Get required modules
        stateMachine = brain.GetModule<StateMachineModule>();
        if (stateMachine == null)
        {
            Debug.LogError("[ParkourStateController] StateMachineModule not found!");
            enabled = false;
            return;
        }

        movementSystem = brain.GetModule<MovementSystem>();
        if (movementSystem == null)
        {
            Debug.LogError("[ParkourStateController] MovementSystem not found!");
            enabled = false;
            return;
        }

        locomotionHandler = movementSystem.Locomotion as ParkourFPSLocomotionHandler;
        if (locomotionHandler == null)
        {
            Debug.LogWarning("[ParkourStateController] ParkourFPSLocomotionHandler not found - parkour will not work!");
        }

        inputProvider = brain.GetModuleImplementing<IInputProvider>();
        rb = rootTransform.GetComponent<Rigidbody>();

        // Subscribe to state changes to reset timers
        stateMachine.OnLowerBodyStateChanged += HandleLowerBodyStateChanged;
        stateMachine.OnPostureStateChanged += HandlePostureStateChanged;

        if (showDebugInfo)
            Debug.Log($"[ParkourStateController] Initialized on {rootTransform.name}");
    }

    #endregion

    #region Update Loop

    public void UpdateModule()
    {
        if (!IsEnabled || stateMachine == null) return;

        // Detect environmental conditions
        DetectWalls();

        // Update parkour logic based on current state
        LowerBodyState currentLowerState = stateMachine.GetLowerBodyState();
        PostureState currentPosture = stateMachine.GetPostureState();

        // Handle state-specific updates
        switch (currentLowerState)
        {
            case LowerBodyState.WallRunningLeft:
            case LowerBodyState.WallRunningRight:
                UpdateWallRun();
                break;

            case LowerBodyState.WallClimbing:
                UpdateWallClimb();
                break;

            case LowerBodyState.Walking:
            case LowerBodyState.Running:
            case LowerBodyState.Sprinting:
                CheckForWallRunEntry();
                CheckForClimbEntry();
                CheckForSlideEntry();
                break;

            case LowerBodyState.AirStrafing:
            case LowerBodyState.Jumping:
            case LowerBodyState.Falling:
                CheckForWallRunEntry();
                break;
        }

        // Handle slide updates
        if (currentPosture == PostureState.Sliding)
        {
            UpdateSlide();
        }

        // Handle jump input for special cases
        if (inputProvider != null && inputProvider.JumpPressed)
        {
            HandleJumpInput();
        }
    }

    #endregion

    #region Wall Detection

    private void DetectWalls()
    {
        // Left/Right wall checks for wall-running
        wallLeft = Physics.Raycast(rootTransform.position, -rootTransform.right, out leftWallHit, wallCheckDistance, wallLayers);
        wallRight = Physics.Raycast(rootTransform.position, rootTransform.right, out rightWallHit, wallCheckDistance, wallLayers);

        // Front wall check for climbing
        wallFront = Physics.SphereCast(rootTransform.position, climbSphereCastRadius, rootTransform.forward,
            out frontWallHit, climbCheckDistance, wallLayers);
    }

    private bool IsAboveGround()
    {
        return !Physics.Raycast(rootTransform.position, Vector3.down, minHeightAboveGround);
    }

    #endregion

    #region Wall Run Logic

    private void CheckForWallRunEntry()
    {
        // Requirements for wall-run:
        // 1. Wall detected on left or right
        // 2. Moving fast enough
        // 3. Above ground
        // 4. Moving forward

        if (!wallLeft && !wallRight) return;
        if (rb.linearVelocity.magnitude < wallRunMinSpeed) return;
        if (!IsAboveGround()) return;

        // Check if moving forward relative to wall
        Vector2 moveInput = inputProvider?.MoveInput ?? Vector2.zero;
        if (moveInput.y <= 0) return; // Must be moving forward

        // Request appropriate wall-run state
        LowerBodyState targetState = wallLeft ? LowerBodyState.WallRunningLeft : LowerBodyState.WallRunningRight;

        bool success = stateMachine.TryTransitionLowerBody(targetState);
        if (success)
        {
            wallRunTimer = wallRunMaxTime;

            if (showDebugInfo)
                Debug.Log($"[ParkourStateController] Entered wall-run {targetState}");
        }
    }

    private void UpdateWallRun()
    {
        // Update timer
        wallRunTimer -= Time.deltaTime;

        // Check exit conditions
        bool shouldExit = false;

        // Timer expired
        if (wallRunTimer <= 0)
        {
            shouldExit = true;
            if (showDebugInfo)
                Debug.Log("[ParkourStateController] Wall-run timer expired");
        }

        // Lost wall contact
        LowerBodyState currentState = stateMachine.GetLowerBodyState();
        if (currentState == LowerBodyState.WallRunningLeft && !wallLeft)
            shouldExit = true;
        if (currentState == LowerBodyState.WallRunningRight && !wallRight)
            shouldExit = true;

        // Stopped moving forward
        Vector2 moveInput = inputProvider?.MoveInput ?? Vector2.zero;
        if (moveInput.y <= 0)
            shouldExit = true;

        // Too slow
        if (rb.linearVelocity.magnitude < wallRunMinSpeed * 0.5f)
            shouldExit = true;

        // Exit to air strafing
        if (shouldExit)
        {
            stateMachine.TryTransitionLowerBody(LowerBodyState.AirStrafing);
            stateMachine.TryTransitionPosture(PostureState.Airborne);

            if (showDebugInfo)
                Debug.Log("[ParkourStateController] Exited wall-run");
        }
    }

    #endregion

    #region Wall Climb Logic

    private void CheckForClimbEntry()
    {
        if (!wallFront) return;

        // Check look angle
        float wallLookAngle = Vector3.Angle(rootTransform.forward, -frontWallHit.normal);
        if (wallLookAngle > maxWallLookAngle) return;

        // Check forward input
        Vector2 moveInput = inputProvider?.MoveInput ?? Vector2.zero;
        if (moveInput.y <= 0) return;

        // Check for new wall
        bool newWall = frontWallHit.transform != lastWall ||
                      Mathf.Abs(Vector3.Angle(lastWallNormal, frontWallHit.normal)) > minWallNormalAngleChange;

        if (!newWall && climbTimer <= 0) return;

        // Request climb state
        bool lowerBodySuccess = stateMachine.TryTransitionLowerBody(LowerBodyState.WallClimbing);
        bool upperBodySuccess = stateMachine.TryTransitionUpperBody(UpperBodyState.Climbing);
        bool postureSuccess = stateMachine.TryTransitionPosture(PostureState.Climbing);

        if (lowerBodySuccess && upperBodySuccess && postureSuccess)
        {
            climbTimer = climbMaxTime;
            lastWall = frontWallHit.transform;
            lastWallNormal = frontWallHit.normal;

            if (showDebugInfo)
                Debug.Log("[ParkourStateController] Entered wall climb");
        }
    }

    private void UpdateWallClimb()
    {
        // Update timer
        climbTimer -= Time.deltaTime;

        // Check exit conditions
        bool shouldExit = false;

        // Timer expired
        if (climbTimer <= 0)
            shouldExit = true;

        // Lost wall
        if (!wallFront)
            shouldExit = true;

        // Not pressing forward
        Vector2 moveInput = inputProvider?.MoveInput ?? Vector2.zero;
        if (moveInput.y <= 0)
            shouldExit = true;

        if (shouldExit)
        {
            stateMachine.TryTransitionLowerBody(LowerBodyState.Falling);
            stateMachine.TryTransitionUpperBody(UpperBodyState.Idle);
            stateMachine.TryTransitionPosture(PostureState.Airborne);

            if (showDebugInfo)
                Debug.Log("[ParkourStateController] Exited wall climb");
        }
    }

    #endregion

    #region Slide Logic

    private void CheckForSlideEntry()
    {
        // Requirements:
        // 1. Moving fast enough
        // 2. Grounded
        // 3. Crouch input (using sprint as slide trigger)

        if (!stateMachine.IsGrounded) return;
        if (rb.linearVelocity.magnitude < slideMinSpeed) return;

        bool sprintPressed = inputProvider?.SprintHeld ?? false;
        if (!sprintPressed) return;

        // Already moving - initiate slide
        Vector2 moveInput = inputProvider?.MoveInput ?? Vector2.zero;
        if (moveInput.magnitude < 0.1f) return;

        // Request slide
        bool lowerSuccess = stateMachine.TryTransitionLowerBody(LowerBodyState.Sliding);
        bool postureSuccess = stateMachine.TryTransitionPosture(PostureState.Sliding);

        if (lowerSuccess && postureSuccess)
        {
            slideTimer = slideMaxTime;

            if (showDebugInfo)
                Debug.Log("[ParkourStateController] Entered slide");
        }
    }

    private void UpdateSlide()
    {
        // Update timer
        slideTimer -= Time.deltaTime;

        // Check exit conditions
        bool shouldExit = false;

        // Timer expired
        if (slideTimer <= 0)
            shouldExit = true;

        // Not grounded
        if (!stateMachine.IsGrounded)
            shouldExit = true;

        // Released sprint
        bool sprintHeld = inputProvider?.SprintHeld ?? false;
        if (!sprintHeld)
            shouldExit = true;

        // Too slow
        if (rb.linearVelocity.magnitude < slideMinSpeed * 0.3f)
            shouldExit = true;

        if (shouldExit)
        {
            stateMachine.TryTransitionLowerBody(LowerBodyState.Walking);
            stateMachine.TryTransitionPosture(PostureState.Standing);

            if (showDebugInfo)
                Debug.Log("[ParkourStateController] Exited slide");
        }
    }

    #endregion

    #region Jump Input Handling

    private void HandleJumpInput()
    {
        LowerBodyState currentLowerState = stateMachine.GetLowerBodyState();

        switch (currentLowerState)
        {
            case LowerBodyState.WallRunningLeft:
            case LowerBodyState.WallRunningRight:
                // Wall jump
                PerformWallJump();
                break;

            case LowerBodyState.WallClimbing:
                // Climb jump (jump away from wall)
                PerformClimbJump();
                break;

            default:
                // Normal jump - handled by other systems
                break;
        }
    }

    private void PerformWallJump()
    {
        // Get wall normal
        LowerBodyState currentState = stateMachine.GetLowerBodyState();
        Vector3 wallNormal = currentState == LowerBodyState.WallRunningLeft ? leftWallHit.normal : rightWallHit.normal;

        // Request jump state
        bool success = stateMachine.TryTransitionLowerBody(LowerBodyState.Jumping);
        if (success && locomotionHandler != null)
        {
            locomotionHandler.ApplyWallJumpImpulse(wallNormal);

            if (showDebugInfo)
                Debug.Log("[ParkourStateController] Wall jump!");
        }
    }

    private void PerformClimbJump()
    {
        // Jump away from wall
        Vector3 wallNormal = frontWallHit.normal;

        bool success = stateMachine.TryTransitionLowerBody(LowerBodyState.Jumping);
        if (success && locomotionHandler != null)
        {
            locomotionHandler.ApplyWallJumpImpulse(wallNormal);

            if (showDebugInfo)
                Debug.Log("[ParkourStateController] Climb jump!");
        }
    }

    #endregion

    #region State Change Handlers

    private void HandleLowerBodyStateChanged(LowerBodyState oldState, LowerBodyState newState)
    {
        // Reset timers on new state entry
        if (newState == LowerBodyState.WallRunningLeft || newState == LowerBodyState.WallRunningRight)
        {
            wallRunTimer = wallRunMaxTime;
        }
        else if (newState == LowerBodyState.WallClimbing)
        {
            climbTimer = climbMaxTime;
        }
        else if (newState == LowerBodyState.Sliding)
        {
            slideTimer = slideMaxTime;
        }

        // Handle grounded/airborne transitions
        if (stateMachine.IsGrounded)
        {
            // Refresh climb timer when landing
            bool newWall = frontWallHit.transform != lastWall ||
                          Mathf.Abs(Vector3.Angle(lastWallNormal, frontWallHit.normal)) > minWallNormalAngleChange;

            if (wallFront && newWall)
            {
                climbTimer = climbMaxTime;
            }
        }
    }

    private void HandlePostureStateChanged(PostureState oldState, PostureState newState)
    {
        // Handle posture-specific resets if needed
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get current wall normal (for wall-run camera tilt, etc.)
    /// </summary>
    public Vector3 GetCurrentWallNormal()
    {
        LowerBodyState currentState = stateMachine.GetLowerBodyState();
        
        if (currentState == LowerBodyState.WallRunningLeft && wallLeft)
            return leftWallHit.normal;
        else if (currentState == LowerBodyState.WallRunningRight && wallRight)
            return rightWallHit.normal;
        else if (currentState == LowerBodyState.WallClimbing && wallFront)
            return frontWallHit.normal;

        return Vector3.zero;
    }

    /// <summary>
    /// Is currently in any parkour state?
    /// </summary>
    public bool IsInParkourState()
    {
        LowerBodyState currentState = stateMachine.GetLowerBodyState();

        return currentState == LowerBodyState.WallRunningLeft ||
               currentState == LowerBodyState.WallRunningRight ||
               currentState == LowerBodyState.WallClimbing ||
               currentState == LowerBodyState.Vaulting ||
               currentState == LowerBodyState.Mantling ||
               currentState == LowerBodyState.LedgeHanging;
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!showDebugInfo || !Application.isPlaying || rootTransform == null) return;

        // Draw wall checks
        if (wallLeft)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(rootTransform.position, -rootTransform.right * wallCheckDistance);
            Gizmos.DrawWireSphere(leftWallHit.point, 0.1f);
        }

        if (wallRight)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(rootTransform.position, rootTransform.right * wallCheckDistance);
            Gizmos.DrawWireSphere(rightWallHit.point, 0.1f);
        }

        if (wallFront)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(rootTransform.position, rootTransform.forward * climbCheckDistance);
            Gizmos.DrawWireSphere(frontWallHit.point, 0.1f);
        }

        // Draw timer info
#if UNITY_EDITOR
        Vector3 labelPos = rootTransform.position + Vector3.up * 2.5f;
        string timerText = $"WallRun: {wallRunTimer:F1}s\nClimb: {climbTimer:F1}s\nSlide: {slideTimer:F1}s";
        UnityEditor.Handles.Label(labelPos, timerText);
#endif
    }

    private void OnDestroy()
    {
        if (stateMachine != null)
        {
            stateMachine.OnLowerBodyStateChanged -= HandleLowerBodyStateChanged;
            stateMachine.OnPostureStateChanged -= HandlePostureStateChanged;
        }
    }

    #endregion
}
