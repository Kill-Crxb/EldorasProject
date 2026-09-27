using System;
using UnityEngine;

// A short dip on the hips at touchdown, scaled by how hard she hit. Landings with no vertical
// give read as weightless no matter how good the clip is.
public class LandingImpactSystem : AnimatorIKModule
{
    [Header("Impact Range")]
    [Tooltip("Fall speed below which a landing produces nothing. A step off a kerb should not " +
             "buckle her knees.")]
    [SerializeField] private float minImpactSpeed = 3f;
    [SerializeField] private float maxImpactSpeed = 14f;

    [Header("Dip")]
    [SerializeField] private float maxDip = 0.18f;

    [Tooltip("Metres per second the hips travel down into the dip. Fast — the compression is an " +
             "impact, not an ease.")]
    [SerializeField] private float compressSpeed = 1.6f;

    [Tooltip("Metres per second coming back up. Slower than the compression; that asymmetry is " +
             "what reads as absorbing weight rather than bouncing.")]
    [SerializeField] private float recoverSpeed = 0.45f;

    private MovementSystem movement;
    private CharacterMotor motor;

    private bool wasGrounded = true;
    private float fallSpeed;
    private float targetDip;
    private float currentDip;
    private bool compressing;

    public event Action<float> OnLanded;

    public float CurrentDip => currentDip;

    // Between the feet and the head: the feet want the unmodified pose, the head wants the
    // shifted one.
    public override int IKOrder => 5;

    protected override void ResolveReferences()
    {
        movement = brain?.Movement;
        motor = FeelReferences.FindMotor(brain);
    }

    public override void UpdateModule()
    {
        base.UpdateModule();

        bool grounded = IsGrounded();

        if (!grounded)
        {
            float vertical = movement != null ? movement.Velocity.y : 0f;
            fallSpeed = Mathf.Max(fallSpeed, -vertical);
        }
        else if (!wasGrounded)
        {
            Land();
        }

        wasGrounded = grounded;
    }

    private void Land()
    {
        float strength = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, fallSpeed);
        fallSpeed = 0f;

        if (strength <= 0f) return;

        targetDip = strength * maxDip;
        compressing = true;

        OnLanded?.Invoke(strength);
    }

    protected override void Solve(Animator animator)
    {
        float dt = Time.deltaTime;

        if (compressing)
        {
            currentDip = Mathf.MoveTowards(currentDip, targetDip, compressSpeed * dt);
            if (currentDip >= targetDip) compressing = false;
        }
        else
        {
            currentDip = Mathf.MoveTowards(currentDip, 0f, recoverSpeed * dt);
        }

        if (currentDip <= 0f) return;

        animator.bodyPosition -= Vector3.up * currentDip;
    }

    private bool IsGrounded()
    {
        if (motor != null) return motor.IsGrounded;
        if (movement != null) return movement.IsGrounded;
        return brain != null && brain.IsGrounded;
    }
}
