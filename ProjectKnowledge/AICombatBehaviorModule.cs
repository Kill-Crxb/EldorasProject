using UnityEngine;

/// <summary>
/// Base class for AI combat behaviors
/// Place under Component_AI alongside GOAPModule
/// 
/// UPDATED: Removed AIModule and NPCMovementModule dependencies
/// Movement is now automatic via MovementSystem + AIControlSource
/// Combat behaviors focus only on attack logic
/// </summary>
public abstract class AICombatBehaviorModule : MonoBehaviour, IAICombatBehavior, IBrainModule
{
    [Header("Combat Behavior Settings")]
    [SerializeField] protected bool isEnabled = true;

    [Header("Attack Settings")]
    [SerializeField] protected float attackCooldown = 1.5f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float exitCombatRange = 3f;

    [Header("Debug")]
    [SerializeField] protected bool showDebugInfo = true;
    [SerializeField] protected bool debugMode = false;

    // Module references
    protected ControllerBrain brain;
    protected MovementSystem movementSystem; // UPDATED: Use MovementSystem instead of NPCMovementModule

    // Timing
    protected float lastAttackTime;

    #region IBrainModule Properties

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    #endregion

    #region IAICombatBehavior Properties

    public float AttackRange => attackRange;
    public float ExitCombatRange => exitCombatRange;

    #endregion

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Get MovementSystem (replaces NPCMovementModule)
        movementSystem = brain.GetModule<MovementSystem>();

        if (movementSystem == null)
            Debug.LogWarning($"[{GetType().Name}] MovementSystem not found on {gameObject.name}");

        OnInitialize();
    }

    public void UpdateModule()
    {
        // This is called by ControllerBrain, but we don't need it
        // Combat behavior is updated by GOAP goals or AISystem when in combat
    }

    #endregion

    #region IAICombatBehavior Implementation

    // REMOVED: AIModule parameter - no longer used
    // public virtual void Initialize(AIModule aiModule, ControllerBrain brain)

    /// <summary>
    /// Override for custom initialization in derived classes
    /// </summary>
    protected virtual void OnInitialize() { }

    public abstract void UpdateCombat(Transform target);

    public virtual bool CanEnterCombat()
    {
        return isEnabled;
    }

    public virtual bool CanAttack()
    {
        return isEnabled && IsAttackReady();
    }

    /// <summary>
    /// Execute attack - Interface requirement (parameterless)
    /// Override this in derived classes OR override ExecuteAttack(Transform target)
    /// </summary>
    public virtual void ExecuteAttack()
    {
        // Default implementation - calls overload with null target
        ExecuteAttack(null);
    }

    /// <summary>
    /// Execute attack with target parameter - Preferred overload
    /// Override this in derived classes for target-aware attacks
    /// </summary>
    public virtual void ExecuteAttack(Transform target)
    {
        // Default implementation - override in derived classes
        if (debugMode)
        {
            Debug.LogWarning($"[AICombatBehaviorModule] ExecuteAttack(Transform) called but not overridden in {GetType().Name}");
        }
    }

    public virtual bool ShouldExitCombat(Transform target)
    {
        if (target == null) return true;

        float distance = Vector3.Distance(transform.position, target.position);
        return distance > exitCombatRange;
    }

    public virtual void OnCombatEnter(Transform target)
    {
        if (showDebugInfo)
            Debug.Log($"[{GetType().Name}] {gameObject.name} entering combat with {target.name}");

        lastAttackTime = Time.time - attackCooldown; // Allow immediate attack
    }

    public virtual void OnCombatExit()
    {
        if (showDebugInfo)
            Debug.Log($"[{GetType().Name}] {gameObject.name} exiting combat");
    }

    #endregion

    #region Protected Helper Methods

    protected bool IsAttackReady()
    {
        return Time.time >= lastAttackTime + attackCooldown;
    }

    protected void RecordAttack()
    {
        lastAttackTime = Time.time;
    }

    #endregion
}