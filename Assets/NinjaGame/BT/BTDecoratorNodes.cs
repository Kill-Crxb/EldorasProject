using UnityEngine;

/// <summary>
/// Base class for decorator nodes (nodes with single child that modify behavior)
/// </summary>
public abstract class BTDecoratorNode : BTNode
{
    protected BTNode child;

    protected BTDecoratorNode(BTNode child = null, string name = null) : base(name)
    {
        SetChild(child);
    }

    /// <summary>
    /// Set the child node
    /// </summary>
    public BTDecoratorNode SetChild(BTNode newChild)
    {
        if (newChild != null)
        {
            child = newChild;
            child.Parent = this;
        }
        return this;
    }

    /// <summary>
    /// Get the child node
    /// </summary>
    public BTNode GetChild() => child;

    public override void Reset()
    {
        base.Reset();
        child?.Reset();
    }
}


/// <summary>
/// Inverter - Inverts child result
/// Success → Failure, Failure → Success, Running → Running
/// </summary>
public class BTInverter : BTDecoratorNode
{
    // Parameterless constructor for serialization
    public BTInverter() : base(null, "Inverter") { }

    public BTInverter(BTNode child, string name = null)
        : base(child, name ?? "Inverter") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (child == null)
        {
            context.LogWarning($"Inverter '{NodeName}' has no child");
            return NodeState.Failure;
        }

        var childState = child.Evaluate(context);

        switch (childState)
        {
            case NodeState.Success:
                return NodeState.Failure;
            case NodeState.Failure:
                return NodeState.Success;
            case NodeState.Running:
                return NodeState.Running;
            default:
                return NodeState.Failure;
        }
    }
}


/// <summary>
/// Repeater - Repeats child N times or indefinitely
/// Returns Running while repeating, Success when complete
/// </summary>
public class BTRepeater : BTDecoratorNode
{
    [SerializeField] private int repeatCount = -1; // -1 for infinite
    private int currentCount;

    // Parameterless constructor for serialization
    public BTRepeater() : base(null, "Repeater") { }

    public BTRepeater(BTNode child, int repeatCount = -1, string name = null)
        : base(child, name ?? "Repeater")
    {
        this.repeatCount = repeatCount;
        this.currentCount = 0;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (child == null)
        {
            context.LogWarning($"Repeater '{NodeName}' has no child");
            return NodeState.Failure;
        }

        // Infinite repeat
        if (repeatCount < 0)
        {
            child.Evaluate(context);
            return NodeState.Running;
        }

        // Limited repeat
        if (currentCount < repeatCount)
        {
            var childState = child.Evaluate(context);

            if (childState == NodeState.Success || childState == NodeState.Failure)
            {
                currentCount++;
                child.Reset(); // Reset for next iteration
            }

            return currentCount >= repeatCount ? NodeState.Success : NodeState.Running;
        }

        return NodeState.Success;
    }

    public override void Reset()
    {
        base.Reset();
        currentCount = 0;
    }
}


/// <summary>
/// Cooldown - Prevents child execution until cooldown expires
/// Returns Failure if on cooldown, otherwise evaluates child
/// </summary>
public class BTCooldown : BTDecoratorNode
{
    [SerializeField] private float cooldownDuration = 1f;
    private float lastExecutionTime = -999f;

    // Parameterless constructor for serialization
    public BTCooldown() : base(null, "Cooldown") { }

    public BTCooldown(BTNode child, float cooldownDuration = 1f, string name = null)
        : base(child, name ?? "Cooldown")
    {
        this.cooldownDuration = cooldownDuration;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (child == null)
        {
            context.LogWarning($"Cooldown '{NodeName}' has no child");
            return NodeState.Failure;
        }

        // Check if cooldown has expired
        float currentTime = Time.time;
        if (currentTime - lastExecutionTime < cooldownDuration)
        {
            if (debugMode)
            {
                float remaining = cooldownDuration - (currentTime - lastExecutionTime);
                context.Log($"Cooldown '{NodeName}' active: {remaining:F2}s remaining");
            }
            return NodeState.Failure;
        }

        // Execute child
        var childState = child.Evaluate(context);

        // Record execution time when child completes
        if (childState == NodeState.Success || childState == NodeState.Failure)
        {
            lastExecutionTime = currentTime;
        }

        return childState;
    }

    public override void Reset()
    {
        base.Reset();
        lastExecutionTime = -999f; // Allow immediate execution after reset
    }
}


/// <summary>
/// UntilSuccess - Repeats child until it returns Success
/// Returns Running while child is Failure or Running
/// Returns Success when child succeeds
/// </summary>
public class BTUntilSuccess : BTDecoratorNode
{
    // Parameterless constructor for serialization
    public BTUntilSuccess() : base(null, "UntilSuccess") { }

    public BTUntilSuccess(BTNode child, string name = null)
        : base(child, name ?? "UntilSuccess") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (child == null)
        {
            context.LogWarning($"UntilSuccess '{NodeName}' has no child");
            return NodeState.Failure;
        }

        var childState = child.Evaluate(context);

        if (childState == NodeState.Success)
        {
            return NodeState.Success;
        }

        // Keep trying
        return NodeState.Running;
    }
}


/// <summary>
/// UntilFailure - Repeats child until it returns Failure
/// Returns Running while child is Success or Running
/// Returns Success when child fails (we wanted it to eventually fail)
/// </summary>
public class BTUntilFailure : BTDecoratorNode
{
    // Parameterless constructor for serialization
    public BTUntilFailure() : base(null, "UntilFailure") { }

    public BTUntilFailure(BTNode child, string name = null)
        : base(child, name ?? "UntilFailure") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (child == null)
        {
            context.LogWarning($"UntilFailure '{NodeName}' has no child");
            return NodeState.Failure;
        }

        var childState = child.Evaluate(context);

        if (childState == NodeState.Failure)
        {
            return NodeState.Success; // Child failed as we wanted
        }

        // Keep trying
        return NodeState.Running;
    }
}


/// <summary>
/// Succeeder - Always returns Success regardless of child result
/// Useful for making optional branches that don't block parent sequences
/// </summary>
public class BTSucceeder : BTDecoratorNode
{
    // Parameterless constructor for serialization
    public BTSucceeder() : base(null, "Succeeder") { }

    public BTSucceeder(BTNode child, string name = null)
        : base(child, name ?? "Succeeder") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (child != null)
        {
            child.Evaluate(context);
        }
        return NodeState.Success;
    }
}


/// <summary>
/// Failer - Always returns Failure regardless of child result
/// Useful for testing or creating guaranteed failure paths
/// </summary>
public class BTFailer : BTDecoratorNode
{
    // Parameterless constructor for serialization
    public BTFailer() : base(null, "Failer") { }

    public BTFailer(BTNode child, string name = null)
        : base(child, name ?? "Failer") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (child != null)
        {
            child.Evaluate(context);
        }
        return NodeState.Failure;
    }
}


/// <summary>
/// TimeLimit - Enforces maximum execution time on child
/// Returns Failure if child exceeds time limit
/// </summary>
public class BTTimeLimit : BTDecoratorNode
{
    [SerializeField] private float timeLimit = 5f;
    private float startTime = -1f;

    // Parameterless constructor for serialization
    public BTTimeLimit() : base(null, "TimeLimit") { }

    public BTTimeLimit(BTNode child, float timeLimit = 5f, string name = null)
        : base(child, name ?? "TimeLimit")
    {
        this.timeLimit = timeLimit;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (child == null)
        {
            context.LogWarning($"TimeLimit '{NodeName}' has no child");
            return NodeState.Failure;
        }

        // Start timer on first execution
        if (startTime < 0f)
        {
            startTime = Time.time;
        }

        // Check time limit
        float elapsed = Time.time - startTime;
        if (elapsed >= timeLimit)
        {
            if (debugMode)
            {
                context.LogWarning($"TimeLimit '{NodeName}' exceeded: {elapsed:F2}s / {timeLimit:F2}s");
            }
            startTime = -1f; // Reset timer
            return NodeState.Failure;
        }

        // Execute child
        var childState = child.Evaluate(context);

        // Reset timer when child completes
        if (childState == NodeState.Success || childState == NodeState.Failure)
        {
            startTime = -1f;
        }

        return childState;
    }

    public override void Reset()
    {
        base.Reset();
        startTime = -1f;
    }
}