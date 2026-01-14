using UnityEngine;

/// <summary>
/// Simple Attack Hitbox - For Phase 1.1 testing
/// 
/// Press SPACE to activate hitbox for 0.2 seconds
/// Hits anything in front of player
/// Uses DamageModule with equipped weapon damage
/// 
/// TEMPORARY: Phase 1.3 will use proper animation-driven hitboxes
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class SimpleAttackHitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    [SerializeField] private float hitboxRadius = 1.5f;
    [SerializeField] private float hitboxDistance = 1f; // Distance in front of player
    [SerializeField] private float hitboxDuration = 0.2f;

    [Header("Damage")]
    [SerializeField] private float fallbackDamage = 10f; // If no weapon equipped

    [Header("Debug")]
    [SerializeField] private bool debugHitbox = true;
    [SerializeField] private bool showGizmos = true;

    // References
    private ControllerBrain brain;
    private DamageSystem damageSystem;
    private InputSystem inputSystem;
    private SphereCollider hitboxCollider;

    // State
    private bool isActive;
    private float activationTime;
    private System.Collections.Generic.HashSet<Collider> hitThisSwing = new System.Collections.Generic.HashSet<Collider>();

    void Start()
    {
        brain = GetComponentInParent<ControllerBrain>();

        if (brain == null)
        {
            Debug.LogError("[SimpleAttackHitbox] No ControllerBrain found in parent!");
            enabled = false;
            return;
        }

        damageSystem = brain.GetModule<DamageSystem>();
        inputSystem = brain.GetModule<InputSystem>();

        if (damageSystem == null)
        {
            Debug.LogError("[SimpleAttackHitbox] No DamageModule found!");
            enabled = false;
            return;
        }

        if (inputSystem == null)
        {
            Debug.LogError("[SimpleAttackHitbox] No InputSystem found!");
            enabled = false;
            return;
        }

        // Setup collider
        hitboxCollider = GetComponent<SphereCollider>();
        hitboxCollider.isTrigger = true;
        hitboxCollider.radius = hitboxRadius;
        hitboxCollider.enabled = false; // Start disabled

        // Position in front of player
        transform.localPosition = Vector3.forward * hitboxDistance;

        if (debugHitbox)
        {
            Debug.Log("[SimpleAttackHitbox] Initialized - Press 1 (Hotbar1) to attack");
        }
    }

    void Update()
    {
        // Check for attack input (Hotbar1 = key "1")
        if (inputSystem != null && inputSystem.Hotbar1Pressed && !isActive)
        {
            Attack();
        }

        // Deactivate after duration
        if (isActive && Time.time - activationTime > hitboxDuration)
        {
            DeactivateHitbox();
        }
    }

    void Attack()
    {
        isActive = true;
        activationTime = Time.time;
        hitboxCollider.enabled = true;
        hitThisSwing.Clear();

        if (debugHitbox)
        {
            Debug.Log("[SimpleAttackHitbox] Attack!");
        }
    }

    void DeactivateHitbox()
    {
        isActive = false;
        hitboxCollider.enabled = false;

        if (debugHitbox && hitThisSwing.Count == 0)
        {
            Debug.Log("[SimpleAttackHitbox] Swing missed (hit nothing)");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isActive) return;

        // Don't hit self
        if (other.transform.root == transform.root) return;

        // Don't hit same target twice in one swing
        if (hitThisSwing.Contains(other)) return;

        // Try to damage
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null)
        {
            damageable = other.GetComponentInParent<IDamageable>();
        }

        if (damageable != null)
        {
            hitThisSwing.Add(other);

            // Create attack data
            CombatAttackData attackData = new CombatAttackData
            {
                baseDamage = fallbackDamage,
                attackerTransform = brain.transform,
                hitPoint = other.ClosestPoint(transform.position),
                hitNormal = (other.transform.position - transform.position).normalized,
                damageType = DamageType.Physical,
                comboCount = 0,
                comboMultiplier = 1f,
                weaponDamageMultiplier = 1f
            };

            // Calculate damage (this uses equipped weapon via EquippedWeaponBridge!)
            CombatDamagePacket packet = damageSystem.CalculateDamage(attackData);

            // Apply damage
            damageable.TakeDamage(packet.finalDamage);

            if (debugHitbox)
            {
                Debug.Log($"[SimpleAttackHitbox] Hit {other.name} for {packet.finalDamage:F1} damage!");
            }
        }
        else if (debugHitbox)
        {
            Debug.Log($"[SimpleAttackHitbox] Hit {other.name} but it's not damageable");
        }
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Show hitbox position
        Vector3 worldPos = transform.position;

        if (isActive)
        {
            Gizmos.color = Color.red; // Active = red
        }
        else
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Inactive = orange transparent
        }

        Gizmos.DrawWireSphere(worldPos, hitboxRadius);

        // Show forward direction
        if (transform.parent != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.parent.position, worldPos);
        }
    }

    #region Debug Helpers

    [ContextMenu("Test Attack")]
    public void TestAttack()
    {
        Attack();
    }

    #endregion
}