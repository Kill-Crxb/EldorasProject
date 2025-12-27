using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Fate System - Executes branching combat combos with RNG variety
/// 
/// Responsibilities:
/// - Execute GOAPActions (roll dice, pick branch, execute sequence)
/// - Handle move delays and animation timing
/// - Support move skipping for combo variety
/// - Interrupt detection and cleanup
/// 
/// Integration with GOAP:
/// - AttackGoal calls FateSystem.ExecuteAction(action)
/// - FateSystem selects a branch and executes the move sequence
/// - While executing, entity is "busy" (GOAPContext.isBusy = true)
/// - When complete, returns control to GOAP for next goal selection
/// 
/// Example Flow:
/// 1. AttackGoal selected by GOAP
/// 2. AttackGoal.Execute() calls FateSystem.ExecuteAction(bearClawAttack)
/// 3. FateSystem rolls dice → selects "Double Swipe" branch (40%)
/// 4. FateSystem executes: [move 0] → delay → [move 1] → complete
/// 5. GOAP re-evaluates goals
/// </summary>
public class FateSystem : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // References
    private ControllerBrain brain;
    private IAbilityProvider abilityModule;

    // Execution state
    private bool isExecuting;
    private GOAPAction currentAction;
    private FateBranch currentBranch;
    private List<AbilityDefinition> currentSequence;
    private int currentMoveIndex;
    private float moveDelay;

    // Coroutine tracking
    private Coroutine executionCoroutine;

    #region Initialization

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        abilityModule = brain.GetModuleImplementing<IAbilityProvider>();

        if (abilityModule == null)
        {
            Debug.LogError("[FateSystem] No IAbilityProvider found - cannot execute abilities!");
        }

        if (debugMode)
        {
            Debug.Log($"[FateSystem] Initialized on {brain.name}");
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Execute a GOAP action (roll dice, pick branch, execute sequence)
    /// </summary>
    public void ExecuteAction(GOAPAction action)
    {
        if (action == null)
        {
            Debug.LogError("[FateSystem] Null action passed to ExecuteAction!");
            return;
        }

        if (isExecuting)
        {
            if (debugMode)
                Debug.LogWarning($"[FateSystem] Already executing {currentAction?.actionName}, cannot start {action.actionName}");
            return;
        }

        if (!action.Validate())
        {
            Debug.LogError($"[FateSystem] Action {action.actionName} failed validation!");
            return;
        }

        // Roll dice and select branch
        FateBranch selectedBranch = action.SelectBranch();

        if (selectedBranch == null)
        {
            Debug.LogError($"[FateSystem] Failed to select branch for {action.actionName}");
            return;
        }

        if (debugMode)
        {
            Debug.Log($"[FateSystem] Executing {action.actionName}");
            Debug.Log($"  Branch: {selectedBranch.branchName} ({selectedBranch.probability:F1}%)");
            Debug.Log($"  Sequence: [{string.Join(", ", selectedBranch.moveSequence)}]");
        }

        // Set up execution
        currentAction = action;
        currentBranch = selectedBranch;
        currentSequence = selectedBranch.GetMoves(action.moveLibrary);
        currentMoveIndex = 0;
        moveDelay = action.moveDelay;
        isExecuting = true;

        // Start execution coroutine
        executionCoroutine = StartCoroutine(ExecuteSequence());
    }

    /// <summary>
    /// Is the Fate System currently executing a combo?
    /// </summary>
    public bool IsExecuting()
    {
        return isExecuting;
    }

    /// <summary>
    /// Check if execution is complete
    /// </summary>
    public bool IsComplete()
    {
        return !isExecuting;
    }

    /// <summary>
    /// Force interrupt current execution
    /// </summary>
    public void Interrupt()
    {
        if (!isExecuting) return;

        if (debugMode)
            Debug.Log($"[FateSystem] INTERRUPTED: {currentAction?.actionName}");

        // Stop coroutine
        if (executionCoroutine != null)
        {
            StopCoroutine(executionCoroutine);
            executionCoroutine = null;
        }

        // Clean up state
        CleanupExecution();
    }

    #endregion

    #region Execution

    /// <summary>
    /// Execute the move sequence with delays
    /// </summary>
    private IEnumerator ExecuteSequence()
    {
        // Execute each move in sequence
        for (int i = 0; i < currentSequence.Count; i++)
        {
            currentMoveIndex = i;
            AbilityDefinition move = currentSequence[i];

            if (move == null)
            {
                Debug.LogError($"[FateSystem] Null move at index {i} in sequence!");
                continue;
            }

            // Execute the move
            if (debugMode)
                Debug.Log($"[FateSystem] Executing move {i + 1}/{currentSequence.Count}: {move.abilityName}");

            ExecuteMove(move);

            // Wait for move delay (unless this is the last move)
            if (i < currentSequence.Count - 1)
            {
                yield return new WaitForSeconds(moveDelay);

                // Check if we should interrupt
                if (currentAction != null && !currentAction.canBeInterrupted)
                {
                    // Non-interruptible - continue
                }
                else
                {
                    // Interruptible - could add interrupt check here
                    // For now, just continue
                }
            }
        }

        // Sequence complete
        if (debugMode)
            Debug.Log($"[FateSystem] Sequence complete: {currentAction?.actionName}");

        CleanupExecution();
    }

    /// <summary>
    /// Execute a single move using the ability system
    /// </summary>
    private void ExecuteMove(AbilityDefinition move)
    {
        if (abilityModule == null)
        {
            Debug.LogError("[FateSystem] No ability module - cannot execute move!");
            return;
        }

        // Try to execute via AbilityModule
        var abilityMod = abilityModule as AbilityModule;
        if (abilityMod != null)
        {
            // Find which slot has this ability
            // For NPCs with natural weapons, this might be in a specific slot
            // For now, we'll use a simple approach - try to execute by ability reference

            // Note: This is a simplified implementation
            // In production, you'd want to either:
            // 1. Have abilities mapped to slot keys in the action definition
            // 2. Have FateSystem work with ability slot keys directly
            // 3. Have a way to invoke abilities by reference

            // For now, let's log that we would execute this ability
            if (debugMode)
                Debug.Log($"[FateSystem] Would execute ability: {move.abilityName}");

            // TODO: Implement actual ability execution
            // This will depend on how your AbilityModule is set up
            // Example: abilityMod.UseAbility("NaturalWeapon1");
        }
    }

    /// <summary>
    /// Clean up execution state
    /// </summary>
    private void CleanupExecution()
    {
        isExecuting = false;
        currentAction = null;
        currentBranch = null;
        currentSequence = null;
        currentMoveIndex = 0;
        executionCoroutine = null;
    }

    #endregion

    #region Debug

    /// <summary>
    /// Get current execution state for debugging
    /// </summary>
    public string GetDebugInfo()
    {
        if (!isExecuting)
            return "Fate System: Idle";

        return $"Fate System: Executing {currentAction?.actionName}\n" +
               $"  Branch: {currentBranch?.branchName}\n" +
               $"  Move: {currentMoveIndex + 1}/{currentSequence?.Count ?? 0}";
    }

    #endregion
}