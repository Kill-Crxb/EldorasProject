using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// AbilityDefinition - Blackboard Partial
/// Semantic requirement checking with optimized category defaults
/// </summary>
public partial class AbilityDefinition
{
    // ========================================
    // BLACKBOARD REQUIREMENTS
    // ========================================

    [Header("Blackboard Requirements")]
    [Tooltip("Additional required facts (ANY logic - ability usable if ANY fact is true)")]
    public List<string> requiredFactsAny = new List<string>();

    [Tooltip("Additional required facts (ALL logic - ability requires ALL facts true)")]
    public List<string> requiredFactsAll = new List<string>();

    [Tooltip("Additional forbidden facts (blocked if ALL forbidden facts are true)")]
    public List<string> forbiddenFactsAll = new List<string>();

    [Tooltip("Override category defaults? (Use custom requirements instead)")]
    public bool overrideDefaults = false;

    [Tooltip("Custom required facts (when overriding defaults)")]
    public List<string> customRequiredFacts = new List<string>();

    [Tooltip("Custom forbidden facts (when overriding defaults)")]
    public List<string> customForbiddenFacts = new List<string>();

    // ========================================
    // CATEGORY DEFAULT CACHE (Hot-path optimization)
    // ========================================

    private static readonly Dictionary<AbilityCategory, string[]> CategoryForbiddenCache =
        new Dictionary<AbilityCategory, string[]>
        {
            { AbilityCategory.Spell, new[] { "IsSilenced", "IsStunned" } },
            { AbilityCategory.Physical, new[] { "IsDisarmed", "IsStunned" } },
            { AbilityCategory.Movement, new[] { "IsRooted", "IsStunned" } },
            { AbilityCategory.Defense, new[] { "IsDisarmed", "IsStunned" } },
            { AbilityCategory.Natural, new[] { "IsStunned" } },  // Can't be disarmed
            { AbilityCategory.Utility, System.Array.Empty<string>() }  // Minimal restrictions
        };

    // ========================================
    // POLICY HELPERS
    // ========================================

    /// <summary>
    /// Get blackboard requirements for this ability (includes category defaults)
    /// OPTIMIZED: Returns cached arrays where possible, minimizes allocations
    /// Returns: (requiredFactsAny, requiredFactsAll, forbiddenFactsAll)
    /// </summary>
    public (List<string> requiredAny, List<string> requiredAll, List<string> forbiddenAll) GetBlackboardRequirements()
    {
        // If overriding defaults, use custom requirements only
        if (overrideDefaults)
        {
            return (
                new List<string>(customRequiredFacts),
                new List<string>(),  // Custom doesn't distinguish ANY/ALL yet
                new List<string>(customForbiddenFacts)
            );
        }

        // Get cached category defaults
        var categoryForbidden = GetCategoryDefaultForbiddenFacts();

        // Build combined lists (only allocate if we have ability-specific additions)
        var requiredAny = new List<string>(requiredFactsAny);
        var requiredAll = new List<string>(requiredFactsAll);
        
        // Combine category defaults + ability-specific forbidden facts
        var forbiddenAll = new List<string>(categoryForbidden.Length + forbiddenFactsAll.Count);
        forbiddenAll.AddRange(categoryForbidden);
        forbiddenAll.AddRange(forbiddenFactsAll);

        return (requiredAny, requiredAll, forbiddenAll);
    }

    /// <summary>
    /// Get default forbidden facts for this ability's category
    /// OPTIMIZED: Returns cached readonly array - zero allocation
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string[] GetCategoryDefaultForbiddenFacts()
    {
        return CategoryForbiddenCache.TryGetValue(abilityCategory, out var facts)
            ? facts
            : System.Array.Empty<string>();
    }

    /// <summary>
    /// Fast check if ability is blocked by a specific fact
    /// Useful for AI evaluation without full requirement check
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsBlockedBy(string fact)
    {
        // Check category defaults
        var categoryForbidden = GetCategoryDefaultForbiddenFacts();
        foreach (var forbidden in categoryForbidden)
        {
            if (forbidden == fact) return true;
        }

        // Check ability-specific forbidden facts
        return forbiddenFactsAll.Contains(fact);
    }
}
