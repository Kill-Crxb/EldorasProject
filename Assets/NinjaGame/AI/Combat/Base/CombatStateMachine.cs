using UnityEngine;
using RPG.Tactical;

/// <summary>
/// Base combat state machine for enemy AI.
/// Provides clean separation between positioning-based states and free combat states.
/// </summary>
public abstract class CombatStateMachine : AICombatBehaviorModule
{
    [Header("State Machine Settings")]
    [SerializeField] protected bool usePositioningSystem = true;
    [SerializeField] protected int entityPriority = 0;

    [Header("Debug")]
    [SerializeField] protected bool debugStateMachine = true; // Re-enabled for diagnosis

    // Current state
    protected ICombatState currentState;
    protected string currentStateName;

    // Positioning system
    protected TacticalPositioningSystem targetPositioningSystem;
    protected TacticalPoint assignedPoint;

    // Public properties for state access
    public TacticalPoint AssignedPoint => assignedPoint;
    public bool DebugStateMachine => debugStateMachine;

    // Providers
    protected IAbilityProvider abilityProvider;
    protected IMovementProvider movementProvider;
    protected IHealthProvider healthProvider;
    protected IHealthProvider targetHealthProvider;
    protected IResourceProvider resourceProvider;
    protected Animator animator;

    // State timing
    protected float stateEnterTime;
    protected float pointArrivalTime;
    protected float pointDwellDuration;
    protected bool hasArrivedAtPoint;

    #region Initialization

    protected override void OnInitialize()
    {
        base.OnInitialize();

        // Get providers
        abilityProvider = brain.GetModuleImplementing<IAbilityProvider>();
        movementProvider = brain.GetModuleImplementing<IMovementProvider>();
        healthProvider = brain.GetModuleImplementing<IHealthProvider>();
        resourceProvider = brain.GetModuleImplementing<IResourceProvider>();
        animator = brain.GetComponentInChildren<Animator>();

        if (abilityProvider == null)
        {
            Debug.LogError($"[CombatStateMachine] No IAbilityProvider found on {gameObject.name}");
            isEnabled = false;
            return;
        }

        // Initialize to starting state
        InitializeStates();

        if (debugStateMachine)
        {
            Debug.Log($"[CombatStateMachine] Initialized on {gameObject.name}");
        }
    }

    /// <summary>
    /// Override this to set up initial state
    /// </summary>
    protected abstract void InitializeStates();

    #endregion

    #region State Management

    public void ChangeState(ICombatState newState, string stateName)
    {
        if (currentState == newState) return;

        // Exit current state
        currentState?.OnExit();

        // Change state
        ICombatState previousState = currentState;
        string previousStateName = currentStateName;

        currentState = newState;
        currentStateName = stateName;
        stateEnterTime = Time.time;

        // Reset point tracking
        hasArrivedAtPoint = false;
        pointArrivalTime = 0f;
        pointDwellDuration = 0f;

        // Enter new state
        currentState?.OnEnter();

        if (debugStateMachine)
        {
            Debug.Log($"[CombatStateMachine] {gameObject.name}: {previousStateName} → {currentStateName}");
        }
    }

    public string GetCurrentStateName() => currentStateName;
    public float GetTimeInState() => Time.time - stateEnterTime;

    #endregion

    #region Combat Update

    public override void UpdateCombat(Transform target)
    {
        if (!isEnabled || target == null || currentState == null) return;

        // Find/cache positioning system
        if (usePositioningSystem && targetPositioningSystem == null)
        {
            targetPositioningSystem = target.GetComponent<TacticalPositioningSystem>();
        }

        // Update current state
        currentState.UpdateState(this, target);

        // Check for state transitions
        CheckStateTransitions(target);
    }

    /// <summary>
    /// Override this to define state transition logic
    /// </summary>
    protected abstract void CheckStateTransitions(Transform target);

    #endregion

    #region Positioning System Helpers (Public for States)

    /// <summary>
    /// Request a tactical point with role and preference
    /// </summary>
    public TacticalPoint RequestTacticalPoint(TacticalRole role, PointPreference preference = null)
    {
        if (!usePositioningSystem || targetPositioningSystem == null)
            return null;

        preference = preference ?? PointPreference.Any;

        TacticalPoint requestedPoint = targetPositioningSystem.RequestPoint(
            gameObject,
            role,
            preference,
            entityPriority
        );

        if (requestedPoint != assignedPoint)
        {
            assignedPoint = requestedPoint;
            hasArrivedAtPoint = false;
            pointArrivalTime = 0f;

            if (debugStateMachine && assignedPoint != null)
            {
                Debug.Log($"[CombatStateMachine] Assigned to {assignedPoint.Direction}/{assignedPoint.Ring}");
            }
        }

        return assignedPoint;
    }

    /// <summary>
    /// Check if entity has arrived at assigned point
    /// </summary>
    public bool CheckPointArrival(float threshold = 1.5f)
    {
        if (assignedPoint == null) return false;
        if (hasArrivedAtPoint) return true;

        float distance = Vector3.Distance(transform.position, assignedPoint.WorldPosition);

        if (distance <= threshold)
        {
            if (!hasArrivedAtPoint)
            {
                hasArrivedAtPoint = true;
                pointArrivalTime = Time.time;
                pointDwellDuration = Random.Range(2f, 4f); // Increased: 2-4 seconds (was 1-3)

                if (debugStateMachine)
                {
                    Debug.Log($"[CombatStateMachine] Arrived at point, dwelling for {pointDwellDuration:F1}s");
                }
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if dwell time at point is complete
    /// </summary>
    public bool IsDwellComplete()
    {
        if (!hasArrivedAtPoint) return false;
        return Time.time - pointArrivalTime >= pointDwellDuration;
    }

    /// <summary>
    /// Get adjacent point direction (for strafing in quadrant)
    /// </summary>
    public PointDirection GetAdjacentDirection(PointDirection current, bool clockwise = true)
    {
        return TacticalUtility.GetNextDirection(current, clockwise);
    }

    /// <summary>
    /// Release current tactical point
    /// </summary>
    public void ReleaseTacticalPoint()
    {
        if (usePositioningSystem && targetPositioningSystem != null && assignedPoint != null)
        {
            targetPositioningSystem.ReleasePoint(gameObject);
            assignedPoint = null;
            hasArrivedAtPoint = false;
        }
    }

    #endregion

    #region Helper Methods (Public for States)

    public float GetHealthPercent()
    {
        if (healthProvider == null) return 1f;
        float current = healthProvider.GetCurrentHealth();
        float max = healthProvider.GetMaxHealth();
        return max > 0 ? current / max : 1f;
    }

    public float GetTargetHealthPercent(Transform target)
    {
        if (target == null) return 1f;

        if (targetHealthProvider == null)
        {
            var targetBrain = target.GetComponent<ControllerBrain>();
            if (targetBrain != null)
            {
                targetHealthProvider = targetBrain.GetModuleImplementing<IHealthProvider>();
            }
        }

        if (targetHealthProvider == null) return 1f;

        float current = targetHealthProvider.GetCurrentHealth();
        float max = targetHealthProvider.GetMaxHealth();
        return max > 0 ? current / max : 1f;
    }

    public float GetStaminaPercent()
    {
        if (resourceProvider == null) return 1f;
        float current = resourceProvider.GetResource(ResourceType.Stamina);
        float max = resourceProvider.GetMaxResource(ResourceType.Stamina);
        return max > 0 ? current / max : 1f;
    }

    public float GetDistanceToTarget(Transform target)
    {
        if (target == null) return float.MaxValue;
        return Vector3.Distance(transform.position, target.position);
    }

    #endregion

    #region Combat Enter/Exit

    public override void OnCombatEnter(Transform target)
    {
        base.OnCombatEnter(target);

        // Reset positioning
        assignedPoint = null;
        targetHealthProvider = null;
        targetPositioningSystem = null;
        hasArrivedAtPoint = false;

        if (debugStateMachine)
        {
            Debug.Log($"[CombatStateMachine] {gameObject.name} entered combat");
        }
    }

    public override void OnCombatExit()
    {
        base.OnCombatExit();

        // Release tactical point
        ReleaseTacticalPoint();

        // Exit current state
        currentState?.OnExit();

        if (debugStateMachine)
        {
            Debug.Log($"[CombatStateMachine] {gameObject.name} exited combat");
        }
    }

    #endregion
}

/// <summary>
/// Interface for combat states
/// </summary>
public interface ICombatState
{
    void OnEnter();
    void UpdateState(CombatStateMachine machine, Transform target);
    void OnExit();
}