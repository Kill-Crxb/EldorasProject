using UnityEngine;

/// <summary>
/// Node execution state returned by Evaluate()
/// </summary>
public enum NodeState
{
    Running,  // Node still executing (async actions, waiting, etc.)
    Success,  // Node completed successfully
    Failure   // Node failed (condition not met, action impossible, etc.)
}

/// <summary>
/// Base class for all Behavior Tree nodes
/// Design: Stateless when possible - context provides all data
/// </summary>
public abstract class BTNode
{
    // Node identification
    public string NodeName { get; set; } = "Unnamed Node";
    public string NodeId { get; set; } // GUID for serialization

    // Tree structure
    public BTNode Parent { get; set; }

    // Execution tracking
    protected NodeState currentState = NodeState.Failure;

    // Debug
    protected bool debugMode = false;

    /// <summary>
    /// Constructor with optional name
    /// </summary>
    protected BTNode(string name = null)
    {
        NodeName = name ?? GetType().Name;
        NodeId = System.Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Main evaluation method - call this to execute the node
    /// Returns NodeState representing current execution status
    /// </summary>
    public NodeState Evaluate(BTContext context)
    {
        if (debugMode)
        {
            Debug.Log($"[BTNode] Evaluating: {NodeName} ({GetType().Name})");
        }

        currentState = OnEvaluate(context);

        if (debugMode)
        {
            Debug.Log($"[BTNode] {NodeName} returned: {currentState}");
        }

        return currentState;
    }

    /// <summary>
    /// Override this in derived classes to implement node behavior
    /// </summary>
    protected abstract NodeState OnEvaluate(BTContext context);

    /// <summary>
    /// Called when node starts executing (first Evaluate after being idle)
    /// Override for initialization logic
    /// </summary>
    protected virtual void OnEnter(BTContext context) { }

    /// <summary>
    /// Called when node finishes executing (Success or Failure)
    /// Override for cleanup logic
    /// </summary>
    protected virtual void OnExit(BTContext context) { }

    /// <summary>
    /// Reset node state (useful for decorators and stateful nodes)
    /// </summary>
    public virtual void Reset()
    {
        currentState = NodeState.Failure;
    }

    /// <summary>
    /// Enable/disable debug logging for this node
    /// </summary>
    public void SetDebug(bool enabled)
    {
        debugMode = enabled;
    }

    /// <summary>
    /// Get current state (for debugging/visualization)
    /// </summary>
    public NodeState GetCurrentState() => currentState;
}
