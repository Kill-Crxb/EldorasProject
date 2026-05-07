using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// WeaponHitbox — unified hitbox for all weapon types.
///
/// Replaces: NaturalWeaponHitbox, SimpleAttackHitbox
/// Deletes needed: NaturalWeaponAnimationEvents, NaturalWeaponInitializer
///
/// Works identically whether attached to:
///   - A blade child on an equipped weapon prefab (sword, axe, etc.)
///   - A knuckle bone child for unarmed / fist attacks
///   - Any other strike surface on any entity
///
/// Driven by AbilitySystem via HitboxStart / HitboxEnd animation events.
/// On hit, reads the currently executing ability from the attacker's AbilitySystem
/// and delivers its damage effects to the target — no ability re-execution.
///
/// Setup:
///   1. Add any trigger Collider to the GameObject (Box recommended for blades).
///   2. Add this component.
///   3. Set hitLayers to the layers you want to register hits on.
///   4. AbilitySystem calls Enable() / Disable() when HitboxStart / HitboxEnd fire.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WeaponHitbox : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Display name shown in debug logs (e.g. 'Katana Blade', 'Left Fist')")]
    [SerializeField] private string weaponName = "Weapon";

    [Tooltip("Layers this hitbox registers hits on. Set to Enemy (and Player for friendly fire).")]
    [SerializeField] private LayerMask hitLayers;

    [Header("Debug")]
    [SerializeField] private bool debugHitbox = false;
    [SerializeField] private bool showGizmos = true;

    // ── Runtime references ────────────────────────────────────────────────
    private ControllerBrain brain;
    private AbilitySystem abilitySystem;
    private DamageSystem damageSystem;
    private Collider hitboxCollider;

    // ── State ─────────────────────────────────────────────────────────────
    private bool isActive = false;
    private readonly HashSet<Collider> hitThisSwing = new HashSet<Collider>();

    // ── Properties ────────────────────────────────────────────────────────
    public bool IsActive => isActive;

    // =====================================================================
    // Unity lifecycle
    // =====================================================================

    void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
        }
        // Brain resolution deferred to Enable() — this GameObject may not be
        // parented to the ControllerBrain hierarchy yet when Awake fires
        // (weapon prefabs are instantiated then parented to a socket).
    }

    void Start()
    {
        // Second chance resolve — by Start() the weapon is parented to its socket
        TryResolveBrain();
    }

    private void TryResolveBrain()
    {
        if (brain != null) return;

        brain = GetComponentInParent<ControllerBrain>();
        abilitySystem = brain?.GetModule<AbilitySystem>();
        damageSystem = brain?.GetModule<DamageSystem>();

        if (debugHitbox)
        {
            if (brain != null)
                Debug.Log($"[WeaponHitbox] '{weaponName}' resolved brain: {brain.name}");
            else
                Debug.LogWarning($"[WeaponHitbox] '{weaponName}' could not resolve ControllerBrain — will retry on Enable()");
        }
    }

    // =====================================================================
    // Public API — called by AbilitySystem
    // =====================================================================

    /// <summary>Enable the hitbox for one swing. Clears per-swing hit tracking.</summary>
    public void Enable()
    {
        TryResolveBrain();

        if (brain == null)
        {
            Debug.LogError($"[WeaponHitbox] '{weaponName}' cannot enable — no ControllerBrain found in parent hierarchy.");
            return;
        }

        isActive = true;
        hitThisSwing.Clear();
        hitboxCollider.enabled = true;

        if (debugHitbox)
            Debug.Log($"[WeaponHitbox] '{weaponName}' enabled");
    }

    /// <summary>Disable the hitbox at the end of the swing.</summary>
    public void Disable()
    {
        isActive = false;
        hitboxCollider.enabled = false;
        hitThisSwing.Clear();

        if (debugHitbox)
            Debug.Log($"[WeaponHitbox] '{weaponName}' disabled");
    }

    // =====================================================================
    // Hit detection
    // =====================================================================

    void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;
        if (((1 << other.gameObject.layer) & hitLayers) == 0) return;
        if (other.transform.root == transform.root) return;
        if (hitThisSwing.Contains(other)) return;

        ControllerBrain targetBrain = other.GetComponentInParent<ControllerBrain>()
                                   ?? other.GetComponent<ControllerBrain>();

        if (targetBrain == null)
        {
            TryHitLegacy(other);
            return;
        }

        var targetDamage = targetBrain.GetModule<DamageSystem>();
        if (targetDamage == null) return;

        hitThisSwing.Add(other);

        AbilityDefinition ability = abilitySystem?.CurrentAbility;

        // Notify hit procs — rolls HitProcEntry probability per entry on the source ability
        abilitySystem?.NotifyHitLanded(ability?.abilityId, targetBrain);

        if (ability?.damageEffects != null && ability.damageEffects.Count > 0)
        {
            foreach (var effect in ability.damageEffects)
            {
                effect.SetDamageSystem(damageSystem);
                effect.Apply(targetDamage);
            }

            // Spawn hit VFX at impact point via attacker's VFXSystem
            if (ability.hitEffectPrefab != null)
            {
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                brain.GetModule<VFXSystem>()?.SpawnEffectAt(ability.hitEffectPrefab, hitPoint);
            }

            if (debugHitbox)
                Debug.Log($"[WeaponHitbox] '{weaponName}' hit {other.name} via {ability.damageEffects.Count} effect(s) (ability: {ability.abilityName})");
        }
        else
        {
            // Fallback — ability has no damage effects configured
            var fallback = new CombatAttackData
            {
                baseDamage = 10f,
                damageType = DamageType.Physical,
                attackerTransform = brain.transform,
                hitPoint = other.ClosestPoint(transform.position),
                hitNormal = (other.transform.position - transform.position).normalized,
                comboCount = 0,
                comboMultiplier = 1f,
                weaponDamageMultiplier = 1f,
            };
            CombatDamagePacket packet = damageSystem.CalculateDamage(fallback);
            targetDamage.TakeDamage(packet);

            if (debugHitbox)
                Debug.Log($"[WeaponHitbox] '{weaponName}' hit {other.name} for {packet.finalDamage:F1} (fallback — ability: {ability?.abilityName ?? "none"})");
        }
    }

    private void TryHitLegacy(Collider other)
    {
        var damageable = other.GetComponentInParent<IDamageable>()
                      ?? other.GetComponent<IDamageable>();

        if (damageable == null) return;

        hitThisSwing.Add(other);

        var fallback = new CombatAttackData
        {
            baseDamage = 10f,
            damageType = DamageType.Physical,
            attackerTransform = brain.transform,
            hitPoint = other.ClosestPoint(transform.position),
            hitNormal = (other.transform.position - transform.position).normalized,
            comboCount = 0,
            comboMultiplier = 1f,
            weaponDamageMultiplier = 1f,
        };
        CombatDamagePacket packet = damageSystem.CalculateDamage(fallback);
        damageable.TakeDamage(packet.finalDamage);

        if (debugHitbox)
            Debug.Log($"[WeaponHitbox] '{weaponName}' hit legacy destructible {other.name} for {packet.finalDamage:F1}");
    }

    // =====================================================================
    // Gizmos
    // =====================================================================

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = isActive
            ? Color.red
            : new Color(0f, 1f, 0f, 0.25f);

        if (col is BoxCollider box)
        {
            Matrix4x4 m = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.matrix = m;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(transform.position + sphere.center,
                                  sphere.radius * transform.lossyScale.x);
        }
        else if (col is CapsuleCollider capsule)
        {
            Gizmos.DrawWireSphere(transform.position + capsule.center,
                                  capsule.radius * transform.lossyScale.x);
        }
    }
}