using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The bearer's named, expiring state. One instance per status id per entity.
///
/// This module owns a clock and a list. Its one mechanic is the tick (damage or healing over
/// time); everything else a status does, it does by pushing stat contributions and blackboard
/// facts that other systems were already reading. Mitigation, bonus damage, movement gating — none of those systems learn that
/// statuses exist, and gear and talents reach the same stats through the same door.
///
/// Removal is by contribution KEY, never by subtracting a remembered number, so a status
/// cannot drift a stat by expiring twice, by being re-applied mid-flight, or by having its
/// authored contribution list edited while it was live.
///
/// Lives on the BEARER, so a status outlives whoever applied it and anything can ask an
/// entity what is on it.
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

    [Header("Crowd control (Combat_Framework §11)")]
    [Tooltip("Diminishing returns: seconds from the last application of a category before it lands full again.")]
    [SerializeField] private float drWindow = 15f;
    [Tooltip("Duration share of the 1st, 2nd, 3rd application of one category in the window; the next is refused.")]
    [SerializeField] private float[] drSteps = { 1f, 0.5f, 0.25f };
    [Tooltip("k in clamp(1 + (source CC power − tenacity) × k, 0.5, 2).")]
    [SerializeField] private float ccPowerScale = 0.05f;

    #endregion

    #region State

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    private ControllerBrain brain;
    private IStatProvider stats;
    private Blackboard blackboard;
    private DamageSystem damage;

    private readonly List<StatusInstance> active = new();
    private readonly Dictionary<string, StatusInstance> byId = new();
    private readonly Dictionary<ControlCategory, (int count, float last)> diminishing = new();
    private readonly List<StatusDefinition> heldAtDeath = new();

    // Blackboard facts are shared ground. Two statuses asserting "rooted" must not clear it
    // out from under each other when the first one expires, so each claims the fact under its
    // own key on the blackboard's ledger (Blackboard.Claim), shared with talent grants.

    /// <summary>What is currently on this entity. The buff bar reads this.</summary>
    public IReadOnlyList<StatusInstance> Active => active;

    public event Action<StatusInstance> OnStatusApplied;
    public event Action<StatusInstance> OnStatusRemoved;
    public event Action<StatusInstance> OnStacksChanged;

    // A status already present was applied again (its timer restarted). A second flinch during a
    // flinch is one, so the hit reaction replays.
    public event Action<StatusInstance> OnStatusReapplied;

    // A status was refused: immune, diminished away, or resisted down to no time at all.
    public event Action<StatusDefinition> OnStatusResisted;

    // The bearer made its saving throw: the status lands halved, or as its downgrade.
    public event Action<StatusDefinition> OnStatusSaved;

    // Resist, Immune and Intensify from talents and gear (StatusModifierReward).
    public StatusModifiers Modifiers { get; } = new();

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
            Debug.LogError($"[StatusSystem] No IStatProvider on {brain.EntityName} — every status will be inert.");

        if (damage != null) damage.OnDeath += HandleDeath;
        if (damage != null) damage.OnDamageApplied += HandleDamageApplied;
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;
        if (active.Count == 0) return;

        float deltaTime = Time.deltaTime;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            StatusInstance instance = active[i];
            AdvanceTicks(instance, deltaTime);
            // A tick's damage can kill the bearer or break statuses, changing the list under this loop.
            if (i >= active.Count || active[i] != instance) continue;
            if (instance.IsPermanent) continue;

            instance.Remaining -= deltaTime;
            if (instance.Remaining > 0f) continue;

            RemoveAt(i);
        }
    }

    // Death clears every status, so the instance is checked before each further tick.
    private void AdvanceTicks(StatusInstance instance, float deltaTime)
    {
        if (!instance.Definition.Ticks) return;

        instance.NextTick -= deltaTime;
        while (instance.NextTick <= 0f && byId.ContainsKey(instance.Id))
        {
            instance.NextTick += instance.Definition.tickSeconds;
            Tick(instance);
        }
    }

    private void Tick(StatusInstance instance)
    {
        StatusDefinition definition = instance.Definition;
        if (definition.tickHeals && brain.Health != null)
            brain.Health.ApplyHealing(RollPerStack(definition.tickHeal, instance.Stacks));

        if (definition.tickDealsDamage && damage != null)
            damage.TakeDamage(TickPacket(instance));
    }

    private static int RollPerStack(DiceRoll dice, int stacks)
    {
        int total = 0;
        for (int i = 0; i < stacks; i++) total += dice.Roll();
        return total;
    }

    // Dealt by whoever applied it, so the kill is theirs; by the bearer once that source is gone.
    private CombatDamagePacket TickPacket(StatusInstance instance)
    {
        DamageSystem dealer = instance.Source != null && instance.Source.Damage != null ? instance.Source.Damage : damage;
        var attack = new CombatAttackData
        {
            baseDamage = RollPerStack(instance.Definition.tickDamage, instance.Stacks),
            damageType = instance.Definition.tickDamageType,
            attackerTransform = dealer.transform,
            hitPoint = transform.position,
            hitNormal = Vector3.up,
            source = DamageSource.Tick
        };
        return dealer.CalculateDamage(attack);
    }

    #endregion

    #region Apply

    /// <summary>
    /// Apply a status, or re-apply one already present. Source is who did it — informational,
    /// and safe to leave null. Seconds above 0 replaces the authored lifetime for this
    /// application — a hit state scaled by the damage that caused it.
    /// </summary>
    public void Apply(StatusDefinition definition, ControllerBrain source = null, float seconds = 0f)
    {
        if (definition == null) return;

        // Every application passes here: Immune, diminishing returns, the save, the bearer's Resist and the
        // source's Intensify, CC power vs tenacity.
        if (Modifiers.Immune(definition) || DiminishStep(definition) <= 0f)
        {
            OnStatusResisted?.Invoke(definition);
            return;
        }

        bool saved = SavedAgainst(definition, source);
        if (saved) OnStatusSaved?.Invoke(definition);
        if (saved && definition.saveDowngrade != null)
        {
            Apply(definition.saveDowngrade, source);
            return;
        }

        StatusSystem dealer = source != null ? source.GetModule<StatusSystem>() : null;
        float lifetime = seconds > 0f ? seconds : definition.Seconds;
        if (lifetime > 0f)
        {
            lifetime += Modifiers.Seconds(definition, StatusSide.Taken);
            if (dealer != null) lifetime += dealer.Modifiers.Seconds(definition, StatusSide.Dealt);
            lifetime *= ControlScale(definition, source) * DiminishStep(definition) * (saved ? 0.5f : 1f);
            if (lifetime <= 0f)
            {
                OnStatusResisted?.Invoke(definition);
                return;
            }
        }

        RecordControl(definition);

        StatusInstance instance = Place(definition, source, lifetime);
        if (instance == null || definition.stacking != StatusStacking.Stack) return;

        int extra = dealer != null ? dealer.Modifiers.Stacks(definition, StatusSide.Dealt) : 0;
        for (int i = 0; i < extra; i++) Reapply(instance, source, lifetime);
    }

    private StatusInstance Place(StatusDefinition definition, ControllerBrain source, float seconds)
    {
        if (string.IsNullOrEmpty(definition.id))
        {
            Debug.LogWarning($"[StatusSystem] '{definition.name}' has no id and cannot be applied.");
            return null;
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
                Reapply(existing, source, seconds);
                return existing;
            }
        }

        StatusInstance instance = new StatusInstance(definition, source);
        if (seconds > 0f) instance.Remaining = seconds;

        active.Add(instance);
        byId[definition.id] = instance;

        WriteContributions(instance);
        UpdateFlags(instance, 0, instance.Stacks);
        RefreshDebugList();

        if (debugStatuses)
            Debug.Log($"[StatusSystem] +{definition.id} on {transform.root.name} ({instance.Remaining:F2}s)");

        OnStatusApplied?.Invoke(instance);
        return instance;
    }

    private void Reapply(StatusInstance instance, ControllerBrain source, float seconds)
    {
        instance.Source = source;
        instance.Remaining = seconds > 0f ? seconds : instance.Definition.Seconds;
        OnStatusReapplied?.Invoke(instance);

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
            Debug.Log($"[StatusSystem] {instance.Id} x{instance.Stacks} on {transform.root.name}");

        OnStacksChanged?.Invoke(instance);
    }

    // ---- Crowd control and saves ----

    // 1 for anything that isn't crowd control or is outside the window; then 1 → ½ → ¼ → 0 (refused).
    private float DiminishStep(StatusDefinition definition)
    {
        if (definition.control == ControlCategory.None) return 1f;
        if (!diminishing.TryGetValue(definition.control, out var dr) || Time.time - dr.last > drWindow) return 1f;
        return dr.count < drSteps.Length ? drSteps[dr.count] : 0f;
    }

    private void RecordControl(StatusDefinition definition)
    {
        if (definition.control == ControlCategory.None) return;

        bool inWindow = diminishing.TryGetValue(definition.control, out var dr) && Time.time - dr.last <= drWindow;
        diminishing[definition.control] = (inWindow ? dr.count + 1 : 1, Time.time);
    }

    private float ControlScale(StatusDefinition definition, ControllerBrain source)
    {
        if (definition.control == ControlCategory.None) return 1f;

        float power = source != null ? DialIds.Read(source.Stats, DialIds.CcPower) : 0f;
        float tenacity = DialIds.Read(stats, DialIds.Tenacity);
        return Mathf.Clamp(1f + (power - tenacity) * ccPowerScale, 0.5f, 2f);
    }

    // d20 + the bearer's save stat vs 10 + the source's matching stat. Nothing saves against its own statuses.
    private bool SavedAgainst(StatusDefinition definition, ControllerBrain source)
    {
        if (definition.save == SaveType.None || source == brain) return false;

        (string saveStat, string dcStat) = SaveStats(definition.save);
        float dc = 10f + (source != null ? DialIds.Read(source.Stats, dcStat) : 0f);
        return DiceRoll.Die(20) + DialIds.Read(stats, saveStat) >= dc;
    }

    private static (string save, string dc) SaveStats(SaveType save) => save switch
    {
        SaveType.Toughness => ("def.toughness", "cmb.efficacy"),
        SaveType.Alteration => ("def.alteration", "cmb.arcana"),
        SaveType.Dexterity => ("def.dexterity", "cmb.initiative"),
        _ => ("def.preparation", "cmb.cunning"),
    };

    // Sleep, charm, pacify: any damage that reaches the bearer ends them.
    private void HandleDamageApplied(CombatDamagePacket packet, float applied)
    {
        if (applied <= 0f) return;

        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].Definition.breaksOnDamage) RemoveAt(i);
        }
    }

    #endregion

    #region Remove

    public void Remove(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!byId.TryGetValue(id, out StatusInstance instance)) return;

        RemoveAt(active.IndexOf(instance));
    }

    private void HandleDeath()
    {
        heldAtDeath.Clear();
        foreach (StatusInstance instance in active) heldAtDeath.Add(instance.Definition);
        RemoveAll();
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
            Debug.Log($"[StatusSystem] -{instance.Id} on {transform.root.name}");

        OnStatusRemoved?.Invoke(instance);
    }

    #endregion

    #region Queries

    public bool Has(string id) => !string.IsNullOrEmpty(id) && byId.ContainsKey(id);

    // On now, or on at the moment of death — a killer's trigger runs after death has cleared everything (Spread).
    public bool HadAtDeathOrHas(string id) => Has(id) || heldAtDeath.Exists(d => d.id == id);

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

            if (!held && holds) blackboard?.Claim(new BlackboardKey(flags[i].fact).hash, instance.Definition.SourceKey);
            if (held && !holds) blackboard?.Release(new BlackboardKey(flags[i].fact).hash, instance.Definition.SourceKey);
        }
    }

    #endregion

    #region Teardown

    private void OnDestroy()
    {
        if (damage != null) damage.OnDeath -= HandleDeath;
        if (damage != null) damage.OnDamageApplied -= HandleDamageApplied;

        // Deliberately NOT RemoveAll(). On teardown the StatSystem and BlackboardSystem may
        // already be destroyed, and `stats` is held as an interface — Unity's null-check
        // operator overload does not apply through one, so `stats?.ClearContributions` would
        // sail past the guard and throw MissingReferenceException. There is nothing to clean
        // up anyway: contributions live in a store that is being destroyed alongside this.
        active.Clear();
        byId.Clear();

        OnStatusApplied = null;
        OnStatusRemoved = null;
        OnStacksChanged = null;
        OnStatusReapplied = null;
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
