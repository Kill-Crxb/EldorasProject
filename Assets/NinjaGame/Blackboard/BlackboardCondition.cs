using UnityEngine;

/// <summary>
/// Condition that evaluates a value source against a threshold.
/// Results are written to blackboard as semantic facts.
/// 
/// Architecture:
/// ValueSource → GetValue() → Compare to Threshold → Write Fact to Blackboard
/// 
/// Single Source of Truth:
/// All threshold logic lives HERE, not scattered across systems.
/// UI, GOAP, Abilities all query the resulting blackboard fact.
/// 
/// Example Flow:
/// 1. SemanticBridge calls: condition.Evaluate(brain)
/// 2. Condition queries: valueSource.GetValue(brain) → 0.2 (20% health)
/// 3. Condition compares: 0.2 < 0.25 → true
/// 4. Condition returns: true
/// 5. SemanticBridge writes: blackboard.SetBool("IsWounded", true)
/// 6. Systems consume: if (blackboard.GetBool(IsWounded)) { flee(); }
/// 
/// Comparison Operations:
/// - LessThan: value < threshold
/// - LessThanOrEqual: value <= threshold
/// - GreaterThan: value > threshold
/// - GreaterThanOrEqual: value >= threshold
/// - Equals: value ≈ threshold (with epsilon)
/// - NotEquals: value !≈ threshold
/// 
/// Example Configuration:
/// IsWounded.asset:
///   valueSource: Health_Percentage.asset
///   comparison: LessThan
///   threshold: 0.25
///   outputFactKey: "IsWounded"
///   
/// CanDodge.asset:
///   valueSource: Stamina_Current.asset
///   comparison: GreaterThanOrEqual
///   threshold: 20.0
///   outputFactKey: "CanDodge"
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
[CreateAssetMenu(fileName = "Condition", menuName = "NinjaGame/Blackboard/Condition")]
public class BlackboardCondition : ScriptableObject
{
    [Header("Value Source")]
    [Tooltip("Where to get the value to evaluate")]
    [SerializeField] private ValueSourceDefinition valueSource;

    [Header("Comparison")]
    [Tooltip("How to compare value against threshold")]
    [SerializeField] private ComparisonOp comparison = ComparisonOp.LessThan;

    [Tooltip("Threshold value to compare against")]
    [SerializeField] private float threshold = 0.5f;

    [Header("Output")]
    [Tooltip("Blackboard fact key to write result to (e.g., 'IsWounded', 'CanDodge')")]
    [SerializeField] private string outputFactKey;

    [Header("Optional")]
    [Tooltip("Enable/disable this condition")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Description for designers")]
    [TextArea(2, 4)]
    [SerializeField] private string description;

    // Properties
    public ValueSourceDefinition ValueSource => valueSource;
    public string OutputFactKey => outputFactKey;
    public bool IsEnabled => isEnabled;

    /// <summary>
    /// Evaluate this condition for the given entity.
    /// Returns true if value passes comparison against threshold.
    /// </summary>
    public bool Evaluate(ControllerBrain brain)
    {
        // Guard clauses (flat logic - no nesting!)
        if (!isEnabled) return false;
        if (brain == null) return false;
        if (valueSource == null) return false;

        // Get value from source
        float value = valueSource.GetValue(brain);

        // Compare against threshold
        return CompareValue(value, threshold, comparison);
    }

    /// <summary>
    /// Perform comparison operation
    /// </summary>
    private bool CompareValue(float value, float threshold, ComparisonOp op)
    {
        const float epsilon = 0.001f;

        switch (op)
        {
            case ComparisonOp.LessThan:
                return value < threshold;

            case ComparisonOp.LessThanOrEqual:
                return value <= threshold;

            case ComparisonOp.GreaterThan:
                return value > threshold;

            case ComparisonOp.GreaterThanOrEqual:
                return value >= threshold;

            case ComparisonOp.Equals:
                return Mathf.Abs(value - threshold) < epsilon;

            case ComparisonOp.NotEquals:
                return Mathf.Abs(value - threshold) >= epsilon;

            default:
                return false;
        }
    }

    /// <summary>
    /// Validate condition configuration
    /// </summary>
    public bool Validate(out string error)
    {
        if (valueSource == null)
        {
            error = "Value source not assigned";
            return false;
        }

        if (string.IsNullOrEmpty(outputFactKey))
        {
            error = "Output fact key cannot be empty";
            return false;
        }

        // Validate value source
        if (!valueSource.Validate(out string sourceError))
        {
            error = $"Value source invalid: {sourceError}";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Get human-readable description of this condition
    /// </summary>
    public string GetReadableDescription()
    {
        if (valueSource == null) return "Invalid (no source)";

        string comparisonStr = comparison switch
        {
            ComparisonOp.LessThan => "<",
            ComparisonOp.LessThanOrEqual => "≤",
            ComparisonOp.GreaterThan => ">",
            ComparisonOp.GreaterThanOrEqual => "≥",
            ComparisonOp.Equals => "=",
            ComparisonOp.NotEquals => "≠",
            _ => "?"
        };

        return $"{valueSource.GetDisplayName()} {comparisonStr} {threshold} → {outputFactKey}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Validate(out string error))
        {
            Debug.LogWarning($"[BlackboardCondition] Validation failed: {error}", this);
        }
    }
#endif
}

/// <summary>
/// Comparison operations for condition evaluation
/// </summary>
public enum ComparisonOp
{
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    Equals,
    NotEquals
}
