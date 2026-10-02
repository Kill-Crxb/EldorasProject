using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Holds this entity's live resource pools. Each pool's ceiling comes from a stat, so
/// anything that feeds that stat — core stats, gear, level — moves the ceiling, and the
/// pool follows without knowing what moved it.
///
/// Regeneration is budgeted, not free. A tier-one pool (Health, Stamina, Mana) refills by
/// spending a tier-two pool (Regeneration, Recovery, Recollection), converting one point of
/// the source into amountPerSourcePoint of the target over secondsPerSourcePoint. Run the
/// source dry and the pool stops refilling until the source itself comes back.
/// </summary>
public class ResourceSystem : MonoBehaviour, IResourceProvider, IHealthProvider, IBrainModule, ISaveable
{
    private const float DEFAULT_MAX_VALUE = 100f;
    private const float EPSILON = 0.001f;

    /// <summary>
    /// One pool. Serialized so the live numbers are visible in the inspector during play —
    /// this is the only readout of current values that does not go through the HUD.
    /// </summary>
    [Serializable]
    public class ResourceState
    {
        public ResourceDefinition definition;
        public float current;
        public float max;

        [NonSerialized] public float regenReadyAt;
    }

    [SerializeField] private bool isEnabled = true;

    [Header("Live State")]
    [Tooltip("Populated at runtime. Read-only in practice — editing these in play mode " +
             "writes straight to the pool without firing the change events the HUD listens to.")]
    [SerializeField] private List<ResourceState> states = new();

    private ControllerBrain brain;
    private IStatProvider stats;
    private readonly Dictionary<string, ResourceState> resources = new();
    private ResourceState healthResource;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public event Action<ResourceDefinition, float> OnResourceChanged;
    public event Action<float> OnHealthChanged;
    public event Action OnDeath;

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = brain.Stats;

        if (stats == null)
        {
            Debug.LogError($"[ResourceSystem] No stat provider found on {brain.EntityName}");
            return;
        }

        InitializeResources();

        if (healthResource == null)
            Debug.LogError("[ResourceSystem] No health resource defined (triggerDeathOnDepletion = true)");
    }

    /// <summary>
    /// Ceilings are resolved a second time here, after StatDerivationSystem has applied what
    /// the core stats are worth. Reading them in Initialize alone would snapshot the value
    /// before Body had contributed anything, and every pool would sit at its authored base.
    /// </summary>
    public void LateInitialize()
    {
        if (stats == null) return;

        RefreshAllMaxValues();
        stats.OnStatChanged += HandleStatChanged;
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        float delta = Time.deltaTime;

        for (int i = 0; i < states.Count; i++) TickRegen(states[i], delta);
    }

    #endregion

    #region Setup

    private void InitializeResources()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError($"[ResourceSystem] ResourceManager.Instance is null on {brain.EntityName}");
            return;
        }

        states.Clear();
        resources.Clear();

        foreach (var def in ResourceManager.Instance.GetAll())
        {
            if (def == null) continue;
            if (string.IsNullOrEmpty(def.resourceId)) continue;

            float maxValue = CalculateMaxValue(def);

            var state = new ResourceState
            {
                definition = def,
                current = maxValue,
                max = maxValue
            };

            resources[def.resourceId] = state;
            states.Add(state);

            if (def.triggerDeathOnDepletion) healthResource = state;
        }
    }

    private float CalculateMaxValue(ResourceDefinition def)
    {
        if (string.IsNullOrEmpty(def.maxStatId)) return DEFAULT_MAX_VALUE;

        float statValue = stats.GetValue(def.maxStatId);
        return statValue > 0f ? statValue : DEFAULT_MAX_VALUE;
    }

    #endregion

    #region Max Values

    private void HandleStatChanged(string statId, float oldValue, float newValue)
    {
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.definition == null) continue;
            if (state.definition.maxStatId != statId) continue;

            ApplyMaxValue(state, newValue);
        }
    }

    private void RefreshAllMaxValues()
    {
        for (int i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.definition == null) continue;

            ApplyMaxValue(state, CalculateMaxValue(state.definition));
        }
    }

    /// <summary>
    /// Moves the ceiling and decides what happens to what is already in the pool. A pool
    /// that was full stays full, so a fresh character spawns topped up and a level-up is a
    /// clean gain. A partly spent pool keeps its absolute value, so swapping a +health item
    /// in and out is never a heal and taking it off can never kill you.
    /// </summary>
    private void ApplyMaxValue(ResourceState state, float newMax)
    {
        if (newMax <= 0f) newMax = DEFAULT_MAX_VALUE;
        if (Mathf.Approximately(newMax, state.max)) return;

        bool wasFull = state.current >= state.max - EPSILON;

        state.max = newMax;
        state.current = Mathf.Clamp(wasFull ? newMax : state.current, 0f, newMax);

        // Fired even when current did not move: a ceiling that rose under a partly spent
        // pool leaves the value alone, and the HUD still needs to redraw the ratio.
        OnResourceChanged?.Invoke(state.definition, state.current);

        if (state.definition.triggerDeathOnDepletion) OnHealthChanged?.Invoke(state.current);
    }

    #endregion

    #region Regeneration

    private void TickRegen(ResourceState state, float delta)
    {
        var def = state.definition;
        if (def == null) return;
        if (state.current >= state.max - EPSILON) return;
        if (Time.time < state.regenReadyAt) return;

        if (def.regenSource == null) TickFlatRegen(state, def, delta);
        else TickBudgetedRegen(state, def, delta);
    }

    /// <summary>How a source pool comes back: a flat trickle, or nothing at all.</summary>
    private void TickFlatRegen(ResourceState state, ResourceDefinition def, float delta)
    {
        if (def.regenPerSecond <= 0f) return;

        float headroom = state.max - state.current;
        float granted = Mathf.Min(def.regenPerSecond * delta, headroom);

        SetResourceValue(state, state.current + granted);
    }

    /// <summary>
    /// Spends the source pool to refill this one. The cost is charged against what was
    /// actually granted, so topping off mid-payout wastes no part of a source point.
    /// </summary>
    private void TickBudgetedRegen(ResourceState state, ResourceDefinition def, float delta)
    {
        if (def.amountPerSourcePoint <= 0f) return;
        if (def.secondsPerSourcePoint <= 0f) return;

        if (!resources.TryGetValue(def.regenSource.resourceId, out var source)) return;
        if (source.current <= 0f) return;

        float headroom = state.max - state.current;
        float perSecond = def.amountPerSourcePoint / def.secondsPerSourcePoint;
        float granted = Mathf.Min(perSecond * delta, headroom);
        float cost = granted / def.amountPerSourcePoint;

        if (cost > source.current)
        {
            cost = source.current;
            granted = cost * def.amountPerSourcePoint;
        }

        SetResourceValue(source, source.current - cost);
        SetResourceValue(state, state.current + granted);
    }

    /// <summary>
    /// Holds regen off after a spend or a hit. Paying for regen out of a source pool does
    /// not call this — a pool being drained to heal you should not also stall its own refill.
    /// </summary>
    private void BlockRegen(ResourceState state)
    {
        if (state?.definition == null) return;
        if (state.definition.regenDelay <= 0f) return;

        state.regenReadyAt = Time.time + state.definition.regenDelay;
    }

    #endregion

    #region ISaveable

    public string GetSaveId() => "resources";
    public int GetSaveVersion() => 1;

    public string GetSaveData()
    {
        var saveData = new ResourceSaveData
        {
            version = GetSaveVersion(),
            resources = new List<ResourceEntry>()
        };

        foreach (var kvp in resources)
        {
            if (kvp.Value == null) continue;

            float pct = kvp.Value.max > EPSILON
                ? kvp.Value.current / kvp.Value.max
                : 1f;

            saveData.resources.Add(new ResourceEntry
            {
                resourceId = kvp.Key,
                percentage = pct
            });
        }

        return JsonUtility.ToJson(saveData);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var saveData = JsonUtility.FromJson<ResourceSaveData>(json);
        if (saveData?.resources == null) return;

        foreach (var entry in saveData.resources)
        {
            if (!resources.TryGetValue(entry.resourceId, out var state)) continue;

            float restored = Mathf.Clamp01(entry.percentage) * state.max;
            SetResourceValue(state, restored);
        }
    }

    [Serializable]
    private class ResourceSaveData
    {
        public int version;
        public List<ResourceEntry> resources;
    }

    [Serializable]
    private class ResourceEntry
    {
        public string resourceId;
        public float percentage;
    }

    #endregion

    #region IResourceProvider

    public float GetResource(ResourceDefinition def)
    {
        if (!resources.TryGetValue(def.resourceId, out var state)) return 0f;
        return state.current;
    }

    public float GetMaxResource(ResourceDefinition def)
    {
        if (!resources.TryGetValue(def.resourceId, out var state)) return 0f;
        return state.max;
    }

    public float GetResourcePercentage(ResourceDefinition def)
    {
        if (!resources.TryGetValue(def.resourceId, out var state)) return 0f;
        if (state.max <= EPSILON) return 0f;
        return state.current / state.max;
    }

    public bool HasResource(ResourceDefinition def, float amount)
        => GetResource(def) >= amount;

    public bool ConsumeResource(ResourceDefinition def, float amount)
    {
        if (!HasResource(def, amount)) return false;

        if (!resources.TryGetValue(def.resourceId, out var state)) return false;

        ModifyResourceDirect(state, -amount);
        BlockRegen(state);
        return true;
    }

    public void RestoreResource(ResourceDefinition def, float amount)
        => ModifyResource(def, amount);

    public void SetResourceToMax(ResourceDefinition def)
    {
        if (!resources.TryGetValue(def.resourceId, out var state)) return;
        SetResourceValue(state, state.max);
    }

    public IReadOnlyDictionary<ResourceDefinition, float> GetAllResources()
    {
        var result = new Dictionary<ResourceDefinition, float>(resources.Count);
        foreach (var kvp in resources)
            result[kvp.Value.definition] = kvp.Value.current;
        return result;
    }

    #endregion

    #region IHealthProvider

    public float GetCurrentHealth() => healthResource?.current ?? 0f;
    public float GetMaxHealth() => healthResource?.max ?? 0f;

    public float GetHealthPercentage()
    {
        if (healthResource == null) return 0f;
        if (healthResource.max <= EPSILON) return 0f;
        return healthResource.current / healthResource.max;
    }

    public bool IsAlive() => GetCurrentHealth() > 0f;

    public void ApplyDamage(float amount)
    {
        if (!isEnabled) return;
        if (healthResource == null) return;

        ModifyResourceDirect(healthResource, -Mathf.Abs(amount));
        BlockRegen(healthResource);
    }

    public void ApplyHealing(float amount)
    {
        if (!isEnabled) return;
        if (healthResource == null) return;

        ModifyResourceDirect(healthResource, Mathf.Abs(amount));
    }

    #endregion

    #region Mutation

    private void ModifyResource(ResourceDefinition def, float delta)
    {
        if (!resources.TryGetValue(def.resourceId, out var state)) return;
        ModifyResourceDirect(state, delta);
    }

    private void ModifyResourceDirect(ResourceState state, float delta)
        => SetResourceValue(state, state.current + delta);

    private void SetResourceValue(ResourceState state, float value)
    {
        float clamped = Mathf.Clamp(value, 0f, state.max);
        if (Mathf.Approximately(clamped, state.current)) return;

        state.current = clamped;

        OnResourceChanged?.Invoke(state.definition, clamped);

        if (!state.definition.triggerDeathOnDepletion) return;

        OnHealthChanged?.Invoke(clamped);

        if (clamped <= 0f) OnDeath?.Invoke();
    }

    #endregion

    private void OnDestroy()
    {
        if (stats != null) stats.OnStatChanged -= HandleStatChanged;

        OnResourceChanged = null;
        OnHealthChanged = null;
        OnDeath = null;
    }
}
