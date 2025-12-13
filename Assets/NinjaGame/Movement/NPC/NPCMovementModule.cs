using UnityEngine;

/// <summary>
/// Movement system for NPCs - replaces ThirdPersonController for enemies.
/// Implements IMovementState for combat system compatibility.
/// Controlled by AI or network instead of player input.
/// </summary>
public class NPCMovementModule : MonoBehaviour, IBrainModule, IMovementState, IMovementProvider
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 5f;
    [SerializeField] private float rotationSpeed = 20f; // ⭐ INCREASED FROM 10

    [Header("Gravity")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedGravity = -2f;

    [Header("Animation")]
    [SerializeField] private bool updateAnimator = true;
    [SerializeField] private float animationSmoothTime = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // References
    private ControllerBrain brain;
    private CharacterController characterController;
    private Animator animator => brain?.EntityAnimator; // Dynamic property
    private Transform rootTransform; // ⭐ ADDED

    // Movement state
    private Vector3 moveDirection;
    private Vector3 velocity;
    private MovementSpeed currentSpeed = MovementSpeed.Walk;
    private float currentAnimatorSpeed;
    private float animatorVelocity;

    // Properties
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    // IMovementState - Required for combat system compatibility
    public bool IsGrounded => characterController != null && characterController.isGrounded;
    public bool IsSprinting => currentSpeed == MovementSpeed.Sprint;
    public bool IsMoving => moveDirection.magnitude > 0.1f;
    public Vector3 Velocity => characterController != null ? characterController.velocity : Vector3.zero;
    public Vector3 MoveDirection => moveDirection;
    public MovementSpeed CurrentSpeed => currentSpeed;

    // ========================================
    // Movement Speed Enum
    // ========================================

    public enum MovementSpeed
    {
        Idle = 0,
        Walk = 1,
        Run = 2,
        Sprint = 3
    }

    // ========================================
    // IBrainModule Implementation
    // ========================================

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Search up the hierarchy for CharacterController (it's on root GameObject)
        characterController = GetComponentInParent<CharacterController>();

        if (characterController == null)
        {
            // Fallback: try same GameObject
            characterController = GetComponent<CharacterController>();
        }

        if (characterController == null)
        {
            Debug.LogError("[NPCMovementModule] CharacterController not found!");
            isEnabled = false;
            return;
        }

        // ⭐ CACHE THE ROOT TRANSFORM
        rootTransform = characterController.transform;

        // Don't cache animator - now a dynamic property

        if (showDebugInfo)
        {
            Debug.Log($"[NPCMovementModule] Initialized - Root: {rootTransform.name}");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        ApplyGravity();
        ApplyMovement();

        if (updateAnimator && animator != null)
        {
            UpdateAnimation();
        }
    }

    // ========================================
    // Movement Control (Called by AI)
    // ========================================

    /// <summary>
    /// Move towards a target position
    /// </summary>
    public void MoveTowards(Vector3 targetPosition, MovementSpeed speed = MovementSpeed.Run)
    {
        if (!isEnabled || rootTransform == null) return;

        Vector3 direction = (targetPosition - rootTransform.position).normalized;
        moveDirection = new Vector3(direction.x, 0f, direction.z);
        currentSpeed = speed;

        // ⭐ ALSO ROTATE WHILE MOVING
        RotateTowards(targetPosition);
    }

    /// <summary>
    /// Move in a specific direction (normalized)
    /// </summary>
    public void MoveInDirection(Vector3 direction, MovementSpeed speed = MovementSpeed.Run)
    {
        if (!isEnabled) return;

        moveDirection = new Vector3(direction.x, 0f, direction.z).normalized;
        currentSpeed = speed;
    }

    /// <summary>
    /// Rotate to face a target position
    /// </summary>
    public void RotateTowards(Vector3 targetPosition)
    {
        if (!isEnabled || rootTransform == null) return;

        Vector3 direction = (targetPosition - rootTransform.position).normalized;
        direction.y = 0f;

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rootTransform.rotation = Quaternion.Slerp(
                rootTransform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// Rotate to face a specific direction
    /// </summary>
    public void RotateToDirection(Vector3 direction)
    {
        if (!isEnabled || rootTransform == null) return;

        direction.y = 0f;

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rootTransform.rotation = Quaternion.Slerp(
                rootTransform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// Stop all movement
    /// </summary>
    public void Stop()
    {
        moveDirection = Vector3.zero;
        currentSpeed = MovementSpeed.Idle;
    }

    /// <summary>
    /// Set movement speed directly
    /// </summary>
    public void SetMovementSpeed(MovementSpeed speed)
    {
        currentSpeed = speed;
    }

    // ========================================
    // Internal Movement Logic
    // ========================================

    private void ApplyGravity()
    {
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = groundedGravity; // Small downward force to stay grounded
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }
    }

    private void ApplyMovement()
    {
        // Auto-detect idle state
        if (moveDirection.magnitude < 0.01f)
        {
            currentSpeed = MovementSpeed.Idle;
        }

        // Calculate movement speed based on current speed setting
        float speed = GetSpeedValue();

        // Calculate final movement vector
        Vector3 move = moveDirection * speed;
        move.y = velocity.y;

        // Apply movement
        characterController.Move(move * Time.deltaTime);

        // Clear move direction each frame (AI must set it every frame if moving)
        moveDirection = Vector3.zero;
    }

    private float GetSpeedValue()
    {
        switch (currentSpeed)
        {
            case MovementSpeed.Walk:
                return walkSpeed;
            case MovementSpeed.Run:
                return runSpeed;
            case MovementSpeed.Sprint:
                return sprintSpeed;
            case MovementSpeed.Idle:
            default:
                return 0f;
        }
    }

    // ========================================
    // Animation System
    // ========================================

    private void UpdateAnimation()
    {
        if (animator == null) return;

        // Calculate normalized speed (0-1) for blend tree
        // Idle=0, Walk=0.33, Run=0.66, Sprint=1.0
        float normalizedSpeed = 0f;
        switch (currentSpeed)
        {
            case MovementSpeed.Idle:
                normalizedSpeed = 0f;
                break;
            case MovementSpeed.Walk:
                normalizedSpeed = 0.33f;
                break;
            case MovementSpeed.Run:
                normalizedSpeed = 0.66f;
                break;
            case MovementSpeed.Sprint:
                normalizedSpeed = 1.0f;
                break;
        }

        // Smooth transition
        currentAnimatorSpeed = Mathf.SmoothDamp(
            currentAnimatorSpeed,
            normalizedSpeed,
            ref animatorVelocity,
            animationSmoothTime
        );

        // Set MovementSpeed parameter (float for blend tree)
        animator.SetFloat("MovementSpeed", currentAnimatorSpeed);

        // Set IsGrounded parameter
        animator.SetBool("IsGrounded", characterController.isGrounded);
    }

    // ========================================
    // Utility Methods
    // ========================================

    /// <summary>
    /// Check if NPC can move (used by AI to check for stuns/roots)
    /// </summary>
    public bool CanMove()
    {
        if (!isEnabled) return false;

        // TODO: Check for status effects (stun, root, freeze)
        // For now, always return true
        return true;
    }

    /// <summary>
    /// Get distance to a target position
    /// </summary>
    public float GetDistanceTo(Vector3 targetPosition)
    {
        return Vector3.Distance(transform.position, targetPosition);
    }

    /// <summary>
    /// Check if NPC is facing a target position (within angle threshold)
    /// </summary>
    public bool IsFacing(Vector3 targetPosition, float angleThreshold = 45f)
    {
        if (rootTransform == null) return false;

        Vector3 directionToTarget = (targetPosition - rootTransform.position).normalized;
        float angle = Vector3.Angle(rootTransform.forward, directionToTarget);
        return angle <= angleThreshold;
    }

    // ========================================
    // Debug
    // ========================================

    void OnDrawGizmos()
    {
        if (!showDebugInfo || rootTransform == null) return;

        // Draw movement direction
        if (moveDirection.magnitude > 0.1f)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(rootTransform.position + Vector3.up, moveDirection * 2f);
        }

        // Draw forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(rootTransform.position + Vector3.up, rootTransform.forward * 1.5f);
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 200, 300, 150));
        GUILayout.Label("=== NPC MOVEMENT ===");
        GUILayout.Label($"Speed: {currentSpeed}");
        GUILayout.Label($"Grounded: {IsGrounded}");
        GUILayout.Label($"Moving: {IsMoving}");
        GUILayout.Label($"Velocity: {Velocity.magnitude:F2}");
        GUILayout.EndArea();
    }

    // ========================================
    // Context Menu Helpers
    // ========================================

    [ContextMenu("Test: Walk Forward")]
    private void TestWalkForward()
    {
        MoveInDirection(transform.forward, MovementSpeed.Walk);
    }

    [ContextMenu("Test: Run Forward")]
    private void TestRunForward()
    {
        MoveInDirection(transform.forward, MovementSpeed.Run);
    }

    [ContextMenu("Test: Stop")]
    private void TestStop()
    {
        Stop();
    }
    #region IMovementProvider Implementation

    void IMovementProvider.Move(Vector3 direction, float speed)
    {
        MoveInDirection(direction, MovementSpeed.Walk);
    }

    void IMovementProvider.Rotate(Quaternion rotation)
    {
        if (rootTransform != null)
            rootTransform.rotation = rotation;
    }

    void IMovementProvider.Stop()
    {
        Stop();
    }

    void IMovementProvider.ApplyImpulse(Vector3 direction, float force)
    {
        // NPCs can receive knockback/impulses from effects
        if (characterController != null)
        {
            velocity += direction.normalized * force;
        }
    }

    void IMovementProvider.TeleportTo(Vector3 position)
    {
        // Teleport the NPC to a new position
        if (characterController != null)
        {
            characterController.enabled = false;
            transform.position = position;
            characterController.enabled = true;
        }
    }

    #endregion

}