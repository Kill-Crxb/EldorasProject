using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceSystem : MonoBehaviour, IResourceProvider, IHealthProvider, IBrainModule
{
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool debugResources = false;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    private ControllerBrain brain;
    private StatSystem stats;

    private readonly Dictionary<string, float> current = new();
    private readonly Dictionary<string, float> max = new();
    private readonly Dictionary<string, ResourceDefinition> definitions = new();

    public event Action<ResourceDefinition, float> OnResourceChanged;
    public event Action<float> OnHealthChanged;
    public event Action OnDeath;

    private string healthResourceId;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = brain.Stats;

        if (stats == null)
        {
            Debug.LogError($"[ResourceSystem] No StatSystem found on {brain.name}");
            return;
        }

        foreach (var def in ResourceManager.Instance.GetAll())
        {
            if (def == null || string.IsNullOrEmpty(def.resourceId))
                continue;

            float maxValue = !string.IsNullOrEmpty(def.maxStatId)
                ? stats.GetValue(def.maxStatId)
                : 100f;

            if (maxValue <= 0f)
                maxValue = 100f;

            string id = def.resourceId;
            current[id] = maxValue;
            max[id] = maxValue;
            definitions[id] = def;

            if (def.triggerDeathOnDepletion)
                healthResourceId = id;
        }

        if (string.IsNullOrEmpty(healthResourceId))
            Debug.LogError("[ResourceSystem] No health resource defined (triggerDeathOnDepletion = true)");
    }

    public void UpdateModule()
    {
    }

    public float GetResource(ResourceDefinition def)
    {
        if (def == null) return 0f;
        return current.TryGetValue(def.resourceId, out var v) ? v : 0f;
    }

    public float GetMaxResource(ResourceDefinition def)
    {
        if (def == null) return 0f;
        return max.TryGetValue(def.resourceId, out var v) ? v : 0f;
    }

    public float GetResourcePercentage(ResourceDefinition def)
    {
        if (def == null) return 0f;
        float m = GetMaxResource(def);
        return m > 0f ? GetResource(def) / m : 0f;
    }

    public bool HasResource(ResourceDefinition def, float amount)
        => GetResource(def) >= amount;

    public bool ConsumeResource(ResourceDefinition def, float amount)
    {
        if (!HasResource(def, amount))
            return false;

        Modify(def, -amount);
        return true;
    }

    public void RestoreResource(ResourceDefinition def, float amount)
    {
        Modify(def, amount);
    }

    public void SetResourceToMax(ResourceDefinition def)
    {
        if (def == null) return;
        if (!max.TryGetValue(def.resourceId, out float maxVal)) return;
        Set(def, maxVal);
    }

    public IReadOnlyDictionary<ResourceDefinition, float> GetAllResources()
    {
        var result = new Dictionary<ResourceDefinition, float>();
        foreach (var kvp in current)
        {
            if (definitions.TryGetValue(kvp.Key, out var def))
                result[def] = kvp.Value;
        }
        return result;
    }

    private void Modify(ResourceDefinition def, float delta)
    {
        if (def == null) return;
        if (!current.ContainsKey(def.resourceId)) return;
        Set(def, current[def.resourceId] + delta);
    }

    private void Set(ResourceDefinition def, float value)
    {
        if (def == null) return;

        string id = def.resourceId;
        if (!current.ContainsKey(id)) return;

        float maxVal = max[id];
        float clamped = Mathf.Clamp(value, 0f, maxVal);
        current[id] = clamped;

        OnResourceChanged?.Invoke(def, clamped);

        if (id == healthResourceId)
        {
            OnHealthChanged?.Invoke(clamped);

            if (clamped <= 0f)
                OnDeath?.Invoke();
        }
    }

    public float GetCurrentHealth()
    {
        if (string.IsNullOrEmpty(healthResourceId)) return 0f;
        return current.TryGetValue(healthResourceId, out var v) ? v : 0f;
    }

    public float GetMaxHealth()
    {
        if (string.IsNullOrEmpty(healthResourceId)) return 0f;
        return max.TryGetValue(healthResourceId, out var v) ? v : 0f;
    }

    public float GetHealthPercentage()
    {
        float maxHP = GetMaxHealth();
        return maxHP > 0f ? GetCurrentHealth() / maxHP : 0f;
    }

    public bool IsAlive()
        => GetCurrentHealth() > 0f;

    public void ApplyDamage(float amount)
    {
        if (!isEnabled) return;
        if (string.IsNullOrEmpty(healthResourceId)) return;
        if (!definitions.TryGetValue(healthResourceId, out var healthDef)) return;

        Modify(healthDef, -Mathf.Abs(amount));
    }

    public void ApplyHealing(float amount)
    {
        if (!isEnabled) return;
        if (string.IsNullOrEmpty(healthResourceId)) return;
        if (!definitions.TryGetValue(healthResourceId, out var healthDef)) return;

        Modify(healthDef, Mathf.Abs(amount));
    }
}