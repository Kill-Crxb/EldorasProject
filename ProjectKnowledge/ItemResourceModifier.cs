using UnityEngine;

/// <summary>
/// ItemResourceModifier - Resource modifications for items
/// 
/// Purpose: Define how items modify character resources (Health, Mana, Stamina)
/// Integration: Uses ResourceSystem's ResourceDefinition ScriptableObjects
/// 
/// Examples:
/// - "+50 Max Health" (resource=HealthDef, maxIncrease=50)
/// - "+2 Health Regen/sec" (resource=HealthDef, regenBonus=2)
/// - "+100 Max Mana + 5 Mana Regen" (resource=ManaDef, maxIncrease=100, regenBonus=5)
/// 
/// Usage:
/// - Add to ItemDefinition.resourceModifiers array
/// - ResourceSystem applies on equip/unequip
/// 
/// Benefits:
/// - Integrates with existing ResourceSystem
/// - Uses ScriptableObject references (type-safe)
/// - Designer-friendly inspector configuration
/// - Supports both max and regen modifications
/// </summary>
[System.Serializable]
public class ItemResourceModifier
{
    [Header("Target Resource")]
    [Tooltip("Resource to modify (Health, Mana, Stamina, etc.)")]
    public ResourceDefinition resource;

    [Header("Max Value Modification")]
    [Tooltip("Increase to max resource capacity (e.g., +50 Max Health)")]
    public float maxIncrease;

    [Header("Regeneration Modification")]
    [Tooltip("Bonus regeneration per second (e.g., +2 HP/sec)")]
    public float regenBonus;

    [Header("Percent-Based (Future)")]
    [Tooltip("Should increases be percentage-based instead of flat?")]
    public bool isPercentage;

    [Tooltip("If percentage, this is the multiplier (e.g., 0.1 = +10% Max Health)")]
    public float percentageValue;

    /// <summary>
    /// Apply this modifier to ResourceSystem
    /// </summary>
    public void ApplyToResourceSystem(ResourceSystem resourceSystem, string sourceId)
    {
        // Guard clause - null check
        if (resourceSystem == null || resource == null) return;

        // TODO: ResourceSystem needs AddModifier() method
        // For now, this is a placeholder for Phase 3 implementation

        if (maxIncrease > 0)
        {
            // resourceSystem.AddMaxModifier(resource, sourceId, maxIncrease);
            Debug.Log($"[ItemResourceModifier] Would add +{maxIncrease} max to {resource.displayName}");
        }

        if (regenBonus > 0)
        {
            // resourceSystem.AddRegenModifier(resource, sourceId, regenBonus);
            Debug.Log($"[ItemResourceModifier] Would add +{regenBonus}/sec regen to {resource.displayName}");
        }
    }

    /// <summary>
    /// Remove this modifier from ResourceSystem
    /// </summary>
    public void RemoveFromResourceSystem(ResourceSystem resourceSystem, string sourceId)
    {
        // Guard clause - null check
        if (resourceSystem == null || resource == null) return;

        // TODO: ResourceSystem needs RemoveModifier() method
        // resourceSystem.RemoveModifier(resource, sourceId);

        Debug.Log($"[ItemResourceModifier] Would remove modifier from {resource.displayName}");
    }

    /// <summary>
    /// Get display text for UI tooltips
    /// </summary>
    public string GetDisplayText()
    {
        string text = "";

        if (maxIncrease > 0)
        {
            if (isPercentage)
                text += $"+{percentageValue * 100}% Max {resource.displayName}";
            else
                text += $"+{maxIncrease} Max {resource.displayName}";
        }

        if (regenBonus > 0)
        {
            if (!string.IsNullOrEmpty(text)) text += ", ";
            text += $"+{regenBonus} {resource.displayName} Regen/sec";
        }

        return text;
    }

    /// <summary>
    /// Validation helper
    /// </summary>
    public bool IsValid()
    {
        return resource != null && (maxIncrease > 0 || regenBonus > 0);
    }
}