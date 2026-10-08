using UnityEngine;

// Hands the clip's root motion to the character's movement handler. Sits next to the Animator, because
// Unity only calls OnAnimatorMove there. Having this callback is also what stops the Animator moving the
// model itself: the body stays on the capsule, and the travel goes to the handler, which uses it only
// while a useRootMotion move plays (MovementSystem.RootMotionDriven). Walking stays the handler's.
// No brain (the creation preview) means nothing to hand it to, so the model plays in place.
[RequireComponent(typeof(Animator))]
public class RootMotionRelay : MonoBehaviour, IModelPart
{
    private Animator animator;
    private MovementSystem movement;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = true; // handled here; never applied to the transform
    }

    public void Bind(ControllerBrain brain, ModelModule model)
    {
        movement = brain != null ? brain.Movement : null;
    }

    private void OnAnimatorMove()
    {
        if (movement == null || movement.Locomotion == null) return;
        movement.Locomotion.AddRootMotion(animator.deltaPosition, Time.deltaTime);
    }
}
