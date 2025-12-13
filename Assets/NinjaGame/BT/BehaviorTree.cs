using UnityEngine;

/// <summary>
/// Container for a complete behavior tree
/// Holds root node and provides evaluation interface
/// </summary>
public class BehaviorTree
{
    // Tree structure
    private BTNode rootNode;
    private BTContext context;

    // Metadata
    public string TreeName { get; set; } = "Unnamed Tree";
    public string TreeId { get; private set; }

    // State
    private bool isInitialized = false;

    // Debug
    private bool debugMode = false;

    /// <summary>
    /// Constructor
    /// </summary>
    public BehaviorTree(string treeName = null)
    {
        TreeName = treeName ?? "Behavior Tree";
        TreeId = System.Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Initialize tree with brain and root node
    /// </summary>
    public void Initialize(ControllerBrain brain, BTNode root)
    {
        if (brain == null)
        {
            Debug.LogError($"[BehaviorTree:{TreeName}] Cannot initialize with null brain");
            return;
        }

        if (root == null)
        {
            Debug.LogError($"[BehaviorTree:{TreeName}] Cannot initialize with null root node");
            return;
        }

        context = new BTContext(brain);
        rootNode = root;
        isInitialized = true;

        if (debugMode)
        {
            Debug.Log($"[BehaviorTree:{TreeName}] Initialized successfully with root: {rootNode.NodeName}");
        }
    }

    /// <summary>
    /// Evaluate the tree (call this every frame from BehaviorTreeRunner)
    /// </summary>
    public NodeState Evaluate()
    {
        if (!isInitialized)
        {
            Debug.LogError($"[BehaviorTree:{TreeName}] Cannot evaluate - tree not initialized");
            return NodeState.Failure;
        }

        if (rootNode == null)
        {
            Debug.LogError($"[BehaviorTree:{TreeName}] Root node is null");
            return NodeState.Failure;
        }

        return rootNode.Evaluate(context);
    }

    /// <summary>
    /// Set root node
    /// </summary>
    public void SetRootNode(BTNode root)
    {
        rootNode = root;
    }

    /// <summary>
    /// Get root node
    /// </summary>
    public BTNode GetRootNode() => rootNode;

    /// <summary>
    /// Get context (for external systems to modify blackboard)
    /// </summary>
    public BTContext GetContext() => context;

    /// <summary>
    /// Reset entire tree
    /// </summary>
    public void Reset()
    {
        if (rootNode != null)
        {
            rootNode.Reset();
        }

        if (context != null)
        {
            context.ClearBlackboard();
        }

        if (debugMode)
        {
            Debug.Log($"[BehaviorTree:{TreeName}] Tree reset");
        }
    }

    /// <summary>
    /// Enable/disable debug mode
    /// </summary>
    public void SetDebug(bool enabled)
    {
        debugMode = enabled;
    }

    /// <summary>
    /// Check if tree is initialized
    /// </summary>
    public bool IsInitialized() => isInitialized;
}
