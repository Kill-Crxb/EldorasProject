using System;
using UnityEngine;

/// <summary>
/// All data types used by the slot transformation system.
/// Plain serialisable C# — no MonoBehaviour deps, fully unit-testable.
/// </summary>

public enum TransformationType
{
    HitProc,           // Priority 30 — on-hit proc (event-driven)
    SequencePrereq,    // Priority 25 — ability-used sequence (event-driven)
    BuffStatus,        // Priority 20 — blackboard fact present (reactive, 10Hz)
    ResourceThreshold, // Priority 15 — resource above/below threshold (reactive, 10Hz)
    TargetCondition,   // Priority 10 — target's blackboard fact (reactive, 10Hz)
}

// ComparisonOp (GTE/LTE) is defined in BlackboardCondition.cs — reused here.

// One hit-proc entry on an AbilityDefinition. When a hit lands with the source ability, each
// entry rolls its own d20 against its DC (Audit 5 O2: dice, not percentages).
[Serializable]
public class HitProcEntry
{
    [Tooltip("Ability that becomes available on the slot when this proc fires")]
    public AbilityDefinition targetAbility;

    [Range(1, 21)]
    [Tooltip("Fires on a d20 roll of this or higher. 1 = always, 11 = half the time, 21 = never.")]
    public int dc = 16;

    [Tooltip("How long the override window stays open (seconds)")]
    public float windowSeconds = 8f;
}

/// <summary>
/// One sequence-prerequisite entry on an AbilityDefinition.
/// When triggerAbilityId completes, the slot holding this definition
/// transforms to transformAbility for windowSeconds.
/// </summary>
[Serializable]
public class SequencePrerequisite
{
    [Tooltip("AbilityDefinition.abilityId that must complete first")]
    [IdRef(IdKind.Ability)] public string triggerAbilityId;

    [Tooltip("Override window duration after the trigger completes")]
    public float windowSeconds = 2f;

    [Tooltip("Ability that becomes available during the window")]
    public AbilityDefinition transformAbility;
}

/// <summary>
/// Replaces the slot ability when a resource crosses a threshold.
/// Re-evaluated at 10Hz; override is set/cleared automatically.
/// </summary>
[Serializable]
public class ResourceThresholdTransform
{
    [Tooltip("ResourceDefinition.resourceId to watch")]
    [IdRef(IdKind.Resource)] public string resourceId;

    [Range(0f, 1f)]
    [Tooltip("Threshold as a fraction of max (0–1)")]
    public float threshold = 0.5f;

    public ComparisonOp op = ComparisonOp.LessThanOrEqual;

    [Tooltip("Ability shown when the condition is met")]
    public AbilityDefinition transformAbility;
}

/// <summary>
/// Replaces the slot ability while a blackboard fact is true on this entity.
/// Re-evaluated at 10Hz.
/// </summary>
[Serializable]
public class BuffTransform
{
    [Tooltip("Blackboard key name (hashed at runtime — must match a SemanticBridge OutputFactKey)")]
    [IdRef(IdKind.Fact)] public string blackboardKey;

    [Tooltip("Ability shown while the key is true")]
    public AbilityDefinition transformAbility;
}

/// <summary>
/// Replaces the slot ability while a blackboard fact is true on the locked target.
/// Re-evaluated at 10Hz via TargetLockModule.
/// </summary>
[Serializable]
public class TargetConditionTransform
{
    [Tooltip("Blackboard key name to check on the locked target's Blackboard")]
    [IdRef(IdKind.Fact)] public string blackboardKey;

    [Tooltip("Ability shown while the condition is true")]
    public AbilityDefinition transformAbility;
}

/// <summary>
/// One active override on a slot. Stored in SlotTransformationSystem.
/// </summary>
public class SlotOverride
{
    public AbilityDefinition ability;
    public TransformationType type;
    public float expiresAt;   // Time.time + duration; -1 = permanent until cleared
    public float appliedAt;   // Time.time when applied — used for the UI timer ring
    public int priority;
}
