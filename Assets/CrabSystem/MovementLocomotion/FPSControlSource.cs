using UnityEngine;

/// <summary>
/// FPS Control Source - Player input for first-person movement
/// 
/// Handles camera-relative input transformation for FPS games.
/// Unlike third-person, FPS movement is always relative to where you're looking.
/// </summary>
public class FPSControlSource : MonoBehaviour, IMovementControlSource
{
    [Header("Settings")]
    [SerializeField] private bool sprintToggle = false;

    [Header("Slide/Crouch")]
    [Tooltip("Use sprint key as slide when moving")]
    [SerializeField] private bool sprintAsSlide = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private ControllerBrain brain;
    private IInputProvider inputProvider;
    private ICameraProvider cameraProvider;
    private Transform cameraTransform;

    private bool isActive;
    private bool isSprintToggled;

    public bool IsActive => isActive;
    public string SourceName => "FPS Control";

    #region IMovementControlSource Implementation

    public MovementInput GetMovementInput()
    {
        if (inputProvider == null)
            return MovementInput.Zero;

        // Get raw WASD input
        Vector2 rawInput = inputProvider.MoveInput;

        // Transform to camera space (FPS is ALWAYS camera-relative)
        Vector2 moveDirection = TransformToCameraSpace(rawInput);

        // Look direction is always camera forward (player faces where camera looks)
        Vector2 lookDirection = GetCameraForward2D();

        // Handle sprint/slide
        bool sprintInput = inputProvider.SprintHeld;
        if (sprintToggle)
        {
            if (sprintInput && !isSprintToggled)
                isSprintToggled = true;
            else if (sprintInput && isSprintToggled)
                isSprintToggled = false;
        }

        bool sprint = sprintToggle ? isSprintToggled : sprintInput;

        // Handle jump
        bool jump = inputProvider.JumpPressed;

        // Handle dash (if implemented in InputModule)
        bool dash = inputProvider.DashPressed;

        return new MovementInput
        {
            MoveDirection = moveDirection,
            LookDirection = lookDirection,
            Sprint = sprint,
            Jump = jump,
            Dash = dash
        };
    }

    public void OnActivated()
    {
        isActive = true;

        brain = GetComponentInParent<ControllerBrain>();
        if (brain == null)
        {
            Debug.LogError("[FPSControlSource] ControllerBrain not found!");
            return;
        }

        inputProvider = brain.GetModuleImplementing<IInputProvider>();
        if (inputProvider == null)
        {
            Debug.LogError("[FPSControlSource] IInputProvider not found!");
            return;
        }

        cameraProvider = brain.GetModuleImplementing<ICameraProvider>();
        if (cameraProvider != null)
        {
            cameraTransform = cameraProvider.CameraTransform;
        }

        // Fallback to Camera.main if no provider
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                cameraTransform = mainCam.transform;
        }

        if (showDebugInfo)
            Debug.Log($"[FPSControlSource] Activated on {brain.name}");
    }

    public void OnDeactivated()
    {
        isActive = false;
        isSprintToggled = false;

        if (showDebugInfo)
            Debug.Log($"[FPSControlSource] Deactivated");
    }

    public void UpdateSource()
    {
        // Input gathered on-demand in GetMovementInput
    }

    #endregion

    #region Input Transformation

    /// <summary>
    /// Transform WASD input to world space based on camera direction
    /// For FPS, this is straightforward: W = camera forward, A = camera left, etc.
    /// </summary>
    private Vector2 TransformToCameraSpace(Vector2 rawInput)
    {
        if (rawInput.magnitude < 0.01f)
            return Vector2.zero;

        if (cameraTransform == null)
            return rawInput;

        // Get camera directions (flattened to XZ plane)
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        // Calculate movement direction in world space
        Vector3 moveDirection = cameraForward * rawInput.y + cameraRight * rawInput.x;

        return new Vector2(moveDirection.x, moveDirection.z);
    }

    /// <summary>
    /// Get camera forward direction as 2D vector (for look direction)
    /// </summary>
    private Vector2 GetCameraForward2D()
    {
        if (cameraTransform == null)
            return Vector2.up; // Default forward

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        return new Vector2(forward.x, forward.z);
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!showDebugInfo || !isActive || !Application.isPlaying) return;

        if (brain == null) return;

        // Draw movement direction
        MovementInput input = GetMovementInput();
        if (input.MoveDirection.magnitude > 0.1f)
        {
            Vector3 moveDir3D = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);

            Gizmos.color = input.Sprint ? Color.red : Color.green;
            Gizmos.DrawRay(brain.transform.position + Vector3.up * 0.5f, moveDir3D * 2f);

            // Draw raw WASD input for comparison
            if (inputProvider != null)
            {
                Vector2 rawInput = inputProvider.MoveInput;
                Vector3 rawDir = new Vector3(rawInput.x, 0f, rawInput.y);

                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(brain.transform.position + Vector3.up * 1f, rawDir * 1.5f);
            }
        }

        // Draw camera forward
        if (cameraTransform != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            Gizmos.DrawRay(brain.transform.position + Vector3.up * 1.5f, cameraForward.normalized * 2f);
        }
    }

    #endregion
}
