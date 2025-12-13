using UnityEngine;
using System.Collections;
using RPG.Tactical;

/// <summary>
/// Bear combat behavior with advanced tactical AI.
/// Features:
/// - 7 tactical states (Observing, Engaging, Defensive, Aggressive, Retreating, Flanking, Recovering)
/// - Threat assessment and dynamic behavior
/// - Tactical positioning system integration
/// - Weighted random ability selection
/// - Circling, flanking, and retreating behaviors
/// </summary>
public class BearCombatBehavior : TacticalCombatBehavior
{
    [Header("Bear Attack Settings")]
    [SerializeField] private string leftClawAbilityId = "bear_left_claw";
    [SerializeField] private string rightClawAbilityId = "bear_right_claw";
    [SerializeField] private string biteAbilityId = "bear_bite";

    [SerializeField] private float leftClawWeight = 0.4f;
    [SerializeField] private float rightClawWeight = 0.4f;
    [SerializeField] private float biteWeight = 0.2f;

    [Header("Bear Movement")]
    [SerializeField] private float minRange = 2f;
    [SerializeField] private float maxAttackRange = 6f;
    [SerializeField] private float optimalRange = 4f;
    [SerializeField] private float circleSpeed = 1.5f;
    [SerializeField] private float lungeSpeedMultiplier = 1.5f;
    [SerializeField] private float backAwaySpeedMultiplier = 0.8f;
    [SerializeField] private float rotationSpeed = 5f;

    [Header("Behavior Tuning")]
    [SerializeField] private float facingAngleThreshold = 45f;
    [SerializeField] private float arrivalThreshold = 1.5f;

    // Ability provider
    private IAbilityProvider abilityProvider;
    private IMovementProvider movementProvider;
    private Animator animator;

    // State tracking
    private bool isExecutingAttack = false;
    private int lastAttackType = -1;
    private float circleAngle = 0f;
    private PointDirection currentCircleDirection = PointDirection.Right;

    #region Initialization

    protected override void OnInitialize()
    {
        base.OnInitialize();

        // Override base attack ranges
        attackRange = maxAttackRange;
        exitCombatRange = maxAttackRange + 3f;

        // Get ability provider
        abilityProvider = brain.GetModuleImplementing<IAbilityProvider>();
        if (abilityProvider == null)
        {
            Debug.LogError($"[BearCombatBehavior] No IAbilityProvider found on {gameObject.name}");
            isEnabled = false;
            return;
        }

        // Get movement provider
        movementProvider = brain.GetModuleImplementing<IMovementProvider>();
        if (movementProvider == null)
        {
            Debug.LogWarning($"[BearCombatBehavior] No IMovementProvider found on {gameObject.name}");
        }

        // Get animator
        animator = brain.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"[BearCombatBehavior] No Animator found on {gameObject.name}");
        }

        if (debugMode || debugTactical)
        {
            Debug.Log($"[BearCombatBehavior] Initialized successfully on {gameObject.name}");
        }
    }

    #endregion

    #region Tactical Behavior Execution

    protected override void ExecuteTacticalBehavior(Transform target)
    {
        if (target == null || npcMovement == null) return;

        float distance = Vector3.Distance(transform.position, target.position);

        // Execute behavior based on tactical state
        switch (CurrentTacticalState)
        {
            case TacticalState.Observing:
                ObserveBehavior(distance);
                break;

            case TacticalState.Engaging:
                EngageBehavior(distance);
                break;

            case TacticalState.Defensive:
                DefensiveBehavior(distance);
                break;

            case TacticalState.Aggressive:
                AggressiveBehavior(distance);
                break;

            case TacticalState.Retreating:
                RetreatBehavior(distance);
                break;

            case TacticalState.Flanking:
                FlankingBehavior(distance);
                break;

            case TacticalState.Recovering:
                RecoveringBehavior(distance);
                break;
        }
    }

    #endregion

    #region Tactical Behaviors

    private void ObserveBehavior(float distance)
    {
        // Circle target slowly, studying patterns
        if (usePositioningSystem && assignedPoint != null)
        {
            CircleToPoint(assignedPoint);
        }
        else
        {
            CircleTargetManually();
        }

        // Occasional feint or test attack
        if (distance <= maxAttackRange && CanAttack())
        {
            if (Random.value < 0.2f) // 20% chance
            {
                string chosenAbility = ChooseAttackAbility();
                ExecuteAttackAbility(chosenAbility);
            }
        }

        // Maintain optimal distance
        if (distance < optimalRange - 1f)
        {
            BackAwayFromTarget();
        }
        else if (distance > optimalRange + 1f && distance <= maxAttackRange * 1.5f)
        {
            ApproachTarget();
        }
    }

    private void EngageBehavior(float distance)
    {
        // Standard aggressive combat
        if (distance > maxAttackRange)
        {
            ApproachTarget();
        }
        else if (distance <= maxAttackRange && CanAttack())
        {
            // Attack when in range
            string chosenAbility = ChooseAttackAbility();
            ExecuteAttackAbility(chosenAbility);
        }
        else if (distance < minRange)
        {
            BackAwayFromTarget();
        }
        else
        {
            // Circle to maintain pressure
            if (usePositioningSystem && assignedPoint != null)
            {
                CircleToPoint(assignedPoint);
            }
            else
            {
                CircleTargetManually();
            }
        }
    }

    private void DefensiveBehavior(float distance)
    {
        // Cautious: maintain distance, only attack when safe
        if (distance < optimalRange + 1f)
        {
            BackAwayFromTarget();
        }
        else
        {
            // Circle at range, looking for openings
            if (usePositioningSystem && assignedPoint != null)
            {
                CircleToPoint(assignedPoint);
            }
            else
            {
                CircleTargetManually();
            }

            // Only attack if target looks vulnerable
            if (distance <= maxAttackRange && CanAttack())
            {
                if (ThreatLevel < 0.5f && Random.value < 0.3f)
                {
                    string chosenAbility = ChooseAttackAbility();
                    ExecuteAttackAbility(chosenAbility);
                }
            }
        }
    }

    private void AggressiveBehavior(float distance)
    {
        // Press advantage: close distance aggressively, use strongest attacks
        if (distance > maxAttackRange * 0.8f)
        {
            // Lunge toward target
            ApproachTarget(lungeSpeedMultiplier);
        }
        else if (CanAttack())
        {
            // Prefer strongest attack (bite) when available
            if (abilityProvider.CanUseAbility(biteAbilityId))
            {
                ExecuteAttackAbility(biteAbilityId);
            }
            else
            {
                string chosenAbility = ChooseAttackAbility();
                ExecuteAttackAbility(chosenAbility);
            }
        }
        else if (distance < minRange)
        {
            // Too close, back up slightly but stay aggressive
            BackAwayFromTarget();
        }
    }

    private void RetreatBehavior(float distance)
    {
        // Survival mode: back away while facing target
        BackAwayFromTarget();
        FaceTarget();

        // Defensive swipes if cornered or pressured
        if (distance < minRange && CanAttack())
        {
            // Use quick attacks to create space
            if (abilityProvider.CanUseAbility(leftClawAbilityId))
            {
                ExecuteAttackAbility(leftClawAbilityId);
            }
            else if (abilityProvider.CanUseAbility(rightClawAbilityId))
            {
                ExecuteAttackAbility(rightClawAbilityId);
            }
        }
    }

    private void FlankingBehavior(float distance)
    {
        // Move to assigned flanking point or calculate manually
        if (usePositioningSystem && assignedPoint != null)
        {
            MoveToPosition(assignedPoint.WorldPosition);
            FaceTarget();

            // Attack once in flanking position
            if (HasReachedPoint(assignedPoint, arrivalThreshold) && distance <= maxAttackRange && CanAttack())
            {
                string chosenAbility = ChooseAttackAbility();
                ExecuteAttackAbility(chosenAbility);

                // Return to engaging after flanking attack
                CurrentTacticalState = TacticalState.Engaging;
                stateEnterTime = Time.time;
            }
        }
        else
        {
            // Fallback: move to target's side
            FlankTargetManually(distance);
        }
    }

    private void RecoveringBehavior(float distance)
    {
        // Back away to regenerate stamina
        if (distance < optimalRange + 2f)
        {
            BackAwayFromTarget();
        }
        else
        {
            // Circle at safe distance while stamina recovers
            if (usePositioningSystem && assignedPoint != null)
            {
                CircleToPoint(assignedPoint);
            }
            else
            {
                CircleTargetManually();
            }
        }

        // Don't attack unless absolutely necessary (cornered)
        if (distance < minRange - 0.5f && CanAttack())
        {
            // Defensive swipe only if pressured
            string chosenAbility = ChooseAttackAbility();
            ExecuteAttackAbility(chosenAbility);
        }

        // Return to engaging once stamina recovers
        if (GetStaminaPercent() > 0.5f)
        {
            CurrentTacticalState = TacticalState.Engaging;
            stateEnterTime = Time.time;
        }
    }

    #endregion

    #region Movement Helpers

    private void CircleToPoint(TacticalPoint point)
    {
        if (point == null) return;

        MoveToPosition(point.WorldPosition);
        FaceTarget();

        // Request next point in circle when arrived
        if (HasReachedPoint(point, arrivalThreshold) && targetPositioningSystem != null)
        {
            // Get next direction in circle
            currentCircleDirection = TacticalUtility.GetNextDirection(currentCircleDirection, true);

            var nextPoint = targetPositioningSystem.RequestPoint(
                gameObject,
                TacticalRole.Circling,
                new PointPreference
                {
                    PreferredDirection = currentCircleDirection,
                    PreferredRing = PointRing.Outer,
                    AllowAnyDirection = true
                },
                entityPriority
            );
        }
    }

    private void CircleTargetManually()
    {
        if (currentTarget == null) return;

        circleAngle += circleSpeed * Time.deltaTime;
        Vector3 offset = new Vector3(
            Mathf.Cos(circleAngle) * optimalRange,
            0f,
            Mathf.Sin(circleAngle) * optimalRange
        );
        Vector3 circlePosition = currentTarget.position + offset;

        MoveToPosition(circlePosition);
        FaceTarget();
    }

    private void FlankTargetManually(float distance)
    {
        if (currentTarget == null) return;

        Vector3 targetRight = currentTarget.right;
        Vector3 toTarget = currentTarget.position - transform.position;
        float dot = Vector3.Dot(transform.right, toTarget.normalized);

        // Flank to opposite side
        Vector3 flankPosition;
        if (dot < 0)
            flankPosition = currentTarget.position + targetRight * optimalRange;
        else
            flankPosition = currentTarget.position - targetRight * optimalRange;

        MoveToPosition(flankPosition);
        FaceTarget();

        // Attack once in flanking position
        float distanceToFlankPos = Vector3.Distance(transform.position, flankPosition);
        if (distanceToFlankPos < arrivalThreshold && distance <= maxAttackRange && CanAttack())
        {
            string chosenAbility = ChooseAttackAbility();
            ExecuteAttackAbility(chosenAbility);
            CurrentTacticalState = TacticalState.Engaging;
            stateEnterTime = Time.time;
        }
    }

    private void ApproachTarget(float speedMultiplier = 1f)
    {
        if (currentTarget == null || npcMovement == null) return;

        npcMovement.MoveTowards(
            currentTarget.position,
            speedMultiplier > 1f ? NPCMovementModule.MovementSpeed.Run : NPCMovementModule.MovementSpeed.Walk
        );
    }

    private void BackAwayFromTarget()
    {
        if (currentTarget == null || npcMovement == null) return;

        Vector3 direction = (transform.position - currentTarget.position).normalized;
        npcMovement.MoveInDirection(direction, NPCMovementModule.MovementSpeed.Walk);
        npcMovement.RotateTowards(currentTarget.position);
    }

    private void FaceTarget()
    {
        if (currentTarget == null || npcMovement == null) return;
        npcMovement.RotateTowards(currentTarget.position);
    }

    private void MoveToPosition(Vector3 targetPosition)
    {
        if (npcMovement == null) return;
        npcMovement.MoveTowards(targetPosition, NPCMovementModule.MovementSpeed.Walk);
    }

    #endregion

    #region Ability Selection & Execution

    private string ChooseAttackAbility()
    {
        // Build list of available abilities
        var availableAbilities = new System.Collections.Generic.List<(string id, float weight)>();

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

                // Set lastAttackType for animation
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
        if (string.IsNullOrEmpty(abilityId)) return;
        if (!abilityProvider.CanUseAbility(abilityId)) return;

        isExecutingAttack = true;

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
        StartCoroutine(WaitForAttackCompletion());

        if (debugMode || debugTactical)
        {
            Debug.Log($"[BearCombatBehavior] {gameObject.name} used {abilityId} ({CurrentTacticalState})");
        }
    }

    private IEnumerator WaitForAttackCompletion()
    {
        yield return new WaitForSeconds(0.6f);
        isExecutingAttack = false;
    }

    #endregion

    #region Attack Validation

    public override bool CanAttack()
    {
        if (!base.CanAttack()) return false;
        if (isExecutingAttack) return false;

        // Check facing
        bool isFacing = true;
        if (aiModule != null && aiModule.CurrentTarget != null && npcMovement != null)
        {
            isFacing = npcMovement.IsFacing(aiModule.CurrentTarget.position, facingAngleThreshold);
        }

        // Check if any ability is usable
        bool canUseAnyAbility = abilityProvider != null && (
            abilityProvider.CanUseAbility(leftClawAbilityId) ||
            abilityProvider.CanUseAbility(rightClawAbilityId) ||
            abilityProvider.CanUseAbility(biteAbilityId)
        );

        return isFacing && canUseAnyAbility;
    }

    #endregion

    #region Combat Enter/Exit

    public override void OnCombatEnter(Transform target)
    {
        base.OnCombatEnter(target);

        isExecutingAttack = false;
        lastAttackType = -1;
        circleAngle = Random.Range(0f, 360f); // Random starting angle
        currentCircleDirection = (PointDirection)Random.Range(0, 8); // Random starting direction

        if (debugMode || debugTactical)
        {
            Debug.Log($"[BearCombatBehavior] {gameObject.name} entered combat");
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

        if (debugMode || debugTactical)
        {
            Debug.Log($"[BearCombatBehavior] {gameObject.name} exited combat");
        }
    }

    #endregion
}