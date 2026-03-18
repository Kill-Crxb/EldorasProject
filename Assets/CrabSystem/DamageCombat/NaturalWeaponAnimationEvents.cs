using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Animation Event Forwarder for Natural Weapons
/// 
/// This component MUST be on the same GameObject as the Animator.
/// Animation events call methods on this script, which then forwards
/// the enable/disable commands to the actual NaturalWeaponHitbox components.
/// 
/// Setup:
/// 1. Add this to the bear model GameObject (the one with Animator)
/// 2. It will automatically find all NaturalWeaponHitbox children
/// 3. Animation events call: EnableHitbox("bear_left_claw") / DisableHitbox()
/// </summary>
public class NaturalWeaponAnimationEvents : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugEvents = true;

    // Cache of all hitboxes, keyed by ability ID
    private Dictionary<string, NaturalWeaponHitbox> hitboxLookup = new Dictionary<string, NaturalWeaponHitbox>();

    // Currently active hitbox (for DisableHitbox without parameters)
    private NaturalWeaponHitbox currentActiveHitbox;

    void Awake()
    {
        // Find all NaturalWeaponHitbox components in children
        var hitboxes = GetComponentsInChildren<NaturalWeaponHitbox>(true);

        if (hitboxes.Length == 0)
        {
            Debug.LogWarning($"[NaturalWeaponAnimationEvents] No NaturalWeaponHitbox components found on {gameObject.name}");
            return;
        }

        // Build lookup dictionary
        // Note: We'll need to manually configure which hitbox corresponds to which ability
        // For now, just store them and we'll identify by ability ID when enabling
        if (debugEvents)
        {
            Debug.Log($"[NaturalWeaponAnimationEvents] Found {hitboxes.Length} natural weapon hitboxes on {gameObject.name}");
        }
    }

    /// <summary>
    /// Called by animation event to enable a specific hitbox.
    /// The abilityId should match the ability being executed (e.g., "bear_left_claw").
    /// </summary>
    /// <param name="abilityId">Ability ID to execute on hit</param>
    public void EnableHitbox(string abilityId)
    {
        if (string.IsNullOrEmpty(abilityId))
        {
            Debug.LogWarning($"[NaturalWeaponAnimationEvents] EnableHitbox called with empty abilityId");
            return;
        }

        // Map ability ID to hitbox name
        string targetHitboxName = GetHitboxNameFromAbilityId(abilityId);

        if (string.IsNullOrEmpty(targetHitboxName))
        {
            if (debugEvents)
            {
                Debug.LogWarning($"[NaturalWeaponAnimationEvents] Unknown ability ID: {abilityId}");
            }
            return;
        }

        // Find the appropriate hitbox by name
        var hitboxes = GetComponentsInChildren<NaturalWeaponHitbox>(true);

        bool foundHitbox = false;
        foreach (var hitbox in hitboxes)
        {
            // Check if this hitbox's GameObject name matches
            if (hitbox.gameObject.name.Contains(targetHitboxName))
            {
                // Enable the hitbox - it will handle the ability ID internally
                hitbox.EnableHitbox(abilityId);
                currentActiveHitbox = hitbox;
                foundHitbox = true;

                if (debugEvents)
                {
                    Debug.Log($"[NaturalWeaponAnimationEvents] Enabled hitbox {hitbox.gameObject.name} for ability: {abilityId}");
                }

                break;
            }
        }

        if (!foundHitbox && debugEvents)
        {
            Debug.LogWarning($"[NaturalWeaponAnimationEvents] No hitbox found matching '{targetHitboxName}' for ability: {abilityId}");
        }
    }

    /// <summary>
    /// Map ability ID to expected hitbox GameObject name
    /// </summary>
    private string GetHitboxNameFromAbilityId(string abilityId)
    {
        switch (abilityId)
        {
            case "bear_left_claw":
                return "LeftClaw";
            case "bear_right_claw":
                return "RightClaw";
            case "bear_bite":
                return "Bite";
            default:
                return null;
        }
    }

    /// <summary>
    /// Called by animation event to disable the currently active hitbox.
    /// </summary>
    public void DisableHitbox()
    {
        if (currentActiveHitbox != null)
        {
            currentActiveHitbox.DisableHitbox();

            if (debugEvents)
            {
                Debug.Log($"[NaturalWeaponAnimationEvents] Disabled hitbox {currentActiveHitbox.gameObject.name}");
            }

            currentActiveHitbox = null;
        }
        else
        {
            // Disable all hitboxes as fallback
            var hitboxes = GetComponentsInChildren<NaturalWeaponHitbox>(true);
            foreach (var hitbox in hitboxes)
            {
                hitbox.DisableHitbox();
            }
        }
    }

    /// <summary>
    /// Called by animation event to disable all hitboxes.
    /// Useful as a safety measure at animation end.
    /// </summary>
    public void DisableAllHitboxes()
    {
        var hitboxes = GetComponentsInChildren<NaturalWeaponHitbox>(true);
        foreach (var hitbox in hitboxes)
        {
            hitbox.DisableHitbox();
        }

        currentActiveHitbox = null;

        if (debugEvents)
        {
            Debug.Log($"[NaturalWeaponAnimationEvents] Disabled all hitboxes");
        }
    }

    /// <summary>
    /// Debug helper - logs all found hitboxes
    /// </summary>
    [ContextMenu("Debug: List All Hitboxes")]
    private void DebugListHitboxes()
    {
        var hitboxes = GetComponentsInChildren<NaturalWeaponHitbox>(true);
        Debug.Log($"=== Found {hitboxes.Length} Natural Weapon Hitboxes ===");

        foreach (var hitbox in hitboxes)
        {
            Debug.Log($"  - {hitbox.gameObject.name} at path: {GetFullPath(hitbox.transform)}");
        }
    }

    private string GetFullPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}