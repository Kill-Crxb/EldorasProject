using UnityEngine;

public class SimpleThirdPersonCamera : MonoBehaviour, IBrainModule, ICameraImplementation
{
    [Header("Camera Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float rotationSpeed = 8f;
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

    public bool IsEnabled { get; set; } = true;

    private Transform player;
    private Transform cameraOrbitPoint;
    private ControllerBrain brain;
    private TargetLockModule targetLockModule;
    private bool isInitialized;

    private float horizontalRotation;
    private float verticalRotation;
    private Transform lockTarget;
    private bool isTargetLocked;

    private float originalDistance;
    private float currentDistance;
    private bool shouldProcessInput = true;

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

        targetLockModule = brain.GetModule<TargetLockModule>();
        originalDistance = cameraOffset.magnitude;
        currentDistance = originalDistance;
        horizontalRotation = player.eulerAngles.y;
        heightOffset = Vector3.up * cameraHeight;

        Cursor.lockState = CursorLockMode.Locked;
        isInitialized = true;
    }

    public void UpdateModule() { }

    void LateUpdate()
    {
        if (!IsEnabled || !isInitialized) return;

        HandleTargetLock();
        HandleMouseInput();
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
        }
    }

    void HandleMouseInput()
    {
        if (!shouldProcessInput) return;

        Vector2 mouseInput = Vector2.zero;
        var inputControls = brain?.GetInputControls();

        if (inputControls?.Player != null)
        {
            mouseInput = inputControls.Player.Look.ReadValue<Vector2>() * mouseSensitivity * Time.deltaTime;
        }
        else
        {
            return;
        }

        if (!isTargetLocked)
        {
            horizontalRotation += mouseInput.x;
            verticalRotation = Mathf.Clamp(verticalRotation - mouseInput.y, minVerticalAngle, maxVerticalAngle);
        }
        else if (lockTarget != null)
        {
            horizontalRotation += mouseInput.x * 0.3f;

            Vector3 directionToTarget = lockTarget.position - player.position;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude > 0.01f)
            {
                float targetAngle = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
                horizontalRotation = Mathf.LerpAngle(horizontalRotation, targetAngle, rotationSpeed * Time.deltaTime);
            }
        }
    }

    void UpdateCamera()
    {
        cameraOrbitPoint.position = player.position + heightOffset;
        cameraOrbitPoint.rotation = Quaternion.Euler(0f, horizontalRotation, 0f);

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

    public void SetMouseSensitivity(float sensitivity) => mouseSensitivity = sensitivity;
    public void SetCameraOffset(Vector3 offset)
    {
        cameraOffset = offset;
        originalDistance = offset.magnitude;
    }

    public float GetHorizontalRotation() => horizontalRotation;
    public float GetVerticalRotation() => verticalRotation;
    public bool IsLocked() => isTargetLocked;
    public Transform GetLockTarget() => lockTarget;
}

