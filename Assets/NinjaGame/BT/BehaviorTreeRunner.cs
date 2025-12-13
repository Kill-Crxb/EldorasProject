using UnityEngine;

/// <summary>
/// MonoBehaviour that runs a BehaviorTree every frame
/// Should be placed under Component_Brain or as sibling to ControllerBrain
/// Brain will auto-discover this as an IBrainModule
/// Design: Single responsibility - just execute the tree
/// </summary>
public class BehaviorTreeRunner : MonoBehaviour, IBrainModule
{
    [Header("Behavior Tree Configuration")]
    [SerializeField] private bool autoInitialize = true;
    [SerializeField] private bool runOnStart = true;

    [Header("Execution Control")]
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private float updateInterval = 0f; // 0 = every frame, >0 = interval in seconds

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool logStateChanges = false;

    // References
    private ControllerBrain brain;
    private BehaviorTree tree;

    // Execution timing
    private float lastUpdateTime;
    private NodeState lastState = NodeState.Failure;

    // ========================================
    // IBrainModule Properties
    // ========================================

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    // ========================================
    // Initialization
    // ========================================

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        if (debugMode)
        {
            Debug.Log($"[BehaviorTreeRunner] Initialized on {gameObject.name}");
        }

        if (autoInitialize && tree != null && !tree.IsInitialized())
        {
            // Tree exists but not initialized - initialize it now
            InitializeTree();
        }
    }

    void Start()
    {
        // Brain should be set via Initialize() by ControllerBrain
        // But if not initialized yet, try to find it
        if (brain == null)
        {
            // Try parent first (we're likely under Component_Brain)
            brain = GetComponentInParent<ControllerBrain>();

            // Try siblings
            if (brain == null)
                brain = transform.parent?.GetComponent<ControllerBrain>();

            // Last resort - same GameObject
            if (brain == null)
                brain = GetComponent<ControllerBrain>();

            if (brain == null)
            {
                Debug.LogError($"[BehaviorTreeRunner] No ControllerBrain found on {gameObject.name} or parents! BehaviorTreeRunner must be under or sibling to ControllerBrain.");
                enabled = false;
                return;
            }
        }

        if (runOnStart && tree == null)
        {
            Debug.LogWarning($"[BehaviorTreeRunner] runOnStart is true but no tree assigned on {gameObject.name}");
        }
    }

    // ========================================
    // IBrainModule Update
    // ========================================

    public void UpdateModule()
    {
        if (!isEnabled || tree == null || !tree.IsInitialized())
            return;

        // Check update interval
        if (updateInterval > 0f)
        {
            if (Time.time - lastUpdateTime < updateInterval)
                return;

            lastUpdateTime = Time.time;
        }

        // Execute tree
        ExecuteTree();
    }

    // ========================================
    // Tree Management
    // ========================================

    /// <summary>
    /// Set and initialize a behavior tree
    /// </summary>
    public void SetTree(BehaviorTree newTree)
    {
        if (newTree == null)
        {
            Debug.LogWarning($"[BehaviorTreeRunner] Attempted to set null tree on {gameObject.name}");
            return;
        }

        tree = newTree;
        tree.SetDebug(debugMode);

        if (brain != null && !tree.IsInitialized())
        {
            InitializeTree();
        }

        if (debugMode)
        {
            Debug.Log($"[BehaviorTreeRunner] Tree set: {tree.TreeName} on {gameObject.name}");
        }
    }

    /// <summary>
    /// Get current tree
    /// </summary>
    public BehaviorTree GetTree() => tree;

    /// <summary>
    /// Initialize the current tree
    /// </summary>
    private void InitializeTree()
    {
        if (tree == null || brain == null)
            return;

        var rootNode = tree.GetRootNode();
        if (rootNode == null)
        {
            Debug.LogError($"[BehaviorTreeRunner] Cannot initialize tree '{tree.TreeName}' - no root node");
            return;
        }

        tree.Initialize(brain, rootNode);

        if (debugMode)
        {
            Debug.Log($"[BehaviorTreeRunner] Tree '{tree.TreeName}' initialized on {gameObject.name}");
        }
    }

    /// <summary>
    /// Execute tree evaluation
    /// </summary>
    private void ExecuteTree()
    {
        NodeState currentState = tree.Evaluate();

        // Log state changes
        if (logStateChanges && currentState != lastState)
        {
            Debug.Log($"[BehaviorTreeRunner:{gameObject.name}] State changed: {lastState} → {currentState}");
            lastState = currentState;
        }
    }

    // ========================================
    // Public Control Methods
    // ========================================

    /// <summary>
    /// Reset the behavior tree
    /// </summary>
    public void ResetTree()
    {
        if (tree != null)
        {
            tree.Reset();

            if (debugMode)
            {
                Debug.Log($"[BehaviorTreeRunner] Tree reset on {gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Pause tree execution
    /// </summary>
    public void Pause()
    {
        isEnabled = false;

        if (debugMode)
        {
            Debug.Log($"[BehaviorTreeRunner] Paused on {gameObject.name}");
        }
    }

    /// <summary>
    /// Resume tree execution
    /// </summary>
    public void Resume()
    {
        isEnabled = true;

        if (debugMode)
        {
            Debug.Log($"[BehaviorTreeRunner] Resumed on {gameObject.name}");
        }
    }

    /// <summary>
    /// Get current blackboard context (for external manipulation)
    /// </summary>
    public BTContext GetContext()
    {
        return tree?.GetContext();
    }

    // ========================================
    // Debug Helpers
    // ========================================

    void OnValidate()
    {
        if (tree != null)
        {
            tree.SetDebug(debugMode);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!debugMode || tree == null || !tree.IsInitialized())
            return;

        // Could add Gizmo visualization here for tree state
        // For now, just show if tree is running
        var context = tree.GetContext();
        if (context != null)
        {
            Gizmos.color = isEnabled ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.2f);
        }
    }
#endif
}