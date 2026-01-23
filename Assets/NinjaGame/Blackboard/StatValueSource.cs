using UnityEngine;

/// <summary>
/// Value source that queries stat values through IStatProvider.
/// Uses string-based lookup (flexible across game types).
/// 
/// Use Cases:
/// - Armor value for "HighArmor" condition
/// - Crit chance for "CritFocused" condition
/// - Movement speed for "Slowed" condition
/// 
/// String-Based Design:
/// Unlike ResourceValueSource (type-safe assets), stats use strings because:
/// - 50-100+ stats per game (too many for individual assets)
/// - Stats are schema-defined (not individual ScriptableObjects)
/// - String IDs work across game genres (RPG, racing, etc.)
/// 
/// Future Enhancement (Phase 1.1):
/// - Add StatHandle (int) support for performance
/// - Maintain string fallback for flexibility
/// 
/// Example Configuration:
/// Armor_Stat.asset:
///   statId: "combat.armor"
///   
/// Crit_Chance_Stat.asset:
///   statId: "combat.crit_chance"
/// 
/// Usage in Condition:
/// BlackboardCondition "HighArmor":
///   valueSource: Armor_Stat.asset
///   comparison: GreaterThanOrEqual
///   threshold: 50.0
///   outputFactKey: "HighArmor"
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
[CreateAssetMenu(fileName = "StatValue", menuName = "NinjaGame/Blackboard/Value Sources/Stat")]
public class StatValueSource : ValueSourceDefinition
{
    [Header("Stat Query")]
    [Tooltip("Stat ID to query (e.g., 'combat.armor', 'character.health')")]
    [SerializeField] private string statId;

    [Header("Future: Handle Support (Phase 1.1)")]
    [Tooltip("Optional: Use StatHandle for performance (0 = use string)")]
    [SerializeField] private int statHandle = 0;

    public override float GetValue(ControllerBrain brain)
    {
        // Guard clauses (flat logic)
        if (brain == null) return 0f;
        if (string.IsNullOrEmpty(statId)) return 0f;

        var stats = brain.Stats;
        if (stats == null) return 0f;

        // Query stat (currently string-based, handle support in Phase 1.1)
        if (statHandle != 0)
        {
            // TODO Phase 1.1: Handle-based lookup
            // return stats.GetValue(statHandle);
        }

        return stats.GetValue(statId, 0f);
    }

    public override string GetDisplayName()
    {
        if (string.IsNullOrEmpty(statId)) return "None";
        return statId;
    }

    public override bool Validate(out string error)
    {
        if (string.IsNullOrEmpty(statId))
        {
            error = "Stat ID cannot be empty";
            return false;
        }

        error = null;
        return true;
    }
}
