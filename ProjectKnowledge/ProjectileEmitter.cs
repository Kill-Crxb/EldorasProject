using UnityEngine;

/// <summary>
/// Sprays decorative projectiles out of a volume while it lives — the visible half of a Stream.
///
/// THE SPLIT THIS EXISTS TO KEEP:
/// A flamethrower built out of real projectiles shotguns. Stand close to a wide breath and you
/// eat six bolts in a frame; stand at the edge and you eat one; the damage depends on how the
/// particles happened to fall. So ProjectileAreaTicker does the damage — once per target per
/// tick, regardless of what is on screen — and this fires the bolts people actually see, with
/// ProjectileData.cosmeticOnly set so not one of them can touch anybody.
///
/// The two halves are tuned independently, which is the point: make the spray denser because it
/// looks better, and the damage does not move.
///
/// A ParticleSystem on the volume's visualPrefab is usually the better tool for a fine spray
/// and costs no code at all. This is for when the bolts want to be real objects — travelling at
/// a readable speed, arcing, bouncing, or big enough that a player tracks them individually.
///
/// SETUP: add to the Base_Projectile archetype. Inert until emitData is assigned.
/// </summary>
[RequireComponent(typeof(ProjectileBrain))]
public class ProjectileEmitter : MonoBehaviour, IProjectileModule
{
    [Header("Spray")]
    [Tooltip("What to fire. MUST have Cosmetic Only ticked, or the volume's damage and the " +
             "spray's damage stack and you are back to shotgunning.")]
    [SerializeField] private ProjectileData emitData;

    [Tooltip("Seconds between bolts. Independent of the volume's damage tick on purpose — this " +
             "is a look, that is a rate.")]
    [SerializeField] private float emitInterval = 0.08f;

    [Tooltip("Bolts released per emission.")]
    [Min(1)]
    [SerializeField] private int emitCount = 1;

    [Tooltip("Random cone the bolts scatter into, in degrees. Set it near the volume's own " +
             "areaAngle so the spray covers what the damage covers — a breath that visibly " +
             "misses the person it is burning reads as a bug.")]
    [SerializeField] private float scatterAngle = 45f;

    private ProjectileBrain brain;
    private float nextEmit;

    public void Initialize(ProjectileBrain projectileBrain)
    {
        brain = projectileBrain;
    }

    public void OnLaunch()
    {
        nextEmit = 0f;
    }

    public void UpdateModule()
    {
        if (emitData == null) return;
        if (emitInterval <= 0f) return;
        if (brain.ElapsedTime < nextEmit) return;

        nextEmit += emitInterval;

        for (int i = 0; i < emitCount; i++)
            Emit();
    }

    public void OnRelease() { }

    private void Emit()
    {
        GameObject prefab = emitData.archetypePrefab;
        if (prefab == null) return;

        ProjectilePool.Prewarm(prefab, emitData.prewarmCount);

        GameObject instance = ProjectilePool.Get(prefab);
        if (instance == null) return;

        var bolt = instance.GetComponent<ProjectileBrain>();
        if (bolt == null) return;

        // Seeded from the emitter's own data, then given the VOLUME's colour, so a tinted
        // Stream sprays bolts in its own school without a second set of assets per element.
        ProjectileRuntime runtime = ProjectileRuntime.FromData(emitData);
        runtime.tint = brain.Runtime.tint;

        var launch = new ProjectileLaunch
        {
            source = brain.Source,
            ability = brain.SourceAbility,
            origin = transform.position,
            direction = Scatter(transform.forward),
            target = null,
        };

        bolt.Launch(emitData, runtime, launch);
    }

    /// <summary>
    /// A direction inside the scatter cone. Uses Random rather than an even fan because a
    /// spray wants to look uncounted — an evenly divided one reads as a mechanism.
    /// </summary>
    private Vector3 Scatter(Vector3 forward)
    {
        if (scatterAngle <= 0f) return forward;

        float half = scatterAngle * 0.5f;

        Quaternion offset = Quaternion.Euler(
            Random.Range(-half, half),
            Random.Range(-half, half),
            0f);

        return offset * forward;
    }
}
