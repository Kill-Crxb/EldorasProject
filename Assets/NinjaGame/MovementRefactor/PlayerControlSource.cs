using UnityEngine;

/// <summary>
/// Player Control Source - Bridges player input to MovementSystem
/// Handles camera-relative transformation and lock-on support
/// </summary>
public class PlayerControlSource : MonoBehaviour, IMovementControlSource
{
    [Header("Settings")]
    [SerializeField] private bool cameraRelativeMovement = true;
    [SerializeField] private bool sprintToggle = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private ControllerBrain brain;
    private InputModule inputModule;
    private ICameraProvider cameraProvider;
    private TargetLockModule targetLock;

    private bool isActive;
    private bool isSprintToggled;

    public bool IsActive => isActive;
    public string SourceName => "Player Control";

    public MovementInput GetMovementInput()
    {
        if (inputModule == null)
            return MovementInput.Zero;

        Vector2 rawInput = inputModule.MoveInput;
        Vector2 moveDirection = TransformInputToCameraSpace(rawInput);
        Vector2 lookDirection = CalculateLookDirection(moveDirection);

        bool sprintInput = inputModule.SprintHeld;
        if (sprintToggle && sprintInput)
            isSprintToggled = !isSprintToggled;

        return new MovementInput
        {
            MoveDirection = moveDirection,
            LookDirection = lookDirection,
            Sprint = sprintToggle ? isSprintToggled : sprintInput,
            Jump = inputModule.JumpPressed,
            Dash = inputModule.DashPressed
        };
    }

    public void OnActivated()
    {
        isActive = true;

        brain = GetComponentInParent<ControllerBrain>();
        if (brain == null)
        {
            Debug.LogError("[PlayerControlSource] ControllerBrain not found!");
            return;
        }

        inputModule = brain.GetModule<InputModule>();
        if (inputModule == null)
        {
            Debug.LogError("[PlayerControlSource] InputModule not found!");
            return;
        }

        cameraProvider = brain.GetModuleImplementing<ICameraProvider>();
        targetLock = brain.GetModule<TargetLockModule>();
    }

    public void OnDeactivated()
    {
        isActive = false;
        isSprintToggled = false;
    }

    public void UpdateSource()
    {
        // Input gathered on-demand in GetMovementInput
    }

    private Vector2 TransformInputToCameraSpace(Vector2 rawInput)
    {
        if (rawInput.magnitude < 0.01f)
            return Vector2.zero;

        if (!cameraRelativeMovement)
            return rawInput;

        Transform cameraTransform = cameraProvider?.CameraTransform;

        // Fallback to Camera.main
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
                return rawInput;

            cameraTransform = mainCam.transform;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * rawInput.y + cameraRight * rawInput.x;

        return new Vector2(moveDirection.x, moveDirection.z);
    }

    private Vector2 CalculateLookDirection(Vector2 moveDirection)
    {
        // Lock-on: look at target
        if (targetLock != null && targetLock.IsLockedOn)
        {
            Vector3 directionToTarget = targetLock.LockedTarget.position - brain.transform.position;
            directionToTarget.y = 0f;

            if (directionToTarget.magnitude > 0.1f)
            {
                directionToTarget.Normalize();
                return new Vector2(directionToTarget.x, directionToTarget.z);
            }
        }

        // Free movement: look in movement direction
        if (moveDirection.magnitude > 0.1f)
            return moveDirection;

        return Vector2.zero;
    }
}