using NinjaGame.Animation;
using UnityEngine;

/// <summary>
/// ProjectileLauncher — the bridge between an ability and a projectile. Lives on the
/// CHARACTER brain, not on the projectile.
///
/// Listens to AbilitySystem.OnAbilityAnimationEvent and fires when the event matches the
/// executing ability's effectTrigger. That is the same moment WeaponHitbox would open for a
/// melee swing, so a throw releases exactly on the animation frame you place the event on.
///
/// Auto-discovered: ControllerBrain.CacheModuleArrays walks GetComponentsInChildren
/// &lt;IBrainModule&gt;, so this needs NO serialized field on ControllerBrain and no edit to it.
///
/// Works identically for player and NPC. Q routes through
/// HotbarKeybindSet.QuickslotQ → HotbarSystem.TriggerSlot → AbilitySystem.UseAbility;
/// GOAP routes through AbilitySystem.UseAbility directly. Neither path knows this exists.
///
/// AIM is configured per-projectile in ProjectileData.aim (direction mode + target sources)
/// and executed here, because the character-side lookups — target lock, perception — only
/// exist on this side. Even that needs no IsPlayer check: TargetLock is null on NPCs and
/// PerceptionModule is null on the player, so each falls through to what it actually has.
///
/// SETUP: add this component to the Component_Brain GameObject (or any child of it).
///
/// The ability points at a ProjectileData, which points at its archetype prefab — so one
/// Base_Projectile serves every projectile, and its collider is resized from the data.
///
/// GOTCHA: the ability's effectTrigger MUST be Effect1/2/3. With anything else,
/// AbilitySystem.ExecuteAbility applies effects directly, OnAbilityAnimationEvent never
/// fires, and nothing spawns — silently.
/// </summary>
public class ProjectileLauncher : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    private ControllerBrain brain;
    private AbilitySystem abilitySystem;
    private VFXSystem vfxSystem;
    private PerceptionModule perception;
    private TargetLockModule targetLock;
    // Empty rather than null: Fire loops it unconditionally, and an entity whose LateInitialize
    // bailed should fire an unmodified projectile, not throw.
    private IProjectileLaunchModifier[] modifiers = System.Array.Empty<IProjectileLaunchModifier>();
    private IProjectileArrivalHandler arrival;
    private bool subscribed;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        abilitySystem = brain.GetModule<AbilitySystem>();
        vfxSystem = brain.GetModule<VFXSystem>();

        // Anything on the brain that wants a say in what gets fired. Collected once, in
        // discovery order. Empty on most entities, which costs a zero-length loop per shot.
        modifiers = brain.GetComponentsInChildren<IProjectileLaunchModifier>(true);

        // Resolved here, once, and carried on every launch. An interface can never be in
        // ControllerBrain's provider cache, so GetModule for one is always a hierarchy walk —
        // and the payload would otherwise pay that walk on every single contact.
        arrival = brain.GetComponentInChildren<IProjectileArrivalHandler>(true);

        // PerceptionModule is deliberately NOT cached here. On NPCs it is added at runtime
        // by the archetype config, which can land after LateInitialize — caching now would
        // leave every NPC permanently aimless. Resolved lazily in ResolveLockedTarget instead.

        if (abilitySystem == null)
        {
            isEnabled = false;
            Debug.LogError($"[ProjectileLauncher] No AbilitySystem on {brain.EntityName} — projectiles cannot fire.", this);
            return;
        }

        abilitySystem.OnAbilityAnimationEvent += HandleAnimationEvent;
        subscribed = true;
    }

    public void UpdateModule() { }

    private void OnDestroy()
    {
        if (!subscribed) return;
        if (abilitySystem == null) return;

        abilitySystem.OnAbilityAnimationEvent -= HandleAnimationEvent;
        subscribed = false;
    }

    // ── Trigger ───────────────────────────────────────────────────────────

    private void HandleAnimationEvent(AnimationEventType eventType)
    {
        if (!isEnabled) return;

        AbilityDefinition ability = abilitySystem.CurrentAbility;
        if (ability == null) return;
        if (ability.projectileData == null) return;
        if (eventType != ability.effectTrigger) return;

        Fire(ability);
    }

    // ── Firing ────────────────────────────────────────────────────────────

    private void Fire(AbilityDefinition ability)
    {
        // THE SEAM. The plan starts as an unmodified copy of what the ability named, then
        // every modifier on the brain gets a turn — talents, weapon upgrades, buffs, spells.
        // All of them resolve once, here, into flat values. Nothing inside the projectile
        // changes, and nothing here knows what any of those systems are.
        ProjectileLaunchPlan plan;
        plan.data = ability.projectileData;
        plan.runtime = ProjectileRuntime.FromData(plan.data);
        plan.dice = plan.data.weaponData;
        plan.count = 1;
        plan.spreadDegrees = 0f;

        for (int i = 0; i < modifiers.Length; i++)
            modifiers[i].ModifyLaunch(ability, ref plan);

        // The ability names the DATA; the data names its archetype prefab. That indirection is
        // what lets Shuriken, Kunai and Fireball all come off one Base_Projectile and share
        // one pool — the prefab is structure, the data is the projectile. A modifier may have
        // swapped the data by now, which is how a spell's Form chooses its delivery.
        ProjectileData data = plan.data;

        if (data == null)
        {
            Debug.LogError($"[ProjectileLauncher] '{ability.abilityName}' ended up with no " +
                           $"ProjectileData — a launch modifier cleared it.", this);
            return;
        }

        GameObject prefab = data.archetypePrefab;

        if (prefab == null)
        {
            Debug.LogError($"[ProjectileLauncher] '{data.name}' has no Archetype Prefab assigned — " +
                           $"'{ability.abilityName}' cannot fire.", this);
            return;
        }

        // No-ops after the first call for a given prefab, and when prewarmCount is 0.
        ProjectilePool.Prewarm(prefab, data.prewarmCount);

        // Aim resolves ONCE for the whole volley, after the modifiers, because the probe needs
        // the final stance — a healing bolt has to look for allies — and because every
        // instance of one cast should agree about what it was aimed at.
        ProjectileLaunch launch = ResolveLaunch(data, ability, plan.runtime.targetStance);
        launch.dice = plan.dice;
        launch.arrival = arrival;

        int count = plan.count > 0 ? plan.count : 1;

        for (int i = 0; i < count; i++)
            Spawn(prefab, data, plan, launch, Fan(launch.direction, plan.spreadDegrees, i, count));
    }

    /// <summary>One instance. Everything it needs is already resolved; this only pulls and launches.</summary>
    private void Spawn(GameObject prefab, ProjectileData data, ProjectileLaunchPlan plan,
                       ProjectileLaunch launch, Vector3 direction)
    {
        GameObject instance = ProjectilePool.Get(prefab);
        if (instance == null) return;

        var projectile = instance.GetComponent<ProjectileBrain>();

        if (projectile == null)
        {
            Debug.LogError($"[ProjectileLauncher] Pooled '{prefab.name}' lost its ProjectileBrain.", this);
            return;
        }

        // launch is a struct, so this is this instance's copy — the volley's shared aim with
        // one direction changed.
        launch.direction = direction;

        projectile.Launch(data, plan.runtime, launch);
    }

    /// <summary>
    /// Spreads a volley evenly across the fan, around world up. Two instances sit at the
    /// edges, three put one dead centre. A single projectile, or a spread of zero, is
    /// untouched — which is every non-split shot in the game.
    /// </summary>
    private static Vector3 Fan(Vector3 direction, float spreadDegrees, int index, int count)
    {
        if (count <= 1) return direction;
        if (spreadDegrees <= 0f) return direction;

        float t = index / (float)(count - 1);
        float angle = Mathf.Lerp(-spreadDegrees * 0.5f, spreadDegrees * 0.5f, t);

        return Quaternion.AngleAxis(angle, Vector3.up) * direction;
    }

    // ── Aim ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Where it starts, which way it points, and what it may track.
    ///
    /// The order is not obvious and matters. Two would-be circular dependencies: the aim
    /// probe needs an origin but an AboveTarget origin needs the target, and TowardTarget
    /// needs a target but the probe needs a direction. Both dissolve by probing from the
    /// ANCHOR rather than the eventual spawn point — a probe comes from the caster's chest
    /// regardless of where the projectile appears. Sky-dropped meteors are still aimed by
    /// the person casting them.
    /// </summary>
    private ProjectileLaunch ResolveLaunch(ProjectileData data, AbilityDefinition ability,
                                           TargetStance stance)
    {
        ProjectileSpawn spawn = data.spawn;
        ProjectileAim aim = data.aim;

        Vector3 anchorOrigin = spawn.ResolveAnchor(brain, vfxSystem);
        Vector3 baseDirection = aim.BaseDirection(brain);

        Transform target = aim.UsesLockedTarget ? ResolveLockedTarget() : null;

        if (target == null && aim.UsesProbe)
            target = aim.Probe(brain, anchorOrigin, baseDirection, data.hitMask, stance);

        Vector3 origin = spawn.ResolveOrigin(brain, anchorOrigin, baseDirection, target);

        Vector3 direction = spawn.OverridesDirection
            ? spawn.ResolveDirection(origin, baseDirection, target, anchorOrigin)
            : aim.FinalDirection(origin, baseDirection, target);

        return new ProjectileLaunch
        {
            source = brain,
            ability = ability,
            origin = origin,
            direction = direction,
            target = target,
        };
    }

    /// <summary>
    /// The entity's DELIBERATE target — its explicit choice, as opposed to whatever the aim
    /// probe happens to find. Only consulted when ProjectileAim ticks Locked Target.
    ///
    /// ==== THIS IS THE SEAM FOR REAL TARGETING ====
    /// TargetLockModule is the legacy module and is off by default in ProjectileAim. When a
    /// proper targeting system exists, this ONE METHOD is the only thing that changes — the
    /// aim block, the launcher and every projectile stay as they are.
    ///
    /// No IsPlayer branch needed: TargetLock is null on NPCs and PerceptionModule is null on
    /// the player, so each entity falls through to whatever it actually has.
    /// </summary>
    private Transform ResolveLockedTarget()
    {
        // Resolved through GetModule, not a brain property — ControllerBrain no longer exposes
        // target lock at all. Returns null when the module is absent, which is the common case.
        if (targetLock == null)
            targetLock = brain.GetModule<TargetLockModule>();

        if (targetLock != null && targetLock.IsLockedOn && targetLock.LockedTarget != null)
            return targetLock.LockedTarget;

        if (perception == null)
            perception = brain.GetModule<PerceptionModule>();

        if (perception != null && perception.HasTarget)
            return perception.CurrentTarget;

        return null;
    }
}
