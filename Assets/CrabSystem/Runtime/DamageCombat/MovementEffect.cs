using System;
using UnityEngine;

/// <summary>
/// Which basis a MovementEffect's direction is expressed in.
///
/// ⚠ Appended, never reordered — these serialize by integer and every authored asset stores a
/// number, so inserting a member silently repoints every existing effect.
/// </summary>
public enum MovementDirectionSource
{
    CasterFacing,
    Movement,
}

[System.Serializable]
public class MovementEffect
{
    public enum MovementType
    {
        Impulse,      // Instant velocity boost (additive, for knockback)
        Dash,         // Override movement for duration (dash abilities)
        Teleport,     // Instant position change
        Launch        // Upward/directional launch
    }

    [Header("Movement Configuration")]
    public MovementType movementType = MovementType.Dash;

    [Header("Direction")]
    [Tooltip("What 'forward' means for this effect.\n\n" +
             "CasterFacing — the body's facing. On the parkour controller the body tracks the " +
             "camera, so this is 'dash where I'm looking'.\n\n" +
             "Movement — the direction of travel, taken from horizontal velocity. Momentum-first: " +
             "a dash extends the run you were already doing rather than snapping to the camera. " +
             "Falls back to facing below MinMovementSpeed, because a standing player has no " +
             "direction of travel and 'where I'm looking' is the only honest answer.")]
    public MovementDirectionSource directionSource = MovementDirectionSource.CasterFacing;

    [Tooltip("Direction in the basis chosen above. Vector3.forward is the common case. Magnitude " +
             "is ignored — it is normalised, and `speed` is the force. Vector3.back with a " +
             "Movement basis is a backstep along your own momentum.")]
    public Vector3 direction = Vector3.forward;

    [Header("Dash/Impulse Settings")]
    public float speed = 15f;

    [Tooltip("Caster stat added to Speed (and to Speed Cap when one is set), e.g. mov.dash_speed, so talents and " +
             "the burden track move the push. Empty = speed as authored.")]
    [IdRef(IdKind.Stat)] public string speedStatId;
    public float duration = 0.2f;

    [Header("Speed Cap")]
    [Tooltip("Top speed this effect will push you to ALONG ITS OWN DIRECTION, in m/s. Zero means " +
             "no cap and the impulse simply adds, which is the old behaviour.\n\n" +
             "This is what stops a dash stacking with itself. With a cap the impulse tops you up " +
             "toward it instead of adding: dash at 5 toward a cap of 13 gives the full push, dash " +
             "again at 13 gives nothing. Dash SIDEWAYS at 13 still gives the full push, because " +
             "the speed measured along the new direction is near zero — so redirecting hard at " +
             "speed keeps working and only repeating the same direction is bounded.\n\n" +
             "It is the same clamp Accelerate uses for ordinary movement, which is why a capped " +
             "impulse stops being a special case in a controller whose one law is that " +
             "acceleration is clamped and total speed never is.\n\n" +
             "⚠ ALONG ITS OWN DIRECTION is literal, and it bites on a diagonal. A 45° up-and-" +
             "forward dash only sees 0.707 of your ground speed, so a cap of 13 lets you reach " +
             "18.4 m/s along the floor before it stops adding. Scale it: " +
             "cap = intended ground speed × the direction's horizontal fraction. For (0,1,1) that " +
             "fraction is 0.707, so 13 m/s on the ground wants a cap of 9.2.\n\n" +
             "Leave at 0 for knockback: being hit twice SHOULD hurt twice.")]
    public float speedCap = 0f;

    [Header("Friction Holiday")]
    [Tooltip("Seconds of no GROUND friction after the impulse lands. This is what makes an impulse " +
             "read as a dash rather than a shove: friction decays velocity with a time constant of " +
             "1/groundFriction, so at friction 5 an unprotected impulse is most of the way gone in " +
             "0.2s. Zero keeps the old behaviour, so existing assets are unchanged.\n\n" +
             "Airborne impulses do not need this — friction only applies on the ground.\n\n" +
             "Keep it short. Held too long the character reads as being on ice, and nothing " +
             "cancels it early: it is a timer, not a state.")]
    public float frictionHoliday = 0f;

    [Header("Teleport Settings")]
    public float teleportDistance = 10f;

    public event Action OnCompleted;

    [System.NonSerialized]
    private MovementSystem movementSystem;

    [System.NonSerialized]
    private Transform casterTransform;

    [System.NonSerialized]
    private bool warnedNoDash;

    public void SetMovementSystem(MovementSystem system)
    {
        movementSystem = system;
        casterTransform = ResolveRoot(system);
    }

    /// <summary>
    /// The entity root, asked for by name rather than assumed to be one level up the hierarchy.
    /// `system.transform.parent` happened to be right on Base_PC and breaks silently on anything
    /// nested differently — and a wrong root here means every dash points the wrong way.
    /// </summary>
    static Transform ResolveRoot(MovementSystem system)
    {
        if (system == null) return null;
        if (system.Brain != null && system.Brain.EntityRoot != null) return system.Brain.EntityRoot;
        if (system.Brain != null) return system.Brain.transform;

        return system.transform.parent;
    }

    // Backward compatibility - deprecated
    [System.Obsolete("Use SetMovementSystem instead")]
    public void SetMovementProvider(object provider)
    {
        // Try to get MovementSystem from provider
        if (provider is MonoBehaviour mb)
        {
            movementSystem = mb.GetComponent<MovementSystem>();
            casterTransform = mb.transform.parent;
        }
    }

    public void Apply(MovementSystem target)
    {
        SetMovementSystem(target);
        ExecuteEffect();
    }

    // Backward compatibility - deprecated
    [System.Obsolete("Use Apply(MovementSystem) instead")]
    public void Apply(object target)
    {
        if (target is MonoBehaviour mb)
        {
            movementSystem = mb.GetComponent<MovementSystem>();
            casterTransform = mb.transform.parent;
            ExecuteEffect();
        }
    }

    private void ExecuteEffect()
    {
        if (movementSystem == null || casterTransform == null)
        {
            Debug.LogWarning("[MovementEffect] MovementSystem or casterTransform not set!");
            return;
        }

        if (movementSystem.Locomotion == null)
        {
            Debug.LogWarning($"[MovementEffect] No LocomotionHandler on {casterTransform.name} — " +
                             $"movement effects cannot run.");
            return;
        }

        switch (movementType)
        {
            case MovementType.Impulse:
                ApplyImpulse();
                break;

            case MovementType.Dash:
                ApplyDash();
                break;

            case MovementType.Teleport:
                ApplyTeleport();
                break;

            case MovementType.Launch:
                ApplyLaunch();
                break;
        }

        OnCompleted?.Invoke();
    }

    // ⚠ Every method below used to be gated on `movementSystem.Locomotion is ARPGLocomotionHandler`.
    // Slice 1 swapped the player onto ParkourLocomotionHandler, the cast started returning null, and
    // Impulse / Teleport / Launch became SILENT no-ops with no else branch — Dash "fell back" to
    // Impulse, which failed the same way. Nothing on the player has moved through this class since.
    // These now call through LocomotionHandler, which is what MovementSystem.Locomotion is typed as.
    // Do not reintroduce a concrete-handler cast here.

    private float SpeedBonus => string.IsNullOrEmpty(speedStatId) ? 0f : DialIds.Read(movementSystem.Brain != null ? movementSystem.Brain.Stats : null, speedStatId);

    private float Speed => Mathf.Max(0f, speed + SpeedBonus);

    private void ApplyImpulse()
    {
        Vector3 worldDirection = CalculateWorldDirection(casterTransform);
        Push(worldDirection);
    }

    private void ApplyDash()
    {
        Vector3 worldDirection = CalculateWorldDirection(casterTransform);

        // A dash OWNS locomotion for a duration, which not every handler offers — the parkour
        // handler deliberately does not, because that mode belongs to a LowerBodyState rather than
        // a coroutine. Say so once and degrade to an impulse, rather than doing nothing quietly.
        if (movementSystem.Locomotion.BeginDash(worldDirection, Speed, duration)) return;

        if (!warnedNoDash)
        {
            warnedNoDash = true;
            Debug.LogWarning($"[MovementEffect] {movementSystem.Locomotion.GetType().Name} has no " +
                             $"dash mode — applying an impulse of {Speed} instead. Dash is channel 4 " +
                             $"and is not built yet; see Movement_Ability_Interface.md.");
        }

        ApplyImpulse();
    }

    private void ApplyTeleport()
    {
        Vector3 worldDirection = CalculateWorldDirection(casterTransform);
        Vector3 teleportPosition = casterTransform.position + worldDirection * teleportDistance;

        movementSystem.Locomotion.TeleportTo(teleportPosition);
    }

    private void ApplyLaunch()
    {
        Vector3 worldDirection = CalculateWorldDirection(casterTransform);
        Push(worldDirection);
    }

    /// <summary>
    /// Speed below which there is no meaningful direction of travel and Movement falls back to
    /// facing. Above walking pace would steal the fallback from a genuine slow walk; far below it
    /// would let post-landing drift or a nudge against a wall pick the direction.
    /// </summary>
    private const float MinMovementSpeed = 0.5f;

    /// <summary>
    /// Apply the impulse, capped along its own direction when the asset asks for it.
    ///
    /// The uncapped path is a plain add, which is correct for knockback and was the only
    /// behaviour before. The capped path measures how fast you ALREADY are in this direction and
    /// adds only the shortfall — so repeated pushes converge on the cap instead of compounding.
    ///
    /// Nothing happens at all when the shortfall is zero, INCLUDING the friction holiday. A dash
    /// that adds no speed must not also hand out free momentum retention, or spamming it at the
    /// cap would still be better than not.
    /// </summary>
    private void Push(Vector3 worldDirection)
    {
        float bonus = SpeedBonus;
        float force = Mathf.Max(0f, speed + bonus);

        if (speedCap > 0f)
        {
            float already = Vector3.Dot(movementSystem.Velocity, worldDirection);
            force = Mathf.Min(force, Mathf.Max(speedCap + bonus - already, 0f));
        }

        if (force <= 0f) return;

        movementSystem.Locomotion.ApplyImpulse(worldDirection, force);
        movementSystem.Locomotion.SuppressFriction(frictionHoliday);
    }

    private Vector3 CalculateWorldDirection(Transform caster)
    {
        // A zeroed direction would normalise to zero and apply no force at all — silently. Forward
        // is what an author leaving the field blank meant.
        Vector3 local = direction.sqrMagnitude < 0.000001f ? Vector3.forward : direction.normalized;

        return ResolveBasis(caster) * local;
    }

    /// <summary>
    /// The rotation `direction` is expressed in. Replaces the old special-casing of exact forward
    /// and back, which was doing the same thing three ways — Transform.TransformDirection ignores
    /// position and scale, so it is already just `rotation * v`.
    /// </summary>
    private Quaternion ResolveBasis(Transform caster)
    {
        if (directionSource == MovementDirectionSource.CasterFacing) return caster.rotation;

        Vector3 travel = movementSystem.Velocity;
        travel.y = 0f;

        if (travel.sqrMagnitude < MinMovementSpeed * MinMovementSpeed) return caster.rotation;

        return Quaternion.LookRotation(travel.normalized, Vector3.up);
    }

    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}