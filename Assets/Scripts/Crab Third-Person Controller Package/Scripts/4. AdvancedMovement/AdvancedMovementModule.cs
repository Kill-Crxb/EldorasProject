using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class AdvancedMovementModule : MonoBehaviour, IPlayerModule, IInputHandler
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float jumpStaminaCost = 10f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private int maxAirJumps = 0;

    [Header("Jump Input Buffer")]
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Animation Parameters")]
    [SerializeField] private string jumpTriggerParam = "JumpTrigger";
    [SerializeField] private string isGroundedParam = "IsGrounded";
    [SerializeField] private string airJumpCountParam = "AirJumpCount";

    private ControllerBrain brain;
    private ThirdPersonController controller;
    private Animator animator;

    // Jump state
    private bool isJumping;
    private bool canJump = true;
    private float lastGroundedTime;
    private float lastGroundExitTime;
    private int currentAirJumps;

    // Input buffering
    private bool jumpInputPressed;
    private float jumpInputTime;

    // Initialization tracking
    private bool isFullyInitialized = false;

    // Interface requirement
    public bool IsEnabled { get; set; } = true;

    // Events
    public System.Action OnJumpPerformed;
    public System.Action<int> OnAirJumpPerformed;

    // Properties
    public bool IsJumping => isJumping;
    public int CurrentAirJumps => currentAirJumps;
    public int MaxAirJumps => maxAirJumps;
    public bool CanGroundJump => canJump && (brain.IsGrounded || Time.time <= lastGroundExitTime + coyoteTime);
    public bool CanAirJump => canJump && !brain.IsGrounded && currentAirJumps < maxAirJumps;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        controller = brain.Controller;
        animator = brain.PlayerAnimator;

        if (brain == null)
        {
            Debug.LogError("AdvancedMovementModule: Brain reference is null!");
            return;
        }

        if (controller == null)
        {
            Debug.LogError("AdvancedMovementModule: Controller reference is null!");
            return;
        }

        // Subscribe to feet events
        brain.OnFeetEnter += HandleFeetEnter;
        brain.OnFeetExit += HandleFeetExit;
    }

    void Start()
    {
        StartCoroutine(DelayedInitialization());
    }

    private IEnumerator DelayedInitialization()
    {
        yield return null;

        // Reset any phantom input states
        jumpInputPressed = false;
        jumpInputTime = 0f;

        isFullyInitialized = true;
    }

    public void UpdateModule()
    {
        if (!IsEnabled || !isFullyInitialized) return;

        HandleJumpInput();
        UpdateAnimations();
    }

    #region Input Handling

    public void SubscribeToInputs(PlayerInputControls inputControls)
    {
        if (inputControls?.Player == null)
        {
            Debug.LogWarning("AdvancedMovementModule: InputControls or Player map is null");
            return;
        }

        inputControls.Player.Jump.started += OnJumpInput;
        inputControls.Player.Jump.canceled += OnJumpReleased;
    }

    public void UnsubscribeFromInputs(PlayerInputControls inputControls)
    {
        if (inputControls?.Player == null) return;

        inputControls.Player.Jump.started -= OnJumpInput;
        inputControls.Player.Jump.canceled -= OnJumpReleased;
    }

    private void OnJumpInput(InputAction.CallbackContext ctx)
    {
        if (!isFullyInitialized) return;

        jumpInputPressed = true;
        jumpInputTime = Time.time;
    }

    private void OnJumpReleased(InputAction.CallbackContext ctx)
    {
        jumpInputPressed = false;
    }

    #endregion

    #region Jump System

    private void HandleJumpInput()
    {
        bool hasRecentJumpInput = jumpInputPressed || (Time.time <= jumpInputTime + jumpBufferTime);
        if (!hasRecentJumpInput) return;

        bool canGroundJump = CanGroundJump;
        bool canAirJump = CanAirJump;

        if (canGroundJump || canAirJump)
        {
            PerformJump();
        }
    }

    private void PerformJump(float customForce = -1f)
    {
        if (controller == null)
        {
            Debug.LogError("AdvancedMovementModule: Cannot jump - Controller is null");
            return;
        }

        // Consume the input
        jumpInputPressed = false;
        jumpInputTime = 0f;

        // Check stamina
        if (!controller.ConsumeStamina(jumpStaminaCost))
        {
            return;
        }

        float appliedForce = customForce > 0 ? customForce : jumpForce;
        controller.ApplyJumpVelocity(appliedForce);

        bool wasGrounded = brain.IsGrounded;
        isJumping = true;

        // Only set canJump = false if we've used all our air jumps
        if (!wasGrounded)
        {
            currentAirJumps++;
            OnAirJumpPerformed?.Invoke(currentAirJumps);

            // Only disable jumping if we've reached max air jumps
            if (currentAirJumps >= maxAirJumps)
            {
                canJump = false;
            }
        }
        else
        {
            // Ground jump - keep canJump true for air jumps
            // canJump stays true
        }

        // Trigger animations
        TriggerJumpAnimation();
        OnJumpPerformed?.Invoke();
    }

    private void TriggerJumpAnimation()
    {
        if (animator == null) return;

        if (!string.IsNullOrEmpty(jumpTriggerParam))
        {
            controller.TriggerAnimation(jumpTriggerParam);
        }

        if (!string.IsNullOrEmpty(airJumpCountParam))
        {
            controller.SetAnimationInt(airJumpCountParam, currentAirJumps);
        }
    }

    private void UpdateAnimations()
    {
        if (animator == null || controller == null) return;

        if (!string.IsNullOrEmpty(isGroundedParam))
        {
            controller.SetAnimationBool(isGroundedParam, brain.IsGrounded);
        }
    }

    private void HandleFeetEnter(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground)
        {
            lastGroundedTime = Time.time;
            currentAirJumps = 0;
            isJumping = false;
            canJump = true;
        }
    }

    private void HandleFeetExit(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground && !brain.IsGrounded)
        {
            lastGroundExitTime = Time.time;
        }
    }

    #endregion

    #region Public API

    public void ForceJump(float customForce = -1f)
    {
        if (!IsEnabled || !isFullyInitialized) return;
        PerformJump(customForce);
    }

    public void ResetJumpState()
    {
        currentAirJumps = 0;
        isJumping = false;
        canJump = true;
        jumpInputPressed = false;
        jumpInputTime = 0f;
    }

    public void SetMaxAirJumps(int newMax)
    {
        maxAirJumps = Mathf.Max(0, newMax);
    }

    public void AddAirJump()
    {
        maxAirJumps++;
    }

    public void RemoveAirJump()
    {
        maxAirJumps = Mathf.Max(0, maxAirJumps - 1);
        currentAirJumps = Mathf.Min(currentAirJumps, maxAirJumps);
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