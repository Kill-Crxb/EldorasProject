using System;
using UnityEngine;

/// <summary>
/// How a projectile is delivered. One block with toggles rather than a class per behaviour —
/// travel, gravity, spin, homing and hitscan are all the same shape with different switches.
///
/// Lives INLINE on ProjectileData, not as its own asset. Once behaviour is toggles rather
/// than subclasses there is nothing left to swap polymorphically, so an extra asset per
/// projectile only buys indirection.
///
/// STILL SHARED. One ProjectileData serves every in-flight copy at once, so this block must
/// stay stateless — velocity, direction and target all live on ProjectileBrain. Reading
/// brain.Runtime here is fine; writing a field on this class is not.
///
/// The modifiable stats (speed, gravity, homingStrength) come from ProjectileRuntime, so a
/// modifier can turn a straight fireball into a seeking one by raising homingStrength alone.
/// Everything in this block is authoring-time tuning.
///
/// Pointing and target acquisition live in ProjectileAim, not here — this block is only
/// concerned with how the thing flies once it has been aimed.
/// </summary>

public enum ProjectileMode
{
    /// <summary>Spawns and flies. The sweep resolves hits along the way.</summary>
    Travel,

    /// <summary>Resolves instantly along the aim line, then lingers only to show a tracer.</summary>
    Hitscan,
}

[Serializable]
public class ProjectileMovement
{
    // =========================================================================
    // Mode
    // =========================================================================

    [Header("Mode")]
    [Tooltip("Travel flies through the world. Hitscan skips travel entirely and resolves " +
             "along the aim line on the first frame.")]
    public ProjectileMode mode = ProjectileMode.Travel;

    // =========================================================================
    // Travel
    // =========================================================================

    [Header("Travel")]
    [Tooltip("Degrees per second the Visual child spins around its own forward axis. Cosmetic " +
             "only — the root's rotation is the travel direction. Shuriken wants ~720, a bolt 0.")]
    public float spinDegreesPerSecond = 0f;

    // =========================================================================
    // Hitscan
    // =========================================================================

    [Header("Hitscan")]
    [Tooltip("How far the instant resolve reaches.")]
    public float hitscanRange = 100f;

    [Tooltip("Seconds the object stays alive after resolving, so a trail or beam has time to " +
             "render. 0 releases the same frame.")]
    public float hitscanLingerTime = 0f;

    // =========================================================================
    // Homing
    // =========================================================================

    [Header("Homing")]
    [Tooltip("Maximum degrees per second at homingStrength 1. Scaled down by the runtime value, " +
             "so homingStrength 0 means no steering at all and this block is inert.\n\n" +
             "Steering also needs a TARGET, which comes from ProjectileData.aim — a projectile " +
             "that acquired nothing flies straight however high homingStrength is.")]
    public float maxTurnDegreesPerSecond = 180f;

    [Tooltip("Seconds of straight flight before steering begins. 0 tracks from the moment of " +
             "release; above 0 the projectile flies out, then turns — the classic missile boost.\n\n" +
             "This is the WHEN of homing. The WHAT (locked target vs aim probe) is Target Sources " +
             "in the Aim block, and the HOW HARD is Homing Strength. All three are independent.")]
    public float homingDelay = 0f;

    [Tooltip("Stop steering once this close. Prevents the corkscrew orbit at point blank.")]
    public float minSteerDistance = 0.5f;

    // =========================================================================
    // Travel Step
    // =========================================================================

    /// <summary>
    /// Where this projectile should be after deltaTime. Collision is NOT done here —
    /// ProjectileMovementHandler sweeps between the old and new position, so every
    /// configuration gets non-tunnelling hit detection for free.
    /// </summary>
    public Vector3 Step(ProjectileBrain brain, Vector3 currentPosition, float deltaTime)
    {
        Vector3 velocity = brain.Velocity;
        bool changed = false;

        if (IsHoming(brain))
        {
            velocity = Steer(brain, currentPosition, velocity, deltaTime);
            changed = true;
        }
        else if (brain.Runtime.gravity != 0f)
        {
            // Gravity applies only when NOT homing — steering rebuilds velocity from the
            // target direction each frame, which would erase accumulated fall anyway.
            velocity += Vector3.down * brain.Runtime.gravity * deltaTime;
            changed = true;
        }

        if (changed)
        {
            brain.Velocity = velocity;
            brain.SetDirection(velocity);
        }

        ApplySpin(brain, deltaTime);

        return currentPosition + velocity * deltaTime;
    }

    /// <summary>
    /// Three independent conditions, all of which must hold:
    ///   strength  — homingStrength above 0 (a runtime stat, so a modifier can grant it)
    ///   target    — something was actually acquired; a shot at empty air never tracks
    ///   time      — homingDelay has elapsed, for a missile that flies out before turning
    /// </summary>
    public bool IsHoming(ProjectileBrain brain)
    {
        if (brain.Runtime.homingStrength <= 0f) return false;
        if (brain.Target == null) return false;
        if (brain.ElapsedTime < homingDelay) return false;

        return true;
    }

    private Vector3 Steer(ProjectileBrain brain, Vector3 currentPosition, Vector3 velocity, float deltaTime)
    {
        float heightOffset = brain.Data.aim != null ? brain.Data.aim.targetHeightOffset : 1.2f;

        Vector3 aimPoint = brain.Target.position + Vector3.up * heightOffset;
        Vector3 toTarget = aimPoint - currentPosition;

        if (toTarget.magnitude < minSteerDistance) return velocity;
        if (velocity.sqrMagnitude < 0.0001f) return velocity;

        float maxRadians = maxTurnDegreesPerSecond * brain.Runtime.homingStrength * Mathf.Deg2Rad * deltaTime;
        Vector3 steered = Vector3.RotateTowards(velocity.normalized, toTarget.normalized, maxRadians, 0f);

        return steered * brain.Runtime.speed;
    }

    /// <summary>
    /// Spins the Visual child, never the root — the root's rotation is the travel direction.
    /// </summary>
    private void ApplySpin(ProjectileBrain brain, float deltaTime)
    {
        if (Mathf.Approximately(spinDegreesPerSecond, 0f)) return;

        Transform visual = brain.VisualRoot;
        if (visual == null) return;

        visual.Rotate(Vector3.forward, spinDegreesPerSecond * deltaTime, Space.Self);
    }

}
