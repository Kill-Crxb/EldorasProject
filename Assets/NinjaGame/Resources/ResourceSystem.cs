using System;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

public class ResourceSystem : MonoBehaviour, IResourceProvider, IHealthProvider
{
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled => isEnabled;

    private ControllerBrain brain;
    private StatSystem stats;

    private readonly Dictionary<ResourceDefinition, float> current = new();
    private readonly Dictionary<ResourceDefinition, float> max = new();


    public IReadOnlyDictionary<ResourceDefinition, float> GetAllResources()
    {
        return current;
    }

    [Header("Debug")]
    [SerializeField] private bool debugResources = false;

    public event Action<ResourceDefinition, float> OnResourceChanged;
    public event Action<float> OnHealthChanged;
    public event Action OnDeath;

    private ResourceDefinition healthDef;

    #region Initialization


    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = brain.Stats;

        foreach (var def in ResourceManager.Instance.GetAll())
        {
            float maxValue = GetMaxResource(def);

            current[def] = maxValue; // start full by default
            max[def] = maxValue;

            // Assign health role explicitly (NOT via flag)
            if (def.triggerDeathOnDepletion)
                healthDef = def;
        }

        if (healthDef == null)
            Debug.LogError("[ResourceSystem] No health resource defined (triggerDeathOnDepletion = true)");

        if (debugResources)
            Debug.Log($"[ResourceSystem] Initialized {current.Count} resources on {brain.name}");
    }

    #endregion

    #region IResourceProvider (Definition-driven)

    public float GetResource(ResourceDefinition def)
        => def != null && current.TryGetValue(def, out var v) ? v : 0f;

    public float GetMaxResource(ResourceDefinition def)
        => def != null && max.TryGetValue(def, out var v) ? v : 0f;

    public float GetResourcePercentage(ResourceDefinition def)
    {
        float m = GetMaxResource(def);
        return m > 0 ? GetResource(def) / m : 0f;
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
        if (def == null || !max.ContainsKey(def)) return;
        Set(def, max[def]);
    }



    #endregion

    #region Internal Mutation

    private void Modify(ResourceDefinition def, float delta)
    {
        if (def == null || !current.ContainsKey(def)) return;
        Set(def, current[def] + delta);
    }

    private void Set(ResourceDefinition def, float value)
    {
        if (def == null || !current.ContainsKey(def)) return;

        float clamped = Mathf.Clamp(value, 0f, max[def]);
        current[def] = clamped;

        OnResourceChanged?.Invoke(def, clamped);

        if (def == healthDef)
        {
            OnHealthChanged?.Invoke(clamped);

            if (clamped <= 0f)
                OnDeath?.Invoke();
        }
    }

    #endregion

    #region IHealthProvider (Health as Resource)

    public float GetCurrentHealth()
        => GetResource(healthDef);

    public float GetMaxHealth()
        => GetMaxResource(healthDef);

    public float GetHealthPercentage()
        => GetResourcePercentage(healthDef);

    public bool IsAlive()
        => GetCurrentHealth() > 0f;

    public void ApplyDamage(float amount)
    {
        if (!isEnabled) return;
        Modify(healthDef, -Mathf.Abs(amount));
    }

    public void ApplyHealing(float amount)
    {
        if (!isEnabled) return;
        Modify(healthDef, Mathf.Abs(amount));
    }

    #endregion
}
