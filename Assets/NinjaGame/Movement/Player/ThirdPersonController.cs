using UnityEngine;
using System;

/// <summary>
/// DEPRECATED: Backward compatibility stub for ThirdPersonController.
/// This redirects calls to the new LocomotionModule and other providers.
/// TODO: Remove this and update all references to use IMovementProvider directly.
/// </summary>
[System.Obsolete("Use LocomotionModule via IMovementProvider instead")]
public class ThirdPersonController : MonoBehaviour, IMovementState
{
    private ControllerBrain brain;
    private IMovementProvider movementProvider;
    private IResourceProvider resourceProvider;
    private IAnimationProvider animationProvider;

    // Events that used to exist
    public event Action<Vector2> OnMoveInput;
    public event Action OnSprintStarted;
    public event Action OnSprintCanceled;

    private void Awake()
    {
        brain = GetComponentInParent<ControllerBrain>();
        if (brain != null)
        {
            movementProvider = brain.Movement;
            resourceProvider = brain.GetModuleImplementing<IResourceProvider>();
            animationProvider = brain.GetModuleImplementing<IAnimationProvider>();
        }
    }

    // IMovementState implementation (for AnimationStateModule compatibility)
    public bool IsGrounded => movementProvider?.IsGrounded ?? false;
    public bool IsMoving => movementProvider?.IsMoving ?? false;
    public bool IsSprinting => movementProvider?.IsSprinting ?? false;
    public Vector3 Velocity => movementProvider?.Velocity ?? Vector3.zero;
    public float CurrentSpeed => Velocity.magnitude;
    public Vector3 MoveDirection => movementProvider?.MoveDirection ?? Vector3.zero;

    // Stamina properties (redirect to IResourceProvider)
    public float CurrentStamina => resourceProvider?.GetResource(ResourceType.Stamina) ?? 0f;
    public float MaxStamina => resourceProvider?.GetMaxResource(ResourceType.Stamina) ?? 100f;

    // Animator property (redirect to brain)
    public Animator Animator => brain?.EntityAnimator;

    // Methods
    public void Stop()
    {
        movementProvider?.Stop();
    }

    public bool ConsumeStamina(float amount)
    {
        return resourceProvider?.ConsumeResource(ResourceType.Stamina, amount) ?? false;
    }

    public ControllerBrain GetBrain()
    {
        return brain;
    }

    public void SetLockOnTarget(Transform target)
    {
        // This was camera-related functionality
        // For now, just log that it's deprecated
        Debug.LogWarning("[ThirdPersonController] SetLockOnTarget is deprecated. Update TargetLockModule to use camera system directly.");
    }

    // Animation helpers
    public void TriggerAnimation(string triggerName)
    {
        animationProvider?.SetTrigger(triggerName);
    }

    public void SetAnimationInt(string paramName, int value)
    {
        animationProvider?.SetInteger(paramName, value);
    }
}