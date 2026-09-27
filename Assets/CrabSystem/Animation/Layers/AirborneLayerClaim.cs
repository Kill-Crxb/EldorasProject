using UnityEngine;

// Holds the full-body layer up while the character is off the ground, so jump, fall and land
// actually contribute to the pose.
//
// Deliberately NOT an edit to the locomotion handler. The jump states are already authored and
// already transition correctly; the only thing missing was weight on the layer they live on.
// Claiming from a separate module keeps signed-off movement code untouched, and means the claim
// is released by grounding rather than by remembering to release it on every exit path.
public class AirborneLayerClaim : MonoBehaviour, IBrainModule
{
    [SerializeField] private string layerName = AnimationLayerNames.FullBodyActions;
    [SerializeField, Range(0f, 1f)] private float weight = 1f;

    [Tooltip("How long the claim is held after touching down, so the land clip plays out instead " +
             "of being cut off the instant the ground probe says grounded.")]
    [SerializeField] private float holdAfterLanding = 0.25f;

    private ControllerBrain brain;
    private AnimationLayerController layers;
    private MovementSystem movement;

    private bool claimed;
    private float releaseTime;

    public bool IsEnabled { get; set; } = true;
    public bool Claimed => claimed;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        layers = brain?.GetModule<AnimationLayerController>();
        movement = brain?.Movement;

        if (layers == null)
            Debug.LogError($"[AirborneLayerClaim] No AnimationLayerController found for '{name}'. Jump animation will not be weighted.", this);
    }

    public void UpdateModule()
    {
        if (layers == null) return;

        if (!IsEnabled)
        {
            ReleaseNow();
            return;
        }

        if (!IsGrounded())
        {
            releaseTime = 0f;

            if (!claimed)
            {
                layers.Claim(this, layerName, weight, AnimationLayerController.PriorityLocomotion);
                claimed = true;
            }

            return;
        }

        if (!claimed) return;

        if (releaseTime <= 0f) releaseTime = Time.time + holdAfterLanding;
        if (Time.time < releaseTime) return;

        ReleaseNow();
    }

    private void ReleaseNow()
    {
        if (!claimed) return;

        layers.Release(this, layerName);
        claimed = false;
        releaseTime = 0f;
    }

    private bool IsGrounded()
    {
        if (movement != null) return movement.IsGrounded;
        return brain != null && brain.IsGrounded;
    }

    void OnDisable()
    {
        ReleaseNow();
    }
}
