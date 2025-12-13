using UnityEngine;

/// <summary>
/// AI_System - Coordinates all AI-related components for NPCs
/// Handles behavior, perception, and decision making
/// Separate from NPC identity (which is in IdentityProviderCoordinator)
/// 
/// Structure:
/// - AIModule: State management, target tracking
/// - AITargetDetection: Perception, vision, faction checking
/// - BehaviorTreeRunner: Decision making via Behavior Trees
/// - [Combat Behavior]: Dynamically added by archetype
/// </summary>
public class AI_System : ProviderCoordinator
{
    [Header("Core AI Components")]
    [Tooltip("AIModule - State management and target tracking")]
    [SerializeField] private AIModule aiModule;

    [Tooltip("AITargetDetection - Perception and vision system")]
    [SerializeField] private AITargetDetection targetDetection;

    [Tooltip("BehaviorTreeRunner - Executes behavior tree decisions")]
    [SerializeField] private BehaviorTreeRunner behaviorTree;

    [Header("Optional AI Components")]
    [Tooltip("AIStateUpdater - Legacy state transitions (will be deprecated by BT)")]
    [SerializeField] private AIStateUpdater stateUpdater;

    [Tooltip("Combat Behavior - Usually added dynamically by archetype")]
    [SerializeField] private MonoBehaviour combatBehaviorComponent;

    // Cached interface reference for combat behavior
    private IAICombatBehavior combatBehavior;

    // Public accessors
    public AIModule AI => aiModule;
    public AITargetDetection TargetDetection => targetDetection;
    public BehaviorTreeRunner BehaviorTree => behaviorTree;
    public AIStateUpdater StateUpdater => stateUpdater;
    public IAICombatBehavior CombatBehavior => combatBehavior;

    // Convenience properties
    public bool HasBehaviorTree => behaviorTree != null;
    public bool HasCombatBehavior => combatBehavior != null;
    public Transform CurrentTarget => aiModule?.CurrentTarget;
    public AIState CurrentState => aiModule?.CurrentState ?? AIState.Idle;

    protected override bool ValidateSlots()
    {
        bool valid = true;

        // Required: AIModule
        if (aiModule == null)
        {
            Debug.LogError($"[AI_System] AIModule is REQUIRED! Assign AIModule component.", this);
            valid = false;
        }

        // Recommended: AITargetDetection
        if (targetDetection == null)
        {
            Debug.LogWarning($"[AI_System] No AITargetDetection assigned - AI won't detect targets automatically.", this);
        }

        // Optional: BehaviorTreeRunner
        if (behaviorTree == null)
        {
            Debug.LogWarning($"[AI_System] No BehaviorTreeRunner assigned - BT system won't work.", this);
        }

        // Optional: AIStateUpdater (legacy, will be deprecated)
        // No validation needed - it's optional

        // Optional: Combat Behavior (can be added dynamically)
        if (combatBehaviorComponent != null && !(combatBehaviorComponent is IAICombatBehavior))
        {
            Debug.LogWarning($"[AI_System] Combat Behavior must implement IAICombatBehavior!", this);
            combatBehaviorComponent = null;
        }

        return valid;
    }

    protected override void CacheProviders()
    {
        // Cache combat behavior interface if component is assigned
        if (combatBehaviorComponent != null)
        {
            combatBehavior = combatBehaviorComponent as IAICombatBehavior;
        }

        if (Application.isPlaying)
        {
            // Log what's configured
            Debug.Log($"[AI_System] Configured on {brain.name}:\n" +
                     $"  AIModule: {(aiModule != null ? "✓" : "✗")}\n" +
                     $"  TargetDetection: {(targetDetection != null ? "✓" : "✗")}\n" +
                     $"  BehaviorTree: {(behaviorTree != null ? "✓" : "✗")}\n" +
                     $"  StateUpdater: {(stateUpdater != null ? "✓ (legacy)" : "✗")}\n" +
                     $"  CombatBehavior: {(combatBehavior != null ? "✓" : "○ (will be added by archetype)")}");
        }
    }

    protected override void OnInitialized()
    {
        // Initialize all AI components
        if (aiModule is IBrainModule m1) m1.Initialize(brain);
        if (targetDetection is IBrainModule m2) m2.Initialize(brain);
        if (behaviorTree is IBrainModule m3) m3.Initialize(brain);
        if (stateUpdater is IBrainModule m4) m4.Initialize(brain);
        if (combatBehaviorComponent is IBrainModule m5) m5.Initialize(brain);
    }

    /// <summary>
    /// Set combat behavior dynamically (called by NPCConfigurationHandler)
    /// </summary>
    public void SetCombatBehavior(IAICombatBehavior behavior)
    {
        combatBehavior = behavior;
        combatBehaviorComponent = behavior as MonoBehaviour;

        if (behavior is IBrainModule module && brain != null)
        {
            module.Initialize(brain);
        }

        Debug.Log($"[AI_System] Combat behavior set: {behavior?.GetType().Name ?? "null"}");
    }

    /// <summary>
    /// Set behavior tree dynamically (for runtime tree assignment)
    /// </summary>
    public void SetBehaviorTree(BehaviorTree tree)
    {
        if (behaviorTree == null)
        {
            Debug.LogWarning($"[AI_System] No BehaviorTreeRunner assigned - cannot set tree!");
            return;
        }

        behaviorTree.SetTree(tree);
        Debug.Log($"[AI_System] Behavior tree set: {tree.TreeName}");
    }

    /// <summary>
    /// Enable or disable AI system
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        if (aiModule != null)
            aiModule.IsEnabled = enabled;

        if (behaviorTree != null)
            behaviorTree.IsEnabled = enabled;

        Debug.Log($"[AI_System] AI {(enabled ? "enabled" : "disabled")}");
    }

    /// <summary>
    /// Pause AI (useful for cutscenes, dialogue, etc.)
    /// </summary>
    public void Pause()
    {
        SetEnabled(false);
    }

    /// <summary>
    /// Resume AI
    /// </summary>
    public void Resume()
    {
        SetEnabled(true);
    }

    /// <summary>
    /// Set target manually (useful for scripted events)
    /// </summary>
    public void SetTarget(Transform target)
    {
        if (aiModule != null)
        {
            // AIModule will need a public SetTarget method
            // For now, this is a placeholder
            Debug.Log($"[AI_System] Target set: {target?.name ?? "null"}");
        }

        // Also update BT context if available
        if (behaviorTree != null)
        {
            var context = behaviorTree.GetContext();
            context?.SetTarget(target);
        }
    }

    /// <summary>
    /// Clear current target
    /// </summary>
    public void ClearTarget()
    {
        SetTarget(null);
    }
}