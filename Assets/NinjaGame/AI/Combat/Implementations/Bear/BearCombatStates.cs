using UnityEngine;
using RPG.Tactical;
using System.Collections;

/// <summary>
/// Bear-specific combat states for state machine
/// </summary>

#region Observing State

/// <summary>
/// Observing State: Bear circles at outer ring, studying the player
/// </summary>
public class BearObservingState : ICombatState
{
    private BearCombatStateMachine bear;
    private PointDirection targetDirection;
    private bool hasRequestedPoint;

    public void OnEnter()
    {
        // Start at random direction
        targetDirection = (PointDirection)Random.Range(0, 8);
        hasRequestedPoint = false;
    }

    public void UpdateState(CombatStateMachine machine, Transform target)
    {
        bear = machine as BearCombatStateMachine;
        if (bear == null) return;

        if (bear.DebugStateMachine && !hasRequestedPoint)
        {
            Debug.Log("[BearObserving] Entered - circling outer ring");
        }

        // Request point in outer ring
        if (!hasRequestedPoint || bear.IsDwellComplete())
        {
            var preference = new PointPreference
            {
                PreferredDirection = targetDirection,
                PreferredRing = PointRing.Outer,
                AllowAnyDirection = true,
                PreferEmpty = true
            };

            bear.RequestTacticalPoint(TacticalRole.Circling, preference);
            hasRequestedPoint = true;
        }

        // Move to assigned point
        if (bear.AssignedPoint != null)
        {
            // Check arrival first
            bool hasArrived = bear.CheckPointArrival();

            if (!hasArrived)
            {
                // Still moving to point
                bear.MoveToPosition(bear.AssignedPoint.WorldPosition);
                bear.FaceTarget(target);
            }
            else
            {
                // At point - dwell
                bear.StopMovement();
                bear.FaceTarget(target);

                if (bear.IsDwellComplete())
                {
                    // Choose next adjacent direction
                    bool clockwise = Random.value > 0.5f;
                    targetDirection = bear.GetAdjacentDirection(targetDirection, clockwise);
                    hasRequestedPoint = false;
                }
            }
        }
        else
        {
            // FALLBACK: No assigned point yet - slowly approach and circle
            bear.ApproachTarget(target);
            bear.FaceTarget(target);
        }

        // Occasional feint attack (only if in range)
        float distanceToTarget = (bear.transform.position - target.position).magnitude;
        if (distanceToTarget <= bear.maxAttackRange && Random.value < 0.01f && bear.CanExecuteAttack(target))
        {
            bear.ExecuteRandomAttack();
        }
    }

    public void OnExit()
    {
        // No cleanup needed
    }
}

#endregion

#region Strafing State

/// <summary>
/// Strafing State: Bear at inner ring, feeling out player, ready to attack
/// </summary>
public class BearStrafingState : ICombatState
{
    private BearCombatStateMachine bear;
    private PointDirection currentDirection;
    private bool hasRequestedPoint;

    public void OnEnter()
    {
        // Pick initial direction based on current position relative to target
        hasRequestedPoint = false;
    }

    public void UpdateState(CombatStateMachine machine, Transform target)
    {
        bear = machine as BearCombatStateMachine;
        if (bear == null) return;

        // Request point in inner ring (strafing ring)
        if (!hasRequestedPoint)
        {
            // Find closest direction to current position
            currentDirection = GetClosestDirection(bear.transform.position, target.position, bear.strafeRingRadius);

            var preference = new PointPreference
            {
                PreferredDirection = currentDirection,
                PreferredRing = PointRing.Inner,
                AllowAnyDirection = true,
                PreferEmpty = true
            };

            bear.RequestTacticalPoint(TacticalRole.Pressuring, preference);
            hasRequestedPoint = true;
        }

        // Move to assigned point
        if (bear.AssignedPoint != null)
        {
            // Check arrival first
            bool hasArrived = bear.CheckPointArrival();

            if (!hasArrived)
            {
                // Still moving to point
                bear.MoveToPosition(bear.AssignedPoint.WorldPosition);
                bear.FaceTarget(target);
            }
            else
            {
                // At point - dwell and face player
                bear.StopMovement();
                bear.FaceTarget(target);

                if (bear.IsDwellComplete())
                {
                    // Stay in same quadrant - only move ±1 or ±2 positions
                    int offset = Random.Range(1, 3); // 1 or 2 positions
                    bool clockwise = Random.value > 0.5f;

                    for (int i = 0; i < offset; i++)
                    {
                        currentDirection = bear.GetAdjacentDirection(currentDirection, clockwise);
                    }

                    hasRequestedPoint = false;
                }
            }
        }

        // Player gets close → Enter attack cycle
        // (Transition handled by state machine)
    }

    public void OnExit()
    {
        // No cleanup needed
    }

    private PointDirection GetClosestDirection(Vector3 position, Vector3 targetPos, float radius)
    {
        Vector3 offset = position - targetPos;
        offset.y = 0;

        if (offset.magnitude < 0.1f)
            return PointDirection.Front;

        float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        // Map angle to 8 directions
        int index = Mathf.RoundToInt(angle / 45f) % 8;
        return (PointDirection)index;
    }
}

#endregion

#region Attack Cycle State

/// <summary>
/// Attack Cycle State: Free combat, no positioning system, pure aggression
/// </summary>
public class BearAttackCycleState : ICombatState
{
    private BearCombatStateMachine bear;

    public void OnEnter()
    {
        // Release positioning point - we're in free combat now
        if (bear != null)
        {
            bear.ReleaseTacticalPoint();
        }
    }

    public void UpdateState(CombatStateMachine machine, Transform target)
    {
        bear = machine as BearCombatStateMachine;
        if (bear == null) return;

        float distance = bear.GetDistanceToTarget(target);

        // More aggressive debug logging - every 10 frames to catch issues
        if (bear.DebugStateMachine && Time.frameCount % 10 == 0)
        {
            Debug.Log($"[AttackCycle] Dist:{distance:F1}m | CanAtk:{bear.CanExecuteAttack(target)}");
        }

        // Pure combat logic - no positioning system
        if (distance > bear.maxAttackRange)
        {
            // Too far, approach (tracks player position every frame)
            if (bear.DebugStateMachine && Time.frameCount % 10 == 0)
            {
                Debug.Log($"[AttackCycle] APPROACHING → TargetPos:{target.position:F1}");
            }
            bear.ApproachTarget(target);
            // CRITICAL: Face target AFTER movement to ensure rotation applies
            bear.FaceTarget(target);
        }
        else if (distance < bear.minAttackRange)
        {
            // Too close, back away
            if (bear.DebugStateMachine && Time.frameCount % 10 == 0)
            {
                Debug.Log($"[AttackCycle] BACKING AWAY");
            }
            bear.BackAwayFromTarget(target);
            // BackAwayFromTarget already handles facing
        }
        else
        {
            // In attack range (1-2.5m) - always face target
            bear.FaceTarget(target);

            if (bear.CanExecuteAttack(target))
            {
                // Stop and attack
                if (bear.DebugStateMachine)
                {
                    Debug.Log($"[AttackCycle] ATTACKING at {distance:F1}m");
                }
                bear.StopMovement();
                bear.ExecuteRandomAttack();
            }
            else
            {
                // Can't attack yet - just maintain position
                if (bear.DebugStateMachine && Time.frameCount % 10 == 0)
                {
                    Debug.Log($"[AttackCycle] WAITING (cooldown/stamina)");
                }
                bear.StopMovement();
            }
        }
    }

    public void OnExit()
    {
        // No cleanup needed
    }
}

#endregion

#region Retreat State

/// <summary>
/// Retreat State: Bear is low health, trying to escape
/// </summary>
public class BearRetreatState : ICombatState
{
    private BearCombatStateMachine bear;

    public void OnEnter()
    {
        if (bear != null)
        {
            bear.ReleaseTacticalPoint();
        }
    }

    public void UpdateState(CombatStateMachine machine, Transform target)
    {
        bear = machine as BearCombatStateMachine;
        if (bear == null) return;

        // Back away from target
        bear.BackAwayFromTarget(target);
        bear.FaceTarget(target);

        float distance = bear.GetDistanceToTarget(target);

        // Defensive swipe if cornered
        if (distance < bear.minAttackRange * 1.5f && bear.CanExecuteAttack(target))
        {
            if (Random.value < 0.3f) // 30% chance
            {
                bear.ExecuteRandomAttack();
            }
        }
    }

    public void OnExit()
    {
        // No cleanup needed
    }
}

#endregion

#region Recover State

/// <summary>
/// Recover State: Bear needs to regenerate stamina
/// </summary>
public class BearRecoverState : ICombatState
{
    private BearCombatStateMachine bear;
    private bool hasRequestedPoint;

    public void OnEnter()
    {
        hasRequestedPoint = false;
    }

    public void UpdateState(CombatStateMachine machine, Transform target)
    {
        bear = machine as BearCombatStateMachine;
        if (bear == null) return;

        // Calculate distance once
        float distance = bear.GetDistanceToTarget(target);

        // Request outer ring point for safety
        if (!hasRequestedPoint)
        {
            var preference = new PointPreference
            {
                PreferredDirection = PointDirection.Back,
                PreferredRing = PointRing.Outer,
                AllowAnyDirection = true
            };

            bear.RequestTacticalPoint(TacticalRole.Waiting, preference);
            hasRequestedPoint = true;
        }

        // Move to point and wait, or just back away if no positioning system
        if (bear.AssignedPoint != null)
        {
            bool hasArrived = bear.CheckPointArrival();

            if (!hasArrived)
            {
                // Still moving to point
                bear.MoveToPosition(bear.AssignedPoint.WorldPosition);
                bear.FaceTarget(target);
            }
            else
            {
                // At point - just dwell, facing player
                bear.StopMovement();
                bear.FaceTarget(target);
            }
        }
        else
        {
            // No positioning system - just back away from player
            if (distance < bear.observeRingRadius)
            {
                bear.BackAwayFromTarget(target);
            }
            else
            {
                bear.StopMovement();
                bear.FaceTarget(target);
            }
        }

        // Don't attack unless pressured
        if (distance < bear.minAttackRange && bear.CanExecuteAttack(target))
        {
            if (Random.value < 0.2f) // 20% chance - reluctant
            {
                bear.ExecuteRandomAttack();
            }
        }
    }

    public void OnExit()
    {
        // No cleanup needed
    }
}

#endregion