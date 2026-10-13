using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Goal selection modes for different AI personalities

namespace CrabSystem
{
    public enum GoalSelectionMode
    {
        HighestWeight,      // Always pick best option (deterministic, predictable)
        WeightedRandom,     // Full randomness weighted by scores (unpredictable)
        TopNWeightedRandom  // Random among top N options (smart variety)
    }

    // GOAP Module - Goal-Oriented Action Planning AI System
    //
    // Replaces AIModule for entities using GOAP-based decision making.
    //
    // Architecture:
    // - Evaluates goal pool every frame
    // - Selects best goal based on selection mode
    // - Executes active goal
    // - Handles goal transitions and interrupts
    //
    // Features:
    // - Anti-thrashing (minimum goal duration, stickiness bonus)
    // - Multiple selection modes (deterministic, random, top-N random)
    // - Interrupt system (evade, react to threats)
    // - Goal stack (resume after interrupt)
    // - Busy watchdog (automatic deadlock recovery)
    // - Type-safe goal references (drag-and-drop in Inspector)
    //
    // Usage:
    // 1. Add GOAPModule to Component_AI
    // 2. Assign goal pool (ScriptableObjects)
    // 3. Configure selection mode and anti-thrashing settings
    // 4. Set special goal references (evade, rush, etc) if needed
    public class GOAPModule : MonoBehaviour, IBrainModule
    {
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

        // Core references
        private ControllerBrain brain;
        private float Now => brain.Clock.Now;
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

        // Set goal pool dynamically (called by AIProviderCoordinator/NPCConfigurationHandler)
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
        }

        // Add a single goal to the pool
        public void AddGoal(GOAPGoal goal)
        {
            if (goal != null && !goalPool.Contains(goal))
            {
                goalPool.Add(goal);
            }
        }

        // Remove a goal from the pool
        public void RemoveGoal(GOAPGoal goal)
        {
            goalPool.Remove(goal);
        }

        public void ClearGoals()
        {
            goalPool.Clear();
        }

        // Get current goal count (for debugging)
        public int GetGoalCount()
        {
            return goalPool != null ? goalPool.Count : 0;
        }

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;

            // Create and initialize context
            context = new GOAPContext();
            context.Initialize(brain);
        }

        public void UpdateModule()
        {
            if (!isEnabled) return;

            // A dead entity has no goals. Ending the goal releases the AI's controls, so the body stops.
            if (brain.Health != null && !brain.Health.IsAlive())
            {
                ClearCurrentGoal();
                return;
            }

            // Update world state
            context.UpdateContext();

            // No target: only goals that need none (wander, patrol) may run.
            if (context.target == null)
            {
                if (CurrentGoal != null && CurrentGoal.requiresTarget)
                {
                    ClearCurrentGoal();
                }

                if (!HasTargetlessGoal()) return;
            }

            // Skip if action-locked (ability executing, movement locked, staggered, etc.)
            if (context.IsActionLocked)
            {
                if (debugMode && Time.frameCount % 60 == 0) // Log once per second
                return;
            }

            // Check for interrupts first
            if (checkInterrupts && context.target != null && ShouldCheckForInterrupt())
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

        private bool HasTargetlessGoal()
        {
            foreach (var goal in goalPool)
                if (goal != null && !goal.requiresTarget) return true;
            return false;
        }

        // Evaluate all goals and select the best one
        private void SelectNewGoal()
        {
            // Clear previous evaluations
            evaluatedGoals.Clear();

            // Evaluate all goals
            foreach (var goal in goalPool)
            {
                if (goal == null) continue;
                if (goal.requiresTarget && context.target == null) continue;

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

        // Deterministic selection - always pick highest weight
        private GOAPGoal SelectHighestWeight()
        {
            var best = evaluatedGoals.OrderByDescending(x => x.weight).First();
            return best.goal;
        }

        // Full weighted random selection across all valid goals
        private GOAPGoal SelectWeightedRandom()
        {
            float totalWeight = evaluatedGoals.Sum(x => x.weight);
            float roll = DiceRoll.Range(0f, totalWeight);

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

        // Weighted random among top N goals (smart variety)
        private GOAPGoal SelectTopNWeightedRandom()
        {
            // Get top N goals
            var topGoals = evaluatedGoals
                .OrderByDescending(x => x.weight)
                .Take(topNCount)
                .ToList();

            // Weighted random among top goals
            float totalWeight = topGoals.Sum(x => x.weight);
            float roll = DiceRoll.Range(0f, totalWeight);

            float cumulative = 0f;
            foreach (var (goal, weight) in topGoals)
            {
                cumulative += weight;
                if (roll <= cumulative)
                    return goal;
            }

            return topGoals[0].goal;
        }

        // Execute the currently active goal
        private void ExecuteActiveGoal(GOAPGoal activeGoal)
        {
            // Check if goal is complete
            if (activeGoal.IsComplete(context))
            {
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
                float timeInGoal = Now - goalStartTime;
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
            if (Now - lastGoalChangeTime > 0.5f)
            {
                // Check if a better goal is available
                SelectNewGoal();
            }
        }

        private void ChangeGoal(GOAPGoal newGoal)
        {
            if (currentGoal != null)
            {
                currentGoal.OnEnd(context);
            }

            currentGoal = newGoal;
            goalStartTime = Now;
            lastGoalChangeTime = Now;

            if (currentGoal != null)
            {
                currentGoal.OnStart(context);
            }
        }

        // Clear current goal without selecting a new one
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

        // Should we check for interrupt goals?
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

        // Check if an interrupt goal should trigger
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

        private void TriggerInterrupt(GOAPGoal interrupt)
        {
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
            interruptStartTime = Now;
            interruptGoal.OnStart(context);
        }

        // Complete interrupt and resume previous goal
        private void CompleteInterrupt()
        {
            interruptGoal.OnEnd(context);
            interruptGoal = null;

            // Resume previous goal if available and not too old
            if (resumeAfterInterrupt && goalStack.Count > 0)
            {
                float interruptDuration = Now - interruptStartTime;

                if (interruptDuration < interruptContextTimeout)
                {
                    GOAPGoal resumeGoal = goalStack.Pop();

                    currentGoal = resumeGoal;
                    goalStartTime = Now;
                    resumeGoal.OnStart(context);
                }
                else
                {
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

        // Set the current target for GOAP evaluation
        public void SetTarget(Transform target)
        {
            if (context != null)
            {
                context.target = target;
            }
        }

        // Force select a specific goal (bypass normal selection)
        // Useful for scripted sequences or testing
        public void ForceGoal(GOAPGoal goal)
        {
            if (goal != null)
            {
                ChangeGoal(goal);
            }
        }

        // Get current goal's debug info
        public string GetCurrentGoalDebug()
        {
            GOAPGoal active = CurrentGoal;
            return active != null ? active.GetDebugInfo(context) : "No Active Goal";
        }

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
    }
}
