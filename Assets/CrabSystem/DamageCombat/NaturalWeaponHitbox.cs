using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Natural Weapon Hitbox - For animal/creature natural attacks (claws, teeth, etc.)
/// 
/// This component goes on hitbox colliders that are children of bones in the creature's skeleton.
/// Animation events enable/disable the hitbox at the right frames.
/// When enabled and a collision occurs, it applies the associated ability's effects.
/// 
/// Example hierarchy:
/// Bear
/// ??? Armature
///     ??? LeftClaw_Bone
///         ??? LeftClawHitbox (this component + SphereCollider)
/// 
/// Usage:
/// 1. Add SphereCollider (trigger) to bone
/// 2. Add this component
/// 3. Set weapon name and hit layers
/// 4. Initialize with DamageModule and HealthProvider references
/// 5. Animation events call EnableHitbox(abilityId) / DisableHitbox()
/// </summary>
[RequireComponent(typeof(Collider))]
public class NaturalWeaponHitbox : MonoBehaviour
{
    [Header("Hitbox Configuration")]
    [Tooltip("Display name for this weapon (e.g., 'Left Claw', 'Bite')")]
    [SerializeField] private string weaponName = "Natural Weapon";

    [Tooltip("Which layers this hitbox can hit (usually Player, and optionally Enemy for friendly fire)")]
    [SerializeField] private LayerMask hitLayers;

    [Header("Debug")]
    [SerializeField] private bool debugHitbox = false;
    [SerializeField] private bool showGizmos = true;

    // References (set via Initialize or animation events)
    private DamageSystem damageSystem;
    private IHealthProvider healthProvider;
    private ControllerBrain brain;
    private Collider hitboxCollider;

    // State
    private bool isActive = false;
    private string currentAbilityId = null;
    private HashSet<Collider> hitThisSwing = new HashSet<Collider>();

    void Awake()
    {
        hitboxCollider = GetComponent<Collider>();

        if (hitboxCollider == null)
        {
            Debug.LogError($"[NaturalWeaponHitbox] No collider found on {gameObject.name}!");
            enabled = false;
            return;
        }

        // Ensure collider is a trigger
        if (!hitboxCollider.isTrigger)
        {
            Debug.LogWarning($"[NaturalWeaponHitbox] Collider on {gameObject.name} should be a trigger! Setting isTrigger = true");
            hitboxCollider.isTrigger = true;
        }

        // Start disabled
        hitboxCollider.enabled = false;

        // Try to find brain automatically
        brain = GetComponentInParent<ControllerBrain>();
    }

    /// <summary>
    /// Initialize the hitbox with required references.
    /// Call this from NPCModule or archetype setup.
    /// </summary>
    public void Initialize(DamageSystem damage, IHealthProvider health)
    {
        damageSystem = damage;
        healthProvider = health;

        if (debugHitbox)
        {
            Debug.Log($"[NaturalWeaponHitbox] {weaponName} initialized on {transform.root.name}");
        }
    }

    /// <summary>
    /// Called by animation event to enable hitbox for a specific ability.
    /// </summary>
    /// <param name="abilityId">ID of the ability to execute on hit (e.g., "bear_left_claw")</param>
    public void EnableHitbox(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId))
        {
            Debug.LogWarning($"[NaturalWeaponHitbox] EnableHitbox called with empty abilityId on {weaponName}");
            return;
        }

        currentAbilityId = abilityId;
        isActive = true;
        hitboxCollider.enabled = true;
        hitThisSwing.Clear(); // Reset hit tracking for new swing

        if (debugHitbox)
        {
            Debug.Log($"[NaturalWeaponHitbox] {weaponName} enabled for ability: {abilityId}");
        }
    }

    /// <summary>
    /// Called by animation event to disable hitbox at end of attack.
    /// </summary>
    public void DisableHitbox()
    {
        isActive = false;
        hitboxCollider.enabled = false;
        currentAbilityId = null;
        hitThisSwing.Clear();

        if (debugHitbox)
        {
            Debug.Log($"[NaturalWeaponHitbox] {weaponName} disabled");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Guard clauses (flat logic - no nesting!)
        if (!isActive) return;
        if (string.IsNullOrEmpty(currentAbilityId)) return;

        // Check layer
        if (((1 << other.gameObject.layer) & hitLayers) == 0) return;

        // Prevent multiple hits
        if (hitThisSwing.Contains(other)) return;

        // Don't hit self
        if (other.transform.root == transform.root) return;

        // Try to find target's ControllerBrain (new architecture)
        ControllerBrain targetBrain = other.GetComponent<ControllerBrain>();
        if (targetBrain == null)
        {
            // Try parent (colliders on child objects)
            targetBrain = other.GetComponentInParent<ControllerBrain>();
        }

        if (targetBrain == null)
        {
            if (debugHitbox)
                Debug.LogWarning($"[NaturalWeaponHitbox] Hit {other.name} but no ControllerBrain found");
            return;
        }

        // Mark as hit
        hitThisSwing.Add(other);

        // Get ability
        AbilityDefinition ability = GetAbilityById(currentAbilityId);
        if (ability == null)
        {
            Debug.LogError($"[NaturalWeaponHitbox] Ability '{currentAbilityId}' not found!");
            return;
        }

        // Guard clause - check attacker brain
        if (brain == null)
        {
            Debug.LogError($"[NaturalWeaponHitbox] No brain reference! Call Initialize() first.");
            return;
        }

        // Execute ability on target using new architecture
        ability.ExecuteOn(brain, targetBrain);

        if (debugHitbox)
        {
            Debug.Log($"[NaturalWeaponHitbox] {weaponName} hit {other.name} with {ability.abilityName}");
        }
    }

    /// <summary>
    /// Get ability definition from the entity's AbilitySystem.
    /// </summary>
    private AbilityDefinition GetAbilityById(string abilityId)
    {
        if (brain == null)
        {
            Debug.LogError($"[NaturalWeaponHitbox] No ControllerBrain found on {gameObject.name}");
            return null;
        }

        var abilitySystem = brain.Abilities;
        if (abilitySystem == null)
        {
            Debug.LogError($"[NaturalWeaponHitbox] No AbilitySystem found on {brain.name}");
            return null;
        }

        var ability = abilitySystem.GetAbility(abilityId);
        if (ability == null)
        {
            Debug.LogWarning($"[NaturalWeaponHitbox] Ability '{abilityId}' not found in AbilitySystem");
        }

        return ability;
    }

    /// <summary>
    /// Visual debug in Scene view
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Show hitbox bounds
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        // Color based on state
        if (isActive)
            Gizmos.color = Color.red; // Active and dangerous
        else if (Application.isPlaying && !isActive)
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Inactive during play
        else
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Edit mode

        // Draw based on collider type
        if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius * transform.lossyScale.x);
        }
        else if (col is BoxCollider box)
        {
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }
        else if (col is CapsuleCollider capsule)
        {
            // Simplified capsule visualization
            Gizmos.DrawWireSphere(transform.position + capsule.center, capsule.radius * transform.lossyScale.x);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Show additional info when selected
        if (debugHitbox && isActive)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}