using System;
using UnityEngine;

/// <summary>
/// Instant Heal Effect
/// 
/// Usage:
/// - Called by abilities to heal targets
/// - Works through IHealthProvider interface
/// - DamageSystem and other health providers supported
/// 
/// Example:
/// var effect = new HealEffect { healAmount = 50f };
/// effect.SetHealthProvider(brain.GetModule<DamageSystem>());
/// effect.Apply();
/// 
/// Updated: January 2026 - Supports DamageSystem
/// </summary>
[System.Serializable]
public class HealEffect
{
    [Header("Heal Configuration")]
    [Tooltip("Amount of health to restore")]
    public float healAmount = 50f;

    /// <summary>Fired when effect completes</summary>
    public event Action OnCompleted;

    [System.NonSerialized]
    private IHealthProvider healthProvider;

    /// <summary>
    /// Set the health provider that will receive healing
    /// Call this before Apply()
    /// </summary>
    public void SetHealthProvider(IHealthProvider provider)
    {
        healthProvider = provider;
    }

    /// <summary>
    /// Apply healing to the health provider (modern - no parameter needed)
    /// </summary>
    public void Apply()
    {
        if (healthProvider != null)
        {
            healthProvider.ApplyHealing(healAmount);
        }
        else
        {
            Debug.LogWarning("[HealEffect] No health provider set - healing not applied");
        }

        OnCompleted?.Invoke();
    }

    /// <summary>
    /// Apply healing to IHealthProvider target (convenience overload)
    /// </summary>
    public void Apply(IHealthProvider target)
    {
        if (target != null)
        {
            target.ApplyHealing(healAmount);
        }

        OnCompleted?.Invoke();
    }

    /// <summary>
    /// Apply healing (legacy IDamageable compatibility)
    /// Note: Doesn't actually use the target - uses healthProvider set earlier
    /// </summary>
    [System.Obsolete("Use Apply() or Apply(IHealthProvider) instead")]
    public void Apply(IDamageable target)
    {
        if (healthProvider != null)
        {
            healthProvider.ApplyHealing(healAmount);
        }
        else
        {
            Debug.LogWarning("[HealEffect] No health provider set - use SetHealthProvider() before Apply()");
        }

        OnCompleted?.Invoke();
    }

    /// <summary>
    /// Cancel the effect (for interruptible abilities)
    /// </summary>
    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}