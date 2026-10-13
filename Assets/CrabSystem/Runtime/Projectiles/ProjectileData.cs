using UnityEngine;

/// <summary>
/// The shared, immutable half of a projectile's configuration.
///
/// One asset per "kind" of projectile — Shuriken, Fireball, Iceball. Several assets can
/// share one archetype prefab: the prefab defines structure (collider shape, which modules
/// are present), the asset defines everything that can vary without changing how the object
/// physically behaves.
///
/// Rule of thumb:
///   Different collider shape or module set  → new archetype prefab
///   Only numbers, visuals or effects differ → same prefab, new ProjectileData
///
/// NEVER mutated at runtime. Per-cast variation lives in ProjectileRuntime, which is a
/// struct copied by value at launch. Nothing in flight can reach this asset.
/// </summary>
[CreateAssetMenu(fileName = "ProjectileData_", menuName = "NinjaGame/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    // =========================================================================
    // Core
    // =========================================================================

    // Core is EXACTLY what ProjectileRuntime copies at launch — the values a
    // modifier can adjust per cast. Everything below it is authoring-time tuning.

    [Header("Core")]
    [Tooltip("Metres per second at launch.")]
    public float speed = 18f;

    [Tooltip("Seconds before the projectile gives up and returns to the pool.")]
    public float lifetime = 5f;

    [Tooltip("Scales every damage effect this projectile applies. Modifiers adjust the runtime copy.")]
    public float damageMultiplier = 1f;

    [Tooltip("Downward acceleration in m/s². 0 = flat flight. Shuriken wants 0, a thrown rock wants ~9.8.")]
    public float gravity = 0f;

    [Tooltip("0 = flies straight, no tracking. Above 0 it steers toward the target the Aim block " +
             "resolved — and if Aim found nothing, this does nothing. Tuning for the turn itself " +
             "is in Movement.")]
    [Range(0f, 1f)]
    public float homingStrength = 0f;

    [Tooltip("Uniform scale applied to the projectile root at launch.")]
    public float scale = 1f;

    [Tooltip("Multiplies scale over the projectile's life, sampled 0..1 across its lifetime.\n\n" +
             "THIS IS WHAT MAKES A CONE. A cone is a slow, wide, high-pierce projectile fired " +
             "from the mouth that grows as it travels — a breath widening. Leave it flat at 1 " +
             "for anything that keeps its size.\n\n" +
             "Empty or missing counts as flat, so every projectile authored before this field " +
             "existed behaves exactly as it did.")]
    public AnimationCurve scaleOverLifetime = AnimationCurve.Constant(0f, 1f, 1f);

    /// <summary>
    /// The scale multiplier at a normalised point in the projectile's life.
    ///
    /// Guarded rather than trusting the curve: an asset serialized before this field existed
    /// deserializes an EMPTY curve, and Evaluate on an empty curve returns 0 — which would
    /// shrink every existing projectile in the game to nothing on its first frame.
    /// </summary>
    public float ScaleAt(float normalisedTime)
    {
        if (scaleOverLifetime == null || scaleOverLifetime.length == 0) return 1f;

        return scaleOverLifetime.Evaluate(normalisedTime);
    }

    [Tooltip("Extra targets this projectile can affect after the first. 0 = stops on the first hit that counts.")]
    public int pierceCount = 0;

    [Tooltip("Who this projectile is for. Anything it should not affect is a PASS-THROUGH, not " +
             "an impact — an ally in a doorway does not eat a fireball, and a healing bolt does " +
             "not stop on an enemy.\n\n" +
             "In Core because it is runtime-modifiable: a composed spell sets it from its Effect, " +
             "so the same projectile serves damage and healing.")]
    public TargetStance targetStance = TargetStance.Hostile;

    // =========================================================================
    // Archetype
    // =========================================================================

    [Header("Archetype")]
    [Tooltip("The prefab this projectile is built from — normally Base_Projectile. It is purely " +
             "structural: the components and the collider, nothing else. Several ProjectileData " +
             "assets share one archetype and therefore share one pool.")]
    public GameObject archetypePrefab;

    // =========================================================================
    // Behaviour blocks
    // =========================================================================

    [Header("Spawn")]
    [Tooltip("Where the projectile comes into the world — a brain anchor, a model socket, or overhead.")]
    public ProjectileSpawn spawn = new ProjectileSpawn();

    [Header("Aim")]
    [Tooltip("Where the launch direction comes from, and which sources may supply a trackable target.")]
    public ProjectileAim aim = new ProjectileAim();

    [Header("Movement")]
    [Tooltip("Travel vs hitscan, spin, and homing tuning — one block of toggles.")]
    public ProjectileMovement movement = new ProjectileMovement();

    // =========================================================================
    // Volume — Stream and Field
    // =========================================================================

    // A Stream and a Field are the same object with two settings different, which is why
    // neither got its own system: a volume that sits somewhere and ticks everything inside it.
    // A Field is placed and stays; a Stream is a cone that rides the caster. Both are just a
    // projectile with speed 0 that resolves repeatedly instead of once.

    [Header("Volume")]
    [Tooltip("Seconds between area resolves. 0 = a normal projectile that resolves ONCE on " +
             "contact — every existing projectile in the game leaves this at 0 and is unaffected.\n\n" +
             "Above 0 turns this into a volume: it stops caring about contact and instead applies " +
             "itself to everything inside its area, every interval, until its lifetime runs out.")]
    public float tickInterval = 0f;

    [Tooltip("Radius of the area resolve. Only used when Tick Interval is above 0.")]
    public float areaRadius = 4f;

    [Tooltip("Full angle of the volume's cone, in degrees, measured about its forward axis.\n\n" +
             "360 is a sphere — that is a Field. Anything less is a wedge in front of the " +
             "origin — that is a Stream, or a breath. Height is NOT considered, so a cone is a " +
             "wedge rather than an ice-cream cone; that is usually what reads correctly in play.")]
    [Range(1f, 360f)]
    public float areaAngle = 360f;

    [Tooltip("The volume rides the caster instead of staying where it was cast.\n\n" +
             "ON for a Stream — a breath has to follow the mouth it comes out of, and turning " +
             "your head has to sweep it. OFF for a Field, which is placed and abandoned.")]
    public bool followSource = false;

    [Tooltip("Ticks whatever is already inside the volume the instant it appears, rather than " +
             "waiting one full interval. Almost always wanted — the alternative is a fireball " +
             "that visibly lands and then does nothing for a second.")]
    public bool tickOnSpawn = true;

    [Tooltip("Purely decorative — carries no payload, deals no damage, and passes through " +
             "everything.\n\n" +
             "This is what lets a Stream spit visible bolts without every one of them hitting: " +
             "the VOLUME does the damage once per tick, and these are the spray. Without the " +
             "split you get shotgunning, where a wide breath deals its damage once per bolt.")]
    public bool cosmeticOnly = false;

    // =========================================================================
    // Collision
    // =========================================================================

    [Header("Collision")]
    [Tooltip("Layers the sweep tests against. Include Hurtbox for entities, plus whatever layer your " +
             "walls and floors use so the projectile stops on geometry.")]
    public LayerMask hitMask;

    [Tooltip("Sweep radius, and the radius of the physical collider. This is the whole hit size — " +
             "a shuriken wants ~0.15, a fireball ~0.4.")]
    public float collisionRadius = 0.15f;

    [Tooltip("Extra length along the direction of travel, for a bolt or arrow rather than a ball. " +
             "0 makes the collider a sphere.\n\n" +
             "This shapes the physical collider only — hit detection is unaffected, because a body " +
             "travelling along its own long axis sweeps the same volume either way.")]
    public float collisionLength = 0f;

    [Tooltip("Stop on walls, floors and scenery. Untick only for something that should fly " +
             "through the world.")]
    public bool hitEnvironment = true;

    // =========================================================================
    // Damage — where the numbers come from, in the order they are consulted
    // =========================================================================

    [Header("Damage")]
    [Tooltip("Dice for this projectile, used when the firing ability's DamageEffect has " +
             "Use Weapon Damage ticked. A thrown weapon IS the weapon — without this a shuriken " +
             "would roll whatever is in the main-hand slot, or the fist dice when unarmed.\n\n" +
             "Leave empty for a spell, and the ability's flat baseDamage applies as usual.")]
    public DiceProfile weaponData;

    [Tooltip("Used only when the firing ability has no DamageEffects AND this projectile has no " +
             "DiceProfile. Normal projectiles never reach this — damage comes from the ability.")]
    public float fallbackDamage = 10f;

    [Tooltip("Damage type for the fallback path, and for hits on legacy destructibles.")]
    public DamageType fallbackDamageType = DamageType.Physical;

    // =========================================================================
    // Presentation
    // =========================================================================

    [Header("Presentation")]
    [Tooltip("Spawned under the archetype's Visual child at launch. This is what makes one orb " +
             "archetype serve Fireball, Iceball and Poison Bolt.")]
    public GameObject visualPrefab;

    [Tooltip("Applied to the visual via MaterialPropertyBlock — never renderer.material, which " +
             "instances a material per object and leaks under pooling.")]
    public Color tint = Color.white;

    [Tooltip("Shader colour property to tint. URP Lit uses _BaseColor; Built-in Standard uses _Color.")]
    public string tintProperty = "_BaseColor";

    [Tooltip("Spawned at the point of contact. The firing ability's own hitEffectPrefab fires " +
             "too — this one is the projectile's own impact (dust, shatter).")]
    public GameObject impactVfxPrefab;

    [Tooltip("Seconds before the impact VFX instance is destroyed.")]
    public float impactVfxLifetime = 3f;

    // =========================================================================
    // Pooling
    // =========================================================================

    [Header("Pooling")]
    [Tooltip("Instances created up front the first time this projectile is fired.")]
    public int prewarmCount = 8;

    // =========================================================================
    // Validation
    // =========================================================================

    private void OnValidate()
    {
        if (speed < 0f) speed = 0f;
        if (lifetime <= 0f) lifetime = 0.1f;
        if (scale <= 0f) scale = 1f;
        if (pierceCount < 0) pierceCount = 0;
        if (collisionRadius <= 0f) collisionRadius = 0.05f;
        if (collisionLength < 0f) collisionLength = 0f;
        if (prewarmCount < 0) prewarmCount = 0;
        if (string.IsNullOrEmpty(tintProperty)) tintProperty = "_BaseColor";
    }
}
