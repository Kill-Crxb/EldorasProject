using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-entity resource system (health, mana, stamina).
/// Loads definitions from ResourceManager and maintains current/max values.
/// </summary>
public class ResourceSystem : MonoBehaviour, IResourceProvider, IHealthProvider, IBrainModule, ISaveable
{
    #region Constants

    private const float DEFAULT_MAX_VALUE = 100f;
    private const float EPSILON = 0.001f;

    #endregion

    #region Resource State

    private class ResourceState
    {
        public ResourceDefinition definition;
        public float current;
        public float max;
    }

    #endregion

    #region Inspector

    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool debugResources = false;

    #endregion

    #region Properties

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    #endregion

    #region State

    private ControllerBrain brain;
    private StatSystem stats;

    private readonly Dictionary<string, ResourceState> resources = new();
    private ResourceState healthResource;

    #endregion

    #region Events

    public event Action<ResourceDefinition, float> OnResourceChanged;
    public event Action<float> OnHealthChanged;
    public event Action OnDeath;

    #endregion

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = brain.Stats;

        if (stats == null)
        {
            Debug.LogError($"[ResourceSystem] No StatSystem found on {brain.name}");
            return;
        }

        InitializeResources();

        if (healthResource == null)
            Debug.LogError("[ResourceSystem] No health resource defined (triggerDeathOnDepletion = true)");
    }

    public void UpdateModule()
    {
        // Future: Regeneration logic
    }

    #endregion

    #region Initialization

    private void InitializeResources()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError($"[ResourceSystem] ResourceManager.Instance is null on {brain.name}");
            return;
        }

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

            if (def.triggerDeathOnDepletion)
                healthResource = state;
        }
    }

    private float CalculateMaxValue(ResourceDefinition def)
    {
        if (string.IsNullOrEmpty(def.maxStatId))
            return DEFAULT_MAX_VALUE;

        float statValue = stats.GetValue(def.maxStatId);
        return statValue > 0f ? statValue : DEFAULT_MAX_VALUE;
    }

    #endregion

    #region ISaveable

    public string GetSaveId() => "resources";
    public int GetSaveVersion() => 1;

    /// <summary>
    /// Saves the current value of every resource as a percentage of its max.
    /// Storing percentages instead of raw values means the save survives stat changes
    /// (e.g. the player equips gear that raises max health between sessions).
    /// </summary>
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

        if (debugResources)
            Debug.Log($"[ResourceSystem] GetSaveData — {saveData.resources.Count} resources serialised");

        return JsonUtility.ToJson(saveData);
    }

    /// <summary>
    /// Restores resource values from saved percentages.
    /// Must be called AFTER InitializeResources() so max values are set first.
    /// SaveManager enforces this via load order: stats → inventory → equipment → resources.
    /// </summary>
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

        if (debugResources)
            Debug.Log($"[ResourceSystem] LoadSaveData — {saveData.resources.Count} resources restored for {brain.name}");
    }

    // ── Save Data Structures ──────────────────────────────────────────────

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

    #region IResourceProvider — Core Queries

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

    #endregion

    #region IResourceProvider — Modification

    public bool ConsumeResource(ResourceDefinition def, float amount)
    {
        if (!HasResource(def, amount)) return false;
        ModifyResource(def, -amount);
        return true;
    }

    public void RestoreResource(ResourceDefinition def, float amount)
        => ModifyResource(def, amount);

    public void SetResourceToMax(ResourceDefinition def)
    {
        if (!resources.TryGetValue(def.resourceId, out var state)) return;
        SetResourceValue(state, state.max);
    }

    #endregion

    #region IResourceProvider — Bulk Queries

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (amount < 0f)
            Debug.LogWarning($"[ResourceSystem] ApplyDamage received negative value {amount}. Use positive values.");
#endif

        ModifyResourceDirect(healthResource, -Mathf.Abs(amount));
    }

    public void ApplyHealing(float amount)
    {
        if (!isEnabled) return;
        if (healthResource == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (amount < 0f)
            Debug.LogWarning($"[ResourceSystem] ApplyHealing received negative value {amount}. Use positive values.");
#endif

        ModifyResourceDirect(healthResource, Mathf.Abs(amount));
    }

    #endregion

    #region Internal Modification

    private void ModifyResource(ResourceDefinition def, float delta)
    {
        if (!resources.TryGetValue(def.resourceId, out var state))
        {
            if (debugResources)
                Debug.LogWarning($"[ResourceSystem] Unknown resource '{def.resourceId}' on {brain.name}");
            return;
        }

        ModifyResourceDirect(state, delta);
    }

    private void ModifyResourceDirect(ResourceState state, float delta)
        => SetResourceValue(state, state.current + delta);

    private void SetResourceValue(ResourceState state, float value)
    {
        float clamped = Mathf.Clamp(value, 0f, state.max);
        state.current = clamped;

        OnResourceChanged?.Invoke(state.definition, clamped);

        if (!state.definition.triggerDeathOnDepletion) return;

        OnHealthChanged?.Invoke(clamped);

        if (clamped <= 0f)
            OnDeath?.Invoke();
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        OnResourceChanged = null;
        OnHealthChanged = null;
        OnDeath = null;
    }

    #endregion
}