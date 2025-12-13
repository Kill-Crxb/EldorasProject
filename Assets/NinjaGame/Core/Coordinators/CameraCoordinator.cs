using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// CameraCoordinator manages camera-related modules and provides camera functionality.
/// Supports multiple camera implementations (SimpleThirdPersonCamera, MMOStyleCamera, etc.)
/// All cameras must implement ICameraImplementation interface.
/// </summary>
public class CameraCoordinator : MonoBehaviour, IBrainModule, ICameraProvider
{
    [Header("Camera Module")]
    [Tooltip("Assign the active camera (SimpleThirdPersonCamera, MMOStyleCamera, etc.)")]
    [SerializeField] private MonoBehaviour cameraModule;

    [Header("Optional Modules")]
    [SerializeField] private TargetLockModule targetLock;

    private ControllerBrain brain;
    private ICameraImplementation cameraImpl;

    public bool IsEnabled { get; set; } = true;

    // ICameraProvider implementation
    public Transform CameraTransform => cameraModule?.transform;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Auto-discover camera if not assigned - only look for ICameraImplementation types
        if (cameraModule == null)
        {
            var simpleCamera = GetComponentInChildren<SimpleThirdPersonCamera>();
            if (simpleCamera != null)
                cameraModule = simpleCamera;
            else
            {
                var mmoCamera = GetComponentInChildren<MMOStyleCamera>();
                if (mmoCamera != null)
                    cameraModule = mmoCamera;
            }
        }

        // Get the camera implementation interface
        if (cameraModule != null)
        {
            cameraImpl = cameraModule as ICameraImplementation;

            if (cameraImpl == null)
            {
                Debug.LogError($"[CameraCoordinator] Camera module {cameraModule.GetType().Name} does not implement ICameraImplementation!");
                return;
            }

            // Initialize camera if it's a brain module
            if (cameraModule is IBrainModule brainModule)
                brainModule.Initialize(brain);
        }
        else
        {
            Debug.LogWarning("[CameraCoordinator] No camera module assigned or found!");
        }

        // Target lock is optional
        if (targetLock == null)
            targetLock = GetComponentInChildren<TargetLockModule>();

        targetLock?.Initialize(brain);
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;
        // Modules update themselves via Brain's cached array
    }

    public void SubscribeToInputs(PlayerInputControls inputControls)
    {
        // Camera modules handle their own input
    }

    public void UnsubscribeFromInputs(PlayerInputControls inputControls)
    {
        // Camera modules handle their own input
    }

    // ICameraProvider interface methods - route to active camera implementation
    public bool IsCameraLocked() => cameraImpl?.IsLocked() ?? false;
    public Transform GetCameraLockTarget() => cameraImpl?.GetLockTarget();
    public void SetCameraInputEnabled(bool enabled) => cameraImpl?.SetInputEnabled(enabled);
    public float GetCameraHorizontalRotation() => cameraImpl?.GetHorizontalRotation() ?? 0f;
    public void SetMouseSensitivity(float sensitivity) => cameraImpl?.SetMouseSensitivity(sensitivity);
    public void SetCameraOffset(Vector3 offset) => cameraImpl?.SetCameraOffset(offset);

    // Type-specific accessors for accessing camera-specific features
    public SimpleThirdPersonCamera SimpleCamera => cameraModule as SimpleThirdPersonCamera;
    public MMOStyleCamera MMOCamera => cameraModule as MMOStyleCamera;
    public TargetLockModule TargetLock => targetLock;

    // Backward compatibility - deprecated
    public SimpleThirdPersonCamera Camera => SimpleCamera;
}

/// <summary>
/// Interface that all camera implementations must implement.
/// Allows CameraCoordinator to work with any camera type.
/// </summary>
public interface ICameraImplementation
{
    void SetInputEnabled(bool enabled);
    void SetMouseSensitivity(float sensitivity);
    void SetCameraOffset(Vector3 offset);
    float GetHorizontalRotation();
    float GetVerticalRotation();
    bool IsLocked();
    Transform GetLockTarget();
}