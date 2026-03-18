using System;
using UnityEngine;

/// <summary>
/// Instant Heal Effect
/// Data-driven, HealthProvider-authoritative
/// </summary>
[Serializable]
public class HealEffect
{
    [Header("Heal Configuration")]
    [Tooltip("Amount of health to restore")]
    public float healAmount = 50f;

    public event Action OnCompleted;

    [NonSerialized]
    private IHealthProvider healthProvider;

    private bool isCompleted;

    /// <summary>
    /// Set the health provider that will receive healing
    /// Optional convenience for Apply()
    /// </summary>
    public void SetHealthProvider(IHealthProvider provider)
    {
        healthProvider = provider;
    }

    /// <summary>
    /// Apply healing using previously set health provider
    /// </summary>
    public void Apply()
    {
        if (isCompleted)
            return;

        if (healthProvider == null)
        {
            Debug.LogWarning("[HealEffect] No health provider set - healing not applied");
            Complete();
            return;
        }

        healthProvider.ApplyHealing(healAmount);
        Complete();
    }

    /// <summary>
    /// Apply healing directly to a target (preferred API)
    /// </summary>
    public void Apply(IHealthProvider target)
    {
        if (isCompleted)
            return;

        if (target == null)
        {
            Debug.LogWarning("[HealEffect] Target health provider is null");
            Complete();
            return;
        }

        target.ApplyHealing(healAmount);
        Complete();
    }

    /// <summary>
    /// Legacy IDamageable compatibility
    /// </summary>
    [Obsolete("Use Apply(IHealthProvider) instead")]
    public void Apply(IDamageable target)
    {
        if (isCompleted)
            return;

        if (healthProvider == null)
        {
            Debug.LogWarning("[HealEffect] No health provider set - legacy heal ignored");
            Complete();
            return;
        }

        healthProvider.ApplyHealing(healAmount);
        Complete();
    }

    public void Cancel()
    {
        Complete();
    }

    private void Complete()
    {
        if (isCompleted)
            return;

        isCompleted = true;
        OnCompleted?.Invoke();
    }
}
