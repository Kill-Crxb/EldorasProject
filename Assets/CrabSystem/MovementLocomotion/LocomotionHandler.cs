using UnityEngine;

/// <summary>
/// Base Locomotion Handler - Defines movement execution
/// 
/// This is the "HOW to move" layer - it defines your game's movement style.
/// Subclass this to create different game genres:
/// - RPGLocomotionHandler (third-person action RPG)
/// - MilSimLocomotionHandler (tactical FPS)
/// - VehicleLocomotionHandler (racing game)
/// 
/// INTERCHANGEABLE: Swap this to change your game's movement feel.
/// UNIVERSAL: Same handler used by both player and NPC entities.
/// 
/// The handler receives MovementInput (world-space, already transformed)
/// and executes movement via CharacterController.
/// </summary>
public abstract class LocomotionHandler : MonoBehaviour
{
    [Header("Core Settings")]
    [SerializeField] protected float walkSpeed = 2f;
    [SerializeField] protected float runSpeed = 4f;
    [SerializeField] protected float sprintSpeed = 6f;

    [Header("Physics")]
    [SerializeField] protected float gravity = -20f;
    [SerializeField] protected float groundedGravity = -2f;

    [Header("Debug")]
    [SerializeField] protected bool showDebugInfo = false;

    // References
    protected MovementSystem movementSystem;
    protected CharacterController characterController;
    protected Transform rootTransform;

    // State
    protected Vector3 currentVelocity;
    protected Vector3 verticalVelocity;

    // ========================================
    // Initialization
    // ========================================

    /// <summary>
    /// Initialize the locomotion handler
    /// Called by MovementSystem during setup
    /// </summary>
    public virtual void Initialize(MovementSystem system)
    {
        movementSystem = system;

        // Find CharacterController (should be on root GameObject)
        characterController = GetComponentInParent<CharacterController>();

        if (characterController == null)
        {
            Debug.LogError($"[{GetType().Name}] CharacterController not found!");
            enabled = false;
            return;
        }

        rootTransform = characterController.transform;

        if (showDebugInfo)
            Debug.Log($"[{GetType().Name}] Initialized on {rootTransform.name}");
    }

    // ========================================
    // Main Execution (Override in subclasses)
    // ========================================

    /// <summary>
    /// Execute movement for this frame
    /// 
    /// This is the main method that defines your game's movement style.
    /// Subclasses override this to implement their specific movement feel.
    /// 
    /// Input is already in world space - no transformation needed.
    /// </summary>
    public abstract void ExecuteMovement(MovementInput input);

    // ========================================
    // Common Helpers (Available to subclasses)
    // ========================================

    /// <summary>
    /// Apply gravity to vertical velocity
    /// Call this in your ExecuteMovement implementation
    /// </summary>
    protected virtual void ApplyGravity()
    {
        if (characterController.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = groundedGravity;
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }
    }

    /// <summary>
    /// Apply rotation to face a direction
    /// </summary>
    protected virtual void ApplyRotation(Vector2 lookDirection, float rotationSpeed)
    {
        if (lookDirection.magnitude < 0.1f) return;

        Vector3 lookDir3D = new Vector3(lookDirection.x, 0f, lookDirection.y);
        Quaternion targetRotation = Quaternion.LookRotation(lookDir3D);

        rootTransform.rotation = Quaternion.Slerp(
            rootTransform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// Move the CharacterController
    /// </summary>
    protected virtual void MoveCharacterController(Vector3 movement)
    {
        characterController.Move(movement * Time.deltaTime);
    }

    // ========================================
    // Public Properties
    // ========================================

    public virtual bool IsGrounded => characterController?.isGrounded ?? false;
    public virtual bool IsMoving => currentVelocity.magnitude > 0.1f;
    public virtual Vector3 Velocity => currentVelocity;
    public virtual Vector3 VerticalVelocity => verticalVelocity;
    public CharacterController CharacterController => characterController;
    public MovementSystem MovementSystem => movementSystem;

    // ========================================
    // Debug Visualization
    // ========================================

    protected virtual void OnDrawGizmos()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        // Draw velocity vector
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up, currentVelocity);

        // Draw vertical velocity
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up, verticalVelocity);
    }
}