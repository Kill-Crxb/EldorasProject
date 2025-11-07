// Simple Weapon Hit Detection - Enhanced for Natural Weapons (v3.1 - FIXED DamageIn Discovery)
using UnityEngine;
using System.Collections.Generic;

public class SimpleWeaponHit : MonoBehaviour
{
    [Header("Identification")]
    [SerializeField] private string weaponName = "Weapon"; // For debug logging
    [Tooltip("Automatically set by WeaponModule, or manually specify")]
    public WeaponType weaponType = WeaponType.Sword;

    [Header("Settings")]
    [SerializeField] private bool debugHits = true;

    [Header("Hit Feedback")]
    [SerializeField] private float hitStopDuration = 0.05f;
    [SerializeField] private bool enableHitStop = false;

    [Header("Natural Weapon Settings")]
    [Tooltip("If true, this is a natural weapon that stays active between attacks")]
    [SerializeField] private bool isNaturalWeapon = false;
    [Tooltip("Layer mask for valid targets")]
    [SerializeField] private LayerMask targetLayers = ~0;

    // Track what we've hit this swing
    private HashSet<GameObject> hitThisSwing = new HashSet<GameObject>();
    private bool canHit = false;
    private bool isInitialized = false;

    // Component references
    private MeleeModule meleeModule;
    private DamageOut damageOut;
    private ComboModule comboModule;
    private WeaponModule weaponModule;
    private AttackModule attackModule;
    private ControllerBrain brain;
    private Transform attackerTransform;

    // Events - NEW SIGNATURE compatible with DamagePacket
    public System.Action<Collider, CombatDamagePacket> OnHitTargetWithPacket;
    public System.Action<Vector3> OnHitWorld;

    // LEGACY events for WeaponModule compatibility (fires damage amount only)
    public System.Action<Collider, float> OnHitTarget;

    // Properties (NEW)
    public bool CanCurrentlyHit => canHit;
    public bool IsInitialized => isInitialized;
    public string WeaponName => weaponName;

    void Start()
    {
        InitializeComponents();
        ValidateSetup();
        SubscribeToEvents();
        isInitialized = true;
    }

    void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    void InitializeComponents()
    {
        // Ensure we have a trigger collider
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        // Find core components
        meleeModule = FindMeleeModule();
        brain = meleeModule?.Brain;
        attackerTransform = brain?.transform.parent ?? transform.root;

        if (brain != null)
        {
            // Find damage system components
            damageOut = brain.GetComponentInChildren<DamageOut>();
            comboModule = brain.GetComponentInChildren<ComboModule>();
            weaponModule = brain.GetComponentInChildren<WeaponModule>();
            attackModule = brain.GetComponentInChildren<AttackModule>();
        }

        // Try to auto-detect weapon name from GameObject
        if (string.IsNullOrEmpty(weaponName) || weaponName == "Weapon")
        {
            weaponName = gameObject.name.Replace("_Model", "").Replace("(Clone)", "").Trim();
        }
    }

    void ValidateSetup()
    {
        if (meleeModule == null)
        {
            Debug.LogError($"[SimpleWeaponHit:{weaponName}] No MeleeModule found - weapon will not work!");
            return;
        }

        if (damageOut == null)
        {
            Debug.LogWarning($"[SimpleWeaponHit:{weaponName}] No DamageOut found - using fallback damage calculation. " +
                           "Add DamageOut component to Component_Damage for proper damage calculations.");
        }

        if (debugHits)
        {
            Debug.Log($"[SimpleWeaponHit:{weaponName}] Initialized:\n" +
                     $"  MeleeModule: {(meleeModule != null ? "✓" : "✗")}\n" +
                     $"  DamageOut: {(damageOut != null ? "✓" : "✗")}\n" +
                     $"  ComboModule: {(comboModule != null ? "✓" : "✗")}\n" +
                     $"  WeaponModule: {(weaponModule != null ? "✓" : "✗")}\n" +
                     $"  AttackModule: {(attackModule != null ? "✓" : "✗")}\n" +
                     $"  Natural Weapon: {isNaturalWeapon}");
        }
    }

    void SubscribeToEvents()
    {
        if (meleeModule != null)
        {
            meleeModule.OnWeaponEnabled += EnableHitting;
            meleeModule.OnWeaponDisabled += DisableHitting;
        }
    }

    void UnsubscribeFromEvents()
    {
        if (meleeModule != null)
        {
            meleeModule.OnWeaponEnabled -= EnableHitting;
            meleeModule.OnWeaponDisabled -= DisableHitting;
        }
    }

    MeleeModule FindMeleeModule()
    {
        // Try parent hierarchy first
        var melee = GetComponentInParent<MeleeModule>();
        if (melee != null) return melee;

        // Try through WeaponModule (weapon is deep in hierarchy)
        var weapon = GetComponentInParent<WeaponModule>();
        if (weapon != null)
        {
            var controllerBrain = weapon.GetComponentInParent<ControllerBrain>();
            if (controllerBrain != null)
            {
                melee = controllerBrain.GetModule<MeleeModule>();
                if (melee != null) return melee;
            }
        }

        // Last resort: scene-wide search
        melee = FindFirstObjectByType<MeleeModule>();
        return melee;
    }

    #region Hit Detection Enable/Disable

    /// <summary>
    /// Called by MeleeModule when Attack_On animation event fires
    /// </summary>
    void EnableHitting()
    {
        canHit = true;
        hitThisSwing.Clear(); // Fresh start for each attack

        if (debugHits)
            Debug.Log($"[SimpleWeaponHit:{weaponName}] Hit detection ENABLED");
    }

    /// <summary>
    /// Called by MeleeModule when Attack_Off animation event fires
    /// </summary>
    void DisableHitting()
    {
        canHit = false;

        if (debugHits && hitThisSwing.Count > 0)
            Debug.Log($"[SimpleWeaponHit:{weaponName}] Hit detection DISABLED. Hit {hitThisSwing.Count} target(s) this swing.");
    }

    /// <summary>
    /// PUBLIC: Force enable hit detection (called by WeaponModule for natural weapons)
    /// </summary>
    public void ForceEnableHitting()
    {
        EnableHitting();
    }

    /// <summary>
    /// PUBLIC: Force disable hit detection (called by WeaponModule for natural weapons)
    /// </summary>
    public void ForceDisableHitting()
    {
        DisableHitting();
    }

    /// <summary>
    /// PUBLIC: Clear hit tracking without disabling
    /// </summary>
    public void ClearHitTracking()
    {
        hitThisSwing.Clear();

        if (debugHits)
            Debug.Log($"[SimpleWeaponHit:{weaponName}] Cleared hit tracking");
    }

    #endregion

    #region Collision Detection

    // Updated SimpleWeaponHit.cs - OnTriggerEnter method with Coordinator Pattern
    // Replace lines 217-275 in your SimpleWeaponHit.cs with this code

    void OnTriggerEnter(Collider other)
    {
        if (!canHit) return;

        // Check if already hit this target
        if (hitThisSwing.Contains(other.gameObject))
        {
            return;
        }

        // Check layer mask
        if (targetLayers != (targetLayers | (1 << other.gameObject.layer)))
        {
            if (debugHits)
                Debug.Log($"[SimpleWeaponHit:{weaponName}] Ignored {other.name} - not in target layers");
            return;
        }

        if (IsOwnCollider(other))
        {
            if (debugHits)
                Debug.Log($"[SimpleWeaponHit:{weaponName}] Ignored own collider {other.name}");
            return;
        }

        // ===== MODULAR ARCHITECTURE: Use Coordinator Pattern =====
        // Follows: Brain > CombatCoordinator > DamageCoordinator > DamageIn
        // This respects the modular structure regardless of hierarchy depth

        DamageIn targetDamageIn = null;

        // Step 1: Find ControllerBrain on the target
        ControllerBrain targetBrain = other.GetComponentInParent<ControllerBrain>();
        if (targetBrain == null)
        {
            targetBrain = other.GetComponentInChildren<ControllerBrain>();
        }

        // Step 2: Get DamageIn through the coordinator chain
        if (targetBrain != null)
        {
            // Try via CombatCoordinator first (preferred modular path)
            var combatCoordinator = targetBrain.GetModule<CombatCoordinator>();
            if (combatCoordinator != null)
            {
                var damageCoordinator = combatCoordinator.GetDamageCoordinator();
                if (damageCoordinator != null)
                {
                    targetDamageIn = damageCoordinator.GetDamageIn();

                    if (debugHits)
                    {
                        if (targetDamageIn != null)
                        {
                            Debug.Log($"[SimpleWeaponHit:{weaponName}] ✅ Found DamageIn via CombatCoordinator on {other.name}");
                        }
                        else
                        {
                            Debug.Log($"[SimpleWeaponHit:{weaponName}] ⚠️ DamageCoordinator found but no DamageIn component");
                        }
                    }
                }
            }

            // Fallback: Direct DamageCoordinator (if no CombatCoordinator)
            if (targetDamageIn == null)
            {
                var damageCoordinator = targetBrain.GetModule<DamageCoordinator>();
                if (damageCoordinator != null)
                {
                    targetDamageIn = damageCoordinator.GetDamageIn();

                    if (debugHits && targetDamageIn != null)
                    {
                        Debug.Log($"[SimpleWeaponHit:{weaponName}] ✅ Found DamageIn via DamageCoordinator on {other.name}");
                    }
                }
            }

            // Last resort: Direct search in Brain hierarchy (backwards compatibility)
            if (targetDamageIn == null)
            {
                targetDamageIn = targetBrain.GetComponentInChildren<DamageIn>();

                if (debugHits && targetDamageIn != null)
                {
                    Debug.Log($"[SimpleWeaponHit:{weaponName}] ✅ Found DamageIn via direct Brain search on {other.name}");
                }
            }

            if (debugHits && targetDamageIn == null)
            {
                Debug.Log($"[SimpleWeaponHit:{weaponName}] ❌ Brain found but no DamageIn in any coordinator on {other.name}");
            }
        }
        else
        {
            // Legacy fallback for objects without Brain (backwards compatibility)
            targetDamageIn = other.GetComponentInParent<DamageIn>();
            if (targetDamageIn == null)
            {
                targetDamageIn = other.GetComponentInChildren<DamageIn>();
            }

            if (debugHits)
            {
                if (targetDamageIn != null)
                {
                    Debug.Log($"[SimpleWeaponHit:{weaponName}] Found DamageIn (legacy mode) on {other.name}");
                }
                else
                {
                    Debug.Log($"[SimpleWeaponHit:{weaponName}] No Brain or DamageIn found on {other.name} - treating as world object");
                }
            }
        }
        // ===============================================

        if (targetDamageIn != null)
        {
            ProcessTargetHit(other, targetDamageIn);
        }
        else
        {
            ProcessWorldHit(other);
        }

        // Mark as hit
        hitThisSwing.Add(other.gameObject);
    }

    void ProcessTargetHit(Collider targetCollider, DamageIn targetDamageIn)
    {
        // Get hit point and normal
        Vector3 hitPoint = targetCollider.ClosestPoint(transform.position);
        Vector3 hitNormal = (hitPoint - transform.position).normalized;

        // Create attack data
        CombatAttackData attackData = CreateAttackData(hitPoint, hitNormal);

        // Calculate damage packet
        CombatDamagePacket damagePacket = CalculateDamagePacket(attackData);

        if (debugHits)
        {
            Debug.Log($"[SimpleWeaponHit:{weaponName}] Preparing to hit {targetCollider.gameObject.name}\n" +
                     $"  Damage: {damagePacket.finalDamage:F1}\n" +
                     $"  Critical: {damagePacket.isCriticalHit}\n" +
                     $"  Combo: {damagePacket.comboCount}x");
        }

        // Send damage to target
        bool success = targetDamageIn.TakeDamage(damagePacket);

        if (success)
        {
            // Notify listeners with NEW packet-based event
            OnHitTargetWithPacket?.Invoke(targetCollider, damagePacket);

            // ALSO fire LEGACY event for WeaponModule compatibility
            OnHitTarget?.Invoke(targetCollider, damagePacket.finalDamage);

            // Optional: Hit stop effect
            if (enableHitStop && hitStopDuration > 0f)
            {
                Time.timeScale = 0.1f;
                Invoke(nameof(ResetTimeScale), hitStopDuration);
            }

            if (debugHits)
            {
                Debug.Log($"[SimpleWeaponHit:{weaponName}] HIT TARGET: {targetCollider.gameObject.name}\n" +
                         $"  Damage: {damagePacket.finalDamage:F1}\n" +
                         $"  Critical: {damagePacket.isCriticalHit}\n" +
                         $"  Combo: {damagePacket.comboCount}x\n" +
                         $"  Heavy: {damagePacket.isHeavyAttack}");
            }
        }
    }

    void ProcessWorldHit(Collider other)
    {
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        OnHitWorld?.Invoke(hitPoint);

        if (debugHits)
            Debug.Log($"[SimpleWeaponHit:{weaponName}] Hit world object: {other.gameObject.name}");
    }

    #endregion



    #region Damage Calculation

    CombatAttackData CreateAttackData(Vector3 hitPoint, Vector3 hitNormal)
    {
        // Start with basic attack data
        CombatAttackData attackData = CombatAttackData.CreateBasic(attackerTransform, hitPoint);
        attackData.hitNormal = hitNormal;

        // Get combo count from ComboModule
        if (comboModule != null)
        {
            attackData.comboCount = comboModule.CurrentComboCount;
            attackData.comboMultiplier = comboModule.DamageMultiplier;
        }

        // Get weapon data from WeaponModule (using CurrentWeapon property)
        if (weaponModule != null && weaponModule.CurrentWeapon != null)
        {
            var weaponData = weaponModule.CurrentWeapon;
            // WeaponData.damage is the base damage value, convert to multiplier
            // If base damage in system is 10, and weapon has 25, multiplier is 2.5
            attackData.weaponDamageMultiplier = weaponData.damage / 10f; // Normalize to base damage
            attackData.weaponId = weaponData.weaponName;
        }
        else if (!string.IsNullOrEmpty(weaponName))
        {
            // Fallback: Use this weapon's name
            attackData.weaponId = weaponName;
        }

        // Check if this is a heavy attack (using IsChargingHeavyAttack property)
        if (attackModule != null)
        {
            attackData.isHeavyAttack = attackModule.IsChargingHeavyAttack;
        }

        return attackData;
    }

    CombatDamagePacket CalculateDamagePacket(CombatAttackData attackData)
    {
        // Use DamageOut if available (proper system)
        if (damageOut != null)
        {
            return damageOut.CalculateDamage(attackData);
        }

        // Fallback: Create simple damage packet using constructor
        Debug.LogWarning($"[SimpleWeaponHit:{weaponName}] No DamageOut found, using fallback damage calculation!");

        float baseDmg = 10f;
        float finalDmg = baseDmg * attackData.weaponDamageMultiplier * attackData.comboMultiplier;

        // Calculate attack direction from attacker to hit point
        Vector3 attackDirection = Vector3.zero;
        if (attackData.attackerTransform != null)
        {
            attackDirection = (attackData.hitPoint - attackData.attackerTransform.position).normalized;
        }

        // Use constructor to create immutable packet
        CombatDamagePacket packet = new CombatDamagePacket(
            baseDamage: baseDmg,
            finalDamage: finalDmg,
            isCriticalHit: false,
            criticalMultiplier: 1f,
            damageType: DamageType.Physical,
            attacker: attackData.attackerTransform,
            attackerId: attackData.attackerTransform?.name ?? "Unknown",
            hitPoint: attackData.hitPoint,
            hitNormal: attackData.hitNormal,
            attackDirection: attackDirection,
            comboCount: attackData.comboCount,
            isHeavyAttack: attackData.isHeavyAttack,
            weaponId: !string.IsNullOrEmpty(attackData.weaponId) ? attackData.weaponId : weaponName
        );

        return packet;
    }


    #endregion

    #region Utility

    void ResetTimeScale()
    {
        Time.timeScale = 1f;
    }

    #endregion

    #region Debug

    void OnDrawGizmos()
    {
        if (!debugHits) return;

        // Show hitbox bounds
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            Gizmos.color = canHit ? Color.red : Color.gray;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (collider is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (collider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (collider is CapsuleCollider capsule)
            {
                Gizmos.DrawWireSphere(capsule.center, capsule.radius);
            }
        }
    }

    [ContextMenu("Debug: Print Weapon Info")]
    void DebugPrintInfo()
    {
        Debug.Log($"=== SimpleWeaponHit Debug: {weaponName} ===");
        Debug.Log($"Weapon Type: {weaponType}");
        Debug.Log($"Natural Weapon: {isNaturalWeapon}");
        Debug.Log($"Can Hit: {canHit}");
        Debug.Log($"Initialized: {isInitialized}");
        Debug.Log($"Hits This Swing: {hitThisSwing.Count}");
        Debug.Log($"MeleeModule: {(meleeModule != null ? "Found" : "Missing")}");
        Debug.Log($"DamageOut: {(damageOut != null ? "Found" : "Missing")}");
        Debug.Log($"WeaponModule: {(weaponModule != null ? "Found" : "Missing")}");
    }

    [ContextMenu("Debug: Force Enable")]
    void DebugForceEnable()
    {
        ForceEnableHitting();
        Debug.Log($"[SimpleWeaponHit:{weaponName}] Force enabled via context menu");
    }

    [ContextMenu("Debug: Force Disable")]
    void DebugForceDisable()
    {
        ForceDisableHitting();
        Debug.Log($"[SimpleWeaponHit:{weaponName}] Force disabled via context menu");
    }

    [ContextMenu("Debug: Clear Hit Tracking")]
    void DebugClearTracking()
    {
        ClearHitTracking();
        Debug.Log($"[SimpleWeaponHit:{weaponName}] Hit tracking cleared via context menu");
    }

    bool IsOwnCollider(Collider other)
    {
        // Exclude our exact transform
        if (other.transform == transform) return true;

        // Exclude if it's in our weapon hierarchy
        if (other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform))
            return true;

        // FIXED: Only exclude THIS attacker's hierarchy, not all entities with brains
        if (attackerTransform != null && other.transform.IsChildOf(attackerTransform))
            return true;

        return false;
    }

    #endregion
}

// Extension method for debug logging
public static class TransformExtensions
{
    public static string GetFullPath(this Transform transform)
    {
        if (transform.parent == null)
            return transform.name;
        return transform.parent.GetFullPath() + "/" + transform.name;
    }
}