using UnityEngine;

/// <summary>
/// Test script that builds and runs a simple Behavior Tree
/// Demonstrates: Patrol → Detect → Combat → Return to Patrol
/// 
/// Usage: Attach to enemy with ControllerBrain + BehaviorTreeRunner
/// </summary>
public class SimpleEnemyBehaviorTreeTest : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool autoInitialize = true;
    [SerializeField] private bool debugTree = true;

    [Header("Behavior Parameters")]
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private string attackAbilityId = "BasicAttack";

    [Header("Patrol Settings")]
    [SerializeField] private Vector3[] patrolPoints = new Vector3[]
    {
        new Vector3(0, 0, 0),
        new Vector3(10, 0, 0),
        new Vector3(10, 0, 10),
        new Vector3(0, 0, 10)
    };
    [SerializeField] private float patrolWaitTime = 2f;

    private BehaviorTreeRunner treeRunner;
    private BehaviorTree tree;
    private int currentPatrolIndex = 0;

    void Start()
    {
        // Get or create BehaviorTreeRunner
        treeRunner = GetComponent<BehaviorTreeRunner>();
        if (treeRunner == null)
        {
            treeRunner = gameObject.AddComponent<BehaviorTreeRunner>();
        }

        if (autoInitialize)
        {
            BuildAndSetTree();
        }
    }

    /// <summary>
    /// Build a simple test tree and assign it to the runner
    /// </summary>
    public void BuildAndSetTree()
    {
        tree = BuildSimpleEnemyTree();
        tree.SetDebug(debugTree);
        treeRunner.SetTree(tree);

        Debug.Log($"[SimpleEnemyBehaviorTreeTest] Tree built and assigned: {tree.TreeName}");
    }

    /// <summary>
    /// Build the actual tree structure
    /// Root Selector:
    ///   1. Combat Branch (if target in range)
    ///   2. Patrol Branch (default behavior)
    /// </summary>
    private BehaviorTree BuildSimpleEnemyTree()
    {
        var tree = new BehaviorTree("Simple Enemy AI");

        // ===========================================
        // ROOT: Selector - Try combat first, then patrol
        // ===========================================
        var root = new BTSelector("Root Behavior");

        // ===========================================
        // BRANCH 1: Combat Sequence
        // ===========================================
        var combatBranch = new BTSequence("Combat Branch");

        // Condition: Has target in detection range
        combatBranch.AddChild(new BTHasTargetCondition());
        combatBranch.AddChild(new BTDistanceCheckCondition(
            BTDistanceCheckCondition.ComparisonType.LessThanOrEqual,
            detectionRange,
            "TargetInDetectionRange"
        ));

        // Combat behavior selector
        var combatSelector = new BTSelector("Combat Behavior");

        // Option 1: Attack if in range
        var attackSequence = new BTSequence("Attack Sequence");
        attackSequence.AddChild(new BTDistanceCheckCondition(
            BTDistanceCheckCondition.ComparisonType.LessThanOrEqual,
            attackRange,
            "TargetInAttackRange"
        ));
        attackSequence.AddChild(new BTFaceTargetAction(angleThreshold: 15f));
        attackSequence.AddChild(new BTExecuteAbilityAction(attackAbilityId));
        combatSelector.AddChild(attackSequence);

        // Option 2: Chase if too far
        var chaseSequence = new BTSequence("Chase Sequence");
        chaseSequence.AddChild(new BTMoveTowardTargetAction(
            acceptanceRadius: attackRange,
            speed: NPCMovementModule.MovementSpeed.Run
        ));
        combatSelector.AddChild(chaseSequence);

        combatBranch.AddChild(combatSelector);
        root.AddChild(combatBranch);

        // ===========================================
        // BRANCH 2: Patrol Sequence (Default)
        // ===========================================
        var patrolBranch = new BTSequence("Patrol Branch");

        // Create patrol behavior with custom condition
        var patrolSequence = new BTSequence("Patrol Behavior");

        // Move to current patrol point
        patrolSequence.AddChild(new BTCustomCondition(
            context =>
            {
                // Get current patrol point from blackboard
                int index = context.GetValue("patrolIndex", 0);
                if (index >= patrolPoints.Length)
                {
                    index = 0;
                    context.SetValue("patrolIndex", index);
                }

                // Set target position
                Vector3 targetPos = transform.position + patrolPoints[index];
                context.SetValue("patrolTarget", targetPos);
                return true;
            },
            "SetPatrolTarget"
        ));

        // Move to patrol point
        patrolSequence.AddChild(new BTCustomCondition(
            context =>
            {
                Vector3 targetPos = context.GetValue("patrolTarget", Vector3.zero);
                if (targetPos == Vector3.zero) return false;

                // Move toward patrol point
                float distance = Vector3.Distance(context.Transform.position, targetPos);
                if (distance <= 1f)
                {
                    // Arrived - increment patrol index
                    int index = context.GetValue("patrolIndex", 0);
                    index = (index + 1) % patrolPoints.Length;
                    context.SetValue("patrolIndex", index);
                    return true; // Success
                }

                // Still moving
                if (context.NPCMovement != null)
                {
                    context.NPCMovement.MoveTowards(targetPos, NPCMovementModule.MovementSpeed.Walk);
                }
                return false; // Keep trying (acts as Failure in Sequence)
            },
            "MoveToPatrolPoint"
        ));

        // Wait at patrol point
        patrolSequence.AddChild(new BTWaitAction(patrolWaitTime, "PatrolWait"));

        // Wrap in repeater to loop forever
        var patrolRepeater = new BTRepeater(patrolSequence, repeatCount: -1);
        patrolBranch.AddChild(patrolRepeater);

        root.AddChild(patrolBranch);

        // ===========================================
        // Set root and return
        // ===========================================
        tree.SetRootNode(root);
        return tree;
    }

    // ===========================================
    // Debug Helpers
    // ===========================================

    void OnDrawGizmosSelected()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        // Draw patrol path
        Gizmos.color = Color.yellow;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Vector3 point = transform.position + patrolPoints[i];
            Gizmos.DrawWireSphere(point, 0.5f);

            // Draw line to next point
            Vector3 nextPoint = transform.position + patrolPoints[(i + 1) % patrolPoints.Length];
            Gizmos.DrawLine(point, nextPoint);
        }

        // Draw detection range
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw attack range
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    // ===========================================
    // Public API for testing
    // ===========================================

    public void SetTarget(Transform target)
    {
        var context = treeRunner?.GetContext();
        if (context != null)
        {
            context.SetTarget(target);
            Debug.Log($"[SimpleEnemyBehaviorTreeTest] Target set: {target.name}");
        }
    }

    public void ClearTarget()
    {
        var context = treeRunner?.GetContext();
        if (context != null)
        {
            context.SetTarget(null);
            Debug.Log($"[SimpleEnemyBehaviorTreeTest] Target cleared");
        }
    }

    public void ResetTree()
    {
        treeRunner?.ResetTree();
        Debug.Log($"[SimpleEnemyBehaviorTreeTest] Tree reset");
    }
}
