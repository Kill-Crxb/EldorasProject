using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Maintains active overrides on hotbar slots and evaluates all reactive transforms.
/// The view layer's single query point — ActionBarSlotView always calls GetEffectiveAbility,
/// never decides what to show on its own.
///
/// Override priority: higher wins. FIFO at equal priority.
/// Reactive transforms (ResourceThreshold, Buff, TargetCondition) are evaluated at 10Hz.
/// Event-driven transforms (HitProc, SequencePrereq) are applied externally and expire automatically.
/// </summary>
public class SlotTransformationSystem : MonoBehaviour, IBrainModule
{
    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    // ── Cached refs ───────────────────────────────────────────────────────

    private ControllerBrain brain;
    private HotbarSystem hotbarSystem;
    private AbilitySystem abilitySystem;
    private IResourceProvider resourceProvider;
    private BlackboardSystem blackboardSystem;
    private TargetLockModule targetLock;

    // ── Override state ────────────────────────────────────────────────────

    // Pre-built zero-allocation slot keys (3 bars × 12 max slots).
    private static readonly string[] s_centreKeys;
    private static readonly string[] s_bottomLeftKeys;
    private static readonly string[] s_bottomRightKeys;

    static SlotTransformationSystem()
    {
        s_centreKeys      = BuildKeys("centre",      12);
        s_bottomLeftKeys  = BuildKeys("bottomLeft",  12);
        s_bottomRightKeys = BuildKeys("bottomRight", 12);
    }

    private static string[] BuildKeys(string barId, int count)
    {
        var keys = new string[count];
        for (int i = 0; i < count; i++) keys[i] = $"{barId}:{i}";
        return keys;
    }

    private string SlotKey(string barId, int index)
    {
        if (barId == "centre"      && index < s_centreKeys.Length)      return s_centreKeys[index];
        if (barId == "bottomLeft"  && index < s_bottomLeftKeys.Length)  return s_bottomLeftKeys[index];
        if (barId == "bottomRight" && index < s_bottomRightKeys.Length) return s_bottomRightKeys[index];
        return $"{barId}:{index}"; // fallback for out-of-range
    }

    private readonly Dictionary<string, SlotOverride> activeOverrides
        = new Dictionary<string, SlotOverride>();

    // 10Hz throttle — matches SemanticBridgeSystem pulse
    private float nextUpdateTime;
    private const float UpdateInterval = 0.1f;

    // Resource definition cache built in LateInitialize
    private readonly Dictionary<string, ResourceDefinition> resourceDefCache
        = new Dictionary<string, ResourceDefinition>();

    public event Action<string, int> OnOverrideChanged;

    public bool IsEnabled { get; set; } = true;

    // ── IBrainModule ─────────────────────────────────────────────────────

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain            = controllerBrain;
        abilitySystem    = brain.GetModule<AbilitySystem>();
        blackboardSystem = brain.GetModule<BlackboardSystem>();
        targetLock       = brain.GetModule<TargetLockModule>();
        resourceProvider = brain.GetProvider<IResourceProvider>();
    }

    public void LateInitialize()
    {
        hotbarSystem = brain.GetModule<HotbarSystem>();

        if (abilitySystem != null)
            abilitySystem.OnAbilityUsed += HandleAbilityUsed;

        // Cache ResourceDefinitions by resourceId for threshold checks
        if (ResourceManager.Instance != null)
            foreach (var def in ResourceManager.Instance.GetAll())
                if (!string.IsNullOrEmpty(def.resourceId))
                    resourceDefCache[def.resourceId] = def;

        nextUpdateTime = Time.time + UpdateInterval;
    }

    public void UpdateModule()
    {
        if (!IsEnabled || hotbarSystem == null) return;

        if (Time.time < nextUpdateTime) return;
        nextUpdateTime = Time.time + UpdateInterval;

        TickExpiringOverrides();
        EvaluateReactiveTransforms();
    }

    private void OnDestroy()
    {
        if (abilitySystem != null)
            abilitySystem.OnAbilityUsed -= HandleAbilityUsed;
    }

    // ── Core API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the override ability if active, else resolves from HotbarSystem.
    /// Always call this — never bypass to HotbarSystem directly from the view.
    /// </summary>
    public AbilityDefinition GetEffectiveAbility(string barId, int index)
    {
        string key = SlotKey(barId, index);
        if (activeOverrides.TryGetValue(key, out var ov) && ov.ability != null)
            return ov.ability;

        return hotbarSystem?.ResolveSlotAbility(hotbarSystem.GetSlot(barId, index));
    }

    public bool HasOverride(string barId, int index)
        => activeOverrides.ContainsKey(SlotKey(barId, index));

    public SlotOverride GetActiveOverride(string barId, int index)
    {
        activeOverrides.TryGetValue(SlotKey(barId, index), out var ov);
        return ov;
    }

    /// <summary>
    /// Applies an override following priority/FIFO rules.
    /// duration = -1 means permanent until explicitly cleared.
    /// </summary>
    public void ApplyOverride(string barId, int index, AbilityDefinition ability,
                              TransformationType type, float duration, int priority)
    {
        if (ability == null) return;

        string key = SlotKey(barId, index);

        if (activeOverrides.TryGetValue(key, out var existing))
        {
            // FIFO: equal priority keeps the existing override
            if (existing.priority >= priority) return;
        }

        activeOverrides[key] = new SlotOverride
        {
            ability   = ability,
            type      = type,
            expiresAt = duration >= 0f ? Time.time + duration : -1f,
            appliedAt = Time.time,
            priority  = priority,
        };

        OnOverrideChanged?.Invoke(barId, index);

        if (debugLogging)
            Debug.Log($"[STS] Override applied — {barId}[{index}] → {ability.abilityName} " +
                      $"(type:{type} pri:{priority} dur:{duration})");
    }

    /// <summary>
    /// Drops every active override. Called when HotbarSystem swaps pages — override keys carry
    /// no page, so a proc earned on one page would otherwise apply to whatever slot replaces it.
    /// Reactive transforms re-apply on the next pulse if their condition still holds.
    /// </summary>
    public void ClearAllOverrides()
    {
        if (activeOverrides.Count == 0) return;

        var cleared = new List<string>(activeOverrides.Keys);
        activeOverrides.Clear();

        foreach (var key in cleared)
        {
            ParseKey(key, out var barId, out var index);
            OnOverrideChanged?.Invoke(barId, index);
        }

        if (debugLogging)
            Debug.Log($"[STS] Cleared {cleared.Count} override(s) — page switch");
    }

    public void ClearOverride(string barId, int index)
    {
        string key = SlotKey(barId, index);
        if (!activeOverrides.Remove(key)) return;

        OnOverrideChanged?.Invoke(barId, index);

        if (debugLogging)
            Debug.Log($"[STS] Override cleared — {barId}[{index}]");
    }

    // ── Expiry ────────────────────────────────────────────────────────────

    private void TickExpiringOverrides()
    {
        // Collect expired keys to avoid modifying the dict during iteration
        List<string> expired = null;

        foreach (var kvp in activeOverrides)
        {
            if (kvp.Value.expiresAt >= 0f && Time.time >= kvp.Value.expiresAt)
            {
                expired ??= new List<string>();
                expired.Add(kvp.Key);
            }
        }

        if (expired == null) return;

        foreach (var key in expired)
        {
            activeOverrides.Remove(key);
            ParseKey(key, out var barId, out var index);
            OnOverrideChanged?.Invoke(barId, index);
        }
    }

    // ── Reactive Transforms (10Hz) ────────────────────────────────────────

    private void EvaluateReactiveTransforms()
    {
        if (hotbarSystem == null) return;

        EvaluateBar("centre",      hotbarSystem.GetConfig("centre"));
        EvaluateBar("bottomLeft",  hotbarSystem.GetConfig("bottomLeft"));
        EvaluateBar("bottomRight", hotbarSystem.GetConfig("bottomRight"));
    }

    private void EvaluateBar(string barId, ActionBarConfig config)
    {
        if (config == null) return;

        for (int i = 0; i < config.slots.Count; i++)
        {
            var slot = config.slots[i];
            if (!slot.IsAssigned) continue;

            var ability = hotbarSystem.ResolveSlotAbility(slot);
            if (ability == null) continue;

            EvaluateResourceThresholds(barId, i, ability);
            EvaluateBuffTransforms(barId, i, ability);
            EvaluateTargetConditions(barId, i, ability);
        }
    }

    private void EvaluateResourceThresholds(string barId, int index, AbilityDefinition ability)
    {
        if (ability.resourceThresholds == null || resourceProvider == null) return;

        foreach (var entry in ability.resourceThresholds)
        {
            if (entry?.transformAbility == null) continue;
            if (!resourceDefCache.TryGetValue(entry.resourceId, out var def)) continue;

            float max = resourceProvider.GetMaxResource(def);
            if (max <= 0f) continue;

            float pct = resourceProvider.GetResource(def) / max;
            bool met = entry.op == ComparisonOp.LessThanOrEqual ? pct <= entry.threshold
                                                                 : pct >= entry.threshold;

            string key = SlotKey(barId, index);

            if (met)
            {
                // Only apply if no higher-priority override is already active
                if (!activeOverrides.TryGetValue(key, out var ex) || ex.priority < 15)
                    ApplyOverride(barId, index, entry.transformAbility,
                                  TransformationType.ResourceThreshold, -1f, 15);
            }
            else
            {
                // Clear only if WE own this override (same type + same ability)
                if (activeOverrides.TryGetValue(key, out var ex) &&
                    ex.type == TransformationType.ResourceThreshold &&
                    ex.ability == entry.transformAbility)
                {
                    ClearOverride(barId, index);
                }
            }
        }
    }

    private void EvaluateBuffTransforms(string barId, int index, AbilityDefinition ability)
    {
        if (ability.buffTransforms == null) return;

        var blackboard = blackboardSystem?.Blackboard;
        if (blackboard == null) return;

        foreach (var entry in ability.buffTransforms)
        {
            if (entry?.transformAbility == null || string.IsNullOrEmpty(entry.blackboardKey)) continue;

            bool active = blackboard.GetBool(entry.blackboardKey.GetHashCode());
            string key  = SlotKey(barId, index);

            if (active)
            {
                if (!activeOverrides.TryGetValue(key, out var ex) || ex.priority < 20)
                    ApplyOverride(barId, index, entry.transformAbility,
                                  TransformationType.BuffStatus, -1f, 20);
            }
            else
            {
                if (activeOverrides.TryGetValue(key, out var ex) &&
                    ex.type == TransformationType.BuffStatus &&
                    ex.ability == entry.transformAbility)
                {
                    ClearOverride(barId, index);
                }
            }
        }
    }

    private void EvaluateTargetConditions(string barId, int index, AbilityDefinition ability)
    {
        if (ability.targetConditions == null || targetLock == null) return;

        var target = targetLock.LockedTarget;
        if (target == null) return;

        var targetBrain = target.GetComponentInParent<ControllerBrain>();
        var targetBlackboard = targetBrain?.GetModule<BlackboardSystem>()?.Blackboard;
        if (targetBlackboard == null) return;

        foreach (var entry in ability.targetConditions)
        {
            if (entry?.transformAbility == null || string.IsNullOrEmpty(entry.blackboardKey)) continue;

            bool active = targetBlackboard.GetBool(entry.blackboardKey.GetHashCode());
            string key  = SlotKey(barId, index);

            if (active)
            {
                if (!activeOverrides.TryGetValue(key, out var ex) || ex.priority < 10)
                    ApplyOverride(barId, index, entry.transformAbility,
                                  TransformationType.TargetCondition, -1f, 10);
            }
            else
            {
                if (activeOverrides.TryGetValue(key, out var ex) &&
                    ex.type == TransformationType.TargetCondition &&
                    ex.ability == entry.transformAbility)
                {
                    ClearOverride(barId, index);
                }
            }
        }
    }

    // ── Sequence Prerequisites (event-driven) ─────────────────────────────

    private void HandleAbilityUsed(string usedAbilityId)
    {
        if (hotbarSystem == null) return;

        foreach (var barId in new[] { "centre", "bottomLeft", "bottomRight" })
        {
            var config = hotbarSystem.GetConfig(barId);
            if (config == null) continue;

            for (int i = 0; i < config.slots.Count; i++)
            {
                var slot = config.slots[i];
                if (!slot.IsAssigned) continue;

                var ability = hotbarSystem.ResolveSlotAbility(slot);
                if (ability?.sequencePrerequisites == null) continue;

                foreach (var prereq in ability.sequencePrerequisites)
                {
                    if (prereq?.transformAbility == null) continue;
                    if (prereq.triggerAbilityId != usedAbilityId) continue;

                    ApplyOverride(barId, i, prereq.transformAbility,
                                  TransformationType.SequencePrereq,
                                  prereq.windowSeconds, 25);
                }
            }
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────

    private static void ParseKey(string key, out string barId, out int index)
    {
        int colon = key.LastIndexOf(':');
        if (colon < 0) { barId = key; index = 0; return; }
        barId = key.Substring(0, colon);
        int.TryParse(key.Substring(colon + 1), out index);
    }
}
