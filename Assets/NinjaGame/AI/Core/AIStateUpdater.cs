using UnityEngine;
using System;

/// <summary>
/// AI State Updater - Companion script for AIModule
/// PATCHED VERSION: Properly delegates combat movement to IAICombatBehavior.UpdateCombat()
/// 
/// Handles the detailed update logic for each AI state.
/// 
/// Responsibilities:
/// - Execute Idle state logic (detection)
/// - Execute Chase state logic (pursuit)
/// - Execute Combat state logic (DELEGATES to combat behavior)
/// - Movement coordination
/// - State-specific calculations
/// </summary>
public class AIStateUpdater : MonoBehaviour
{
    [Header("Behavior Settings")]
    [SerializeField] private float combatTimeout = 5f;
    [SerializeField] private float attackCooldown = 1.5f;
    //[SerializeField] private float combatExitBuffer = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // References (set by AIModule)
    private AIModule aiModule;
    private AITargetDetection targetDetection;
    private NPCMovementModule npcMovement;
    private IAICombatBehavior combatBehavior;
    private ControllerBrain brain;

    // Cached values for performance
    private float cachedAttackRange;
    private float cachedExitCombatRange;

    // Timing
    private float lastSeenTargetTime;
    private float lastAttackTime;

    /// <summary>
    /// Initialize the state updater with required references
    /// </summary>
    public void Initialize(AIModule module, AITargetDetection detection, NPCMovementModule movement, IAICombatBehavior combat, ControllerBrain controllerBrain)
    {
        aiModule = module;
        targetDetection = detection;
        npcMovement = movement;
        combatBehavior = combat;
        brain = controllerBrain;

        // Cache combat ranges for performance
        if (combatBehavior != null)
        {
            cachedAttackRange = combatBehavior.AttackRange;
            cachedExitCombatRange = combatBehavior.ExitCombatRange;
        }

        lastAttackTime = -attackCooldown;

        if (debugMode)
        {
            Debug.Log($"[AIStateUpdater] Initialized on {gameObject.name}");
            Debug.Log($"  Attack Range: {cachedAttackRange}");
            Debug.Log($"  Exit Range: {cachedExitCombatRange}");
        }
    }

    #region State Update Methods

    /// <summary>
    /// Update logic for Idle state - detect targets
    /// </summary>
    public void UpdateIdleState()
    {
        if (targetDetection == null)
        {
            if (debugMode)
                Debug.LogWarning("[AIStateUpdater] No target detection available");
            return;
        }

        Transform detectedTarget = targetDetection.DetectTarget();

        if (detectedTarget != null)
        {
            // Target found! Notify AIModule to transition to Chase
            lastSeenTargetTime = Time.time;
            aiModule.OnTargetDetected(detectedTarget);

            if (debugMode)
            {
                Debug.Log($"[AIStateUpdater] Target detected in Idle: {detectedTarget.name}");
            }
        }
    }

    /// <summary>
    /// Update logic for Chase state - pursue target
    /// </summary>
    public void UpdateChaseState()
    {
        Transform currentTarget = aiModule.CurrentTarget;

        if (currentTarget == null)
        {
            if (debugMode)
                Debug.Log("[AIStateUpdater] Chase: No target, returning to Idle");

            aiModule.HandleTargetLost();
            return;
        }

        float distance = Vector3.Distance(transform.position, currentTarget.position);

        // Transition to combat when in attack range
        if (distance <= cachedAttackRange)
        {
            if (combatBehavior != null && combatBehavior.CanEnterCombat())
            {
                if (debugMode)
                    Debug.Log($"[AIStateUpdater] Entering combat - target in range ({distance:F2} <= {cachedAttackRange})");

                aiModule.OnEnteredCombatRange();
                return;
            }
        }

        // Check if target escaped detection range
        float detectionRange = targetDetection.GetDetectionRange();
        if (distance > detectionRange)
        {
            // Target out of range - check timeout
            if (Time.time - lastSeenTargetTime > combatTimeout)
            {
                if (debugMode)
                    Debug.Log($"[AIStateUpdater] Chase timeout - lost sight of target");

                aiModule.HandleTargetLost();
                return;
            }
        }
        else
        {
            // Still in range - update last seen time
            lastSeenTargetTime = Time.time;
        }

        // Always rotate toward target while chasing
        if (npcMovement != null)
        {
            npcMovement.RotateTowards(currentTarget.position);
            npcMovement.MoveTowards(currentTarget.position);
        }
        else if (debugMode)
        {
            Debug.LogWarning("[AIStateUpdater] No movement module for chase");
        }
    }

    /// <summary>
    /// Update logic for Combat state - DELEGATES to combat behavior
    /// PATCHED: Now calls combatBehavior.UpdateCombat() for full control
    /// </summary>
    public void UpdateCombatState()
    {
        Transform currentTarget = aiModule.CurrentTarget;

        if (currentTarget == null)
        {
            if (debugMode)
                Debug.Log("[AIStateUpdater] Combat: No target, returning to Idle");

            aiModule.HandleTargetLost();
            return;
        }

        float distance = Vector3.Distance(transform.position, currentTarget.position);

        // Check if we should exit combat (too far)
        if (distance > cachedExitCombatRange)
        {
            if (debugMode)
                Debug.Log($"[AIStateUpdater] Exiting combat - target too far ({distance:F2} > {cachedExitCombatRange})");

            aiModule.OnExitedCombatRange();
            return;
        }

        // PATCHED: Delegate ALL combat logic to the combat behavior
        // This includes movement, rotation, and attacking
        if (combatBehavior != null)
        {
            combatBehavior.UpdateCombat(currentTarget);

            if (debugMode)
            {
                // Optional: Log combat updates less frequently
                if (Time.frameCount % 60 == 0) // Every 60 frames (~1 second at 60fps)
                {
                    Debug.Log($"[AIStateUpdater] Combat update - distance: {distance:F2}");
                }
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning("[AIStateUpdater] No combat behavior available");
        }
    }

    #endregion

    #region Public Utility Methods

    /// <summary>
    /// Reset timing variables (called when target changes or states reset)
    /// </summary>
    public void ResetTimers()
    {
        lastSeenTargetTime = Time.time;
        // Don't reset lastAttackTime - maintain cooldown between targets
    }

    /// <summary>
    /// Update cached combat ranges (call if combat behavior changes)
    /// </summary>
    public void RefreshCombatRanges()
    {
        if (combatBehavior != null)
        {
            cachedAttackRange = combatBehavior.AttackRange;
            cachedExitCombatRange = combatBehavior.ExitCombatRange;

            if (debugMode)
            {
                Debug.Log($"[AIStateUpdater] Ranges refreshed - Attack: {cachedAttackRange}, Exit: {cachedExitCombatRange}");
            }
        }
    }

    #endregion

    #region Debug

    void OnDrawGizmos()
    {
        if (!debugMode || combatBehavior == null) return;

        // Draw cached ranges
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, cachedAttackRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, cachedExitCombatRange);
    }

    [ContextMenu("Debug: Print State Info")]
    void DebugPrintStateInfo()
    {
        Debug.Log("=== AI STATE UPDATER INFO ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"Attack Range: {cachedAttackRange}");
        Debug.Log($"Exit Range: {cachedExitCombatRange}");
        Debug.Log($"Combat Behavior: {(combatBehavior != null ? combatBehavior.GetType().Name : "NULL")}");
        Debug.Log($"Movement Module: {(npcMovement != null ? "Found" : "NULL")}");
        Debug.Log($"Target Detection: {(targetDetection != null ? "Found" : "NULL")}");

        if (aiModule != null)
        {
            Debug.Log($"Current State: {aiModule.CurrentState}");
            Debug.Log($"Has Target: {aiModule.HasTarget()}");
            if (aiModule.CurrentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, aiModule.CurrentTarget.position);
                Debug.Log($"Distance to Target: {distance:F2}");
            }
        }
    }

    #endregion
}