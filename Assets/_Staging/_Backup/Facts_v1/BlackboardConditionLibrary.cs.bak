using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Universal Condition Library - Single source of truth for all semantic conditions.
/// 
/// Architecture Philosophy:
/// "One library, infinite entities. Entities ignore conditions that don't apply to them."
/// 
/// Why Universal Library?
/// - Zero per-entity configuration (assign library, done)
/// - Consistent semantic facts across all entity types
/// - Single source of truth for gameplay thresholds
/// - Easy balance changes (update once, affects all entities)
/// - Genre-agnostic (works for any game type)
/// 
/// Responsibilities:
/// - Hold all BlackboardCondition assets in the game
/// - Validate conditions on load
/// - Provide condition list to SemanticBridgeSystem
/// 
/// NOT Responsible For:
/// - Evaluating conditions (SemanticBridgeSystem does this)
/// - Storing results (Blackboard does this)
/// - Entity-specific filtering (unnecessary - just evaluate all)
/// 
/// Performance:
/// - 50 conditions @ 10Hz = 500 evaluations/second per entity
/// - Cost per evaluation: ~10 CPU instructions
/// - Total cost: <0.01ms per entity (negligible)
/// 
/// Usage Pattern:
/// 1. Create one ConditionLibrary asset (global)
/// 2. Add ALL conditions to it (health, stamina, mana, combat, movement, etc.)
/// 3. Assign library to SemanticBridgeSystem on each entity
/// 4. Done! Each entity evaluates all, ignores irrelevant ones
/// 
/// Example Conditions:
/// - Health: IsWounded, IsHealthy, IsDying, IsCritical
/// - Stamina: LowStamina, CanDodge, CanSprint, Exhausted
/// - Mana: LowMana, CanCastSpell, OutOfMana, HighMana
/// - Combat: InCombat, CanParry, CanCounter, IsStunned
/// - Movement: CanJump, IsFalling, IsGrounded, IsSwimming
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 20, 2026
/// </summary>
[CreateAssetMenu(fileName = "ConditionLibrary", menuName = "NinjaGame/Blackboard/Condition Library")]
public class BlackboardConditionLibrary : ScriptableObject
{
    [Header("Universal Condition Library")]
    [Tooltip("Every semantic condition in the game - entities ignore conditions that don't apply")]
    public List<BlackboardCondition> conditions = new List<BlackboardCondition>();

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    // Properties
    public int ConditionCount => conditions?.Count ?? 0;

    #region Validation

    /// <summary>
    /// Validate all conditions in the library
    /// </summary>
    public bool Validate(out string error)
    {
        // Guard clauses
        if (conditions == null || conditions.Count == 0)
        {
            error = "No conditions defined in library";
            return false;
        }

        int validCount = 0;
        int invalidCount = 0;
        List<string> errors = new List<string>();

        // Validate each condition
        foreach (var condition in conditions)
        {
            if (condition == null)
            {
                invalidCount++;
                errors.Add("Null condition in library");
                continue;
            }

            if (!condition.Validate(out string conditionError))
            {
                invalidCount++;
                errors.Add($"{condition.name}: {conditionError}");
                continue;
            }

            validCount++;
        }

        // Build error message if any failures
        if (invalidCount > 0)
        {
            error = $"{invalidCount} invalid conditions:\n" + string.Join("\n", errors);
            return false;
        }

        error = "";
        return true;
    }

    /// <summary>
    /// Validate on asset save (Editor only)
    /// </summary>
    private void OnValidate()
    {
        if (conditions == null || conditions.Count == 0)
        {
            if (debugLogging)
                Debug.LogWarning($"[ConditionLibrary:{name}] No conditions defined", this);
            return;
        }

        // Quick validation check
        if (!Validate(out string error))
        {
            Debug.LogWarning($"[ConditionLibrary:{name}] Validation failed:\n{error}", this);
        }
        else if (debugLogging)
        {
            Debug.Log($"[ConditionLibrary:{name}] Validated {ConditionCount} conditions");
        }
    }

    #endregion

    #region Context Menu Helpers

    /// <summary>
    /// Print all conditions in the library (debug)
    /// </summary>
    [ContextMenu("Print All Conditions")]
    private void PrintAllConditions()
    {
        if (conditions == null || conditions.Count == 0)
        {
            Debug.Log($"[ConditionLibrary:{name}] No conditions defined");
            return;
        }

        Debug.Log($"=== Condition Library: {name} ({ConditionCount} conditions) ===");

        foreach (var condition in conditions)
        {
            if (condition == null)
            {
                Debug.Log("  ✗ NULL CONDITION");
                continue;
            }

            string status = condition.IsEnabled ? "✓" : "✗";
            string desc = condition.GetReadableDescription();
            Debug.Log($"  {status} {condition.name}: {desc}");
        }
    }

    /// <summary>
    /// Validate all conditions (debug)
    /// </summary>
    [ContextMenu("Validate All Conditions")]
    private void ValidateAllConditions()
    {
        if (Validate(out string error))
        {
            Debug.Log($"[ConditionLibrary:{name}] ✓ All {ConditionCount} conditions valid");
        }
        else
        {
            Debug.LogError($"[ConditionLibrary:{name}] ✗ Validation failed:\n{error}", this);
        }
    }

    /// <summary>
    /// Count conditions by type (debug)
    /// </summary>
    [ContextMenu("Print Statistics")]
    private void PrintStatistics()
    {
        if (conditions == null || conditions.Count == 0)
        {
            Debug.Log($"[ConditionLibrary:{name}] No conditions defined");
            return;
        }

        int enabledCount = 0;
        int disabledCount = 0;
        int nullCount = 0;

        foreach (var condition in conditions)
        {
            if (condition == null)
            {
                nullCount++;
                continue;
            }

            if (condition.IsEnabled)
                enabledCount++;
            else
                disabledCount++;
        }

        Debug.Log($"=== Condition Library Statistics: {name} ===\n" +
                  $"  Total: {ConditionCount}\n" +
                  $"  Enabled: {enabledCount}\n" +
                  $"  Disabled: {disabledCount}\n" +
                  $"  Null: {nullCount}");
    }

    #endregion
}