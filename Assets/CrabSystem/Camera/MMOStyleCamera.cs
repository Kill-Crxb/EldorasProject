using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// MMO-style third person camera with free cursor and optional auto-reset behind player.
/// Can be swapped with SimpleThirdPersonCamera via ICameraProvider interface.
/// </summary>
public class MMOStyleCamera : MonoBehaviour, IBrainModule, ICameraImplementation
{
    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [SerializeField] private float cameraHeight = 1.7f;

    [Header("Camera Offset")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -5f);

    [Header("Collision")]
    [SerializeField] private LayerMask obstacleLayer = 1;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float collisionSmoothSpeed = 10f;

    [Header("Auto-Reset Behavior")]
    [SerializeField] private bool enableAutoReset = true;
    [SerializeField] private float autoResetAngleThreshold = 90f; // Degrees difference from player forward
    [SerializeField] private float autoResetDelay = 2f; // Seconds of no input before reset starts
    [SerializeField] private float autoResetSpeed = 3f; // Speed of rotation back to player forward

    [Header("Input Mode")]
    [Tooltip("Middle-click rotates camera around player (orbit-only, doesn't change player forward)")]
    [SerializeField] private bool middleClickToRotate = true;
    [Tooltip("Right-click rotates camera and updates player forward direction")]
    [SerializeField] private bool rightClickToRotate = true;
    [SerializeField] private bool lockCursorOnRotate = true; // Lock cursor when rotating

    public bool IsEnabled { get; set; } = true;

    private Transform player;
    private Transform cameraOrbitPoint;
    private ControllerBrain brain;
    private TargetLockModule targetLockModule;
    private bool isInitialized;

    // Rotation tracking
    private float playerFacingRotation; // What the player considers "forward" - only updated by right-click
    private float cameraHorizontalRotation; // Where the camera actually is - updated by both clicks
    private float verticalRotation;
    private Transform lockTarget;
    private bool isTargetLocked;

    // Orbit-only mode tracking
    private bool isOrbitingOnly = false; // True when left-click dragging (orbit without affecting player)

    // Collision
    private float originalDistance;
    private float currentDistance;

    // Input control
    private bool shouldProcessInput = true;
    private bool isRotatingCamera = false; // Tracks if user is actively rotating camera

    // Auto-reset system
    private float timeSinceLastInput = 0f;
    private bool isAutoResetting = false;

    // Cache
    private RaycastHit hitCache;
    private Vector3 heightOffset;
    private Quaternion verticalRotationCache;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        cameraOrbitPoint = transform.parent;
        player = brain.EntityRoot;

        if (cameraOrbitPoint == null || player == null)
        {
            enabled = false;
            return;
        }

        // Target lock is optional for MMO camera
        targetLockModule = brain.GetModule<TargetLockModule>();

        originalDistance = cameraOffset.magnitude;
        currentDistance = originalDistance;

        // Initialize both rotations to player's forward
        playerFacingRotation = player.eulerAngles.y;
        cameraHorizontalRotation = player.eulerAngles.y;

        heightOffset = Vector3.up * cameraHeight;

        // Free cursor by default (MMO style)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isInitialized = true;
    }

    public void UpdateModule() { }

    void LateUpdate()
    {
        if (!IsEnabled || !isInitialized) return;

        HandleTargetLock();
        HandleCameraInput();
        HandleAutoReset();
        UpdateCamera();
    }

    public void SetInputEnabled(bool enabled) => shouldProcessInput = enabled;

    void HandleTargetLock()
    {
        if (targetLockModule == null) return;

        bool newLockState = targetLockModule.IsLockedOn;
        Transform newTarget = targetLockModule.LockedTarget;

        if (newLockState != isTargetLocked || newTarget != lockTarget)
        {
            isTargetLocked = newLockState;
            lockTarget = newTarget;

            // Reset auto-reset timer when target lock changes
            timeSinceLastInput = 0f;
            isAutoResetting = false;
        }
    }

    void HandleCameraInput()
    {
        if (!shouldProcessInput) return;

        var inputControls = brain?.GetInputControls();
        if (inputControls?.Player == null) return;

        // Check if middle or right-click is held - using new Input System
        // Don't consume right-click if the pointer is over a UI element (e.g. inventory icons)
        bool pointerOverUI = UnityEngine.EventSystems.EventSystem.current != null &&
                             UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        bool middleClickHeld = middleClickToRotate && Mouse.current != null && Mouse.current.middleButton.isPressed;
        bool rightClickHeld = rightClickToRotate && Mouse.current != null && Mouse.current.rightButton.isPressed && !pointerOverUI;
        bool canRotate = middleClickHeld || rightClickHeld;

        // Track if we're in orbit-only mode (middle-click without right-click)
        bool wasOrbitingOnly = isOrbitingOnly;
        isOrbitingOnly = middleClickHeld && !rightClickHeld;

        // Handle cursor lock state based on rotation
        if (canRotate && !isRotatingCamera && lockCursorOnRotate)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            isRotatingCamera = true;
        }
        else if (!canRotate && isRotatingCamera)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            isRotatingCamera = false;
        }

        if (canRotate)
        {
            Vector2 mouseInput = inputControls.Player.Look.ReadValue<Vector2>() * mouseSensitivity * Time.deltaTime;

            if (mouseInput.sqrMagnitude > 0.0001f)
            {
                // User is actively controlling camera - reset auto-reset timer
                timeSinceLastInput = 0f;
                isAutoResetting = false;

                if (!isTargetLocked)
                {
                    if (isOrbitingOnly)
                    {
                        // Orbit-only mode (middle-click): camera rotates but player forward stays the same
                        cameraHorizontalRotation += mouseInput.x;
                        // playerFacingRotation does NOT change
                    }
                    else
                    {
                        // Full rotation mode (right-click): both camera AND player forward rotate together
                        cameraHorizontalRotation += mouseInput.x;
                        playerFacingRotation = cameraHorizontalRotation; // Keep them synced
                    }

                    verticalRotation = Mathf.Clamp(verticalRotation - mouseInput.y, minVerticalAngle, maxVerticalAngle);
                }
                else if (lockTarget != null)
                {
                    // Allow some camera adjustment even when locked
                    cameraHorizontalRotation += mouseInput.x * 0.3f;
                    playerFacingRotation = cameraHorizontalRotation;

                    Vector3 directionToTarget = lockTarget.position - player.position;
                    directionToTarget.y = 0f;

                    if (directionToTarget.sqrMagnitude > 0.01f)
                    {
                        float targetAngle = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
                        cameraHorizontalRotation = Mathf.LerpAngle(cameraHorizontalRotation, targetAngle, 5f * Time.deltaTime);
                        playerFacingRotation = cameraHorizontalRotation;
                    }
                }
            }
            else
            {
                // No input - increment idle timer
                timeSinceLastInput += Time.deltaTime;
            }
        }
        else
        {
            // Not rotating - increment idle timer
            timeSinceLastInput += Time.deltaTime;
        }
    }

    void HandleAutoReset()
    {
        if (!enableAutoReset || isTargetLocked || !isInitialized) return;

        // Check if we should start auto-reset
        if (!isAutoResetting && timeSinceLastInput >= autoResetDelay)
        {
            float playerForwardAngle = player.eulerAngles.y;
            float angleDifference = Mathf.DeltaAngle(cameraHorizontalRotation, playerForwardAngle);

            if (Mathf.Abs(angleDifference) >= autoResetAngleThreshold)
            {
                isAutoResetting = true;
            }
        }

        // Perform auto-reset rotation
        if (isAutoResetting)
        {
            float playerForwardAngle = player.eulerAngles.y;
            cameraHorizontalRotation = Mathf.LerpAngle(cameraHorizontalRotation, playerForwardAngle, autoResetSpeed * Time.deltaTime);
            playerFacingRotation = cameraHorizontalRotation; // Keep them synced during reset

            // Stop auto-reset when close enough
            float angleDifference = Mathf.DeltaAngle(cameraHorizontalRotation, playerForwardAngle);
            if (Mathf.Abs(angleDifference) < 1f)
            {
                cameraHorizontalRotation = playerForwardAngle;
                playerFacingRotation = playerForwardAngle;
                isAutoResetting = false;
            }
        }
    }

    void UpdateCamera()
    {
        cameraOrbitPoint.position = player.position + heightOffset;
        cameraOrbitPoint.rotation = Quaternion.Euler(0f, cameraHorizontalRotation, 0f);

        verticalRotationCache = Quaternion.Euler(verticalRotation, 0f, 0f);
        Vector3 rotatedOffset = verticalRotationCache * cameraOffset;
        Vector3 desiredWorldPosition = cameraOrbitPoint.TransformPoint(rotatedOffset);
        Vector3 finalWorldPosition = HandleCameraCollision(cameraOrbitPoint.position, desiredWorldPosition);

        transform.position = finalWorldPosition;

        Vector3 lookDirection;
        if (isTargetLocked && lockTarget != null)
        {
            lookDirection = lockTarget.position - transform.position;
        }
        else
        {
            lookDirection = cameraOrbitPoint.position - transform.position;
        }

        transform.rotation = Quaternion.LookRotation(lookDirection);
    }

    Vector3 HandleCameraCollision(Vector3 orbitPosition, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - orbitPosition;
        float desiredDistance = direction.magnitude;
        direction.Normalize();

        if (Physics.SphereCast(orbitPosition, collisionRadius, direction, out hitCache, desiredDistance, obstacleLayer))
        {
            float hitDistance = Mathf.Max(hitCache.distance - collisionRadius, minDistance);
            currentDistance = Mathf.Lerp(currentDistance, hitDistance, collisionSmoothSpeed * Time.deltaTime);
        }
        else
        {
            currentDistance = Mathf.Lerp(currentDistance, desiredDistance, collisionSmoothSpeed * Time.deltaTime);
        }

        return orbitPosition + direction * currentDistance;
    }

    // Public API (matches SimpleThirdPersonCamera for compatibility)
    public void SetMouseSensitivity(float sensitivity) => mouseSensitivity = sensitivity;

    public void SetCameraOffset(Vector3 offset)
    {
        cameraOffset = offset;
        originalDistance = offset.magnitude;
    }

    // CRITICAL: Returns player-facing rotation (what controller should use for movement)
    // This only updates with right-click, NOT with left-click orbit
    public float GetHorizontalRotation() => playerFacingRotation;

    // Additional: Get actual camera rotation (for debugging/UI)
    public float GetCameraHorizontalRotation() => cameraHorizontalRotation;

    public float GetVerticalRotation() => verticalRotation;
    public bool IsLocked() => isTargetLocked;
    public Transform GetLockTarget() => lockTarget;

    // Orbit mode query - returns true when middle-clicking (camera orbits without affecting player forward)
    public bool IsOrbitingOnly() => isOrbitingOnly;

    // Additional MMO-specific controls
    public void SetAutoResetEnabled(bool enabled) => enableAutoReset = enabled;
    public void SetAutoResetDelay(float delay) => autoResetDelay = delay;
    public void SetAutoResetSpeed(float speed) => autoResetSpeed = speed;
    public void SetMiddleClickToRotate(bool enabled) => middleClickToRotate = enabled;
    public void SetRightClickToRotate(bool enabled) => rightClickToRotate = enabled;

    void OnDisable()
    {
        // Restore cursor on disable
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnDrawGizmosSelected()
    {
        if (cameraOrbitPoint == null || !isInitialized) return;

        // Draw orbit point
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(cameraOrbitPoint.position, 0.1f);

        // Draw camera collision sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);

        // Draw line from orbit point to camera
        Gizmos.color = Color.green;
        Gizmos.DrawLine(cameraOrbitPoint.position, transform.position);

        // Draw target lock line
        if (isTargetLocked && lockTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, lockTarget.position);
        }

        // Draw auto-reset threshold visualization
        if (enableAutoReset && player != null)
        {
            Gizmos.color = isAutoResetting ? Color.cyan : Color.gray;
            Vector3 playerPos = player.position + heightOffset;

            // Draw forward direction
            Vector3 forward = player.forward * 3f;
            Gizmos.DrawLine(playerPos, playerPos + forward);

            // Draw threshold cone
            float leftAngle = player.eulerAngles.y - autoResetAngleThreshold;
            float rightAngle = player.eulerAngles.y + autoResetAngleThreshold;

            Vector3 leftDir = Quaternion.Euler(0, leftAngle, 0) * Vector3.forward * 3f;
            Vector3 rightDir = Quaternion.Euler(0, rightAngle, 0) * Vector3.forward * 3f;

            Gizmos.DrawLine(playerPos, playerPos + leftDir);
            Gizmos.DrawLine(playerPos, playerPos + rightDir);
        }
    }
}