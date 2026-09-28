using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The bearer's named, expiring state. One instance per status id per entity.
///
/// This module owns a clock and a list. It owns no mechanics: everything a status does, it
/// does by pushing stat contributions and blackboard facts that other systems were already
/// reading. Mitigation, bonus damage, movement gating — none of those systems learn that
/// statuses exist, and gear and talents reach the same stats through the same door.
///
/// Removal is by contribution KEY, never by subtracting a remembered number, so a status
/// cannot drift a stat by expiring twice, by being re-applied mid-flight, or by having its
/// authored contribution list edited while it was live.
///
/// Lives on the BEARER. That is the difference from EffectManagerModule, which is held by
/// whoever applied the effect — so its effects die with their applier, and nothing can ask
/// an entity what is on it.
/// </summary>
public class StatusSystem : MonoBehaviour, IBrainModule
{
    #region Inspector

    [Header("Module Config")]
    [SerializeField] private bool isEnabled = true;

    [Header("Debug")]
    [SerializeField] private bool debugStatuses = false;

    [Tooltip("Live view of what is currently on this entity. Read-only — editing does nothing.")]
    [SerializeField] private List<string> activeDebug = new();

    #endregion

    #region State

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    private ControllerBrain brain;
    private IStatProvider stats;
    private Blackboard blackboard;
    private DamageSystem damage;

    private readonly List<StatusInstance> active = new();
    private readonly Dictionary<string, StatusInstance> byId = new();

    // Blackboard facts are shared ground. Two statuses asserting "rooted" must not clear it
    // out from under each other when the first one expires, so hold a count per fact and
    // only write false when the last claimant lets go.
    private readonly Dictionary<int, int> flagClaims = new();

    /// <summary>What is currently on this entity. The buff bar reads this.</summary>
    public IReadOnlyList<StatusInstance> Active => active;

    public event Action<StatusInstance> OnStatusApplied;
    public event Action<StatusInstance> OnStatusRemoved;
    public event Action<StatusInstance> OnStacksChanged;

    #endregion

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    /// <summary>
    /// Cross-module wiring belongs here, not in Initialize — InitializeModules() runs before
    /// every module exists, so brain.Stats is not reliably resolvable yet.
    /// </summary>
    public void LateInitialize()
    {
        stats = brain.Stats;
        blackboard = brain.Blackboard;
        damage = brain.Damage;

        if (stats == null)
            Debug.LogError($"[StatusSystem] No IStatProvider on {brain.name} — every status will be inert.");

        if (damage != null) damage.OnDeath += RemoveAll;
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;
        if (active.Count == 0) return;

        float deltaTime = Time.deltaTime;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            StatusInstance instance = active[i];
            if (instance.IsPermanent) continue;

            instance.Remaining -= deltaTime;
            if (instance.Remaining > 0f) continue;

            RemoveAt(i);
        }
    }

    #endregion

    #region Apply

    /// <summary>
    /// Apply a status, or re-apply one already present. Source is who did it — informational,
    /// and safe to leave null.
    /// </summary>
    public void Apply(StatusDefinition definition, ControllerBrain source = null)
    {
        if (definition == null) return;

        if (string.IsNullOrEmpty(definition.id))
        {
            Debug.LogWarning($"[StatusSystem] '{definition.name}' has no id and cannot be applied.");
            return;
        }

        if (byId.TryGetValue(definition.id, out StatusInstance existing))
        {
            // Two different assets claiming one id is an authoring mistake, and silently
            // keeping the first would apply the wrong numbers for as long as it lasted.
            if (existing.Definition != definition)
            {
                Debug.LogWarning($"[StatusSystem] Two assets share the id '{definition.id}' — " +
                                 $"'{existing.Definition.name}' and '{definition.name}'. Replacing.");
                Remove(definition.id);
            }
            else
            {
                Reapply(existing, source);
                return;
            }
        }

        StatusInstance instance = new StatusInstance(definition, source);

        active.Add(instance);
        byId[definition.id] = instance;

        WriteContributions(instance);
        UpdateFlags(instance, 0, instance.Stacks);
        RefreshDebugList();

        if (debugStatuses)
            Debug.Log($"[StatusSystem] +{definition.id} on {name} ({definition.duration:F1}s)");

        OnStatusApplied?.Invoke(instance);
    }

    private void Reapply(StatusInstance instance, ControllerBrain source)
    {
        instance.Source = source;
        instance.Remaining = instance.Definition.duration;

        if (instance.Definition.stacking != StatusStacking.Stack) return;

        int previous = instance.Stacks;
        instance.Stacks = Mathf.Min(instance.Stacks + 1, Mathf.Max(1, instance.Definition.maxStacks));

        if (instance.Stacks == previous) return;

        // AddContribution replaces under the same key, so re-writing the whole set at the new
        // stack count is exact rather than additive. Nothing accumulates.
        WriteContributions(instance);
        UpdateFlags(instance, previous, instance.Stacks);
        RefreshDebugList();

        if (debugStatuses)
            Debug.Log($"[StatusSystem] {instance.Id} x{instance.Stacks} on {name}");

        OnStacksChanged?.Invoke(instance);
    }

    #endregion

    #region Remove

    public void Remove(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!byId.TryGetValue(id, out StatusInstance instance)) return;

        RemoveAt(active.IndexOf(instance));
    }

    /// <summary>Death, dispel, a full cleanse.</summary>
    public void RemoveAll()
    {
        for (int i = active.Count - 1; i >= 0; i--) RemoveAt(i);
    }

    /// <summary>Strip by disposition — cleanse takes the harmful, dispel takes the helpful.</summary>
    public void RemoveByDisposition(bool beneficial)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].Definition.beneficial == beneficial) RemoveAt(i);
        }
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= active.Count) return;

        StatusInstance instance = active[index];

        active.RemoveAt(index);
        byId.Remove(instance.Id);

        // Drops this status's key from EVERY stat, so removal stays exact even if the
        // definition's contribution list was edited while the status was live.
        stats?.ClearContributions(instance.Definition.SourceKey);

        UpdateFlags(instance, instance.Stacks, 0);
        RefreshDebugList();

        if (debugStatuses)
            Debug.Log($"[StatusSystem] -{instance.Id} on {name}");

        OnStatusRemoved?.Invoke(instance);
    }

    #endregion

    #region Queries

    public bool Has(string id) => !string.IsNullOrEmpty(id) && byId.ContainsKey(id);

    public int Stacks(string id) => byId.TryGetValue(id, out StatusInstance i) ? i.Stacks : 0;

    public float Remaining(string id) => byId.TryGetValue(id, out StatusInstance i) ? i.Remaining : 0f;

    public StatusInstance Get(string id) => byId.GetValueOrDefault(id);

    #endregion

    #region Stats

    private void WriteContributions(StatusInstance instance)
    {
        if (stats == null) return;

        List<StatContribution> list = instance.Definition.contributions;
        string key = instance.Definition.SourceKey;

        for (int i = 0; i < list.Count; i++)
        {
            if (string.IsNullOrEmpty(list[i].statId)) continue;

            stats.AddContribution(list[i].statId, key, list[i].amountPerStack * instance.Stacks);
        }
    }

    #endregion

    #region Blackboard flags

    // Apply, a stack change and removal all pass through here. A flag is claimed when the
    // stack count rises to its threshold and released when it drops below.
    private void UpdateFlags(StatusInstance instance, int previousStacks, int currentStacks)
    {
        List<StatusFlag> flags = instance.Definition.blackboardFlags;

        for (int i = 0; i < flags.Count; i++)
        {
            if (string.IsNullOrEmpty(flags[i].fact)) continue;

            int threshold = Mathf.Max(1, flags[i].minStacks);
            bool held = previousStacks >= threshold;
            bool holds = currentStacks >= threshold;

            if (!held && holds) ClaimFlag(new BlackboardKey(flags[i].fact).hash);
            if (held && !holds) ReleaseFlag(new BlackboardKey(flags[i].fact).hash);
        }
    }

    private void ClaimFlag(int key)
    {
        flagClaims.TryGetValue(key, out int claims);
        flagClaims[key] = claims + 1;

        if (claims == 0) blackboard?.SetBool(key, true);
    }

    private void ReleaseFlag(int key)
    {
        if (!flagClaims.TryGetValue(key, out int claims)) return;

        if (claims > 1)
        {
            flagClaims[key] = claims - 1;
            return;
        }

        flagClaims.Remove(key);
        blackboard?.SetBool(key, false);
    }

    #endregion

    #region Teardown

    private void OnDestroy()
    {
        if (damage != null) damage.OnDeath -= RemoveAll;

        // Deliberately NOT RemoveAll(). On teardown the StatSystem and BlackboardSystem may
        // already be destroyed, and `stats` is held as an interface — Unity's null-check
        // operator overload does not apply through one, so `stats?.ClearContributions` would
        // sail past the guard and throw MissingReferenceException. There is nothing to clean
        // up anyway: contributions live in a store that is being destroyed alongside this.
        active.Clear();
        byId.Clear();
        flagClaims.Clear();

        OnStatusApplied = null;
        OnStatusRemoved = null;
        OnStacksChanged = null;
    }

    #endregion

    #region Debug

    private void RefreshDebugList()
    {
        activeDebug.Clear();

        for (int i = 0; i < active.Count; i++)
        {
            StatusInstance instance = active[i];
            activeDebug.Add(instance.IsPermanent
                ? $"{instance.Id} x{instance.Stacks}"
                : $"{instance.Id} x{instance.Stacks} ({instance.Remaining:F1}s)");
        }
    }

    #endregion
}
