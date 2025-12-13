using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using RPG.Tactical;

/// <summary>
/// Bear combat AI using state machine architecture.
/// 
/// States:
/// - Observing: Circles at outer ring, studies player
/// - Strafing: Inner ring, feeling out player, ready to engage
/// - AttackCycle: Free combat, ignores positioning, pure aggression
/// - Retreat: Low health, trying to escape
/// - Recover: Low stamina, regenerating at safe distance
/// </summary>
public class BearCombatStateMachine : CombatStateMachine
{
    [Header("Bear Abilities")]
    [SerializeField] private string leftClawAbilityId = "bear_left_claw";
    [SerializeField] private string rightClawAbilityId = "bear_right_claw";
    [SerializeField] private string biteAbilityId = "bear_bite";

    [SerializeField] private float leftClawWeight = 0.4f;
    [SerializeField] private float rightClawWeight = 0.4f;
    [SerializeField] private float biteWeight = 0.2f;

    [Header("Bear Combat Ranges")]
    public float minAttackRange = 1f; // Very close
    public float maxAttackRange = 2.5f; // Slightly more room (was 2f)
    public float strafeRingRadius = 5f;
    public float observeRingRadius = 12f;

    [Header("State Transition Thresholds")]
    [SerializeField] private float engageDistance = 3f; // Much closer - must close gap to attack
    [SerializeField] private float strafeDistance = 6f; // Closer strafing distance
    [SerializeField] private float retreatHealthPercent = 0.2f;
    [SerializeField] private float recoverStaminaPercent = 0.2f;

    [Header("Movement")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private float arrivalThreshold = 1.5f;

    [Header("Attack Timing")]
    [SerializeField] private float minTimeBetweenAttacks = 1.5f; // Minimum delay between attacks
    [SerializeField] private float attackCommitTime = 1.0f; // Time to stop after attacking

    // States
    private BearObservingState observingState;
    private BearStrafingState strafingState;
    private BearAttackCycleState attackCycleState;
    private BearRetreatState retreatState;
    private BearRecoverState recoverState;

    // Attack execution
    private bool isExecutingAttack = false;
    private int lastAttackType = -1;
    private float lastAttackTime = -999f; // Track when last attack happened
    private bool isInAttackCommit = false; // Post-attack recovery period

    #region Initialization

    protected override void InitializeStates()
    {
        // Create state instances
        observingState = new BearObservingState();
        strafingState = new BearStrafingState();
        attackCycleState = new BearAttackCycleState();
        retreatState = new BearRetreatState();
        recoverState = new BearRecoverState();

        // Set combat ranges
        attackRange = maxAttackRange;
        exitCombatRange = observeRingRadius + 3f;

        // Start in observing state
        ChangeState(observingState, "Observing");

        if (debugStateMachine)
        {
            Debug.Log($"[BearCombatStateMachine] States initialized on {gameObject.name}");
        }
    }

    #endregion

    #region State Transitions

    protected override void CheckStateTransitions(Transform target)
    {
        if (target == null) return;

        float healthPercent = GetHealthPercent();
        float staminaPercent = GetStaminaPercent();
        float distance = GetDistanceToTarget(target);

        // Debug every 60 frames
        if (debugStateMachine && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[StateTransitions] State: {currentStateName}, Distance: {distance:F2}m, HP: {healthPercent:P0}, Stamina: {staminaPercent:P0}");
        }

        // PRIORITY 1: Retreat if very low health
        if (healthPercent < retreatHealthPercent)
        {
            if (currentStateName != "Retreat")
            {
                ChangeState(retreatState, "Retreat");
            }
            return;
        }

        // PRIORITY 2: Recover if low stamina
        if (staminaPercent < recoverStaminaPercent)
        {
            if (currentStateName != "Recover")
            {
                ChangeState(recoverState, "Recover");
            }
            return;
        }

        // PRIORITY 3: Exit recover state if stamina restored
        if (currentStateName == "Recover" && staminaPercent > 0.5f)
        {
            ChangeState(strafeDistance < distance ? observingState : strafingState,
                        strafeDistance < distance ? "Observing" : "Strafing");
            return;
        }

        // PRIORITY 4: Distance-based transitions

        // CRITICAL: Check AttackCycle exit FIRST before any other distance transitions
        // This provides hysteresis - stay in AttackCycle until player creates real distance
        if (currentStateName == "AttackCycle" && distance >= strafeDistance)
        {
            // Exit AttackCycle only when player is at strafe distance or beyond
            if (debugStateMachine)
            {
                Debug.Log($"[StateTransitions] Distance {distance:F2}m >= {strafeDistance}m → Exiting AttackCycle to Strafing");
            }
            ChangeState(strafingState, "Strafing");
        }
        else if (distance < engageDistance)
        {
            // Close range → Attack Cycle
            if (currentStateName != "AttackCycle")
            {
                if (debugStateMachine)
                {
                    Debug.Log($"[StateTransitions] Distance {distance:F2}m < {engageDistance}m → Entering AttackCycle");
                }
                ChangeState(attackCycleState, "AttackCycle");
            }
        }
        else if (distance < strafeDistance)
        {
            // Medium range → Strafing (only if NOT in AttackCycle)
            if (currentStateName != "Strafing" && currentStateName != "AttackCycle")
            {
                if (debugStateMachine)
                {
                    Debug.Log($"[StateTransitions] Distance {distance:F2}m < {strafeDistance}m → Entering Strafing");
                }
                ChangeState(strafingState, "Strafing");
            }
        }
        else
        {
            // Far range → Observing
            if (currentStateName != "Observing" && currentStateName != "Recover")
            {
                if (debugStateMachine)
                {
                    Debug.Log($"[StateTransitions] Distance {distance:F2}m > {strafeDistance}m → Entering Observing");
                }
                ChangeState(observingState, "Observing");
            }
        }
    }

    #endregion

    #region Movement Helpers (Public for states)

    public void MoveToPosition(Vector3 targetPosition)
    {
        if (npcMovement != null)
        {
            // Use walk speed for deliberate, Dark Souls-style movement
            npcMovement.MoveTowards(targetPosition, NPCMovementModule.MovementSpeed.Walk);
        }
    }

    public void ApproachTarget(Transform target)
    {
        if (npcMovement == null || target == null) return;

        float distance = (transform.position - target.position).magnitude;

        // Determine speed: Run when chasing from distance, Walk when in melee range
        NPCMovementModule.MovementSpeed speed = ShouldChase(distance)
            ? NPCMovementModule.MovementSpeed.Run
            : NPCMovementModule.MovementSpeed.Walk;

        npcMovement.MoveTowards(target.position, speed);
    }

    public void BackAwayFromTarget(Transform target)
    {
        if (npcMovement == null || target == null) return;

        Vector3 direction = (transform.position - target.position).normalized;
        // Always run when retreating - it's an emergency!
        npcMovement.MoveInDirection(direction, NPCMovementModule.MovementSpeed.Run);
        npcMovement.RotateTowards(target.position);
    }

    /// <summary>
    /// Determines if the bear should aggressively chase (run) or fight cautiously (walk)
    /// </summary>
    private bool ShouldChase(float distance)
    {
        // Chase threshold: far enough to warrant running
        const float CHASE_DISTANCE = 4f;

        // Safety thresholds: don't chase if low on resources
        const float LOW_HEALTH_THRESHOLD = 0.3f;  // 30%
        const float LOW_STAMINA_THRESHOLD = 0.2f; // 20%

        // Don't chase if in danger - be cautious
        if (healthProvider != null && resourceProvider != null)
        {
            float healthPercent = healthProvider.GetCurrentHealth() / healthProvider.GetMaxHealth();

            float currentStamina = resourceProvider.GetResource(ResourceType.Stamina);
            float maxStamina = resourceProvider.GetMaxResource(ResourceType.Stamina);
            float staminaPercent = maxStamina > 0 ? currentStamina / maxStamina : 1f;

            bool isInDanger = healthPercent < LOW_HEALTH_THRESHOLD || staminaPercent < LOW_STAMINA_THRESHOLD;

            if (isInDanger)
                return false; // Walk cautiously when low on resources
        }

        // Chase if far enough away - be aggressive!
        return distance > CHASE_DISTANCE;
    }

    public void FaceTarget(Transform target)
    {
        if (npcMovement != null && target != null)
        {
            npcMovement.RotateTowards(target.position);
        }
    }

    public void StopMovement()
    {
        if (npcMovement != null)
        {
            npcMovement.Stop();
        }
    }

    #endregion

    #region Attack Execution (Public for states)

    public bool CanExecuteAttack(Transform target)
    {
        if (isExecutingAttack) return false;
        if (isInAttackCommit) return false; // Still in post-attack recovery
        if (abilityProvider == null) return false;
        if (target == null) return false;

        // Check minimum time between attacks
        if (Time.time - lastAttackTime < minTimeBetweenAttacks)
            return false;

        // Check if facing target
        if (npcMovement != null)
        {
            if (!npcMovement.IsFacing(target.position, 45f))
                return false;
        }

        // Check if any ability is available
        bool hasAvailableAbility = abilityProvider.CanUseAbility(leftClawAbilityId) ||
                                    abilityProvider.CanUseAbility(rightClawAbilityId) ||
                                    abilityProvider.CanUseAbility(biteAbilityId);

        return hasAvailableAbility;
    }

    public void ExecuteRandomAttack()
    {
        string chosenAbility = ChooseAttackAbility();
        if (string.IsNullOrEmpty(chosenAbility)) return;

        ExecuteAttackAbility(chosenAbility);
    }

    private string ChooseAttackAbility()
    {
        var availableAbilities = new List<(string id, float weight)>();

        if (abilityProvider.CanUseAbility(leftClawAbilityId))
            availableAbilities.Add((leftClawAbilityId, leftClawWeight));

        if (abilityProvider.CanUseAbility(rightClawAbilityId))
            availableAbilities.Add((rightClawAbilityId, rightClawWeight));

        if (abilityProvider.CanUseAbility(biteAbilityId))
            availableAbilities.Add((biteAbilityId, biteWeight));

        if (availableAbilities.Count == 0)
            return null;

        // Weighted random selection
        float totalWeight = 0f;
        foreach (var ability in availableAbilities)
            totalWeight += ability.weight;

        float roll = Random.value * totalWeight;
        float cumulative = 0f;

        for (int i = 0; i < availableAbilities.Count; i++)
        {
            cumulative += availableAbilities[i].weight;
            if (roll <= cumulative)
            {
                string selectedId = availableAbilities[i].id;

                // Set attack type for animation
                if (selectedId == leftClawAbilityId)
                    lastAttackType = 0;
                else if (selectedId == rightClawAbilityId)
                    lastAttackType = 1;
                else if (selectedId == biteAbilityId)
                    lastAttackType = 2;

                return selectedId;
            }
        }

        return availableAbilities[0].id;
    }

    private void ExecuteAttackAbility(string abilityId)
    {
        if (!abilityProvider.CanUseAbility(abilityId)) return;

        // STOP MOVEMENT - Commit to attack
        StopMovement();

        isExecutingAttack = true;
        lastAttackTime = Time.time;

        // Trigger animation
        if (animator != null)
        {
            string triggerName = lastAttackType switch
            {
                0 => "LeftClawAttack",
                1 => "RightClawAttack",
                2 => "BiteAttack",
                _ => "LeftClawAttack"
            };
            animator.SetTrigger(triggerName);
        }

        // Use ability
        abilityProvider.UseAbility(abilityId);
        RecordAttack();

        // Start attack completion coroutine
        StartCoroutine(AttackCommitmentCoroutine());

        if (debugStateMachine)
        {
            Debug.Log($"[BearCombatStateMachine] Executed {abilityId} in {currentStateName} state");
        }
    }

    private IEnumerator AttackCommitmentCoroutine()
    {
        // Animation duration
        yield return new WaitForSeconds(0.6f);
        isExecutingAttack = false;

        // Post-attack commit time (can't move immediately)
        isInAttackCommit = true;
        yield return new WaitForSeconds(attackCommitTime);
        isInAttackCommit = false;
    }

    #endregion

    #region Attack Validation Override

    public override bool CanAttack()
    {
        // Delegate to state-aware check
        return !isExecutingAttack && abilityProvider != null;
    }

    #endregion

    #region Combat Enter/Exit

    public override void OnCombatEnter(Transform target)
    {
        base.OnCombatEnter(target);

        isExecutingAttack = false;
        lastAttackType = -1;

        // Reset to observing state
        if (observingState != null)
        {
            ChangeState(observingState, "Observing");
        }

        if (debugStateMachine)
        {
            Debug.Log($"[BearCombatStateMachine] {gameObject.name} entered combat");
        }
    }

    public override void OnCombatExit()
    {
        base.OnCombatExit();

        isExecutingAttack = false;

        if (npcMovement != null)
        {
            npcMovement.Stop();
        }

        if (debugStateMachine)
        {
            Debug.Log($"[BearCombatStateMachine] {gameObject.name} exited combat");
        }
    }

    #endregion
}