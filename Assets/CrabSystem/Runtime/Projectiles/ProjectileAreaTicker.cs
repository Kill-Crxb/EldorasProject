using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a projectile into a VOLUME: something that sits in the world and applies itself to
/// everything inside it, on a beat, until its lifetime runs out.
///
/// This is the whole of Stream and Field. They are not two systems — they are this one with
/// two settings different:
///
///   Field   areaAngle 360, followSource off   a placed sphere that lingers and ticks
///   Stream  areaAngle  60, followSource on    a cone that rides the caster, a breath
///
/// Inert unless ProjectileData.tickInterval is above 0, so every projectile in the game that
/// resolves on contact is untouched by its presence.
///
/// WHY THE DAMAGE IS HERE AND NOT ON THE BOLTS:
/// The obvious way to build a flamethrower is to fire a lot of small projectiles. Then a wide
/// breath deals its damage once per bolt, someone standing close eats six at once, and the
/// spell's output depends on how many particles happen to intersect them. That is shotgunning,
/// and it is unbalanceable. So the volume does the damage — once per target per tick, no matter
/// how it looks — and the bolts are decoration fired by ProjectileEmitter with
/// ProjectileData.cosmeticOnly set.
///
/// SETUP: add to the Base_Projectile archetype alongside ProjectilePayload. It costs one
/// early-out per frame on projectiles that are not volumes.
/// </summary>
[RequireComponent(typeof(ProjectileBrain))]
public class ProjectileAreaTicker : MonoBehaviour, IProjectileModule
{
    private const int MaxOverlap = 32;

    private static readonly Collider[] OverlapBuffer = new Collider[MaxOverlap];

    private ProjectileBrain brain;
    private ProjectilePayload payload;
    private float nextTick;

    // Entities already resolved THIS tick. A humanoid carries several hurtbox colliders, so an
    // overlap returns the same person more than once and without this a wide target would take
    // one hit per limb inside the volume.
    private readonly HashSet<ControllerBrain> hitThisTick = new HashSet<ControllerBrain>();

    public void Initialize(ProjectileBrain projectileBrain)
    {
        brain = projectileBrain;
        payload = GetComponent<ProjectilePayload>();
    }

    public void OnLaunch()
    {
        // A volume that waits a full interval before its first tick looks broken — the fireball
        // lands, sits there, and only then does something.
        nextTick = brain.Data.tickOnSpawn ? 0f : brain.Data.tickInterval;
    }

    public void UpdateModule()
    {
        ProjectileData data = brain.Data;

        if (data == null) return;
        if (data.tickInterval <= 0f) return;
        if (brain.ElapsedTime < nextTick) return;

        nextTick += data.tickInterval;

        Tick(data);
    }

    public void OnRelease()
    {
        hitThisTick.Clear();
    }

    // ── The beat ──────────────────────────────────────────────────────────

    private void Tick(ProjectileData data)
    {
        if (brain.Source == null) return;
        if (payload == null) return;

        hitThisTick.Clear();

        Vector3 centre = transform.position;

        int count = Physics.OverlapSphereNonAlloc(
            centre, data.areaRadius, OverlapBuffer, data.hitMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider candidate = OverlapBuffer[i];
            if (candidate == null) continue;

            ControllerBrain target = candidate.GetComponentInParent<ControllerBrain>();
            if (target == null) continue;
            if (!hitThisTick.Add(target)) continue;
            if (!InsideCone(data, centre, target)) continue;

            // Everything past this point is the contact path, exactly. The payload owns the
            // stance rule, the arrival handler and the dice — a volume must not grow its own
            // opinion about any of them.
            payload.ResolveOnBrain(target, target.transform.position);
        }

        if (count >= MaxOverlap)
            Debug.LogWarning($"[ProjectileAreaTicker] '{data.name}' filled its {MaxOverlap}-collider " +
                             $"buffer — some targets in the volume were missed. Narrow the radius " +
                             $"or tighten the hit mask.", this);
    }

    /// <summary>
    /// Whether a target is within the wedge. A full 360 volume skips the check entirely, which
    /// is the Field case and the common one.
    ///
    /// Flattened to the horizontal plane on purpose: a breath weapon that stops working because
    /// the target is standing slightly downhill is worse than one that reaches slightly further
    /// than it looks.
    /// </summary>
    private bool InsideCone(ProjectileData data, Vector3 centre, ControllerBrain target)
    {
        if (data.areaAngle >= 360f) return true;

        Vector3 toTarget = target.transform.position - centre;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f) return true;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f) return true;

        return Vector3.Angle(forward, toTarget) <= data.areaAngle * 0.5f;
    }
}
