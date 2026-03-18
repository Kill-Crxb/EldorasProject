using NinjaGame.Stats;
using UnityEngine;

/// <summary>
/// ItemStatModifier - Stat modifications for items
/// 
/// Purpose: Define how items modify character stats
/// Integration: Uses StatSystem's stat IDs and contribution bonus system
/// 
/// Examples:
/// - Flat: "+10 Armor" (ModifierType.Flat, statId="combat.armor", value=10)
/// - Percentage: "+10% Attack Speed" (ModifierType.Multiplier, statId="combat.attack_speed", value=0.1)
/// - Scaling: "+1 Armor per 5 Strength" (ModifierType.ScalingContribution, sourceStatId="character.strength", scalingRatio=0.2)
/// 
/// Usage:
/// - Add to ItemDefinition.statModifiers array
/// - StatSystem applies via AddContributionBonus() on equip
/// - StatSystem removes via RemoveContributionBonus() on unequip
/// 
/// Benefits:
/// - Integrates with existing StatSystem formulas
/// - Supports flat, percentage, and scaling modifiers
/// - Designer-friendly inspector configuration
/// - Uses StatSchema definitions from StatsManager
/// </summary>
[System.Serializable]
public class ItemStatModifier
{
    [Header("Target Stat")]
    [Tooltip("Stat ID from StatSchema (e.g., 'combat.armor', 'character.strength')")]
    public string statId;

    [Tooltip("Optional: Code-generated handle for performance (auto-filled by generator)")]
    public int statHandle;

    [Header("Modification Type")]
    [Tooltip("How this modifier affects the stat")]
    public ModifierType modifierType = ModifierType.Flat;

    [Header("Flat/Multiplier Values")]
    [Tooltip("Value for Flat (+10 Armor) or Multiplier (+10% = 0.1) types")]
    public float value;

    [Header("Scaling Contribution Values")]
    [Tooltip("Source stat for scaling (e.g., 'character.strength' for armor scaling)")]
    public string sourceStatId;

    [Tooltip("Optional: Source stat handle for performance")]
    public int sourceStatHandle;

    [Tooltip("Scaling ratio (e.g., 0.2 = +1 target per 5 source)")]
    public float scalingRatio;

    [Header("Conditional (Future)")]
    [Tooltip("Does this modifier have conditions? (e.g., 'While health above 50%')")]
    public bool hasCondition;

    [Tooltip("Human-readable condition description for designer reference")]
    [TextArea(1, 2)]
    public string conditionDescription;

    // Future: BlackboardCondition reference for actual logic
    // public BlackboardCondition condition;

    /// <summary>
    /// Apply this modifier to StatEngine via contribution system
    /// </summary>
    public void ApplyToStatEngine(StatEngine statEngine, string sourceId)
    {
        // Guard clause - null check
        if (statEngine == null || string.IsNullOrEmpty(statId)) return;

        // Guard clause - conditional not yet supported
        if (hasCondition)
        {
            Debug.LogWarning($"[ItemStatModifier] Conditional modifiers not yet supported: {conditionDescription}");
            return;
        }

        switch (modifierType)
        {
            case ModifierType.Flat:
                // FIXED: StatEngine.AddFlatModifier(statId, sourceId, value)
                statEngine.AddFlatModifier(statId, sourceId, value);
                break;

            case ModifierType.Multiplier:
                // FIXED: StatEngine.AddPercentModifier(statId, sourceId, percent)
                statEngine.AddPercentModifier(statId, sourceId, value);
                break;

            case ModifierType.ScalingContribution:
                // FIXED: StatEngine.AddContributionBonus(targetStat, sourceId, sourceStat, multiplier)
                // Example: AddContributionBonus("combat.armor", "helmet_01", "character.strength", 0.2)
                if (!string.IsNullOrEmpty(sourceStatId))
                {
                    statEngine.AddContributionBonus(statId, sourceId, sourceStatId, scalingRatio);
                }
                break;

            case ModifierType.Override:
                // Override sets stat to specific value (rare, use carefully)
                Debug.LogWarning($"[ItemStatModifier] Override modifier type not yet implemented for stat: {statId}");
                break;
        }
    }

    /// <summary>
    /// Remove this modifier from StatEngine
    /// </summary>
    public void RemoveFromStatEngine(StatEngine statEngine, string sourceId)
    {
        // Guard clause - null check
        if (statEngine == null || string.IsNullOrEmpty(sourceId)) return;

        // FIXED: Use RemoveAllModifiersFromSource(sourceId) - removes all modifiers from this item
        statEngine.RemoveAllModifiersFromSource(sourceId);
    }

    /// <summary>
    /// Get display text for UI tooltips
    /// </summary>
    public string GetDisplayText()
    {
        switch (modifierType)
        {
            case ModifierType.Flat:
                return $"+{value} {GetStatDisplayName()}";

            case ModifierType.Multiplier:
                return $"+{value * 100}% {GetStatDisplayName()}";

            case ModifierType.ScalingContribution:
                float perSourceValue = 1f / scalingRatio;
                return $"+1 {GetStatDisplayName()} per {perSourceValue} {GetSourceStatDisplayName()}";

            case ModifierType.Override:
                return $"Set {GetStatDisplayName()} to {value}";

            default:
                return "Unknown modifier";
        }
    }

    private string GetStatDisplayName()
    {
        // TODO: Query StatsManager for display name
        // For now, return formatted stat ID
        return statId.Replace("_", " ").Replace(".", " - ");
    }

    private string GetSourceStatDisplayName()
    {
        // TODO: Query StatsManager for display name
        // For now, return formatted stat ID
        return sourceStatId.Replace("_", " ").Replace(".", " - ");
    }
}

/// <summary>
/// Modifier type determines how value is applied
/// </summary>
public enum ModifierType
{
    /// <summary>
    /// Flat value added to stat (e.g., +10 Armor)
    /// </summary>
    Flat,

    /// <summary>
    /// Percentage multiplier (e.g., +10% Attack Speed)
    /// Value of 0.1 = 10% increase
    /// </summary>
    Multiplier,

    /// <summary>
    /// Scaling contribution from another stat
    /// Example: +1 Armor per 5 Strength (ratio = 0.2)
    /// </summary>
    ScalingContribution,

    /// <summary>
    /// Override stat to specific value (rare, use carefully)
    /// </summary>
    Override
}