using UnityEngine;

/// <summary>
/// Shared plain types for the projectile system — the module contract and the three
/// structs everything passes around.
///
/// The split that matters:
///   ProjectileData    — the shared asset. Never written at runtime.
///   ProjectileRuntime — this cast's numbers. Modifiers write here, nowhere else.
///   ProjectileLaunch  — this cast's context. Who fired it, from where, at what.
/// </summary>

/// <summary>
/// Who a projectile is for. Replaces the two booleans this used to be — ProjectileData's
/// hitFriendly and ProjectileAim's probeIgnoresFriendly — which expressed one concept in two
/// places and could contradict each other (acquire only enemies, then damage an ally you
/// flew into).
///
/// Lives in ProjectileRuntime, so a spell composed at cast time can set it. A healing bolt
/// and a fireball are the same projectile with a different stance.
/// </summary>
public enum TargetStance
{
    /// <summary>Damage, bind, displace. Friendlies are a pass-through.</summary>
    Hostile,

    /// <summary>Restore, ward, empower. Hostiles are a pass-through.</summary>
    Friendly,

    /// <summary>Hits whatever it touches.</summary>
    Any,
}

/// <summary>
/// A component on the projectile root that ProjectileBrain drives. Mirrors IBrainModule,
/// with a pooling-aware lifecycle: Initialize runs once per GameObject, OnLaunch and
/// OnRelease run once per shot.
/// </summary>
public interface IProjectileModule
{
    /// <summary>Once per GameObject, in Awake. Cache references here — never per shot.</summary>
    void Initialize(ProjectileBrain brain);

    /// <summary>Every frame while in flight.</summary>
    void UpdateModule();

    /// <summary>Per shot, after the brain has its data. Reset state here. Optional.</summary>
    void OnLaunch() { }

    /// <summary>Per shot, on return to the pool. Optional.</summary>
    void OnRelease() { }
}

/// <summary>
/// The mutable half of a projectile's configuration — one copy per shot, seeded from
/// ProjectileData and then adjusted by whatever modifiers apply.
///
/// A struct on purpose: copied by value into the brain, so nothing in flight can reach
/// back and touch the shared asset. No Instantiate on the ScriptableObject, no GC per shot.
///
/// Each field is ignored where it does not apply — homingStrength is inert without an
/// acquired target, gravity is ignored while steering.
///
/// Not all numbers: targetStance is here because a spell decides who its projectile is for
/// at cast time, which is exactly what this struct is for.
/// </summary>
[System.Serializable]
public struct ProjectileRuntime
{
    public float speed;
    public float lifetime;
    public float damageMultiplier;
    public float homingStrength;
    public float gravity;
    public float scale;
    public int pierceCount;
    public TargetStance targetStance;

    /// <summary>
    /// The visual tint. Here rather than on ProjectileData for the same reason targetStance
    /// is: a composed spell picks its element at cast time, so one orb serves all six schools.
    /// The asset value is the default, so an ordinary projectile behaves exactly as before.
    /// </summary>
    public Color tint;

    public static ProjectileRuntime FromData(ProjectileData data)
    {
        return new ProjectileRuntime
        {
            speed = data.speed,
            lifetime = data.lifetime,
            damageMultiplier = data.damageMultiplier,
            homingStrength = data.homingStrength,
            gravity = data.gravity,
            scale = data.scale,
            pierceCount = data.pierceCount,
            targetStance = data.targetStance,
            tint = data.tint,
        };
    }
}

/// <summary>Per-cast context, kept separate from ProjectileRuntime so the numbers stay numbers.</summary>
public struct ProjectileLaunch
{
    /// <summary>The attacker's brain. Faction, DamageSystem, VFXSystem and AbilitySystem hang off this.</summary>
    public ControllerBrain source;

    /// <summary>Supplies damageEffects, hitEffectPrefab and hit procs.</summary>
    public AbilityDefinition ability;

    /// <summary>
    /// The dice THIS shot rolls, handed to DamageEffect as weaponOverride. Defaults to
    /// ProjectileData.weaponData — a thrown weapon is the weapon — and a spell replaces it
    /// with its tier's dice.
    ///
    /// Per-cast CONTEXT, so it lives here rather than in ProjectileRuntime, which keeps the
    /// numbers numbers. It sits next to `ability` because both are shared assets this
    /// particular shot happens to be running.
    /// </summary>
    public WeaponData dice;

    /// <summary>
    /// Who decides what this shot MEANS on arrival — heal, bind, plain damage. Null for an
    /// ordinary projectile, which is almost all of them.
    ///
    /// Resolved once by the launcher rather than looked up per hit. It hangs off the firing
    /// brain, so it cannot change between launch and impact, and an interface lookup is the one
    /// kind ControllerBrain's provider cache can never answer — every call is a full hierarchy
    /// walk. A pierce-5 shot was paying five of them.
    /// </summary>
    public IProjectileArrivalHandler arrival;

    public Vector3 origin;
    public Vector3 direction;

    /// <summary>Optional homing target. Null for a plain straight throw.</summary>
    public Transform target;
}

/// <summary>What the movement sweep found. Built per contact and handed to the payload.</summary>
public struct ProjectileHitInfo
{
    public Vector3 point;
    public Vector3 normal;
    public Vector3 travelDirection;
    public Collider collider;

    /// <summary>Set when the thing hit is a Brain entity. Null for props and geometry.</summary>
    public ControllerBrain targetBrain;

    /// <summary>Set when the thing hit is a legacy IDamageable — TargetDummy, breakables.</summary>
    public IDamageable legacyTarget;

    /// <summary>Nothing damageable was found — a wall, the ground, scenery.</summary>
    public bool isEnvironment;
}
