using UnityEngine;

/// <summary>
/// GrantedAbilityData - Defines abilities granted by items
/// 
/// Purpose: Phase 3 prep - Items grant abilities on equip/use
/// 
/// Examples:
/// - Weapon: Grants attack combo + defense ability (Equipment source)
/// - Consumable: Grants temporary buff ability (Consumable source, 3 uses)
/// - Potion: Grants temporary speed boost (Temporary source, 30 second duration)
/// 
/// Usage:
/// - Add to ItemDefinition.grantedAbilities array
/// - Equipment handler calls RuntimeAbilityManager.AddAbility() on equip
/// - RuntimeAbilityManager.RemoveBySource() on unequip
/// 
/// Integration:
/// - Phase 2 RuntimeAbilityManager handles lifecycle
/// - AbilitySystem executes abilities
/// - AbilityLoadoutModule assigns to slots
/// 
/// Benefits:
/// - Equipment-based abilities (weapon swapping changes moveset)
/// - Consumables with limited uses
/// - Temporary buffs with auto-expiration
/// - Source tracking for cleanup
/// </summary>
[System.Serializable]
public class GrantedAbilityData
{
    [Header("Ability")]
    [Tooltip("The ability granted by this item")]
    public AbilityDefinition ability;

    [Header("Source Type")]
    [Tooltip("How this ability is granted")]
    public AbilitySource sourceType = AbilitySource.Equipment;

    [Header("Consumable Settings")]
    [Tooltip("Is this a consumable ability? (limited uses)")]
    public bool isConsumable;

    [Tooltip("Maximum uses if consumable (0 = infinite)")]
    public int maxUses;

    [Header("Temporary Settings")]
    [Tooltip("Is this a temporary ability? (auto-expires)")]
    public bool isTemporary;

    [Tooltip("Duration in seconds if temporary")]
    public float duration;

    [Header("Assignment")]
    [Tooltip("Should this ability auto-assign to a quick slot?")]
    public bool autoAssignToQuickSlot = true;

    [Tooltip("Preferred quick slot index (0-3), -1 for any available")]
    public int preferredQuickSlot = -1;

    [Header("Display")]
    [Tooltip("Custom display name for this granted ability (uses ability name if empty)")]
    public string customDisplayName;

    [TextArea(1, 2)]
    [Tooltip("Description override for this specific grant (uses ability description if empty)")]
    public string customDescription;

    /// <summary>
    /// Get the display name for this granted ability
    /// </summary>
    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(customDisplayName)) return customDisplayName;
        if (ability != null) return ability.abilityName;
        return "Unknown Ability";
    }

    /// <summary>
    /// Get the description for this granted ability
    /// </summary>
    public string GetDescription()
    {
        if (!string.IsNullOrEmpty(customDescription)) return customDescription;
        if (ability != null) return ability.description;  // FIXED: abilityDescription → description
        return "";
    }

    /// <summary>
    /// Create AbilityInstance from this grant data
    /// </summary>
    public AbilityInstance CreateInstance(string sourceItemId)
    {
        // Guard clause - no ability
        if (ability == null) return null;

        // Determine duration
        float durationSeconds = (isTemporary && duration > 0) ? duration : -1f;

        // Create instance using constructor
        // Constructor signature: AbilityInstance(definition, source, sourceId, maxUses, durationSeconds)
        var instance = new AbilityInstance(
            ability,
            sourceType,
            sourceItemId,
            isConsumable ? maxUses : -1,
            durationSeconds
        );

        return instance;
    }

    /// <summary>
    /// Validation helper
    /// </summary>
    public bool IsValid()
    {
        if (ability == null) return false;

        // Validate consumable settings
        if (isConsumable && maxUses <= 0)
        {
            Debug.LogWarning($"[GrantedAbilityData] Consumable ability '{ability.abilityName}' has invalid maxUses: {maxUses}");
            return false;
        }

        // Validate temporary settings
        if (isTemporary && duration <= 0)
        {
            Debug.LogWarning($"[GrantedAbilityData] Temporary ability '{ability.abilityName}' has invalid duration: {duration}");
            return false;
        }

        return true;
    }
}