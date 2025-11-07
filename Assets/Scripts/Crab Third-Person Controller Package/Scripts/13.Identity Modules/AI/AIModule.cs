using UnityEngine;
using System;
using RPG.Factions;

public enum AIState
{
    Idle,
    Chase,
    Combat,
    Patrol,
    Flee,
    Investigate
}

/// <summary>
/// AI Module - Central coordinator for NPC behavior.
/// Delegates specific responsibilities to companion scripts for clean separation of concerns.
/// 
/// ARCHITECTURE:
/// - AIModule: State machine coordination, event publishing, public API
/// - AITargetDetection: Entity detection, faction validation, line of sight
/// - AIStateUpdater: Detailed state logic (idle/chase/combat updates)
/// - AIDebugVisualizer: Debug tools, gizmos, visualization
/// 
/// PHASE 2 COMPLETE: Entity-Agnostic Faction Detection
/// - Works with ANY entity that has ControllerBrain (player, NPC, future types)
/// - Dynamic faction relationship checking via FactionManager
/// - No hardcoded assumptions about entity types
/// 
/// This refactored design makes it easy to:
/// - Add new AI states (Patrol, Flee, Investigate)
/// - Modify detection strategies
/// - Extend debug capabilities
/// - Swap entire behavior systems (e.g., move to Behavior Trees later)
/// </summary>
public class AIModule : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    public bool isEnabled = true;

    [Header("AI State")]
    [SerializeField] private AIState currentState = AIState.Idle;

    [Header("Companion Scripts (Auto-detected or assign manually)")]
    [SerializeField] private AITargetDetection targetDetection;
    [SerializeField] private AIStateUpdater stateUpdater;
    [SerializeField] private AIDebugVisualizer debugVisualizer;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private Transform currentTarget;

    // Core references
    private ControllerBrain brain;
    private NPCMovementModule npcMovement;
    private IAICombatBehavior combatBehavior;

    // Properties
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public AIState CurrentState => currentState;
    public Transform CurrentTarget => currentTarget;

    // Events
    public event Action<AIState, AIState> OnStateChanged;
    public event Action<Transform> OnTargetAcquired;
    public event Action OnTargetLost;

    #region Initialization

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Find required modules
        if (!FindRequiredModules())
        {
            Debug.LogError($"[AIModule] Failed to find required modules on {gameObject.name}");
            isEnabled = false;
            return;
        }

        // Initialize companion scripts
        InitializeCompanions();

        ChangeState(AIState.Idle);

        if (debugMode)
        {
            Debug.Log($"[AIModule] Initialized successfully on {gameObject.name}");
        }
    }

    /// <summary>
    /// Find required modules from Brain
    /// </summary>
    private bool FindRequiredModules()
    {
        // NPCMovementModule - REQUIRED
        npcMovement = brain.GetModule<NPCMovementModule>();

        if (npcMovement == null)
        {
            // Fallback searches
            npcMovement = brain.GetComponentInChildren<NPCMovementModule>();

            if (npcMovement == null)
            {
                npcMovement = GetComponentInParent<NPCMovementModule>();
            }
        }

        if (npcMovement == null)
        {
            Debug.LogError($"[AIModule] NPCMovementModule NOT FOUND on {gameObject.name}! AI will not work.");
            return false;
        }

        if (debugMode)
        {
            Debug.Log($"[AIModule] ✓ Found NPCMovementModule on {npcMovement.gameObject.name}");
        }

        // Combat behavior - OPTIONAL but recommended
        combatBehavior = brain.GetModule<AICombatBehaviorModule>() as IAICombatBehavior;

        if (combatBehavior == null)
        {
            combatBehavior = GetComponentInChildren<IAICombatBehavior>();
        }

        if (combatBehavior != null)
        {
            combatBehavior.Initialize(this, brain);

            if (debugMode)
            {
                Debug.Log($"[AIModule] ✓ Found combat behavior: {combatBehavior.GetType().Name}");
            }
        }
        else
        {
            Debug.LogWarning($"[AIModule] No IAICombatBehavior found on {gameObject.name}");
        }

        return true;
    }

    /// <summary>
    /// Initialize companion scripts (auto-find if not assigned)
    /// </summary>
    private void InitializeCompanions()
    {
        // Auto-find companions if not manually assigned
        if (targetDetection == null)
            targetDetection = GetComponent<AITargetDetection>();

        if (stateUpdater == null)
            stateUpdater = GetComponent<AIStateUpdater>();

        if (debugVisualizer == null)
            debugVisualizer = GetComponent<AIDebugVisualizer>();

        // Initialize target detection
        if (targetDetection != null)
        {
            targetDetection.Initialize(this, brain);

            if (debugMode)
                Debug.Log("[AIModule] ✓ AITargetDetection initialized");
        }
        else
        {
            Debug.LogWarning($"[AIModule] No AITargetDetection found on {gameObject.name}");
        }

        // Initialize state updater
        if (stateUpdater != null)
        {
            stateUpdater.Initialize(this, targetDetection, npcMovement, combatBehavior, brain);

            if (debugMode)
                Debug.Log("[AIModule] ✓ AIStateUpdater initialized");
        }
        else
        {
            Debug.LogWarning($"[AIModule] No AIStateUpdater found on {gameObject.name}");
        }

        // Initialize debug visualizer
        if (debugVisualizer != null)
        {
            debugVisualizer.Initialize(this, targetDetection, combatBehavior);

            if (debugMode)
                Debug.Log("[AIModule] ✓ AIDebugVisualizer initialized");
        }
    }

    #endregion

    #region Update Loop

    public void UpdateModule()
    {
        if (!isEnabled || stateUpdater == null) return;

        // Delegate to state updater based on current state
        switch (currentState)
        {
            case AIState.Idle:
                stateUpdater.UpdateIdleState();
                break;

            case AIState.Chase:
                stateUpdater.UpdateChaseState();
                break;

            case AIState.Combat:
                stateUpdater.UpdateCombatState();
                break;

            // Future states can be added here
            case AIState.Patrol:
                // stateUpdater.UpdatePatrolState();
                break;

            case AIState.Flee:
                // stateUpdater.UpdateFleeState();
                break;

            case AIState.Investigate:
                // stateUpdater.UpdateInvestigateState();
                break;
        }
    }

    #endregion

    #region State Management

    /// <summary>
    /// Change AI state with proper notifications
    /// </summary>
    public void ChangeState(AIState newState)
    {
        if (currentState == newState) return;

        AIState oldState = currentState;
        currentState = newState;

        // Notify combat behavior of state changes
        if (combatBehavior != null)
        {
            if (newState == AIState.Combat)
                combatBehavior.OnCombatEnter(currentTarget);
            else if (oldState == AIState.Combat)
                combatBehavior.OnCombatExit();
        }

        if (debugMode)
        {
            Debug.Log($"[AIModule] {gameObject.name} state: {oldState} → {newState}");
        }

        OnStateChanged?.Invoke(oldState, newState);
    }

    /// <summary>
    /// Force target to be lost and return to idle
    /// </summary>
    public void ForceTargetLost()
    {
        currentTarget = null;

        if (currentState != AIState.Idle)
        {
            ChangeState(AIState.Idle);
        }

        OnTargetLost?.Invoke();
    }

    #endregion

    #region Callbacks from Companion Scripts

    /// <summary>
    /// Called by AIStateUpdater when a target is detected in Idle state
    /// </summary>
    public void OnTargetDetected(Transform target)
    {
        currentTarget = target;
        ChangeState(AIState.Chase);
        OnTargetAcquired?.Invoke(target);
    }

    /// <summary>
    /// Called by AIStateUpdater when target is lost or timeout occurs
    /// </summary>
    public void HandleTargetLost()
    {
        currentTarget = null;
        ChangeState(AIState.Idle);
        OnTargetLost?.Invoke();
    }

    /// <summary>
    /// Called by AIStateUpdater when entering combat range
    /// </summary>
    public void OnEnteredCombatRange()
    {
        ChangeState(AIState.Combat);
    }

    /// <summary>
    /// Called by AIStateUpdater when exiting combat range
    /// </summary>
    public void OnExitedCombatRange()
    {
        ChangeState(AIState.Chase);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Check if this AI has a current target
    /// </summary>
    public bool HasTarget() => currentTarget != null;

    /// <summary>
    /// Check if currently in combat state
    /// </summary>
    public bool IsInCombat() => currentState == AIState.Combat;

    /// <summary>
    /// Get distance to current target
    /// </summary>
    public float GetDistanceToTarget()
    {
        if (currentTarget == null) return float.MaxValue;
        return Vector3.Distance(transform.position, currentTarget.position);
    }

    /// <summary>
    /// Get the combat behavior implementation
    /// </summary>
    public IAICombatBehavior GetCombatBehavior() => combatBehavior;

    /// <summary>
    /// Get this NPC's faction (convenience method, delegates to AITargetDetection)
    /// </summary>
    public FactionType GetFaction()
    {
        return targetDetection != null ? targetDetection.GetFaction() : FactionType.Neutral;
    }

    /// <summary>
    /// Get relationship to a specific target entity
    /// </summary>
    public FactionRelationship GetRelationshipToTarget(Transform target)
    {
        return targetDetection != null ?
            targetDetection.GetRelationshipToTarget(target) :
            FactionRelationship.Neutral;
    }

    /// <summary>
    /// Set detection range (delegates to AITargetDetection)
    /// </summary>
    public void SetDetectionRange(float range)
    {
        if (targetDetection != null)
            targetDetection.SetDetectionRange(range);
    }

    /// <summary>
    /// Get target detection companion
    /// </summary>
    public AITargetDetection GetTargetDetection() => targetDetection;

    /// <summary>
    /// Get state updater companion
    /// </summary>
    public AIStateUpdater GetStateUpdater() => stateUpdater;

    /// <summary>
    /// Get debug visualizer companion
    /// </summary>
    public AIDebugVisualizer GetDebugVisualizer() => debugVisualizer;

    #endregion

    #region Context Menu Debug (Quick Access)

    [ContextMenu("Debug: Print Module Info")]
    private void DebugPrintModuleInfo()
    {
        Debug.Log($"=== AIModule Info ===");
        Debug.Log($"Enabled: {isEnabled}");
        Debug.Log($"State: {currentState}");
        Debug.Log($"Has Target: {HasTarget()}");
        Debug.Log($"Companions:");
        Debug.Log($"  - TargetDetection: {(targetDetection != null ? "✓" : "✗")}");
        Debug.Log($"  - StateUpdater: {(stateUpdater != null ? "✓" : "✗")}");
        Debug.Log($"  - DebugVisualizer: {(debugVisualizer != null ? "✓" : "✗")}");
        Debug.Log($"Modules:");
        Debug.Log($"  - NPCMovement: {(npcMovement != null ? "✓" : "✗")}");
        Debug.Log($"  - CombatBehavior: {(combatBehavior != null ? "✓" : "✗")}");
    }

    [ContextMenu("Debug: Force Idle State")]
    private void DebugForceIdle()
    {
        ChangeState(AIState.Idle);
        currentTarget = null;
        Debug.Log("[AIModule] Forced to Idle state");
    }

    [ContextMenu("Debug: Add Missing Companions")]
    private void DebugAddMissingCompanions()
    {
        bool added = false;

        if (targetDetection == null)
        {
            targetDetection = gameObject.AddComponent<AITargetDetection>();
            Debug.Log("✓ Added AITargetDetection");
            added = true;
        }

        if (stateUpdater == null)
        {
            stateUpdater = gameObject.AddComponent<AIStateUpdater>();
            Debug.Log("✓ Added AIStateUpdater");
            added = true;
        }

        if (debugVisualizer == null)
        {
            debugVisualizer = gameObject.AddComponent<AIDebugVisualizer>();
            Debug.Log("✓ Added AIDebugVisualizer");
            added = true;
        }

        if (!added)
        {
            Debug.Log("All companions already present");
        }
        else if (Application.isPlaying)
        {
            InitializeCompanions();
        }
    }

    #endregion
}