using System;
using UnityEngine;

/// <summary>
/// How a projectile is pointed, and what it is allowed to track. Inline block on
/// ProjectileData, alongside ProjectileMovement.
///
/// Two separate questions, deliberately kept separate:
///
///   WHERE DO I POINT?   directionMode — mutually exclusive, so a dropdown.
///   WHAT DO I TRACK?    targetSources — a priority chain you may want several of,
///                       so flags (tick boxes).
///
/// They are resolved in three stages so there is no circular dependency between
/// "aim at the target" and "probe along the aim line":
///
///   1. BaseDirection()  — camera or body facing. Never depends on a target.
///   2. Probe()          — cast along the base direction to find a target.
///   3. FinalDirection() — point at the target if directionMode says to, else keep base.
///
/// SHARED and stateless, like ProjectileMovement — one ProjectileData serves every copy
/// in flight at once.
/// </summary>

/// <summary>Where the launch direction comes from. Mutually exclusive.</summary>
public enum AimDirectionMode
{
    /// <summary>Free aim down the camera. Falls back to body facing when there is no camera (NPCs).</summary>
    CameraForward,

    /// <summary>Always the body's facing. The honest choice for NPCs and for locked-camera games.</summary>
    EntityForward,

    /// <summary>
    /// Point straight at whatever target was resolved — a homing missile that starts on
    /// course, or a lock-on beam that cannot miss. Falls back to the base direction when
    /// nothing was targeted, so a free-aim shot still works.
    /// </summary>
    TowardTarget,
}

/// <summary>
/// Where a trackable target may come from. Ticked sources are tried in this order:
/// LockedTarget first (an explicit player/AI decision), then AimProbe.
/// </summary>
[Flags]
public enum TargetSource
{
    None = 0,

    /// <summary>
    /// The entity's deliberate target — the player's target lock, or an NPC's perception
    /// target. Leave this OFF until a real targeting system exists; the current
    /// TargetLockModule is legacy.
    /// </summary>
    LockedTarget = 1 << 0,

    /// <summary>
    /// A spherecast along the aim line at the moment of release. This is what makes
    /// free-aim homing a skill rather than an auto-hit — miss the probe and the shot
    /// flies straight.
    /// </summary>
    AimProbe = 1 << 1,
}

[Serializable]
public class ProjectileAim
{
    private static readonly RaycastHit[] ProbeBuffer = new RaycastHit[16];

    // =========================================================================
    // Direction
    // =========================================================================

    [Header("Direction")]
    [Tooltip("Where the launch direction comes from.")]
    public AimDirectionMode directionMode = AimDirectionMode.CameraForward;

    // =========================================================================
    // Targeting
    // =========================================================================

    [Header("Targeting")]
    [Tooltip("Which sources may supply a trackable target. Tried in order: Locked Target, " +
             "then Aim Probe. Set to None for a projectile that must never track.\n\n" +
             "Locked Target is OFF by default — the current TargetLockModule is legacy, so " +
             "nothing should depend on it until a real targeting system exists.")]
    public TargetSource targetSources = TargetSource.AimProbe;

    [Tooltip("Aim this far above a target's origin, so shots go for the body not the feet. " +
             "Used both for pointing at a target and for steering toward one.")]
    public float targetHeightOffset = 1.2f;

    // =========================================================================
    // Probe
    // =========================================================================

    [Header("Aim Probe")]
    [Tooltip("How far the probe reaches.")]
    public float probeRange = 40f;

    [Tooltip("Probe radius — the forgiveness cone. Small means you must be nearly dead on for " +
             "a shot to track; large means it grabs anything roughly ahead of you.")]
    public float probeRadius = 1.5f;

    [Tooltip("Aim assist: when the probe finds nothing, the caster's soft target (TargetingModule) is used if it sits " +
             "within this many degrees of the aim line, and the shot is aimed at it. 0 = off.")]
    public float softAssistDegrees = 8f;

    // =========================================================================
    // Queries
    // =========================================================================

    public bool UsesLockedTarget => (targetSources & TargetSource.LockedTarget) != 0;
    public bool UsesProbe => (targetSources & TargetSource.AimProbe) != 0;

    // =========================================================================
    // Stage 1 — base direction
    // =========================================================================

    /// <summary>
    /// The direction before any target is known. Never depends on a target, which is what
    /// keeps "aim at target" and "probe along aim" from chasing each other.
    /// </summary>
    public Vector3 BaseDirection(ControllerBrain source, Vector3 origin)
    {
        if (source == null) return Vector3.forward;

        Transform root = source.EntityRoot != null ? source.EntityRoot : source.transform;

        if (directionMode == AimDirectionMode.EntityForward)
            return root.forward;

        // CameraForward and TowardTarget both start from the camera where one exists.
        // Resolved through ICameraProvider rather than a concrete camera type — the brain
        // registers CameraModule under that interface, and only for players. NPCs get null
        // and fall through to body facing, which is what they should use anyway.
        ICameraProvider cameraProvider = source.GetModule<ICameraProvider>();

        // From the cast origin to where the screen-centre aim ray lands, so the shot goes where the crosshair is.
        // Flying parallel to the camera instead sent it into the floor whenever the camera looked down at the player.
        if (cameraProvider != null && cameraProvider.CameraTransform != null)
            return AimAt(cameraProvider.AimPoint(probeRange, out _), origin, cameraProvider.CameraTransform.forward);

        return root.forward;
    }

    private static Vector3 AimAt(Vector3 point, Vector3 origin, Vector3 fallback)
    {
        Vector3 to = point - origin;
        return to.sqrMagnitude < 0.0001f ? fallback : to.normalized;
    }

    // =========================================================================
    // Stage 2 — probe
    // =========================================================================

    /// <summary>
    /// Cast along the aim line and return the nearest valid entity, or null.
    /// Only called when AimProbe is ticked and no locked target was supplied.
    ///
    /// The stance is the projectile's, not the aim block's — a healing bolt probes for allies
    /// using the same code a fireball uses to probe for enemies.
    /// </summary>
    public Transform Probe(ControllerBrain source, Vector3 origin, Vector3 direction,
                          LayerMask mask, TargetStance stance)
    {
        if (!UsesProbe) return null;
        if (probeRange <= 0f) return null;
        if (source == null) return null;

        int count = Physics.SphereCastNonAlloc(
            origin, probeRadius, direction, ProbeBuffer, probeRange,
            mask, QueryTriggerInteraction.Collide);

        Transform sourceRoot = source.transform.root;
        Transform best = null;
        float nearest = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = ProbeBuffer[i];

            if (candidate.collider == null) continue;
            if (candidate.distance >= nearest) continue;
            if (candidate.collider.transform.root == sourceRoot) continue;

            ControllerBrain targetBrain = candidate.collider.GetComponentInParent<ControllerBrain>();
            if (targetBrain == null) continue;
            if (!StanceAllows(stance, source, targetBrain)) continue;

            nearest = candidate.distance;
            best = targetBrain.transform.root;
        }

        return best;
    }

    // =========================================================================
    // Stage 3 — final direction
    // =========================================================================

    /// <summary>
    /// The direction the projectile actually launches along. Only TowardTarget bends it,
    /// and only when a target was found.
    /// </summary>
    public Vector3 TargetPoint(Transform target) => target.position + Vector3.up * targetHeightOffset;

    public Vector3 FinalDirection(Vector3 origin, Vector3 baseDirection, Transform target)
    {
        if (directionMode != AimDirectionMode.TowardTarget) return baseDirection;
        if (target == null) return baseDirection;

        Vector3 aimPoint = target.position + Vector3.up * targetHeightOffset;
        Vector3 toTarget = aimPoint - origin;

        if (toTarget.sqrMagnitude < 0.0001f) return baseDirection;

        return toTarget.normalized;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// THE one owner of "may this projectile affect that entity". The probe uses it to pick a
    /// target and ProjectilePayload uses it to decide whether a contact counts, so the two can
    /// no longer disagree — which the pair of booleans this replaced could.
    /// </summary>
    public static bool StanceAllows(TargetStance stance, ControllerBrain source, ControllerBrain target)
    {
        if (stance == TargetStance.Any) return true;

        bool friendly = IsFriendly(source, target);

        return stance == TargetStance.Friendly ? friendly : !friendly;
    }

    /// <summary>
    /// Entities with no FactionSystem are never friendly, matching StrikeHandler.
    /// </summary>
    public static bool IsFriendly(ControllerBrain source, ControllerBrain target)
    {
        if (source == null || target == null) return false;
        if (source.Faction == null || target.Faction == null) return false;

        return source.Faction.GetStanceTo(target) == RPG.Factions.FactionRelationship.Friendly;
    }
}
