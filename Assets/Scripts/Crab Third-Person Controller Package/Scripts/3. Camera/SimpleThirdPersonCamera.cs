using UnityEngine;

public class SimpleThirdPersonCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float minVerticalAngle = -30f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [SerializeField] private float cameraHeight = 1.7f;
    private bool shouldProcessInput = true;

    [Header("Camera Offset")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, 0, -5f);

    [Header("Collision")]
    [SerializeField] private LayerMask obstacleLayer = 1;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float collisionSmoothSpeed = 10f;

    // References (found automatically)
    private Transform player;
    private Transform cameraOrbitPoint; // Component_Camera (parent)
    private ControllerBrain brain;
    private TargetLockModule targetLockModule;

    // Rotation tracking
    private float horizontalRotation = 0f;
    private float verticalRotation = 0f;

    // Target lock system
    private Transform lockTarget;
    private bool isTargetLocked = false;

    // Collision
    private float originalDistance;
    private float currentDistance;

    void Start()
    {
        // Get references from hierarchy
        cameraOrbitPoint = transform.parent; // Component_Camera
        brain = cameraOrbitPoint.GetComponentInParent<ControllerBrain>(); // Component_Brain
        player = brain.PlayerRoot; // Player

        // Get target lock module
        targetLockModule = brain.GetModule<TargetLockModule>();

        // Use the offset distance for collision calculations
        originalDistance = cameraOffset.magnitude;
        currentDistance = originalDistance;

        // Set initial rotation to match player forward
        horizontalRotation = player.eulerAngles.y;

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
    }

    void LateUpdate()
    {
        if (player == null) return;

        HandleTargetLock();
        HandleMouseInput();
        UpdateCamera();
    }
    public void SetInputEnabled(bool enabled)
    {
        shouldProcessInput = enabled;
    }
    void HandleTargetLock()
    {
        if (targetLockModule == null) return;

        // Check if target lock state changed
        bool newLockState = targetLockModule.IsLockedOn;
        Transform newTarget = targetLockModule.LockedTarget;

        if (newLockState != isTargetLocked || newTarget != lockTarget)
        {
            isTargetLocked = newLockState;
            lockTarget = newTarget;
        }
    }

    void HandleMouseInput()
    {
        // Skip input processing if disabled
        if (!shouldProcessInput) return;

        // Get mouse input from the Input System through the brain
        Vector2 mouseInput = Vector2.zero;

   

        if (brain != null)
        {
            var inputControls = brain.GetInputControls();
            if (inputControls != null)
            {
                mouseInput = inputControls.Player.Look.ReadValue<Vector2>() * mouseSensitivity * Time.deltaTime;
            }
        }

        if (!isTargetLocked)
        {
            // Free look - standard Dark Souls camera behavior
            horizontalRotation += mouseInput.x;
            verticalRotation -= mouseInput.y;
            verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
        }
        else if (lockTarget != null)
        {
            // Target locked - allow some camera adjustment but bias toward target
            horizontalRotation += mouseInput.x * 0.3f; // Reduced sensitivity when locked

            // Calculate ideal angle to face target
            Vector3 directionToTarget = (lockTarget.position - player.position).normalized;
            directionToTarget.y = 0; // Keep horizontal

            if (directionToTarget.magnitude > 0.1f)
            {
                float targetAngle = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
                horizontalRotation = Mathf.LerpAngle(horizontalRotation, targetAngle, rotationSpeed * Time.deltaTime);
            }
        }
    }

    void UpdateCamera()
    {
        // Move and rotate the orbit point (parent) with height offset
        cameraOrbitPoint.position = player.position + Vector3.up * cameraHeight;
        cameraOrbitPoint.rotation = Quaternion.Euler(0, horizontalRotation, 0);

        // Apply the camera offset with vertical rotation
        Vector3 rotatedOffset = cameraOffset;

        // Apply vertical rotation to the offset
        Quaternion verticalRotationQuaternion = Quaternion.Euler(verticalRotation, 0, 0);
        rotatedOffset = verticalRotationQuaternion * rotatedOffset;

        // Calculate world position using the orbit point's transform
        Vector3 desiredWorldPosition = cameraOrbitPoint.TransformPoint(rotatedOffset);

        // Handle collision
        Vector3 finalWorldPosition = HandleCameraCollision(cameraOrbitPoint.position, desiredWorldPosition);

        // Set camera position in world space
        transform.position = finalWorldPosition;

        // Handle camera rotation (what it looks at)
        if (isTargetLocked && lockTarget != null)
        {
            // Look at target
            Vector3 lookDirection = (lockTarget.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
        else
        {
            // Look at player position (with height offset)
            Vector3 lookDirection = (cameraOrbitPoint.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    Vector3 HandleCameraCollision(Vector3 orbitPosition, Vector3 desiredPosition)
    {
        Vector3 direction = (desiredPosition - orbitPosition).normalized;
        float desiredDistance = Vector3.Distance(orbitPosition, desiredPosition);

        RaycastHit hit;
        if (Physics.SphereCast(orbitPosition, collisionRadius, direction, out hit, desiredDistance, obstacleLayer))
        {
            float hitDistance = Mathf.Max(hit.distance - collisionRadius, minDistance);
            currentDistance = Mathf.Lerp(currentDistance, hitDistance, collisionSmoothSpeed * Time.deltaTime);
        }
        else
        {
            currentDistance = Mathf.Lerp(currentDistance, desiredDistance, collisionSmoothSpeed * Time.deltaTime);
        }

        return orbitPosition + direction * currentDistance;
    }

    // Public methods
    public void SetMouseSensitivity(float sensitivity)
    {
        mouseSensitivity = sensitivity;
    }

    public void SetCameraOffset(Vector3 offset)
    {
        cameraOffset = offset;
        originalDistance = cameraOffset.magnitude;
    }

    // Debug
    void OnDrawGizmosSelected()
    {
        if (cameraOrbitPoint == null) return;

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
    }
}