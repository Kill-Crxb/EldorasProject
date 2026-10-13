using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Transformation fields added to AbilityDefinition via partial class.
/// All lists are null by default — zero cost on abilities that don't use transforms.
/// Author these fields on the SO in the Inspector; they drive SlotTransformationSystem at runtime.
/// </summary>
public partial class AbilityDefinition
{
    [Header("Hit Procs")]
    [Tooltip("Each entry fires independently when a hit lands with this ability. " +
             "Multiple procs can fire from a single hit.")]
    public List<HitProcEntry> hitProcs;

    [Header("Sequence Prerequisites")]
    [Tooltip("This slot transforms when a preceding ability completes. " +
             "Enables combos like: Feint → slot becomes Riposte for 2s.")]
    public List<SequencePrerequisite> sequencePrerequisites;

    [Header("Resource Threshold Transforms")]
    [Tooltip("This slot transforms when a resource crosses a threshold. " +
             "Re-evaluated at 10Hz. E.g. Ki >= 80%% → Ki Burst.")]
    public List<ResourceThresholdTransform> resourceThresholds;

    [Header("Buff / Status Transforms")]
    [Tooltip("This slot transforms while a blackboard fact is true on this entity. " +
             "Re-evaluated at 10Hz. Integrates with SemanticBridgeSystem.")]
    public List<BuffTransform> buffTransforms;

    [Header("Target Condition Transforms")]
    [Tooltip("This slot transforms while a fact is true on the locked target's blackboard. " +
             "Re-evaluated at 10Hz via TargetLockModule.")]
    public List<TargetConditionTransform> targetConditions;

    [Header("Charge Attack")]
    [Tooltip("Ability that fires when this slot is held past chargeThreshold. " +
             "Charge bypasses the override system entirely.")]
    public AbilityDefinition chargeAbility;

    [Tooltip("Seconds the button must be held before the charge ability fires")]
    public float chargeThreshold = 0.8f;
}
