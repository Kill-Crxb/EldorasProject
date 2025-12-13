using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class for composite nodes (nodes with multiple children)
/// Handles child management and execution order
/// </summary>
public abstract class BTCompositeNode : BTNode
{
    protected List<BTNode> children = new List<BTNode>();
    protected int currentChildIndex = 0;

    protected BTCompositeNode(string name = null) : base(name) { }

    /// <summary>
    /// Add a child node
    /// </summary>
    public BTCompositeNode AddChild(BTNode child)
    {
        if (child == null)
        {
            Debug.LogWarning($"[BTCompositeNode:{NodeName}] Attempted to add null child");
            return this;
        }

        child.Parent = this;
        children.Add(child);
        return this; // Fluent API for chaining
    }

    /// <summary>
    /// Add multiple children at once
    /// </summary>
    public BTCompositeNode AddChildren(params BTNode[] newChildren)
    {
        foreach (var child in newChildren)
        {
            AddChild(child);
        }
        return this;
    }

    /// <summary>
    /// Remove a child node
    /// </summary>
    public void RemoveChild(BTNode child)
    {
        if (children.Remove(child))
        {
            child.Parent = null;
        }
    }

    /// <summary>
    /// Clear all children
    /// </summary>
    public void ClearChildren()
    {
        foreach (var child in children)
        {
            child.Parent = null;
        }
        children.Clear();
    }

    /// <summary>
    /// Get all children (readonly)
    /// </summary>
    public IReadOnlyList<BTNode> GetChildren() => children;

    /// <summary>
    /// Reset composite and all children
    /// </summary>
    public override void Reset()
    {
        base.Reset();
        currentChildIndex = 0;
        foreach (var child in children)
        {
            child.Reset();
        }
    }

    /// <summary>
    /// Property for accessing children (used by decorators and factory)
    /// </summary>
    public List<BTNode> Children => children;
}

/// <summary>
/// Sequence Node - AND logic
/// Executes children in order until one fails
/// Returns Success only if ALL children succeed
/// Returns Failure if ANY child fails
/// Returns Running if current child is still running
/// </summary>
public class BTSequence : BTCompositeNode
{
    // Parameterless constructor for serialization
    public BTSequence() : base("Sequence") { }

    public BTSequence(string name) : base(name ?? "Sequence") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        foreach (var child in Children)
        {
            NodeState result = child.Evaluate(context);

            if (result == NodeState.Failure)
                return NodeState.Failure;

            if (result == NodeState.Running)
                return NodeState.Running;
        }

        return NodeState.Success;
    }
}

/// <summary>
/// Selector Node - OR logic
/// Executes children until one succeeds
/// Returns Success if ANY child succeeds
/// Returns Failure only if ALL children fail
/// Returns Running if current child is still running
/// </summary>
public class BTSelector : BTCompositeNode
{
    // Parameterless constructor for serialization
    public BTSelector() : base("Selector") { }

    public BTSelector(string name) : base(name ?? "Selector") { }

    protected override NodeState OnEvaluate(BTContext context)
    {
        foreach (var child in Children)
        {
            NodeState result = child.Evaluate(context);

            if (result == NodeState.Success)
                return NodeState.Success;

            if (result == NodeState.Running)
                return NodeState.Running;
        }

        return NodeState.Failure;
    }
}

/// <summary>
/// Parallel Node - Executes all children simultaneously
/// Policy determines when to return Success/Failure
/// </summary>
public class BTParallel : BTCompositeNode
{
    public enum Policy
    {
        RequireAll,     // All children must succeed
        RequireOne,     // At least one child must succeed
    }

    [SerializeField] private Policy successPolicy = Policy.RequireAll;
    [SerializeField] private Policy failurePolicy = Policy.RequireOne;

    // Parameterless constructor for serialization
    public BTParallel() : base("Parallel") { }

    public BTParallel(string name, Policy successPolicy = Policy.RequireAll, Policy failurePolicy = Policy.RequireOne)
        : base(name ?? "Parallel")
    {
        this.successPolicy = successPolicy;
        this.failurePolicy = failurePolicy;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        int successCount = 0;
        int failureCount = 0;
        int runningCount = 0;

        foreach (var child in Children)
        {
            NodeState result = child.Evaluate(context);

            switch (result)
            {
                case NodeState.Success:
                    successCount++;
                    break;
                case NodeState.Failure:
                    failureCount++;
                    break;
                case NodeState.Running:
                    runningCount++;
                    break;
            }
        }

        // Check failure policy first
        switch (failurePolicy)
        {
            case Policy.RequireOne:
                if (failureCount > 0)
                    return NodeState.Failure;
                break;
            case Policy.RequireAll:
                if (failureCount == Children.Count)
                    return NodeState.Failure;
                break;
        }

        // Check success policy
        switch (successPolicy)
        {
            case Policy.RequireAll:
                if (successCount == Children.Count)
                    return NodeState.Success;
                break;
            case Policy.RequireOne:
                if (successCount > 0)
                    return NodeState.Success;
                break;
        }

        // Still running if any child is running
        if (runningCount > 0)
            return NodeState.Running;

        // Default to running if unclear
        return NodeState.Running;
    }

    public override void Reset()
    {
        base.Reset();
        // Reset all children for parallel execution
        foreach (var child in children)
        {
            child.Reset();
        }
    }
}