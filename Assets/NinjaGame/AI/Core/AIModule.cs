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

public class AIModule : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    public bool isEnabled = true;

    [Header("AI State")]
    [SerializeField] private AIState currentState = AIState.Idle;

    [Header("Companion Scripts (Auto-detected or assign manually)")]
    [SerializeField] private AITargetDetection targetDetection;
    [SerializeField] private AIStateUpdater stateUpdater;
    [SerializeField] private AICombatBehaviorModule combatBehaviorModule; // ← ADDED: Now you can assign in Inspector!

    [SerializeField] private Transform currentTarget;

    private ControllerBrain brain;
    private NPCMovementModule npcMovement;
    private IAICombatBehavior combatBehavior; // Interface reference (for internal use)

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public AIState CurrentState => currentState;
    public Transform CurrentTarget => currentTarget;

    public event Action<AIState, AIState> OnStateChanged;
    public event Action<Transform> OnTargetAcquired;
    public event Action OnTargetLost;

    #region Initialization

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        if (!FindRequiredModules())
        {
            isEnabled = false;
            return;
        }

        InitializeCompanions();
        ChangeState(AIState.Idle);
    }

public bool FindRequiredModules()
    {
        // Find NPCMovementModule
        npcMovement = brain.GetModule<NPCMovementModule>();

        if (npcMovement == null)
        {
            npcMovement = brain.GetComponentInChildren<NPCMovementModule>();

            if (npcMovement == null)
            {
                npcMovement = GetComponentInParent<NPCMovementModule>();
            }
        }

        if (npcMovement == null)
        {
            Debug.LogError("[AIModule] NPCMovementModule not found!");
            return false;
        }

        // Find Combat Behavior - Try serialized field first, then auto-discovery
        if (combatBehaviorModule != null)
        {
            // Use manually assigned combat behavior
            combatBehavior = combatBehaviorModule as IAICombatBehavior;
            Debug.Log($"[AIModule] Using manually assigned combat behavior: {combatBehaviorModule.GetType().Name}");
        }

        // If not manually assigned, try auto-discovery
        if (combatBehavior == null)
        {
            combatBehavior = brain.GetModule<AICombatBehaviorModule>() as IAICombatBehavior;

            if (combatBehavior == null)
            {
                combatBehavior = GetComponentInChildren<IAICombatBehavior>();
            }

            if (combatBehavior != null)
            {
                Debug.Log($"[AIModule] Auto-discovered combat behavior: {combatBehavior.GetType().Name}");
            }
        }

        // Initialize combat behavior if found
        if (combatBehavior != null)
        {
            combatBehavior.Initialize(this, brain);
        }
        else
        {
            Debug.LogWarning("[AIModule] No combat behavior found! AI will not be able to attack.");
        }

        return true;
    }

    private void InitializeCompanions()
    {
        if (targetDetection == null)
            targetDetection = GetComponent<AITargetDetection>();

        if (stateUpdater == null)
            stateUpdater = GetComponent<AIStateUpdater>();

        if (targetDetection != null)
        {
            targetDetection.Initialize(this, brain);
        }
        else
        {
            Debug.LogWarning("[AIModule] AITargetDetection not found!");
        }

        if (stateUpdater != null)
        {
            stateUpdater.Initialize(this, targetDetection, npcMovement, combatBehavior, brain);
        }
        else
        {
            Debug.LogWarning("[AIModule] AIStateUpdater not found!");
        }
    }

    #endregion

    #region Update Loop

    public void UpdateModule()
    {
        if (!isEnabled || stateUpdater == null) return;

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

            case AIState.Patrol:
                // TODO: Implement patrol behavior
                break;

            case AIState.Flee:
                // TODO: Implement flee behavior
                break;

            case AIState.Investigate:
                // TODO: Implement investigate behavior
                break;
        }
    }

    #endregion

    #region State Management

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

        OnStateChanged?.Invoke(oldState, newState);
    }

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

    public void OnTargetDetected(Transform target)
    {
        currentTarget = target;
        ChangeState(AIState.Chase);
        OnTargetAcquired?.Invoke(target);
    }

    public void HandleTargetLost()
    {
        currentTarget = null;
        ChangeState(AIState.Idle);
        OnTargetLost?.Invoke();
    }

    public void OnEnteredCombatRange()
    {
        ChangeState(AIState.Combat);
    }

    public void OnExitedCombatRange()
    {
        ChangeState(AIState.Chase);
    }

    #endregion

    #region Public API

    public bool HasTarget() => currentTarget != null;

    public bool IsInCombat() => currentState == AIState.Combat;

    public float GetDistanceToTarget()
    {
        if (currentTarget == null) return float.MaxValue;
        return Vector3.Distance(transform.position, currentTarget.position);
    }

    public IAICombatBehavior GetCombatBehavior() => combatBehavior;

    public FactionType GetFaction()
    {
        return targetDetection != null ? targetDetection.GetFaction() : FactionType.Neutral;
    }

    public FactionRelationship GetRelationshipToTarget(Transform target)
    {
        return targetDetection != null ?
            targetDetection.GetRelationshipToTarget(target) :
            FactionRelationship.Neutral;
    }

    public void SetDetectionRange(float range)
    {
        if (targetDetection != null)
            targetDetection.SetDetectionRange(range);
    }

    public AITargetDetection GetTargetDetection() => targetDetection;

    public AIStateUpdater GetStateUpdater() => stateUpdater;

    #endregion
}