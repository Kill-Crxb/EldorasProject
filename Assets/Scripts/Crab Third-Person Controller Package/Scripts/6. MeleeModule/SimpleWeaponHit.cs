// Simple Weapon Hit Detection - Updated for Animation Event System
using UnityEngine;
using System.Collections.Generic;

public class SimpleWeaponHit : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private bool debugHits = true;

    // Track what we've hit this swing
    private HashSet<GameObject> hitThisSwing = new HashSet<GameObject>();
    private bool canHit = false;

    // Events - match WeaponHitbox interface
    public System.Action<Collider, float> OnHitTarget;
    public System.Action<Vector3> OnHitWorld;

    void Start()
    {
        // Ensure we have a trigger collider
        var collider = GetComponent<Collider>();

        if (collider != null)
        {
            collider.isTrigger = true;
        }

        // Subscribe to MeleeModule animation events instead of attack begin/end
        var meleeModule = FindMeleeModule();
        if (meleeModule != null)
        {
            // Subscribe to the new animation event forwarding
            meleeModule.OnWeaponEnabled += EnableHitting;
            meleeModule.OnWeaponDisabled += DisableHitting;
        }
        else
        {
            Debug.LogError("SimpleWeaponHit: No MeleeModule found - weapon will not work!");
        }
    }

    MeleeModule FindMeleeModule()
    {
        // Try parent hierarchy first
        var meleeModule = GetComponentInParent<MeleeModule>();
        if (meleeModule != null) return meleeModule;

        // Try through WeaponModule (weapon is deep in hierarchy)
        var weaponModule = GetComponentInParent<WeaponModule>();
        if (weaponModule != null)
        {
            var brain = weaponModule.GetComponentInParent<ControllerBrain>();
            if (brain != null)
            {
                meleeModule = brain.GetModule<MeleeModule>();
                if (meleeModule != null) return meleeModule;
            }
        }

        // Last resort: scene-wide search
        meleeModule = FindFirstObjectByType<MeleeModule>();

        return meleeModule;
    }

    /// <summary>
    /// Called by MeleeModule when Attack_On animation event fires
    /// </summary>
    void EnableHitting()
    {
        canHit = true;
        hitThisSwing.Clear(); // Fresh start for each attack
    }

    /// <summary>
    /// Called by MeleeModule when Attack_Off animation event fires
    /// </summary>
    void DisableHitting()
    {
        canHit = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canHit)
        {
            return;
        }

        // Don't hit the same target twice per swing
        if (hitThisSwing.Contains(other.gameObject))
        {
            return;
        }

        // Don't hit ourselves or our weapon
        if (IsOwnCollider(other))
        {
            return;
        }

        // Try to damage the target
        var damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            // Mark as hit to prevent double-hits
            hitThisSwing.Add(other.gameObject);

            // Calculate actual damage (could integrate with combat stats here)
            float actualDamage = CalculateDamage();

            // Apply damage
            bool survived = damageable.TakeDamage(actualDamage);

            // Notify listeners
            OnHitTarget?.Invoke(other, actualDamage);
        }
        else
        {
            // Hit world geometry - no damage but still notify
            OnHitWorld?.Invoke(other.transform.position);
        }
    }

    /// <summary>
    /// Calculate damage based on weapon stats and combat modifiers
    /// </summary>
    float CalculateDamage()
    {
        float actualDamage = damage;

        // Try to get damage from MeleeModule/AttackModule for more accurate calculation
        var meleeModule = FindMeleeModule();
        if (meleeModule != null && meleeModule.Attack != null)
        {
            // Use the combat system's damage calculation
            actualDamage = meleeModule.CalculateDamage(false, false); // Light attack, no crit for now
        }

        return actualDamage;
    }

    /// <summary>
    /// Check if this collider belongs to the player or weapon
    /// </summary>
    bool IsOwnCollider(Collider other)
    {
        // Exclude our exact transform
        if (other.transform == transform)
        {
            return true;
        }

        // Exclude anything with player/weapon identifiers
        string name = other.name.ToLower();
        if (name.Contains("katana") ||
            name.Contains("player") ||
            name.Contains("weapon") ||
            name.Contains("character"))
        {
            return true;
        }

        // Exclude if it's in our weapon hierarchy
        if (other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform))
        {
            return true;
        }

        return false;
    }

    #region Public API (WeaponHitbox compatibility)

    /// <summary>
    /// Manual enable/disable (for compatibility with WeaponModule)
    /// </summary>
    public void EnableHitDetection(bool enable)
    {
        enabled = enable;

        if (!enable)
        {
            canHit = false;
            hitThisSwing.Clear();
        }
    }

    /// <summary>
    /// Force enable hitting (for testing or special cases)
    /// </summary>
    public void ForceEnableHitting()
    {
        EnableHitting();
    }

    /// <summary>
    /// Force disable hitting (for testing or special cases)
    /// </summary>
    public void ForceDisableHitting()
    {
        DisableHitting();
    }

    /// <summary>
    /// Clear hit tracking (for manual combo resets)
    /// </summary>
    public void ClearHitTracking()
    {
        hitThisSwing.Clear();
    }

    /// <summary>
    /// Get current hit state
    /// </summary>
    public bool CanCurrentlyHit => canHit;

    /// <summary>
    /// Get number of targets hit this swing
    /// </summary>
    public int HitCount => hitThisSwing.Count;

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        if (!debugHits) return;

        // Draw weapon collider bounds
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = canHit ? Color.red : Color.gray;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);

            if (col is BoxCollider boxCol)
            {
                Gizmos.DrawWireCube(boxCol.center, boxCol.size);
            }
            else if (col is SphereCollider sphereCol)
            {
                Gizmos.DrawWireSphere(sphereCol.center, sphereCol.radius);
            }
            else if (col is CapsuleCollider capsuleCol)
            {
                // Approximate capsule with sphere
                Gizmos.DrawWireSphere(capsuleCol.center, capsuleCol.radius);
            }
        }

        // Reset matrix
        Gizmos.matrix = Matrix4x4.identity;

        // Show hit status
        if (canHit)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }

    void OnGUI()
    {
        if (!debugHits) return;

        // Show weapon hit status in corner
        GUILayout.BeginArea(new Rect(Screen.width - 250, Screen.height - 120, 240, 110));
        GUILayout.Label("=== WEAPON HIT DEBUG ===");
        GUILayout.Label($"Can Hit: {canHit}");
        GUILayout.Label($"Hits This Swing: {hitThisSwing.Count}");
        GUILayout.Label($"Damage: {damage}");
        GUILayout.Label($"Component Enabled: {enabled}");
        GUILayout.EndArea();
    }

    #endregion

    #region Cleanup

    void OnDestroy()
    {
        // Cleanup event subscriptions to prevent memory leaks
        var meleeModule = FindMeleeModule();
        if (meleeModule != null)
        {
            meleeModule.OnWeaponEnabled -= EnableHitting;
            meleeModule.OnWeaponDisabled -= DisableHitting;
        }
    }

    #endregion
}