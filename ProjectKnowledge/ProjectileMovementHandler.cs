using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves the projectile and finds what it crosses.
///
/// Travel comes from ProjectileData.movement — this handler holds no movement logic of its
/// own. Collision is a SPHERECAST FROM LAST POSITION TO NEW POSITION, not a trigger, and
/// that distinction is the reason this class exists: a shuriken at 25 m/s covers 0.4m in a
/// frame, so a trigger that only tests where the object IS is a coin flip against a thin
/// hurtbox, and worse at low framerate. A sweep tests the whole path.
///
/// It also makes hitscan nearly free — one very long sweep resolved on the first frame.
///
/// The root Collider is never a detector — this class has no OnTriggerEnter. It is RESIZED
/// FROM THE DATA at launch and left enabled purely so a weapon hitbox can find the projectile,
/// which is the Projectile ↔ Hitbox parry interaction LayerConfigFactory already reserves.
/// Sweep size comes from ProjectileData.collisionRadius, not from the collider.
/// </summary>
[RequireComponent(typeof(ProjectileBrain))]
public class ProjectileMovementHandler : MonoBehaviour, IProjectileModule
{
    /// <summary>Contacts resolved per frame. Guards against a pass-through loop stalling.</summary>
    private const int MaxSweepSteps = 4;

    private const float MinStepDistance = 0.0001f;

    /// <summary>Shared cast buffer — SphereCastNonAlloc keeps the sweep allocation-free.</summary>
    private static readonly RaycastHit[] CastBuffer = new RaycastHit[32];

    private ProjectileBrain brain;
    private Collider shape;
    private readonly HashSet<Collider> ignored = new HashSet<Collider>();

    private bool hitscanResolved;
    private float lingerTimer;

    public void Initialize(ProjectileBrain projectileBrain)
    {
        brain = projectileBrain;
        shape = GetComponent<Collider>();

        // Trigger, not a solid — the sweep does our detection, so this must never push
        // anything. Left enabled so a weapon hitbox can still find us.
        if (shape != null)
            shape.isTrigger = true;
        else
            Debug.LogWarning($"[ProjectileMovementHandler] '{name}' has no Collider — a weapon " +
                             "hitbox will not be able to find this projectile to parry it.", this);
    }

    public void OnLaunch()
    {
        ignored.Clear();
        IgnoreSourceColliders();
        ResizeCollider();

        hitscanResolved = false;
        lingerTimer = 0f;
    }

    public void UpdateModule()
    {
        ProjectileMovement movement = brain.Data.movement;

        if (movement.mode == ProjectileMode.Hitscan)
        {
            UpdateHitscan(movement);
            return;
        }

        SweepTo(movement.Step(brain, transform.position, Time.deltaTime));
    }

    public void OnRelease()
    {
        ignored.Clear();
    }

    // ── Hitscan ───────────────────────────────────────────────────────────

    /// <summary>
    /// Not a separate code path — one very long sweep resolved on the first frame instead of
    /// a little at a time, so conditions, pass-through, pierce and payload all behave as they
    /// do for a travelling shot. The linger exists only to give a trail time to render.
    /// </summary>
    private void UpdateHitscan(ProjectileMovement movement)
    {
        if (!hitscanResolved)
        {
            hitscanResolved = true;

            SweepTo(transform.position + brain.Direction * movement.hitscanRange);

            if (!brain.IsAlive) return;
        }

        lingerTimer += Time.deltaTime;

        if (lingerTimer >= movement.hitscanLingerTime)
            brain.Release();
    }

    // ── Sweep ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Walk to <paramref name="target"/>, stopping at each contact to let the brain decide.
    /// A contact the brain does not count is added to the ignore set and the sweep continues,
    /// so a filtered target does not swallow the shot.
    /// </summary>
    private void SweepTo(Vector3 target)
    {
        Vector3 from = transform.position;

        for (int step = 0; step < MaxSweepSteps; step++)
        {
            Vector3 delta = target - from;
            float distance = delta.magnitude;

            if (distance < MinStepDistance) break;

            Vector3 direction = delta / distance;

            if (!TryFindHit(from, direction, distance, out RaycastHit hit)) break;

            from = hit.point;
            transform.position = from;

            bool stopped = brain.ReportHit(BuildHitInfo(hit, direction));

            if (!brain.IsAlive || stopped) return;

            ignored.Add(hit.collider);
        }

        transform.position = target;
    }

    /// <summary>Nearest collider along the path that is not already ignored.</summary>
    private bool TryFindHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit result)
    {
        result = default;

        int count = Physics.SphereCastNonAlloc(
            origin, brain.Data.collisionRadius, direction, CastBuffer, distance,
            brain.Data.hitMask, QueryTriggerInteraction.Collide);

        float nearest = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = CastBuffer[i];

            if (candidate.collider == null) continue;
            if (candidate.distance >= nearest) continue;
            if (ignored.Contains(candidate.collider)) continue;

            nearest = candidate.distance;
            result = candidate;
            found = true;
        }

        return found;
    }

    private ProjectileHitInfo BuildHitInfo(RaycastHit hit, Vector3 direction)
    {
        ControllerBrain targetBrain = hit.collider.GetComponentInParent<ControllerBrain>();

        IDamageable legacy = targetBrain == null
            ? hit.collider.GetComponentInParent<IDamageable>()
            : null;

        return new ProjectileHitInfo
        {
            // A zero-distance spherecast returns a zero point and normal — fall back to the
            // collider surface so impact VFX never spawn at world origin.
            point = hit.point.sqrMagnitude > 0f ? hit.point : hit.collider.ClosestPoint(transform.position),
            normal = hit.normal.sqrMagnitude > 0f ? hit.normal : -direction,
            travelDirection = direction,
            collider = hit.collider,
            targetBrain = targetBrain,
            legacyTarget = legacy,
            isEnvironment = targetBrain == null && legacy == null,
        };
    }

    // ── Setup ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The thrower's own hurtbox is on a layer we hit and starts inside the sweep radius.
    /// Ignore everything under the source root for the whole flight.
    /// </summary>
    private void IgnoreSourceColliders()
    {
        if (brain.Source == null) return;

        var sourceColliders = brain.Source.transform.root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < sourceColliders.Length; i++)
            ignored.Add(sourceColliders[i]);
    }

    /// <summary>
    /// Shapes the physical collider from the data, so ONE archetype prefab serves every
    /// projectile in the game — a shuriken and a bolt come off the same Base_Projectile.
    ///
    /// A CapsuleCollider covers both: at collisionLength 0 its height equals its diameter and
    /// it IS a sphere; above 0 it stretches along Z, which is the direction of travel.
    ///
    /// This affects the physical collider only. Hit detection uses collisionRadius directly,
    /// because a body travelling along its own long axis sweeps the same volume either way.
    /// </summary>
    private void ResizeCollider()
    {
        if (shape == null) return;

        ProjectileData data = brain.Data;

        if (shape is CapsuleCollider capsule)
        {
            capsule.direction = 2;
            capsule.center = Vector3.zero;
            capsule.radius = data.collisionRadius;
            capsule.height = data.collisionLength + data.collisionRadius * 2f;
            return;
        }

        if (shape is SphereCollider sphere)
        {
            sphere.center = Vector3.zero;
            sphere.radius = data.collisionRadius;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (brain == null || brain.Data == null) return;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, brain.Data.collisionRadius);
    }
}
