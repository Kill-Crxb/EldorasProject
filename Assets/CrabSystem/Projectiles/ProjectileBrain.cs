using UnityEngine;

/// <summary>
/// The root controller of a projectile — a thin coordinator that discovers its modules,
/// drives them, and holds the state everything else reads. Mirrors ControllerBrain one
/// level down.
///
/// Root GameObject is the logic tier, same reasoning as e_Root. The Visual child is the
/// presentation tier, same reasoning as m_Root, and holds no gameplay logic.
///
/// Lifecycle, pooling-aware:
///   Awake     — discover and Initialize modules once per GameObject
///   Launch    — per shot: take data, runtime and context, reset, OnLaunch modules
///   ReportHit — the movement handler calls this for every contact its sweep finds
///   Release   — OnRelease modules, back to the pool
///
/// The prefab is PURELY STRUCTURAL — one Base_Projectile serves every projectile in the game.
/// It carries no data reference: the ability names a ProjectileData, and that names this
/// archetype. Collider size and shape are resized from the data at launch, so a shuriken and
/// a bolt come off the same prefab.
///
/// Prefab setup:
///   Root: trigger Collider (resized at launch), ProjectileBrain, ProjectileMovementHandler,
///   ProjectilePayload. Layer = Projectile.
///   Child "Visual" with ProjectileVisual, assigned below.
/// </summary>
[DisallowMultipleComponent]
public class ProjectileBrain : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ProjectileVisual visual;

    private IProjectileModule[] modules;
    private ProjectilePayload payload;
    private int piercesLeft;

    /// <summary>The shared asset this shot is running. Read-only — never write through it.</summary>
    public ProjectileData Data { get; private set; }

    /// <summary>This shot's numbers, resolved by the launcher. Nothing in flight changes it.</summary>
    public ProjectileRuntime Runtime { get; private set; }

    public ControllerBrain Source { get; private set; }
    public AbilityDefinition SourceAbility { get; private set; }

    /// <summary>
    /// The dice THIS shot rolls, supplied by the launcher. Null falls back to the asset's
    /// weaponData, so an ordinary thrown weapon is unaffected.
    /// </summary>
    public DiceProfile Dice { get; private set; }

    /// <summary>
    /// What decides this shot's meaning on arrival, resolved by the launcher. Null for an
    /// ordinary projectile — the payload then applies its normal damage.
    /// </summary>
    public IProjectileArrivalHandler Arrival { get; private set; }

    /// <summary>Optional homing target supplied at launch.</summary>
    public Transform Target { get; private set; }

    /// <summary>The Visual child. Cosmetic spin goes here, never on the root.</summary>
    public Transform VisualRoot => visual != null ? visual.transform : null;

    /// <summary>Current travel direction, normalised. Movement updates this.</summary>
    public Vector3 Direction { get; private set; }

    /// <summary>
    /// Current velocity. Lives here rather than on ProjectileMovement because that block is
    /// a shared asset and must stay stateless.
    /// </summary>
    public Vector3 Velocity { get; set; }

    public float ElapsedTime { get; private set; }

    /// <summary>False once released. The movement handler checks this to stop mid-sweep.</summary>
    public bool IsAlive { get; private set; }

    private void Awake()
    {
        modules = GetComponents<IProjectileModule>();
        payload = GetComponent<ProjectilePayload>();

        for (int i = 0; i < modules.Length; i++)
            modules[i].Initialize(this);

        if (visual == null)
            visual = GetComponentInChildren<ProjectileVisual>(true);
    }

    private void Update()
    {
        if (!IsAlive) return;

        ElapsedTime += Time.deltaTime;

        FollowSource();
        ApplyLifetimeScale();

        for (int i = 0; i < modules.Length; i++)
        {
            modules[i].UpdateModule();
            if (!IsAlive) return;
        }

        if (ElapsedTime >= Runtime.lifetime)
            Release();
    }

    // ── Launch ────────────────────────────────────────────────────────────

    /// <summary>
    /// Start a shot. Called by ProjectileLauncher straight after pulling this instance from
    /// the pool. Everything per-cast arrives here — nothing is read from the scene.
    /// </summary>
    public void Launch(ProjectileData data, ProjectileRuntime runtime, ProjectileLaunch launch)
    {
        Data = data;
        Runtime = runtime;
        Source = launch.source;
        SourceAbility = launch.ability;
        Target = launch.target;
        Dice = launch.dice != null ? launch.dice : data.weaponData;
        Arrival = launch.arrival;

        Direction = launch.direction.sqrMagnitude > 0.0001f
            ? launch.direction.normalized
            : transform.forward;

        Velocity = Direction * Runtime.speed;
        ElapsedTime = 0f;
        piercesLeft = Runtime.pierceCount;
        IsAlive = true;

        transform.position = launch.origin;
        transform.rotation = Quaternion.LookRotation(Direction, Vector3.up);
        transform.localScale = Vector3.one * Runtime.scale;

        // Runtime, not Data — the tint is per-cast now, so one orb serves every school.
        visual?.Setup(Data, Runtime);

        for (int i = 0; i < modules.Length; i++)
            modules[i].OnLaunch();
    }

    /// <summary>
    /// Rides the caster — position AND facing. What makes a Stream a breath rather than a
    /// cloud: turning your head has to sweep the cone, so the direction is re-read every frame
    /// instead of being fixed at launch.
    ///
    /// Uses the entity root rather than the launch anchor, because the anchor is resolved once
    /// by the launcher and this needs a live transform. Silently does nothing if the caster
    /// died mid-stream, leaving the volume to finish where it stands.
    /// </summary>
    private void FollowSource()
    {
        if (Data == null || !Data.followSource) return;
        if (Source == null) return;

        Transform root = Source.EntityRoot != null ? Source.EntityRoot : Source.transform;

        transform.position = root.position + Vector3.up * Data.spawn.selfHeight;
        transform.rotation = root.rotation;
    }

    /// <summary>
    /// Grows or shrinks the projectile as it travels, from the data's curve.
    ///
    /// On the ROOT, so the collider grows with the visual — a cone that looked wide but hit
    /// narrow would be the worst kind of lie. The collider is a trigger resized at launch and
    /// the sweep uses Runtime values, so scaling the root scales what it touches too.
    ///
    /// Runtime.scale is this cast's size — asset scale x tier x amplifiers — and the curve
    /// multiplies it, so a Grand-tier cone is a bigger cone rather than a differently-shaped one.
    /// </summary>
    private void ApplyLifetimeScale()
    {
        if (Data == null) return;
        if (Runtime.lifetime <= 0f) return;

        float t = Mathf.Clamp01(ElapsedTime / Runtime.lifetime);
        float scale = Runtime.scale * Data.ScaleAt(t);

        transform.localScale = Vector3.one * scale;
    }

    /// <summary>Movement calls this when it steers. Keeps facing in sync with travel.</summary>
    public void SetDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;

        Direction = direction.normalized;
        transform.rotation = Quaternion.LookRotation(Direction, Vector3.up);
    }

    // ── Hits ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the projectile stopped here. The handler uses that to decide whether
    /// to keep sweeping through the collider.
    ///
    /// Geometry always stops it. A target only stops it when the payload's conditions passed —
    /// a filtered target is a pass-through, so a friendly in the way does not eat the shot.
    /// </summary>
    public bool ReportHit(ProjectileHitInfo hit)
    {
        if (!IsAlive) return false;

        if (hit.isEnvironment)
        {
            SpawnImpactVfx(hit.point);
            Release();
            return true;
        }

        if (payload == null || !payload.Resolve(hit))
            return false;

        SpawnImpactVfx(hit.point);

        if (piercesLeft <= 0)
        {
            Release();
            return true;
        }

        piercesLeft--;
        return false;
    }

    private void SpawnImpactVfx(Vector3 point)
    {
        if (Data.impactVfxPrefab == null) return;

        GameObject instance = Instantiate(Data.impactVfxPrefab, point, Quaternion.identity);
        Destroy(instance, Data.impactVfxLifetime);
    }

    // ── Release ───────────────────────────────────────────────────────────

    /// <summary>Return to the pool. Safe to call more than once.</summary>
    public void Release()
    {
        if (!IsAlive) return;

        IsAlive = false;

        for (int i = 0; i < modules.Length; i++)
            modules[i].OnRelease();

        Source = null;
        SourceAbility = null;
        Target = null;
        Dice = null;
        Arrival = null;

        ProjectilePool.Release(gameObject);
    }
}
