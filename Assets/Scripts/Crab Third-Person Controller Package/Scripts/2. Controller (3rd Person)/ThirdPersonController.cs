// Enhanced Third Person Controller - Movement, Stamina, State-based Animations with Strafe Support
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using CrabThirdPerson.Character;

public class ThirdPersonController : MonoBehaviour, IPlayerModule, IMovementState
{
    [Header("Movement Settings")]
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
    [SerializeField] private float gravity = -9.81f;

    [Header("Stamina System")]
    [SerializeField] private bool useBasicStamina = true;
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaRegenRate = 20f;
    [SerializeField] private float sprintStaminaCost = 15f;

    [Header("Animation Parameters")]
    [SerializeField] private string movementStateParam = "MovementState";
    [SerializeField] private string isLockedOnParam = "IsLockedOn";
    [SerializeField] private string strafeXParam = "StrafeX";
    [SerializeField] private string strafeYParam = "StrafeY";
    [SerializeField] private string movementSpeedParam = "MovementSpeed";

    // Movement state enum for animations
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
    private UnityEngine.CharacterController characterController;
    private Animator animator;
    private PlayerInputControls playerInputControls;
    private ControllerBrainConnector brainConnector;
    private ControllerBrain brain;

    // Player references
    private Transform playerRoot;
    private Transform modelRoot;

    // Movement variables
    private Vector3 currentVelocity;
    private Vector3 targetVelocity;
    private Vector3 verticalVelocity;
    private MovementState currentMovementState;
    private bool isSprinting;

    // Strafe variables
    private Vector2 strafeInput; // Raw input relative to lock-on target
    private Vector2 normalizedStrafeInput; // Normalized for animations (-1 to 1)
    private float currentStrafeSpeed;

    // Stamina
    private float currentStamina;
    private IStaminaProvider staminaProvider;

    // Input
    private Vector2 movementInput;

    // Target lock (managed by target lock module)
    private bool isLockedOn;
    private Transform lockedTarget;

    // Brain compatibility
    private bool usingBrainArchitecture;

    // Performance optimizations - cached values
    private Transform cachedCameraTransform;
    private Dictionary<string, bool> animatorParamCache = new Dictionary<string, bool>();
    private float lastStaminaUpdateValue = -1f;
    private bool animatorCacheBuilt = false;

    // IPlayerModule implementation
    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Get animator from ModelModule instead of searching manually
        var modelModule = brain.GetModule<ModelModule>();
        if (modelModule != null)
        {
            animator = modelModule.ModelAnimator;

            // Subscribe to model changes to update animator reference
            modelModule.OnModelChanged += HandleModelChanged;
        }

        // Fallback: search for animator if no ModelModule
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        CacheAnimatorParameters();
        
    }

    public void UpdateModule()
    {
        // Core update handled in Update()
    }

    // Handle model changes for dynamic model swapping
    private void HandleModelChanged(ModelDatabase.ModelVariant newModel)
    {
        // Update animator reference when model changes
        var modelModule = brain?.GetModule<ModelModule>();
        if (modelModule != null)
        {
            animator = modelModule.ModelAnimator;
            CacheAnimatorParameters(); // Refresh animation parameter cache
            Debug.Log($"ThirdPersonController: Updated animator reference for new model: {newModel.displayName}");
        }
    }

    // IMovementState implementation
    public bool IsGrounded => characterController.isGrounded;
    public bool IsMoving => currentVelocity.magnitude > 0.1f;
    public bool IsSprinting => isSprinting;
    public Vector3 Velocity => currentVelocity;

    // Properties
    public UnityEngine.CharacterController CharacterController => characterController;
    public Animator Animator => animator;
    public Vector2 MovementInput => movementInput;
    public Vector3 CurrentVelocity => currentVelocity;
    public MovementState CurrentMovementState => currentMovementState;
    public bool IsLockedOn => isLockedOn;
    public Transform LockedTarget => lockedTarget;
    public float CurrentStamina => staminaProvider?.CurrentStamina ?? currentStamina;
    public float MaxStamina => staminaProvider?.MaxStamina ?? maxStamina;

    // New strafe properties
    public Vector2 StrafeInput => strafeInput;
    public Vector2 NormalizedStrafeInput => normalizedStrafeInput;
    public float CurrentStrafeSpeed => currentStrafeSpeed;

    // Speed property accessors (for other modules)
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float SprintSpeed => sprintSpeed;

    // Camera transform property
    public Transform CameraTransform => GetCameraTransform();

    // State queries
    public virtual bool CanMove() => true;
    public virtual bool CanSprint() => CurrentStamina > 0;
    public virtual bool CanAct() => true; // For combat and other modules

    // Events
    public System.Action<float> OnStaminaChanged;
    public System.Action<MovementState> OnMovementStateChanged;

    void Awake()
    {
        SetupPlayerReferences();
        GetComponents();

        // Initialize stamina
        if (useBasicStamina)
        {
            currentStamina = maxStamina;
        }

        // Check if using Brain architecture
        usingBrainArchitecture = GetComponent<ControllerBrainConnector>() != null;

        if (!usingBrainArchitecture)
        {
            InitializeInputSystem();
        }

        // Only cache parameters if we have an animator (Brain setup will handle this)
        if (animator != null)
            CacheAnimatorParameters();
    }

    void SetupPlayerReferences()
    {
        // We are Component_Controller under Component_Brain under Player
        var brain = transform.parent;
        playerRoot = brain?.parent;

        if (playerRoot == null)
        {
            Debug.LogError("ThirdPersonController: Could not find Player root!");
            playerRoot = transform;
        }

        // Model is now under Component_Model, but keep fallback search
        if (playerRoot != null)
        {
            // First try to find Component_Model
            var componentModel = brain?.Find("Component_Model");
            if (componentModel != null)
            {
                modelRoot = componentModel.GetComponentInChildren<Animator>()?.transform;
            }

            // Fallback to old search patterns
            if (modelRoot == null)
            {
                modelRoot = playerRoot.Find("3D Model") ??
                           playerRoot.Find("Model") ??
                           playerRoot.Find("Visual") ??
                           playerRoot.Find("Character");
            }
        }

        if (modelRoot == null)
        {
            modelRoot = playerRoot;
        }
    }
    private void GetComponents()
    {
        // Player root is the root transform
        playerRoot = transform.root;

        // CharacterController should be on Player root
        characterController = playerRoot.GetComponent<CharacterController>();

        // Brain should be our parent
        if (brain == null)
        {
            brain = GetComponentInParent<ControllerBrain>();
        }

        // Validation
        if (characterController == null)
        {
            Debug.LogError("ThirdPersonController: CharacterController not found on Player root!");
        }

        if (brain == null)
        {
            Debug.LogError("ThirdPersonController: ControllerBrain not found in parent hierarchy!");
        }
    }

    void CacheAnimatorParameters()
    {
        if (animator?.runtimeAnimatorController == null)
        {
            animatorCacheBuilt = false;
            return;
        }

        animatorParamCache.Clear();
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            string key = $"{param.name}_{param.type}";
            animatorParamCache[key] = true;
        }
        animatorCacheBuilt = true;
       
    }

    void InitializeInputSystem()
    {
        if (usingBrainArchitecture) return;
        playerInputControls = new PlayerInputControls();
    }

    void OnEnable()
    {
        if (usingBrainArchitecture) return;

        if (playerInputControls != null)
        {
            playerInputControls.Enable();
            playerInputControls.Player.Move.performed += OnMoveInput;
            playerInputControls.Player.Move.canceled += OnMoveInput;
            playerInputControls.Player.Sprint.started += OnSprintStarted;
            playerInputControls.Player.Sprint.canceled += OnSprintCanceled;
        }
    }

    void OnDisable()
    {
        if (usingBrainArchitecture) return;

        if (playerInputControls != null)
        {
            playerInputControls.Player.Move.performed -= OnMoveInput;
            playerInputControls.Player.Move.canceled -= OnMoveInput;
            playerInputControls.Player.Sprint.started -= OnSprintStarted;
            playerInputControls.Player.Sprint.canceled -= OnSprintCanceled;
            playerInputControls.Disable();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from model changes
        var modelModule = brain?.GetModule<ModelModule>();
        if (modelModule != null)
            modelModule.OnModelChanged -= HandleModelChanged;

        if (!usingBrainArchitecture)
        {
            playerInputControls?.Dispose();
        }

        // Clear caches
        animatorParamCache.Clear();
        cachedCameraTransform = null;
    }

    void Update()
    {
        HandleMovement();
        HandleStamina();
        UpdateAnimations();
    }

    #region Input Handlers

    public void OnMoveInput(InputAction.CallbackContext context)
    {
        movementInput = context.ReadValue<Vector2>();
    }

    public void OnSprintStarted(InputAction.CallbackContext context)
    {
        // Sprint logic differs for lock-on vs free movement
        if (isLockedOn)
        {
            // Can sprint in any direction while locked on
            if (CanMove() && CanSprint() && movementInput.magnitude > 0.1f)
                isSprinting = true;
        }
        else
        {
            // Only sprint if moving forward in free movement
            if (CanMove() && CanSprint() && movementInput.y > 0.1f)
                isSprinting = true;
        }
    }

    public void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isSprinting = false;
    }

    #endregion

    #region Movement System

    void HandleMovement()
    {
        ApplyGravity();

        if (!CanMove())
        {
            // Stop horizontal movement but keep gravity
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);
            Vector3 finalVelocity = currentVelocity + verticalVelocity;
            characterController.Move(finalVelocity * Time.deltaTime);
            return;
        }

        if (isLockedOn && lockedTarget != null)
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
            verticalVelocity.y = -2f; // Only reset if we're falling or idle
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }
    }

    void HandleFreeMovement()
    {
        // Get camera direction for movement
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
            // Set target velocity based on current speed setting
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

        // Clear strafe input for free movement
        strafeInput = Vector2.zero;
        normalizedStrafeInput = Vector2.zero;
    }

    void HandleLockedOnMovement()
    {
        // Face the locked target
        Vector3 directionToTarget = (lockedTarget.position - playerRoot.position);
        directionToTarget.y = 0;

        // Handle edge case where target is directly above/below
        if (directionToTarget.magnitude < 0.1f)
        {
            directionToTarget = playerRoot.forward; // Use current facing direction
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

        // Calculate strafe movement relative to target
        Vector3 rightVector = Vector3.Cross(Vector3.up, directionToTarget).normalized;
        Vector3 forwardVector = directionToTarget;

        // Store raw strafe input for animations (relative to target facing)
        strafeInput.x = movementInput.x; // Right/Left relative to target
        strafeInput.y = movementInput.y; // Forward/Backward relative to target

        // Normalize for animation blend tree (-1 to 1 range)
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

        // Use faster acceleration for snappy strafe movement
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
        if (isSprinting && CurrentStamina > 0 && movementInput.y > 0.1f)
        {
            return sprintSpeed;
        }
        else if (movementInput.magnitude > 0.5f)
        {
            return runSpeed;
        }
        else
        {
            return walkSpeed;
        }
    }

    float GetStrafeSpeed()
    {
        if (isSprinting && CurrentStamina > 0 && strafeInput.magnitude > 0.1f)
        {
            return strafeSprintSpeed;
        }
        else if (strafeInput.magnitude > 0.5f)
        {
            return strafeRunSpeed;
        }
        else
        {
            return strafeWalkSpeed;
        }
    }

    #endregion

    #region Stamina System

    void HandleStamina()
    {
        if (!useBasicStamina || staminaProvider != null) return;

        bool wasSprintingLastFrame = isSprinting;

        // Update sprinting state based on stamina and input
        if (isLockedOn)
        {
            // Can sprint in any direction while locked on
            isSprinting = isSprinting &&
                         movementInput.magnitude > 0.1f &&
                         currentStamina > 0 &&
                         CanMove();
        }
        else
        {
            // Only sprint forward in free movement
            isSprinting = isSprinting &&
                         movementInput.y > 0.1f && // Moving forward
                         currentStamina > 0 &&
                         CanMove();
        }

        // Only update stamina if sprint state is meaningful
        if (wasSprintingLastFrame && isSprinting)
        {
            currentStamina -= sprintStaminaCost * Time.deltaTime;
        }
        else if (currentStamina < maxStamina)
        {
            currentStamina += staminaRegenRate * Time.deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);

        // Only notify if significantly changed (reduces UI update spam)
        if (Mathf.Abs(currentStamina - lastStaminaUpdateValue) > 1f)
        {
            lastStaminaUpdateValue = currentStamina;
            OnStaminaChanged?.Invoke(currentStamina);
        }
    }

    public void SetStaminaProvider(IStaminaProvider provider)
    {
        staminaProvider = provider;
        useBasicStamina = false;
    }

    // Stamina methods for other modules
    public bool ConsumeStamina(float amount)
    {
        if (staminaProvider != null)
        {
            return staminaProvider.ConsumeStamina(amount);
        }

        if (currentStamina >= amount)
        {
            currentStamina -= amount;
            currentStamina = Mathf.Max(0, currentStamina);
            OnStaminaChanged?.Invoke(currentStamina);
            return true;
        }
        return false;
    }

    public void AddStamina(float amount)
    {
        if (staminaProvider != null)
        {
            staminaProvider.AddStamina(amount);
            return;
        }

        currentStamina += amount;
        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
        OnStaminaChanged?.Invoke(currentStamina);
    }

    #endregion

    #region Animation System

    void UpdateAnimations()
    {
        if (animator == null || !animatorCacheBuilt)
        {
            if (animator?.runtimeAnimatorController != null && !animatorCacheBuilt)
            {
                CacheAnimatorParameters();
            }
            return;
        }

        // Update movement state based on current conditions
        UpdateMovementState();

        // Set animator parameters - check if parameters exist first
        if (HasAnimatorParameter(movementStateParam, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(movementStateParam, (int)currentMovementState);
        }

        if (HasAnimatorParameter(isLockedOnParam, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(isLockedOnParam, isLockedOn);
        }

        // Set strafe parameters for blend trees
        if (HasAnimatorParameter(strafeXParam, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(strafeXParam, normalizedStrafeInput.x);
        }

        if (HasAnimatorParameter(strafeYParam, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(strafeYParam, normalizedStrafeInput.y);
        }

        // Set movement speed for animation blending
        if (HasAnimatorParameter(movementSpeedParam, AnimatorControllerParameterType.Float))
        {
            float normalizedSpeed = isLockedOn ?
                (currentStrafeSpeed / strafeSprintSpeed) :
                (currentVelocity.magnitude / sprintSpeed);
            animator.SetFloat(movementSpeedParam, normalizedSpeed);
        }
    }

    void UpdateMovementState()
    {
        MovementState newState;

        if (!CanMove() || currentVelocity.magnitude < 0.1f)
        {
            newState = isLockedOn ? MovementState.StrafeIdle : MovementState.Idle;
        }
        else if (isLockedOn)
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

        // Notify if state changed
        if (newState != currentMovementState)
        {
            currentMovementState = newState;
            OnMovementStateChanged?.Invoke(currentMovementState);
        }
    }

    // Animation helper methods for other modules
    public void TriggerAnimation(string triggerName)
    {
        if (animator != null && HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(triggerName);
        }
    }

    public void SetAnimationBool(string paramName, bool value)
    {
        if (animator != null && HasAnimatorParameter(paramName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(paramName, value);
        }
    }

    public void SetAnimationFloat(string paramName, float value)
    {
        if (animator != null && HasAnimatorParameter(paramName, AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(paramName, value);
        }
    }

    public void SetAnimationInt(string paramName, int value)
    {
        if (animator != null && HasAnimatorParameter(paramName, AnimatorControllerParameterType.Int))
        {
            animator.SetInteger(paramName, value);
        }
    }

    private bool HasAnimatorParameter(string paramName, AnimatorControllerParameterType paramType)
    {
        if (!animatorCacheBuilt) return false;

        string key = $"{paramName}_{paramType}";
        return animatorParamCache.ContainsKey(key);
    }

    #endregion

    #region Target Lock Methods (Called by Target Lock Module)

    public void SetLockOnTarget(Transform target)
    {
        lockedTarget = target;
        isLockedOn = (target != null);
    }

    #endregion

    #region Advanced Movement Integration

    // Methods for AdvancedMovementModule to control physics
    public void ApplyJumpVelocity(float jumpForce)
    {
        verticalVelocity.y = jumpForce;
    }

    public Vector3 GetVerticalVelocity() => verticalVelocity;

    public void SetVerticalVelocity(Vector3 velocity)
    {
        verticalVelocity = velocity;
    }

    #endregion

    #region Public API

    public ControllerBrain GetBrain()
    {
        return brainConnector?.GetBrain();
    }

    // Force cache refresh if needed
    public void RefreshCaches()
    {
        cachedCameraTransform = null;
        CacheAnimatorParameters();
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        if (playerRoot == null) return;

        // Draw movement velocity
        Gizmos.color = Color.green;
        Gizmos.DrawRay(playerRoot.position + Vector3.up * 0.5f, currentVelocity);

        // Draw target lock line and strafe directions
        if (isLockedOn && lockedTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(playerRoot.position + Vector3.up * 1f, lockedTarget.position);

            // Draw strafe input visualization
            Vector3 directionToTarget = (lockedTarget.position - playerRoot.position).normalized;
            Vector3 rightVector = Vector3.Cross(Vector3.up, directionToTarget);

            // Draw strafe directions
            Gizmos.color = Color.yellow;
            Vector3 strafeDir = (directionToTarget * normalizedStrafeInput.y + rightVector * normalizedStrafeInput.x) * 2f;
            Gizmos.DrawRay(playerRoot.position + Vector3.up * 1.5f, strafeDir);
        }

        // Draw stamina indicator
        if (useBasicStamina)
        {
            float staminaPercent = CurrentStamina / MaxStamina;
            Gizmos.color = Color.Lerp(Color.red, Color.blue, staminaPercent);
            Gizmos.DrawWireSphere(playerRoot.position + Vector3.up * 2f, 0.3f * staminaPercent);
        }
    }
}

// Interface for Stats Package to implement
public interface IStaminaProvider
{
    float CurrentStamina { get; }
    float MaxStamina { get; }
    bool ConsumeStamina(float amount);
    void AddStamina(float amount);
    void SetMaxStamina(float newMax);
}