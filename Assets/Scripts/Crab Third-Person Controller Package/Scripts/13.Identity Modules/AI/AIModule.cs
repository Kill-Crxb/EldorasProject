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


    [SerializeField] private Transform currentTarget;

    private ControllerBrain brain;
    private NPCMovementModule npcMovement;
    private IAICombatBehavior combatBehavior;

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

    private bool FindRequiredModules()
    {
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
            return false;
        }

        combatBehavior = brain.GetModule<AICombatBehaviorModule>() as IAICombatBehavior;

        if (combatBehavior == null)
        {
            combatBehavior = GetComponentInChildren<IAICombatBehavior>();
        }

        if (combatBehavior != null)
        {
            combatBehavior.Initialize(this, brain);
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

        if (stateUpdater != null)
        {
            stateUpdater.Initialize(this, targetDetection, npcMovement, combatBehavior, brain);
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
                break;

            case AIState.Flee:
                break;

            case AIState.Investigate:
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