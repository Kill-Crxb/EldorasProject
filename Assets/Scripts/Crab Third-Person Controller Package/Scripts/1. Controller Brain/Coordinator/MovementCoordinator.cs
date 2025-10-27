using UnityEngine;

public class MovementCoordinator : MonoBehaviour, IBrainModule, ISystemCoordinator
{
    [Header("Module Settings")]
    public bool IsEnabled { get; set; } = true;

    private ControllerBrain brain;
    private ThirdPersonController controller;
    private AdvancedMovementModule advancedMovement;
    private FeetDetectionModule feetDetection;
    private AnimationStateModule animationState;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        controller = brain.GetModule<ThirdPersonController>();
        advancedMovement = brain.GetModule<AdvancedMovementModule>();
        feetDetection = brain.GetModule<FeetDetectionModule>();
        animationState = brain.GetModule<AnimationStateModule>();
    }

    public void UpdateModule()
    {
    }

    public ThirdPersonController GetController() => controller;
    public AdvancedMovementModule GetAdvancedMovement() => advancedMovement;
    public FeetDetectionModule GetFeetDetection() => feetDetection;
    public AnimationStateModule GetAnimationState() => animationState;

    public bool IsGrounded() => brain?.IsGrounded ?? false;
    public bool IsMoving() => controller?.IsMoving ?? false;
    public bool IsSprinting() => controller?.IsSprinting ?? false;

    public bool CanMove() => controller?.CanMove() ?? false;
    public bool CanSprint() => controller?.CanSprint() ?? false;
    public bool CanGroundJump() => advancedMovement?.CanGroundJump ?? false;
    public bool CanAirJump() => advancedMovement?.CanAirJump ?? false;

    public Vector3 GetVelocity() => controller?.Velocity ?? Vector3.zero;
    public float GetCurrentSpeed() => controller?.Velocity.magnitude ?? 0f;

    public float GetCurrentStamina() => controller?.CurrentStamina ?? 0f;
    public float GetMaxStamina() => controller?.MaxStamina ?? 0f;
}