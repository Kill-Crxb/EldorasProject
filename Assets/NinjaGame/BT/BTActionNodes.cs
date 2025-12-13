using UnityEngine;

/// <summary>
/// Base class for action nodes (leaf nodes that do things)
/// Actions are the nodes that actually interact with the game world
/// </summary>
public abstract class BTActionNode : BTNode
{
    protected BTActionNode(string name = null) : base(name) { }
}


/// <summary>
/// Wait Action - Waits for specified duration
/// Returns Running while waiting, Success when complete
/// </summary>
public class BTWaitAction : BTActionNode
{
    [SerializeField] private float duration;
    private float startTime = -1f;

    // Parameterless constructor for serialization
    public BTWaitAction() : base("Wait") { }

    public BTWaitAction(float duration, string name = null) : base(name ?? "Wait")
    {
        this.duration = duration;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        // Start timer
        if (startTime < 0f)
        {
            startTime = Time.time;
            if (debugMode)
                context.Log($"Wait started: {duration}s");
        }

        // Check if complete
        float elapsed = Time.time - startTime;
        if (elapsed >= duration)
        {
            if (debugMode)
                context.Log($"Wait complete: {elapsed:F2}s");
            startTime = -1f; // Reset for next run
            return NodeState.Success;
        }

        return NodeState.Running;
    }

    public override void Reset()
    {
        base.Reset();
        startTime = -1f;
    }
}


/// <summary>
/// Log Action - Logs a message to console
/// Useful for debugging tree execution
/// </summary>
public class BTLogAction : BTActionNode
{
    [SerializeField] private string message;
    [SerializeField] private LogType logType = LogType.Log;

    // Parameterless constructor for serialization
    public BTLogAction() : base("Log") { }

    public BTLogAction(string message, LogType logType = LogType.Log, string name = null)
        : base(name ?? "Log")
    {
        this.message = message;
        this.logType = logType;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        switch (logType)
        {
            case LogType.Log:
                context.Log(message);
                break;
            case LogType.Warning:
                context.LogWarning(message);
                break;
            case LogType.Error:
                Debug.LogError($"[BTLog:{context.GameObject?.name}] {message}");
                break;
        }

        return NodeState.Success;
    }
}


/// <summary>
/// Move To Position Action - Moves entity toward target position
/// Returns Running while moving, Success when close enough
/// </summary>
public class BTMoveToPositionAction : BTActionNode
{
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private float acceptanceRadius = 0.5f;
    [SerializeField] private NPCMovementModule.MovementSpeed movementSpeed = NPCMovementModule.MovementSpeed.Run;

    // Parameterless constructor for serialization
    public BTMoveToPositionAction() : base("MoveToPosition") { }

    public BTMoveToPositionAction(
        Vector3 targetPosition,
        float acceptanceRadius = 0.5f,
        NPCMovementModule.MovementSpeed speed = NPCMovementModule.MovementSpeed.Run,
        string name = null) : base(name ?? "MoveToPosition")
    {
        this.targetPosition = targetPosition;
        this.acceptanceRadius = acceptanceRadius;
        this.movementSpeed = speed;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (context.NPCMovement == null)
        {
            context.LogWarning("MoveToPosition: No NPCMovementModule found");
            return NodeState.Failure;
        }

        // Check if we've arrived
        float distance = Vector3.Distance(context.Transform.position, targetPosition);
        if (distance <= acceptanceRadius)
        {
            if (debugMode)
                context.Log($"MoveToPosition: Arrived at {targetPosition}");
            return NodeState.Success;
        }

        // Move toward target
        context.NPCMovement.MoveTowards(targetPosition, movementSpeed);

        if (debugMode && Time.frameCount % 60 == 0) // Log every 60 frames
        {
            context.Log($"MoveToPosition: Moving to {targetPosition}, distance: {distance:F2}m");
        }

        return NodeState.Running;
    }
}


/// <summary>
/// Move Toward Target Action - Moves entity toward dynamic target
/// Returns Running while moving, Success when close enough
/// </summary>
public class BTMoveTowardTargetAction : BTActionNode
{
    [SerializeField] private float acceptanceRadius = 2f;
    [SerializeField] private NPCMovementModule.MovementSpeed movementSpeed = NPCMovementModule.MovementSpeed.Run;

    // Parameterless constructor for serialization
    public BTMoveTowardTargetAction() : base("MoveTowardTarget") { }

    public BTMoveTowardTargetAction(
        float acceptanceRadius = 2f,
        NPCMovementModule.MovementSpeed speed = NPCMovementModule.MovementSpeed.Run,
        string name = null) : base(name ?? "MoveTowardTarget")
    {
        this.acceptanceRadius = acceptanceRadius;
        this.movementSpeed = speed;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        Transform target = context.GetTarget();
        if (target == null)
        {
            if (debugMode)
                context.LogWarning("MoveTowardTarget: No target found");
            return NodeState.Failure;
        }

        if (context.NPCMovement == null)
        {
            context.LogWarning("MoveTowardTarget: No NPCMovementModule found");
            return NodeState.Failure;
        }

        // Check if we've arrived
        float distance = Vector3.Distance(context.Transform.position, target.position);
        if (distance <= acceptanceRadius)
        {
            if (debugMode)
                context.Log($"MoveTowardTarget: Within range ({distance:F2}m <= {acceptanceRadius}m)");
            return NodeState.Success;
        }

        // Move toward target
        context.NPCMovement.MoveTowards(target.position, movementSpeed);

        if (debugMode && Time.frameCount % 60 == 0)
        {
            context.Log($"MoveTowardTarget: Moving toward {target.name}, distance: {distance:F2}m");
        }

        return NodeState.Running;
    }
}


/// <summary>
/// Face Target Action - Rotates entity to face target
/// Returns Success when facing target (within angle threshold)
/// </summary>
public class BTFaceTargetAction : BTActionNode
{
    [SerializeField] private float angleThreshold = 10f;

    // Parameterless constructor for serialization
    public BTFaceTargetAction() : base("FaceTarget") { }

    public BTFaceTargetAction(float angleThreshold = 10f, string name = null)
        : base(name ?? "FaceTarget")
    {
        this.angleThreshold = angleThreshold;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        Transform target = context.GetTarget();
        if (target == null)
        {
            if (debugMode)
                context.LogWarning("FaceTarget: No target found");
            return NodeState.Failure;
        }

        if (context.NPCMovement == null)
        {
            context.LogWarning("FaceTarget: No NPCMovementModule found");
            return NodeState.Failure;
        }

        // Calculate direction to target
        Vector3 directionToTarget = (target.position - context.Transform.position).normalized;
        directionToTarget.y = 0f; // Keep rotation on horizontal plane

        if (directionToTarget.sqrMagnitude < 0.001f)
        {
            return NodeState.Success; // Too close to determine direction
        }

        // Check if already facing target
        float angle = Vector3.Angle(context.Transform.forward, directionToTarget);
        if (angle <= angleThreshold)
        {
            if (debugMode)
                context.Log($"FaceTarget: Already facing target (angle: {angle:F1}°)");
            return NodeState.Success;
        }

        // Face target
        context.NPCMovement.RotateToDirection(directionToTarget);

        if (debugMode)
            context.Log($"FaceTarget: Rotating toward target (angle: {angle:F1}°)");

        return NodeState.Running;
    }
}


/// <summary>
/// Execute Ability Action - Triggers an ability by ID
/// Returns Running while casting, Success/Failure when complete
/// </summary>
public class BTExecuteAbilityAction : BTActionNode
{
    [SerializeField] private string abilityId;
    private bool hasStarted = false;

    // Parameterless constructor for serialization
    public BTExecuteAbilityAction() : base("ExecuteAbility") { }

    public BTExecuteAbilityAction(string abilityId, string name = null)
        : base(name ?? $"ExecuteAbility({abilityId})")
    {
        this.abilityId = abilityId;
    }

    protected override NodeState OnEvaluate(BTContext context)
    {
        if (context.Ability == null)
        {
            context.LogWarning("ExecuteAbility: No IAbilityProvider found");
            return NodeState.Failure;
        }

        // Check if ability is ready
        if (!context.IsAbilityReady(abilityId))
        {
            if (debugMode)
                context.Log($"ExecuteAbility: {abilityId} not ready");
            return NodeState.Failure;
        }

        // Start ability
        if (!hasStarted)
        {
            bool canUse = context.Ability.CanUseAbility(abilityId);
            if (!canUse)
            {
                if (debugMode)
                    context.LogWarning($"ExecuteAbility: Cannot use {abilityId}");
                return NodeState.Failure;
            }

            context.Ability.UseAbility(abilityId);
            hasStarted = true;

            if (debugMode)
                context.Log($"ExecuteAbility: Started {abilityId}");
        }

        // Check if ability is still on cooldown (means it's still active/just finished)
        bool isOnCooldown = context.Ability.IsAbilityOnCooldown(abilityId);
        if (isOnCooldown)
        {
            return NodeState.Running;
        }

        // Ability complete
        hasStarted = false;
        if (debugMode)
            context.Log($"ExecuteAbility: {abilityId} complete");
        return NodeState.Success;
    }

    public override void Reset()
    {
        base.Reset();
        hasStarted = false;
    }
}