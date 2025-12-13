using UnityEngine;

/// <summary>
/// Base class for condition nodes (leaf nodes that check things)
/// Conditions instantly return Success/Failure based on current state
/// Should NEVER return Running (they're instant checks)
/// </summary>
public abstract class BTConditionNode : BTNode
{
    protected BTConditionNode(string name = null) : base(name) { }
}


/// <summary>
/// Has Target Condition - Checks if entity has a valid target
/// </summary>
public class BTHasTargetCondition : BTConditionNode
{
    // Parameterless constructor for serialization
    public BTHasTargetCondition() : base("HasTarget") { }

    public BTHasTargetCondition(string name) : base(name ?? "HasTarget") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        Transform target = context.GetTarget();
        bool hasTarget = target != null;

        if (debugMode)
            context.Log($"HasTarget: {hasTarget}");

        return hasTarget ? NodeState.Success : NodeState.Failure;
    }
}


/// <summary>
/// Distance Check Condition - Checks if target is within range
/// </summary>
public class BTDistanceCheckCondition : BTConditionNode
{
    public enum ComparisonType { LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual, Between }

    [SerializeField] private ComparisonType comparison;
    [SerializeField] private float distance1;
    [SerializeField] private float distance2; // Only used for Between

    // Parameterless constructor for serialization
    public BTDistanceCheckCondition() : base("DistanceCheck") { }

    public BTDistanceCheckCondition(
        ComparisonType comparison,
        float distance,
        string name = null) : base(name ?? "DistanceCheck")
    {
        this.comparison = comparison;
        this.distance1 = distance;
    }

    public BTDistanceCheckCondition(
        float minDistance,
        float maxDistance,
        string name = null) : base(name ?? "DistanceCheck")
    {
        this.comparison = ComparisonType.Between;
        this.distance1 = minDistance;
        this.distance2 = maxDistance;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        float distance = context.GetDistanceToTarget();
        if (distance == float.MaxValue)
        {
            if (debugMode)
                context.LogWarning("DistanceCheck: No target");
            return NodeState.Failure;
        }

        bool result = false;
        switch (comparison)
        {
            case ComparisonType.LessThan:
                result = distance < distance1;
                break;
            case ComparisonType.LessThanOrEqual:
                result = distance <= distance1;
                break;
            case ComparisonType.GreaterThan:
                result = distance > distance1;
                break;
            case ComparisonType.GreaterThanOrEqual:
                result = distance >= distance1;
                break;
            case ComparisonType.Between:
                result = distance >= distance1 && distance <= distance2;
                break;
        }

        if (debugMode)
        {
            string compStr = comparison == ComparisonType.Between
                ? $"{distance1}-{distance2}"
                : $"{comparison} {distance1}";
            context.Log($"DistanceCheck: {distance:F2}m {compStr} = {result}");
        }

        return result ? NodeState.Success : NodeState.Failure;
    }
}


/// <summary>
/// Health Check Condition - Checks health percentage
/// </summary>
public class BTHealthCheckCondition : BTConditionNode
{
    public enum ComparisonType { LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual }

    [SerializeField] private ComparisonType comparison;
    [SerializeField] private float threshold; // 0-1 range

    // Parameterless constructor for serialization
    public BTHealthCheckCondition() : base("HealthCheck") { }

    public BTHealthCheckCondition(
        ComparisonType comparison,
        float threshold,
        string name = null) : base(name ?? "HealthCheck")
    {
        this.comparison = comparison;
        this.threshold = Mathf.Clamp01(threshold);
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        float healthPercent = context.GetHealthPercentage();

        bool result = false;
        switch (comparison)
        {
            case ComparisonType.LessThan:
                result = healthPercent < threshold;
                break;
            case ComparisonType.LessThanOrEqual:
                result = healthPercent <= threshold;
                break;
            case ComparisonType.GreaterThan:
                result = healthPercent > threshold;
                break;
            case ComparisonType.GreaterThanOrEqual:
                result = healthPercent >= threshold;
                break;
        }

        if (debugMode)
            context.Log($"HealthCheck: {healthPercent * 100:F0}% {comparison} {threshold * 100:F0}% = {result}");

        return result ? NodeState.Success : NodeState.Failure;
    }
}


/// <summary>
/// Ability Ready Condition - Checks if ability is off cooldown
/// </summary>
public class BTAbilityReadyCondition : BTConditionNode
{
    [SerializeField] private string abilityId;

    // Parameterless constructor for serialization
    public BTAbilityReadyCondition() : base("AbilityReady") { }

    public BTAbilityReadyCondition(string abilityId, string name = null)
        : base(name ?? $"AbilityReady({abilityId})")
    {
        this.abilityId = abilityId;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        bool isReady = context.IsAbilityReady(abilityId);

        if (debugMode)
            context.Log($"AbilityReady({abilityId}): {isReady}");

        return isReady ? NodeState.Success : NodeState.Failure;
    }
}


/// <summary>
/// Is In Combat Condition - Checks if entity is in combat state
/// </summary>
public class BTIsInCombatCondition : BTConditionNode
{
    // Parameterless constructor for serialization
    public BTIsInCombatCondition() : base("IsInCombat") { }

    public BTIsInCombatCondition(string name) : base(name ?? "IsInCombat") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        bool inCombat = context.IsInCombat();

        if (debugMode)
            context.Log($"IsInCombat: {inCombat}");

        return inCombat ? NodeState.Success : NodeState.Failure;
    }
}


/// <summary>
/// Is Alive Condition - Checks if entity has health > 0
/// </summary>
public class BTIsAliveCondition : BTConditionNode
{
    // Parameterless constructor for serialization
    public BTIsAliveCondition() : base("IsAlive") { }

    public BTIsAliveCondition(string name) : base(name ?? "IsAlive") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        bool alive = context.IsAlive();

        if (debugMode)
            context.Log($"IsAlive: {alive}");

        return alive ? NodeState.Success : NodeState.Failure;
    }
}


/// <summary>
/// Random Chance Condition - Returns success based on probability
/// Useful for adding variety to behavior
/// </summary>
public class BTRandomChanceCondition : BTConditionNode
{
    [SerializeField] private float probability; // 0-1

    // Parameterless constructor for serialization
    public BTRandomChanceCondition() : base("RandomChance") { }

    public BTRandomChanceCondition(float probability, string name = null)
        : base(name ?? $"RandomChance({probability * 100:F0}%)")
    {
        this.probability = Mathf.Clamp01(probability);
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        float roll = Random.value;
        bool success = roll <= probability;

        if (debugMode)
            context.Log($"RandomChance: rolled {roll:F2}, threshold {probability:F2} = {success}");

        return success ? NodeState.Success : NodeState.Failure;
    }
}


/// <summary>
/// Blackboard Check Condition - Checks if blackboard key exists and matches value
/// </summary>
public class BTBlackboardCheckCondition : BTConditionNode
{
    [SerializeField] private string key;
    [SerializeField] private string expectedValueJson; // Serialized as JSON
    private bool checkExistence; // If true, only check if key exists

    // Parameterless constructor for serialization
    public BTBlackboardCheckCondition() : base("BlackboardCheck") { }

    public BTBlackboardCheckCondition(string key, object expectedValue = null, string name = null)
        : base(name ?? $"BlackboardCheck({key})")
    {
        this.key = key;
        this.expectedValueJson = expectedValue != null ? JsonUtility.ToJson(expectedValue) : null;
        this.checkExistence = expectedValue == null;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (checkExistence)
        {
            bool exists = context.HasValue(key);
            if (debugMode)
                context.Log($"BlackboardCheck({key}): exists = {exists}");
            return exists ? NodeState.Success : NodeState.Failure;
        }

        var value = context.GetValue<object>(key);
        var valueJson = value != null ? JsonUtility.ToJson(value) : null;
        bool matches = valueJson == expectedValueJson;

        if (debugMode)
            context.Log($"BlackboardCheck({key}): {valueJson} == {expectedValueJson} = {matches}");

        return matches ? NodeState.Success : NodeState.Failure;
    }
}


/// <summary>
/// Custom Condition - Allows inline delegate for quick conditions
/// Useful for prototyping without creating new condition classes
/// NOTE: Cannot be serialized with delegates - use for code-defined trees only
/// </summary>
public class BTCustomCondition : BTConditionNode
{
    private System.Func<BTContext, bool> conditionFunc;

    // Parameterless constructor for serialization
    public BTCustomCondition() : base("CustomCondition") { }

    public BTCustomCondition(System.Func<BTContext, bool> condition, string name = null)
        : base(name ?? "CustomCondition")
    {
        this.conditionFunc = condition;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (conditionFunc == null)
        {
            context.LogWarning("CustomCondition: No condition function provided");
            return NodeState.Failure;
        }

        bool result = conditionFunc(context);

        if (debugMode)
            context.Log($"CustomCondition({NodeName}): {result}");

        return result ? NodeState.Success : NodeState.Failure;
    }
}