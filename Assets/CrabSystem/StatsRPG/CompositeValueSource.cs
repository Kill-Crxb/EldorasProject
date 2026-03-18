using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Value source that combines multiple sources using math operations.
/// Enables complex conditions without code changes.
/// 
/// Use Cases:
/// - Total resources: health% + mana% + stamina%
/// - Average defense: (armor + resistance) / 2
/// - Combined threat: distance * damage * enemyCount
/// 
/// Operations:
/// - Add: source1 + source2 + ...
/// - Multiply: source1 * source2 * ...
/// - Average: (source1 + source2 + ...) / count
/// - Min: minimum of all sources
/// - Max: maximum of all sources
/// 
/// Example Configurations:
/// 
/// TotalResources.asset (Add):
///   sources: [Health_Pct, Mana_Pct, Stamina_Pct]
///   operation: Add
///   Result: 0.5 + 0.3 + 0.8 = 1.6
/// 
/// AverageDefense.asset (Average):
///   sources: [Armor_Stat, Resistance_Stat]
///   operation: Average
///   Result: (50 + 30) / 2 = 40
/// 
/// Usage in Condition:
/// BlackboardCondition "LowResources":
///   valueSource: TotalResources.asset
///   comparison: LessThan
///   threshold: 1.5 (all resources combined < 150%)
///   outputFactKey: "LowResources"
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
[CreateAssetMenu(fileName = "CompositeValue", menuName = "NinjaGame/Blackboard/Value Sources/Composite")]
public class CompositeValueSource : ValueSourceDefinition
{
    [Header("Source Combination")]
    [Tooltip("Value sources to combine")]
    [SerializeField] private List<ValueSourceDefinition> sources = new List<ValueSourceDefinition>();

    [Tooltip("How to combine the values")]
    [SerializeField] private CompositeOperation operation = CompositeOperation.Add;

    public override float GetValue(ControllerBrain brain)
    {
        // Guard clauses (flat logic)
        if (brain == null) return 0f;
        if (sources == null || sources.Count == 0) return 0f;

        // Collect values from all sources
        float result = 0f;
        int validCount = 0;

        switch (operation)
        {
            case CompositeOperation.Add:
                result = CalculateAdd(brain, ref validCount);
                break;

            case CompositeOperation.Multiply:
                result = CalculateMultiply(brain, ref validCount);
                break;

            case CompositeOperation.Average:
                result = CalculateAverage(brain, ref validCount);
                break;

            case CompositeOperation.Min:
                result = CalculateMin(brain, ref validCount);
                break;

            case CompositeOperation.Max:
                result = CalculateMax(brain, ref validCount);
                break;
        }

        return validCount > 0 ? result : 0f;
    }

    private float CalculateAdd(ControllerBrain brain, ref int validCount)
    {
        float sum = 0f;

        foreach (var source in sources)
        {
            if (source == null) continue;

            sum += source.GetValue(brain);
            validCount++;
        }

        return sum;
    }

    private float CalculateMultiply(ControllerBrain brain, ref int validCount)
    {
        float product = 1f;

        foreach (var source in sources)
        {
            if (source == null) continue;

            product *= source.GetValue(brain);
            validCount++;
        }

        return product;
    }

    private float CalculateAverage(ControllerBrain brain, ref int validCount)
    {
        float sum = 0f;

        foreach (var source in sources)
        {
            if (source == null) continue;

            sum += source.GetValue(brain);
            validCount++;
        }

        return validCount > 0 ? sum / validCount : 0f;
    }

    private float CalculateMin(ControllerBrain brain, ref int validCount)
    {
        float min = float.MaxValue;

        foreach (var source in sources)
        {
            if (source == null) continue;

            float value = source.GetValue(brain);
            if (value < min) min = value;
            validCount++;
        }

        return validCount > 0 ? min : 0f;
    }

    private float CalculateMax(ControllerBrain brain, ref int validCount)
    {
        float max = float.MinValue;

        foreach (var source in sources)
        {
            if (source == null) continue;

            float value = source.GetValue(brain);
            if (value > max) max = value;
            validCount++;
        }

        return validCount > 0 ? max : 0f;
    }

    public override string GetDisplayName()
    {
        if (sources == null || sources.Count == 0) return "Empty Composite";
        return $"{operation} ({sources.Count} sources)";
    }

    public override bool Validate(out string error)
    {
        if (sources == null || sources.Count == 0)
        {
            error = "No sources assigned";
            return false;
        }

        // Check for null sources
        foreach (var source in sources)
        {
            if (source == null)
            {
                error = "Contains null source";
                return false;
            }
        }

        error = null;
        return true;
    }
}

/// <summary>
/// How to combine multiple value sources
/// </summary>
public enum CompositeOperation
{
    Add,        // Sum all values
    Multiply,   // Product of all values
    Average,    // Mean of all values
    Min,        // Minimum value
    Max         // Maximum value
}
