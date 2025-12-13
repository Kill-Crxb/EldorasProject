using UnityEngine;

public class MovementProviderCoordinator : ProviderCoordinator
{
    [Header("Required Provider Slots")]
    [Tooltip("Must implement IMovementProvider (LocomotionModule, ThirdPersonController, NPCMovementModule, StaticMovementProvider)")]
    [SerializeField] private MonoBehaviour movementProvider;

    [Tooltip("Must implement IAnimationProvider (AnimationStateModule, NullAnimationProvider)")]
    [SerializeField] private MonoBehaviour animationProvider;

    [Header("Optional Movement Modules (Auto-discovered)")]
    [SerializeField] private FeetDetectionModule feetDetection;

    private IMovementProvider movement;
    private new IAnimationProvider animation;

    protected override bool ValidateSlots()
    {
        bool valid = true;

        valid &= ValidateProvider<IMovementProvider>(movementProvider, "Movement Provider");
        valid &= ValidateProvider<IAnimationProvider>(animationProvider, "Animation Provider");

        return valid;
    }

    protected override void CacheProviders()
    {
        movement = movementProvider as IMovementProvider;
        animation = animationProvider as IAnimationProvider;

        // Auto-discover optional modules if not assigned
        if (feetDetection == null)
            feetDetection = GetComponentInChildren<FeetDetectionModule>();
    }

    protected override void OnInitialized()
    {
        // Initialize provider modules
        if (movementProvider is IBrainModule m1) m1.Initialize(brain);
        if (animationProvider is IBrainModule m2) m2.Initialize(brain);
    }

    // Public accessors
    public IMovementProvider Movement => movement;
    public IAnimationProvider Animation => animation;

    // Optional module accessors
    public FeetDetectionModule FeetDetection => feetDetection;

    // Convenience methods
    public bool IsGrounded() => feetDetection?.IsGrounded ?? false;
    public bool IsMoving() => movement?.IsMoving ?? false;

    // Movement capability checks (for LocomotionModule)
    public bool CanJump()
    {
        // Use IsGrounded property from IMovementProvider
        if (movement != null)
            return movement.IsGrounded;
        return false;
    }
}