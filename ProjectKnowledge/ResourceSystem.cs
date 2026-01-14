using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Resource System - Manages consumable resources (Health, Mana, Stamina, etc.)
/// 
/// Architecture:
/// - Implements IResourceProvider (for abilities and other systems)
/// - Integrates with StatSystem for max values
/// - Tracks current resource values separately from stats
/// - Handles resource consumption, regeneration, and events
/// 
/// Integration:
/// - StatSystem provides max values (character.max_health, etc.)
/// - AbilitySystem queries for resource costs
/// - DamageSystem uses health through IHealthProvider
/// 
/// Phase 1.7b: Universal Systems Consolidation
/// Created: January 2026
/// </summary>
public class ResourceSystem : MonoBehaviour,
    IBrainModule,
    IResourceProvider,
    IHealthProvider  // Also implements health interface for DamageSystem
{
    #region Inspector Fields

    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Regeneration Overrides (Optional)")]
    [Tooltip("Override health regen (leave 0 to use ResourceManager settings)")]
    [SerializeField] private float healthRegenOverride = -1f;

    [Tooltip("Override mana regen (leave -1 to use ResourceManager settings)")]
    [SerializeField] private float manaRegenOverride = -1f;

    [Tooltip("Override stamina regen (leave -1 to use ResourceManager settings)")]
    [SerializeField] private float staminaRegenOverride = -1f;

    [Header("Debug")]
    [SerializeField] private bool debugResources = false;

    #endregion

    #region Private Fields

    private ControllerBrain brain;
    private StatSystem stats;

    // Current resource values
    private Dictionary<ResourceType, float> currentResources = new Dictionary<ResourceType, float>();

    // Regeneration timers
    private float timeSinceLastHealthLoss = float.MaxValue;
    private float timeSinceLastManaUse = float.MaxValue;
    private float timeSinceLastStaminaUse = float.MaxValue;

    // Death flag
    private bool isDead = false;

    #endregion

    #region Events

    // IResourceProvider events
    public event Action<ResourceType, float> OnResourceChanged;

    // IHealthProvider events
    public event Action<float> OnHealthChanged;
    public event Action OnDeath;

    #endregion

    #region Properties

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public ControllerBrain Brain => brain;

    #endregion

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = brain.Stats;

        if (stats == null)
        {
            Debug.LogError($"[ResourceSystem] No StatSystem found on {brain.name}! ResourceSystem requires StatSystem.");
            isEnabled = false;
            return;
        }

        // Initialize resources to max values
        InitializeResources();

        if (debugResources)
        {
            Debug.Log($"[ResourceSystem] Initialized on {brain.name}. " +
                     $"Health: {GetResource(ResourceType.Health)}/{GetMaxResource(ResourceType.Health)}");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled || isDead) return;

        float deltaTime = Time.deltaTime;

        // Update regeneration timers
        timeSinceLastHealthLoss += deltaTime;
        timeSinceLastManaUse += deltaTime;
        timeSinceLastStaminaUse += deltaTime;

        // Regenerate resources
        RegenerateResources(deltaTime);

        // Check for death
        if (GetResource(ResourceType.Health) <= 0 && !isDead)
        {
            Die();
        }
    }

    #endregion

    #region Initialization

    private void InitializeResources()
    {
        // Initialize all resource types
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            float maxValue = GetMaxResource(type);
            currentResources[type] = maxValue;
        }
    }

    #endregion

    #region IResourceProvider Implementation

    public float GetResource(ResourceType type)
    {
        if (currentResources.TryGetValue(type, out float value))
        {
            return value;
        }

        // Initialize if missing
        float maxValue = GetMaxResource(type);
        currentResources[type] = maxValue;
        return maxValue;
    }

    public float GetMaxResource(ResourceType type)
    {
        if (stats == null) return 100f;

        // Get stat ID from ResourceManager (data-driven)
        string statId = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetMaxStatId(type)
            : GetStatIdForResource(type); // Fallback if no manager

        if (stats.HasStat(statId))
        {
            return stats.GetValue(statId);
        }

        // Fallback defaults
        return 100f;
    }

    public float GetResourcePercentage(ResourceType type)
    {
        float max = GetMaxResource(type);
        if (max <= 0) return 0f;
        return Mathf.Clamp01(GetResource(type) / max);
    }

    public bool HasResource(ResourceType type, float amount)
    {
        return GetResource(type) >= amount;
    }

    public bool ConsumeResource(ResourceType type, float amount)
    {
        if (!HasResource(type, amount))
            return false;

        float current = GetResource(type);
        float newValue = Mathf.Max(0, current - amount);
        SetResource(type, newValue);

        // Reset regen timer
        ResetRegenTimer(type);

        if (debugResources)
        {
            Debug.Log($"[ResourceSystem] {brain.name} consumed {amount} {type}: {current:F1} → {newValue:F1}");
        }

        return true;
    }

    public void RestoreResource(ResourceType type, float amount)
    {
        if (amount <= 0) return;

        float current = GetResource(type);
        float max = GetMaxResource(type);
        float newValue = Mathf.Min(max, current + amount);
        SetResource(type, newValue);

        if (debugResources)
        {
            Debug.Log($"[ResourceSystem] {brain.name} restored {amount} {type}: {current:F1} → {newValue:F1}");
        }
    }

    public void SetResourceToMax(ResourceType type)
    {
        float max = GetMaxResource(type);
        SetResource(type, max);
    }

    #endregion

    #region IHealthProvider Implementation

    public float GetCurrentHealth()
    {
        return GetResource(ResourceType.Health);
    }

    public float GetMaxHealth()
    {
        return GetMaxResource(ResourceType.Health);
    }

    public float GetHealthPercentage()
    {
        return GetResourcePercentage(ResourceType.Health);
    }

    public bool IsAlive()
    {
        return !isDead && GetCurrentHealth() > 0;
    }

    public void ApplyDamage(float amount)
    {
        if (!isEnabled || isDead) return;

        float current = GetResource(ResourceType.Health);
        float newHealth = Mathf.Max(0, current - amount);
        SetResource(ResourceType.Health, newHealth);

        // Reset health regen timer
        timeSinceLastHealthLoss = 0f;
    }

    public void ApplyHealing(float amount)
    {
        RestoreResource(ResourceType.Health, amount);
    }

    public void SetHealth(float value)
    {
        SetResource(ResourceType.Health, value);
    }

    public void SetHealthToMax()
    {
        SetResourceToMax(ResourceType.Health);
    }

    #endregion

    #region Resource Management

    private void SetResource(ResourceType type, float value)
    {
        float max = GetMaxResource(type);
        float clamped = Mathf.Clamp(value, 0, max);

        currentResources[type] = clamped;

        // Fire events
        OnResourceChanged?.Invoke(type, clamped);

        // Fire health-specific event
        if (type == ResourceType.Health)
        {
            OnHealthChanged?.Invoke(clamped);
        }
    }

    private void ResetRegenTimer(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Health:
                timeSinceLastHealthLoss = 0f;
                break;
            case ResourceType.Mana:
                timeSinceLastManaUse = 0f;
                break;
            case ResourceType.Stamina:
                timeSinceLastStaminaUse = 0f;
                break;
        }
    }

    #endregion

    #region Regeneration

    private void RegenerateResources(float deltaTime)
    {
        // Regenerate each resource type based on ResourceManager config
        RegenerateResource(ResourceType.Health, deltaTime, ref timeSinceLastHealthLoss, healthRegenOverride);
        RegenerateResource(ResourceType.Mana, deltaTime, ref timeSinceLastManaUse, manaRegenOverride);
        RegenerateResource(ResourceType.Stamina, deltaTime, ref timeSinceLastStaminaUse, staminaRegenOverride);
    }

    private void RegenerateResource(ResourceType type, float deltaTime, ref float timeSinceUse, float regenOverride)
    {
        // Get config from ResourceManager (or use override)
        float regenPerSecond = regenOverride >= 0
            ? regenOverride
            : (ResourceManager.Instance?.GetRegenPerSecond(type) ?? 0f);

        float regenDelay = ResourceManager.Instance?.GetRegenDelay(type) ?? 0f;

        // Skip if no regen configured
        if (regenPerSecond <= 0) return;

        // Check delay
        if (timeSinceUse < regenDelay) return;

        // Regenerate
        float current = GetResource(type);
        float max = GetMaxResource(type);
        if (current < max)
        {
            float regen = regenPerSecond * deltaTime;
            RestoreResource(type, regen);
        }
    }

    #endregion

    #region Death Handling

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        OnDeath?.Invoke();

        if (debugResources)
        {
            Debug.Log($"[ResourceSystem] {brain.name} died (health reached 0)");
        }
    }

    public void Resurrect()
    {
        isDead = false;
        SetResourceToMax(ResourceType.Health);

        if (debugResources)
        {
            Debug.Log($"[ResourceSystem] {brain.name} resurrected");
        }
    }

    #endregion

    #region Helper Methods

    private string GetStatIdForResource(ResourceType type)
    {
        return type switch
        {
            ResourceType.Health => "character.max_health",
            ResourceType.Mana => "character.max_mana",
            ResourceType.Stamina => "character.max_stamina",
            ResourceType.Energy => "character.max_energy",
            ResourceType.Rage => "character.max_rage",
            ResourceType.Focus => "character.max_focus",
            _ => $"character.max_{type.ToString().ToLower()}"
        };
    }

    #endregion

    #region Context Menu Helpers

    [ContextMenu("Restore All Resources")]
    private void DebugRestoreAll()
    {
        if (!Application.isPlaying) return;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            SetResourceToMax(type);
        }

        Debug.Log($"[ResourceSystem] All resources restored to max");
    }

    [ContextMenu("Consume 50 Mana")]
    private void DebugConsumeMana()
    {
        if (!Application.isPlaying) return;

        ConsumeResource(ResourceType.Mana, 50f);
    }

    [ContextMenu("Print Resources")]
    private void DebugPrintResources()
    {
        if (!Application.isPlaying) return;

        Debug.Log($"=== {brain.name} Resources ===");
        Debug.Log($"Health: {GetResource(ResourceType.Health):F1}/{GetMaxResource(ResourceType.Health):F1}");
        Debug.Log($"Mana: {GetResource(ResourceType.Mana):F1}/{GetMaxResource(ResourceType.Mana):F1}");
        Debug.Log($"Stamina: {GetResource(ResourceType.Stamina):F1}/{GetMaxResource(ResourceType.Stamina):F1}");
    }

    #endregion
}