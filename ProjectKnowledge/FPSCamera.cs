using UnityEngine;

/// <summary>
/// FPS Camera - State-Reactive First-Person View
/// 
/// ARCHITECTURE:
/// - Reads state from IStateProvider (StateMachineModule)
/// - Reacts to state-derived flags (never infers rules)
/// - Applies rotation, bob, tilt based on state
/// - NEVER decides gameplay rules
/// - NEVER reads physics for gameplay decisions
/// 
/// Implements ICameraImplementation interface for CameraCoordinator compatibility.
/// </summary>
public class FPSCamera : MonoBehaviour, IBrainModule, ICameraImplementation
{
    #region Settings

    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minVerticalAngle = -90f;
    [SerializeField] private float maxVerticalAngle = 90f;

    [Header("Camera Position")]
    [Tooltip("Height offset from player root (eye height)")]
    [SerializeField] private float cameraHeight = 1.6f;
    [Tooltip("Forward offset (positive = ahead of player)")]
    [SerializeField] private float cameraForwardOffset = 0f;

    [Header("Head Bobbing")]
    [SerializeField] private bool enableHeadBob = true;
    [SerializeField] private float bobFrequency = 1.5f;
    [SerializeField] private float bobHorizontalAmplitude = 0.05f;
    [SerializeField] private float bobVerticalAmplitude = 0.05f;

    [Header("Wall-Run Tilt")]
    [SerializeField] private bool enableWallRunTilt = true;
    [SerializeField] private float wallRunTiltSpeed = 5f;

    [Header("Smoothing")]
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float smoothSpeed = 10f;

    [Header("Cursor Lock (TODO: Extract to GameMode)")]
    [Tooltip("This should eventually be moved to a UI/GameMode layer")]
    [SerializeField] private bool lockCursorOnStart = true;
    [SerializeField] private KeyCode unlockCursorKey = KeyCode.Escape;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    public bool IsEnabled { get; set; } = true;

    #endregion

    #region Components

    private ControllerBrain brain;
    private Transform playerRoot;
    private Transform cameraTransform;
    
    // State provider (CRITICAL: Camera reads state, doesn't infer it)
    private IStateProvider stateProvider;
    
    // Input provider
    private IInputProvider inputProvider;

    #endregion

    #region Rotation State

    private float verticalRotation;
    private float horizontalRotation;
    private Vector2 currentRotation;
    private Vector2 targetRotation;
    
    // Camera roll (wall-run tilt)
    private float currentRoll;
    private float targetRoll;

    #endregion

    #region Head Bob State

    private float bobTimer;
    private Vector3 bobOffset;

    #endregion

    #region Camera Pitch Clamp

    private float activePitchMin;
    private float activePitchMax;

    #endregion

    #region State

    private bool isInitialized;
    private Vector3 baseCameraPosition;

    #endregion

    #region Initialization

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        playerRoot = brain.EntityRoot;

        if (playerRoot == null)
        {
            Debug.LogError("[FPSCamera] PlayerRoot not found!");
            enabled = false;
            return;
        }

        // Get camera transform
        cameraTransform = transform;

        // Get STATE PROVIDER (CRITICAL)
        stateProvider = brain.GetModuleImplementing<IStateProvider>();
        if (stateProvider == null)
        {
            Debug.LogError("[FPSCamera] IStateProvider not found! Camera cannot react to state without it.");
            Debug.LogError("[FPSCamera] Make sure StateMachineModule implements IStateProvider.");
            enabled = false;
            return;
        }

        // Get input provider
        inputProvider = brain.GetModuleImplementing<IInputProvider>();

        // Initialize rotation to player's current facing
        horizontalRotation = playerRoot.eulerAngles.y;
        verticalRotation = 0f;
        currentRotation = new Vector2(horizontalRotation, verticalRotation);
        targetRotation = currentRotation;

        // Initialize roll
        currentRoll = 0f;
        targetRoll = 0f;

        // Set base camera position
        baseCameraPosition = new Vector3(0f, cameraHeight, cameraForwardOffset);

        // Initialize pitch clamp
        activePitchMin = minVerticalAngle;
        activePitchMax = maxVerticalAngle;

        // Lock cursor (TODO: Extract to GameMode layer)
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        isInitialized = true;

        if (showDebugInfo)
            Debug.Log($"[FPSCamera] Initialized (state-reactive) on {playerRoot.name}");
    }

    public void UpdateModule() { }

    #endregion

    #region Update Loop

    void LateUpdate()
    {
        if (!IsEnabled || !isInitialized || stateProvider == null) return;

        HandleCursorUnlock(); // TODO: Extract to GameMode
        UpdatePitchClamp();
        HandleInput();
        UpdateRotation();
        UpdateRoll();
        UpdatePosition();
        UpdateHeadBob();
    }

    #endregion

    #region Input Handling

    private void HandleInput()
    {
        // STATE-DRIVEN: Check if state allows camera input
        if (!stateProvider.AllowsCameraInput)
        {
            // State blocks camera input (stunned, ragdoll, dialogue, etc.)
            return;
        }

        // Only process mouse look if cursor is locked (TODO: Extract cursor logic)
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // Get look input
        Vector2 lookInput = inputProvider?.LookInput ?? Vector2.zero;

        if (lookInput.magnitude < 0.01f) return;

        // Apply sensitivity
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        // Update target rotation
        targetRotation.x += mouseX;
        targetRotation.y -= mouseY; // Invert Y for natural mouse look

        // STATE-DRIVEN: Apply pitch clamp (may be overridden by state)
        targetRotation.y = Mathf.Clamp(targetRotation.y, activePitchMin, activePitchMax);
    }

    #endregion

    #region Pitch Clamp Update

    private void UpdatePitchClamp()
    {
        // STATE-DRIVEN: Check if state overrides pitch clamp
        Vector2? clampOverride = stateProvider.CameraPitchClampOverride;

        if (clampOverride.HasValue)
        {
            // State provides override (e.g., tighter during mantle, wider during wall-climb)
            activePitchMin = clampOverride.Value.x;
            activePitchMax = clampOverride.Value.y;
        }
        else
        {
            // Use default settings
            activePitchMin = minVerticalAngle;
            activePitchMax = maxVerticalAngle;
        }

        // Re-clamp current rotation if clamp changed
        targetRotation.y = Mathf.Clamp(targetRotation.y, activePitchMin, activePitchMax);
        currentRotation.y = Mathf.Clamp(currentRotation.y, activePitchMin, activePitchMax);
    }

    #endregion

    #region Rotation Update

    private void UpdateRotation()
    {
        // Smooth rotation if enabled
        if (enableSmoothing)
        {
            currentRotation = Vector2.Lerp(currentRotation, targetRotation, smoothSpeed * Time.deltaTime);
        }
        else
        {
            currentRotation = targetRotation;
        }

        // Apply rotation to camera (with roll)
        cameraTransform.rotation = Quaternion.Euler(
            currentRotation.y,  // Pitch
            currentRotation.x,  // Yaw
            currentRoll         // Roll (wall-run tilt)
        );

        // CRITICAL: Update player root rotation to match horizontal camera rotation
        // This makes the player face where the camera is looking (essential for FPS)
        playerRoot.rotation = Quaternion.Euler(0f, currentRotation.x, 0f);

        // Store final rotation values
        verticalRotation = currentRotation.y;
        horizontalRotation = currentRotation.x;
    }

    #endregion

    #region Roll Update (Wall-Run Tilt)

    private void UpdateRoll()
    {
        if (!enableWallRunTilt)
        {
            currentRoll = 0f;
            return;
        }

        // STATE-DRIVEN: Get camera roll from state
        targetRoll = stateProvider.CameraRoll;

        // Smoothly interpolate to target roll
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, wallRunTiltSpeed * Time.deltaTime);
    }

    #endregion

    #region Position Update

    private void UpdatePosition()
    {
        // Position camera at player root + offset + head bob
        Vector3 targetPosition = playerRoot.position + baseCameraPosition + bobOffset;

        cameraTransform.position = targetPosition;
    }

    #endregion

    #region Head Bobbing (STATE-DRIVEN, NO PHYSICS)

    private void UpdateHeadBob()
    {
        if (!enableHeadBob)
        {
            bobOffset = Vector3.zero;
            return;
        }

        // STATE-DRIVEN: Get head bob speed from state (not physics!)
        float bobSpeed = stateProvider.HeadBobSpeed;

        if (bobSpeed > 0.1f)
        {
            // Head bob is allowed and we have a speed multiplier
            bobTimer += Time.deltaTime * bobFrequency * bobSpeed;

            // Calculate bob offset using sine waves
            float horizontalBob = Mathf.Sin(bobTimer) * bobHorizontalAmplitude;
            float verticalBob = Mathf.Sin(bobTimer * 2f) * bobVerticalAmplitude;

            bobOffset = new Vector3(horizontalBob, verticalBob, 0f);
        }
        else
        {
            // No head bob (either not allowed or not moving)
            bobOffset = Vector3.Lerp(bobOffset, Vector3.zero, Time.deltaTime * 5f);
            bobTimer = 0f;
        }
    }

    #endregion

    #region Cursor Control (TODO: Extract to GameMode)

    private void HandleCursorUnlock()
    {
        // TODO: This logic should be extracted to a UI/GameMode layer
        // Cameras should not own cursor lock logic

        if (Input.GetKeyDown(unlockCursorKey))
        {
            ToggleCursorLock();
        }
    }

    private void ToggleCursorLock()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    #endregion

    #region ICameraImplementation Interface

    public void SetInputEnabled(bool enabled)
    {
        // DEPRECATED: Input is now controlled by state
        // Keeping for interface compatibility, but has no effect
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        mouseSensitivity = sensitivity;
    }

    public void SetCameraOffset(Vector3 offset)
    {
        baseCameraPosition = offset;
    }

    public float GetHorizontalRotation()
    {
        return horizontalRotation;
    }

    public float GetVerticalRotation()
    {
        return verticalRotation;
    }

    // FIXED: Interface uses IsLocked(), not IsCameraLocked()
    public bool IsLocked()
    {
        // FPS camera doesn't support target locking
        return false;
    }

    // FIXED: Interface uses GetLockTarget(), not GetCameraLockTarget()
    public Transform GetLockTarget()
    {
        // FPS camera doesn't support target locking
        return null;
    }

    #endregion
}
