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
// The movement tier the player has chosen or been granted — not how fast they happen to be going.
// None means the handler has no gait concept, and MovementSystem falls back to measured speed.
public enum Gait { None, Crouch, Walk, Run, Sprint }

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
    // External Motion — the ability channel
    // ========================================

    /// <summary>
    /// Additive impulse: knockback, launches, air jumps — anything that ADDS to momentum rather
    /// than replacing it. This is the design doc's IntegrateExternal, and the only way an ability
    /// is allowed to change velocity. Nothing outside a handler writes currentVelocity, and nothing
    /// outside a motor writes the root transform.
    ///
    /// ⚠ Virtual on the BASE deliberately. MovementEffect and KnockbackEffect used to reach this
    /// through `Locomotion as ARPGLocomotionHandler`, so the moment slice 1 swapped the handler the
    /// cast returned null and every knockback, launch, dash and teleport on the player became a
    /// silent no-op — the same failure that blanked MovementDebugDisplay for a whole slice. Call it
    /// through LocomotionHandler. Do not cast to a concrete handler.
    /// </summary>
    public virtual void ApplyImpulse(Vector3 direction, float force)
    {
        currentVelocity += direction.normalized * force;
    }

    /// <summary>
    /// Instant position change. This default is the CharacterController path, which has to be
    /// disabled across the write or it immediately fights it. Handlers owning their own motor
    /// override it and go through the motor instead.
    /// </summary>
    public virtual void TeleportTo(Vector3 position)
    {
        if (characterController == null || rootTransform == null) return;

        characterController.enabled = false;
        rootTransform.position = position;
        characterController.enabled = true;

        currentVelocity = Vector3.zero;
        verticalVelocity = Vector3.zero;
    }

    /// <summary>
    /// Timed velocity override — a dash that OWNS locomotion for a duration rather than nudging it.
    /// Returns false when the handler has no such mode, so a caller can say so out loud instead of
    /// silently doing nothing.
    ///
    /// ParkourLocomotionHandler returns false on purpose. A dash there belongs to a LowerBodyState
    /// plus an impulse, not to a coroutine that hides velocity from friction, gravity and collision
    /// for its whole duration. See Movement_Ability_Interface.md, channel 4.
    /// </summary>
    public virtual bool BeginDash(Vector3 direction, float speed, float duration) => false;

    /// <summary>
    /// Hold ground friction off for a moment. Paired with ApplyImpulse this is what separates a
    /// dash from a shove: friction decays velocity with a time constant of 1/groundFriction, so at
    /// friction 5 an unprotected impulse is 63% gone in 0.2s and a big one buys barely a metre.
    ///
    /// Deliberately NOT a parameter on ApplyImpulse. An ability may want one without the other —
    /// an ice patch suppresses friction with no impulse, knockback wants an impulse with no
    /// holiday — and a combined signature would force every caller to answer both questions.
    ///
    /// No-op on the base: friction is a concept of the handler that has one. ARPGLocomotionHandler
    /// lerps toward a target velocity and has no friction term to suspend.
    /// </summary>
    public virtual void SuppressFriction(float seconds) { }

    // ========================================
    // Public Properties
    // ========================================

    /// <summary>
    /// True when this handler owns grounding and MovementSystem should read IsGrounded from it
    /// instead of from a FeetDetectionModule trigger volume. Sweep-based handlers set this; the
    /// CharacterController-based ones leave it false and the trigger volume stays authoritative.
    /// Two answers to "am I grounded" is one too many, and this is which one wins.
    /// </summary>
    public virtual bool ProvidesGrounding => false;

    public virtual bool IsGrounded => characterController?.isGrounded ?? false;
    public virtual bool IsMoving => currentVelocity.magnitude > 0.1f;
    public virtual Gait CurrentGait => Gait.None;
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