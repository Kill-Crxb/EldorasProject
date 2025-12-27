using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Goal selection modes for different AI personalities
/// </summary>
public enum GoalSelectionMode
{
    HighestWeight,      // Always pick best option (deterministic, predictable)
    WeightedRandom,     // Full randomness weighted by scores (unpredictable)
    TopNWeightedRandom  // Random among top N options (smart variety)
}

/// <summary>
/// GOAP Module - Goal-Oriented Action Planning AI System
/// 
/// Replaces AIModule for entities using GOAP-based decision making.
/// 
/// Architecture:
/// - Evaluates goal pool every frame
/// - Selects best goal based on selection mode
/// - Executes active goal
/// - Handles goal transitions and interrupts
/// 
/// Features:
/// - Anti-thrashing (minimum goal duration, stickiness bonus)
/// - Multiple selection modes (deterministic, random, top-N random)
/// - Interrupt system (evade, react to threats)
/// - Goal stack (resume after interrupt)
/// - Busy watchdog (automatic deadlock recovery)
/// - Type-safe goal references (drag-and-drop in Inspector)
/// 
/// Usage:
/// 1. Add GOAPModule to Component_AI
/// 2. Assign goal pool (ScriptableObjects)
/// 3. Configure selection mode and anti-thrashing settings
/// 4. Set special goal references (evade, rush, etc) if needed
/// </summary>
public class GOAPModule : MonoBehaviour, IBrainModule
{
    #region Serialized Fields

    [Header("Module Settings")]
    [Tooltip("Enable/disable this module")]
    public bool isEnabled = true;

    [Header("Goal Pool")]
    [Tooltip("All goals this AI can choose from")]
    public List<GOAPGoal> goalPool = new List<GOAPGoal>();

    [Header("Selection Policy")]
    [Tooltip("How to select goals from available options")]
    [SerializeField] private GoalSelectionMode selectionMode = GoalSelectionMode.WeightedRandom;

    [Tooltip("For TopNWeightedRandom: How many top goals to consider")]
    [SerializeField] private int topNCount = 3;

    [Header("Anti-Thrashing System")]
    [Tooltip("Require goals to run for minimum duration before switching")]
    [SerializeField] private bool useGoalCommitment = true;

    [Tooltip("Minimum duration in seconds (can be overridden per-goal)")]
    [SerializeField] private float minimumGoalDuration = 0.5f;

    [Tooltip("Weight bonus multiplier for current goal (prevents ping-ponging)")]
    [SerializeField] private float currentGoalStickinessBonus = 1.3f;

    [Header("Interrupt System")]
    [Tooltip("Check for interrupt goals even when executing normal goals")]
    public bool checkInterrupts = true;

    [Tooltip("Allow nested interrupts (interrupt an interrupt)?")]
    [SerializeField] private bool allowNestedInterrupts = false;

    [Tooltip("Maximum interrupt stack depth")]
    [SerializeField] private int maxInterruptStackDepth = 1;

    [Header("Special Goals (Type-Safe References)")]
    [Tooltip("Drag-and-drop evade goal for interrupt system")]
    [SerializeField] private GOAPGoal evadeGoal;

    [Tooltip("Drag-and-drop rush down goal for aggressive interrupt")]
    [SerializeField] private GOAPGoal rushDownGoal;

    [Header("Stack Management")]
    [Tooltip("Resume previous goal after interrupt completes?")]
    [SerializeField] private bool resumeAfterInterrupt = true;

    [Tooltip("How long to remember interrupted goal (seconds)")]
    [SerializeField] private float interruptContextTimeout = 2f;

    [Header("Debug")]
    [Tooltip("Log goal selection and transitions")]
    public bool debugMode = false;

    #endregion

    #region Private State

    // Core references
    private ControllerBrain brain;
    private GOAPContext context;

    // Current execution state
    private GOAPGoal currentGoal;
    private GOAPGoal interruptGoal;
    private Stack<GOAPGoal> goalStack = new Stack<GOAPGoal>();

    // Timing
    private float goalStartTime;
    private float interruptStartTime;
    private float lastGoalChangeTime;

    // Cached evaluations
    private List<(GOAPGoal goal, float weight)> evaluatedGoals = new List<(GOAPGoal, float)>();

    #endregion

    #region Properties

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    // Public properties for external configuration (e.g., from NPCConfigurationHandler)
    public GoalSelectionMode SelectionMode
    {
        get => selectionMode;
        set => selectionMode = value;
    }

    public bool UseGoalCommitment
    {
        get => useGoalCommitment;
        set => useGoalCommitment = value;
    }

    public float MinimumGoalDuration
    {
        get => minimumGoalDuration;
        set => minimumGoalDuration = value;
    }

    public GOAPGoal CurrentGoal => interruptGoal != null ? interruptGoal : currentGoal;
    public bool IsExecutingInterrupt => interruptGoal != null;
    public Transform CurrentTarget => context?.target;
    public GOAPContext GetContext() => context;

    #endregion

    #region Public Configuration API

    /// <summary>
    /// Set goal pool dynamically (called by AIProviderCoordinator/NPCConfigurationHandler)
    /// </summary>
    public void SetGoalPool(System.Collections.Generic.List<GOAPGoal> goals)
    {
        if (goals == null || goals.Count == 0)
        {
            Debug.LogWarning("[GOAPModule] Attempted to set empty goal pool");
            return;
        }

        // Clear existing goals
        goalPool.Clear();

        // Add new goals
        foreach (var goal in goals)
        {
            if (goal != null)
            {
                goalPool.Add(goal);
            }
        }

        Debug.Log($"[GOAPModule] Goal pool set: {goalPool.Count} goals");
    }

    /// <summary>
    /// Add a single goal to the pool
    /// </summary>
    public void AddGoal(GOAPGoal goal)
    {
        if (goal != null && !goalPool.Contains(goal))
        {
            goalPool.Add(goal);
            Debug.Log($"[GOAPModule] Added goal: {goal.goalName}");
        }
    }

    /// <summary>
    /// Remove a goal from the pool
    /// </summary>
    public void RemoveGoal(GOAPGoal goal)
    {
        if (goalPool.Remove(goal))
        {
            Debug.Log($"[GOAPModule] Removed goal: {goal.goalName}");
        }
    }

    /// <summary>
    /// Clear all goals
    /// </summary>
    public void ClearGoals()
    {
        goalPool.Clear();
        Debug.Log("[GOAPModule] Goal pool cleared");
    }

    /// <summary>
    /// Get current goal count (for debugging)
    /// </summary>
    public int GetGoalCount()
    {
        return goalPool != null ? goalPool.Count : 0;
    }

    #endregion

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Create and initialize context
        context = new GOAPContext();
        context.Initialize(brain);

        if (debugMode)
        {
            Debug.Log($"[GOAPModule] Initialized on {brain.name} with {goalPool.Count} goals");
            Debug.Log($"  Selection Mode: {selectionMode}");
            Debug.Log($"  Anti-Thrashing: {useGoalCommitment} (Min Duration: {minimumGoalDuration}s)");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        // Update world state
        context.UpdateContext();

        // If we have no target, skip GOAP (let FSM handle Idle/Patrol states)
        if (context.target == null)
        {
            if (currentGoal != null || interruptGoal != null)
            {
                // Had a goal but lost target - clean up
                if (debugMode)
                    Debug.Log($"[GOAPModule] Lost target, clearing current goal");

                ClearCurrentGoal();
            }
            return;
        }

        // Skip if busy (ability executing, animation locked, staggered)
        if (context.isBusy)
        {
            if (debugMode && Time.frameCount % 60 == 0) // Log once per second
                Debug.Log($"[GOAPModule] Entity busy, skipping goal evaluation");
            return;
        }

        // Check for interrupts first
        if (checkInterrupts && ShouldCheckForInterrupt())
        {
            CheckForInterrupt();
        }

        // Execute current goal (interrupt takes priority)
        GOAPGoal activeGoal = CurrentGoal;
        if (activeGoal != null)
        {
            ExecuteActiveGoal(activeGoal);
        }
        else
        {
            // No current goal - select one
            SelectNewGoal();
        }
    }

    #endregion

    #region Goal Selection

    /// <summary>
    /// Evaluate all goals and select the best one
    /// </summary>
    private void SelectNewGoal()
    {
        // Clear previous evaluations
        evaluatedGoals.Clear();

        // Evaluate all goals
        foreach (var goal in goalPool)
        {
            if (goal == null) continue;

            // Permission check first (hard constraints)
            if (!goal.CanExecute(context))
                continue;

            // Calculate desire/weight
            float weight = goal.CalculateWeight(context);
            if (weight <= 0f)
                continue;

            // Apply base weight multiplier
            weight *= goal.baseWeight;

            // Apply stickiness bonus if this is the current goal (anti-thrashing)
            if (useGoalCommitment && goal == currentGoal)
            {
                weight *= currentGoalStickinessBonus;
            }

            evaluatedGoals.Add((goal, weight));
        }

        // No valid goals?
        if (evaluatedGoals.Count == 0)
        {
            if (debugMode)
                Debug.Log($"[GOAPModule] No valid goals available");
            return;
        }

        // Select goal based on selection mode
        GOAPGoal selectedGoal = null;

        switch (selectionMode)
        {
            case GoalSelectionMode.HighestWeight:
                selectedGoal = SelectHighestWeight();
                break;

            case GoalSelectionMode.WeightedRandom:
                selectedGoal = SelectWeightedRandom();
                break;

            case GoalSelectionMode.TopNWeightedRandom:
                selectedGoal = SelectTopNWeightedRandom();
                break;
        }

        // Change to selected goal if different
        if (selectedGoal != null && selectedGoal != currentGoal)
        {
            ChangeGoal(selectedGoal);
        }
    }

    /// <summary>
    /// Deterministic selection - always pick highest weight
    /// </summary>
    private GOAPGoal SelectHighestWeight()
    {
        var best = evaluatedGoals.OrderByDescending(x => x.weight).First();
        return best.goal;
    }

    /// <summary>
    /// Full weighted random selection across all valid goals
    /// </summary>
    private GOAPGoal SelectWeightedRandom()
    {
        float totalWeight = evaluatedGoals.Sum(x => x.weight);
        float roll = Random.Range(0f, totalWeight);

        float cumulative = 0f;
        foreach (var (goal, weight) in evaluatedGoals)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return goal;
        }

        // Fallback (shouldn't reach here)
        return evaluatedGoals[0].goal;
    }

    /// <summary>
    /// Weighted random among top N goals (smart variety)
    /// </summary>
    private GOAPGoal SelectTopNWeightedRandom()
    {
        // Get top N goals
        var topGoals = evaluatedGoals
            .OrderByDescending(x => x.weight)
            .Take(topNCount)
            .ToList();

        // Weighted random among top goals
        float totalWeight = topGoals.Sum(x => x.weight);
        float roll = Random.Range(0f, totalWeight);

        float cumulative = 0f;
        foreach (var (goal, weight) in topGoals)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return goal;
        }

        return topGoals[0].goal;
    }

    #endregion

    #region Goal Execution

    /// <summary>
    /// Execute the currently active goal
    /// </summary>
    private void ExecuteActiveGoal(GOAPGoal activeGoal)
    {
        // Check if goal is complete
        if (activeGoal.IsComplete(context))
        {
            if (debugMode)
                Debug.Log($"[GOAPModule] Goal completed: {activeGoal.goalName}");

            // If this was an interrupt, resume previous goal
            if (activeGoal == interruptGoal)
            {
                CompleteInterrupt();
            }
            else
            {
                // Normal goal completed
                ClearCurrentGoal();
                SelectNewGoal();
            }
            return;
        }

        // Check if we should switch goals (anti-thrashing check)
        if (!IsExecutingInterrupt && useGoalCommitment)
        {
            float timeInGoal = Time.time - goalStartTime;
            float minDuration = activeGoal.minimumActiveDuration > 0
                ? activeGoal.minimumActiveDuration
                : minimumGoalDuration;

            if (timeInGoal < minDuration)
            {
                // Still committed to this goal - execute it
                activeGoal.Execute(context);
                return;
            }
        }

        // Execute the goal
        activeGoal.Execute(context);

        // Periodically re-evaluate (not every frame for performance)
        if (Time.time - lastGoalChangeTime > 0.5f)
        {
            // Check if a better goal is available
            SelectNewGoal();
        }
    }

    #endregion

    #region Goal Transitions

    /// <summary>
    /// Change to a new goal
    /// </summary>
    private void ChangeGoal(GOAPGoal newGoal)
    {
        if (currentGoal != null)
        {
            currentGoal.OnEnd(context);

            if (debugMode)
                Debug.Log($"[GOAPModule] Goal transition: {currentGoal.goalName} → {newGoal.goalName}");
        }

        currentGoal = newGoal;
        goalStartTime = Time.time;
        lastGoalChangeTime = Time.time;

        if (currentGoal != null)
        {
            currentGoal.OnStart(context);
        }
    }

    /// <summary>
    /// Clear current goal without selecting a new one
    /// </summary>
    private void ClearCurrentGoal()
    {
        if (currentGoal != null)
        {
            currentGoal.OnEnd(context);
            currentGoal = null;
        }

        if (interruptGoal != null)
        {
            interruptGoal.OnEnd(context);
            interruptGoal = null;
        }

        goalStack.Clear();
    }

    #endregion

    #region Interrupt System

    /// <summary>
    /// Should we check for interrupt goals?
    /// </summary>
    private bool ShouldCheckForInterrupt()
    {
        // Already executing an interrupt?
        if (interruptGoal != null && !allowNestedInterrupts)
            return false;

        // Stack too deep?
        if (goalStack.Count >= maxInterruptStackDepth)
            return false;

        return true;
    }

    /// <summary>
    /// Check if an interrupt goal should trigger
    /// </summary>
    private void CheckForInterrupt()
    {
        // Check evade goal (highest priority interrupt)
        if (evadeGoal != null && evadeGoal.CanExecute(context))
        {
            float evadeWeight = evadeGoal.CalculateWeight(context);
            if (evadeWeight > 0f)
            {
                // High urgency - trigger evade
                TriggerInterrupt(evadeGoal);
                return;
            }
        }

        // Check rush down goal (aggressive interrupt)
        if (rushDownGoal != null && rushDownGoal.CanExecute(context))
        {
            float rushWeight = rushDownGoal.CalculateWeight(context);
            if (rushWeight > 0f)
            {
                // Opportunity to rush - trigger
                TriggerInterrupt(rushDownGoal);
                return;
            }
        }
    }

    /// <summary>
    /// Trigger an interrupt goal
    /// </summary>
    private void TriggerInterrupt(GOAPGoal interrupt)
    {
        if (debugMode)
            Debug.Log($"[GOAPModule] INTERRUPT: {interrupt.goalName}");

        // Push current goal to stack if we want to resume
        if (resumeAfterInterrupt && currentGoal != null)
        {
            goalStack.Push(currentGoal);
        }

        // End current goal (don't clear it, it's on the stack)
        if (currentGoal != null)
        {
            currentGoal.OnEnd(context);
        }

        // Start interrupt
        interruptGoal = interrupt;
        interruptStartTime = Time.time;
        interruptGoal.OnStart(context);
    }

    /// <summary>
    /// Complete interrupt and resume previous goal
    /// </summary>
    private void CompleteInterrupt()
    {
        if (debugMode)
            Debug.Log($"[GOAPModule] Interrupt completed: {interruptGoal.goalName}");

        interruptGoal.OnEnd(context);
        interruptGoal = null;

        // Resume previous goal if available and not too old
        if (resumeAfterInterrupt && goalStack.Count > 0)
        {
            float interruptDuration = Time.time - interruptStartTime;

            if (interruptDuration < interruptContextTimeout)
            {
                GOAPGoal resumeGoal = goalStack.Pop();

                if (debugMode)
                    Debug.Log($"[GOAPModule] Resuming goal: {resumeGoal.goalName}");

                currentGoal = resumeGoal;
                goalStartTime = Time.time;
                resumeGoal.OnStart(context);
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[GOAPModule] Interrupt too long ({interruptDuration:F2}s), selecting new goal");

                goalStack.Clear();
                SelectNewGoal();
            }
        }
        else
        {
            // No resume - select new goal
            SelectNewGoal();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Set the current target for GOAP evaluation
    /// </summary>
    public void SetTarget(Transform target)
    {
        if (context != null)
        {
            context.target = target;
        }
    }

    /// <summary>
    /// Force select a specific goal (bypass normal selection)
    /// Useful for scripted sequences or testing
    /// </summary>
    public void ForceGoal(GOAPGoal goal)
    {
        if (goal != null)
        {
            ChangeGoal(goal);
        }
    }

    /// <summary>
    /// Get current goal's debug info
    /// </summary>
    public string GetCurrentGoalDebug()
    {
        GOAPGoal active = CurrentGoal;
        return active != null ? active.GetDebugInfo(context) : "No Active Goal";
    }

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        if (!debugMode || context == null) return;

        // Draw line to target
        if (context.target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, context.target.position);
        }
    }

    #endregion
}