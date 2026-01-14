using System;
using UnityEngine;

/// <summary>
/// Heal Over Time (HoT) Effect
/// 
/// Usage:
/// - Called by abilities to apply periodic healing
/// - Works through IHealthProvider interface
/// - Requires manual Tick() calls (from EffectManager or ability)
/// 
/// Example:
/// var effect = new HealOverTimeEffect 
/// { 
///     duration = 5f, 
///     tickInterval = 1f, 
///     healPerTick = 10f
/// };
/// effect.SetHealthProvider(brain.GetModule<DamageSystem>());
/// effect.Apply();
/// 
/// // In Update loop:
/// effect.Tick(Time.deltaTime);
/// 
/// Updated: January 2026 - Supports DamageSystem
/// </summary>
[System.Serializable]
public class HealOverTimeEffect
{
    [Header("HoT Configuration")]
    [Tooltip("Total duration of the effect (seconds)")]
    public float duration = 5f;

    [Tooltip("Time between heal ticks (seconds)")]
    public float tickInterval = 1f;

    [Tooltip("Healing applied per tick")]
    public float healPerTick = 10f;

    /// <summary>Fired when effect completes</summary>
    public event Action OnCompleted;

    /// <summary>Fired on each heal tick (for VFX)</summary>
    public event Action OnTick;

    [System.NonSerialized]
    private IntervalTimer timer;

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
    /// Start the HoT effect (modern - no parameter needed)
    /// </summary>
    public void Apply()
    {
        if (healthProvider == null)
        {
            Debug.LogWarning("[HealOverTimeEffect] No health provider set - use SetHealthProvider() before Apply()");
            OnCompleted?.Invoke();
            return;
        }

        timer = new IntervalTimer(duration, tickInterval);
        timer.OnInterval += OnInterval;
        timer.OnTimerFinished += OnFinished;
        timer.Start();
    }

    /// <summary>
    /// Start HoT effect on IHealthProvider target (convenience overload)
    /// </summary>
    public void Apply(IHealthProvider target)
    {
        SetHealthProvider(target);
        Apply();
    }

    /// <summary>
    /// Start HoT effect (legacy IDamageable compatibility)
    /// Note: Doesn't actually use the target - uses healthProvider set earlier
    /// </summary>
    [System.Obsolete("Use Apply() or Apply(IHealthProvider) instead")]
    public void Apply(IDamageable target)
    {
        if (healthProvider == null)
        {
            Debug.LogWarning("[HealOverTimeEffect] No health provider set - use SetHealthProvider() before Apply()");
        }

        Apply();
    }

    /// <summary>
    /// Update the HoT effect (call from Update or EffectManager)
    /// </summary>
    public void Tick(float deltaTime)
    {
        timer?.Tick(deltaTime);
    }

    /// <summary>
    /// Called each tick interval
    /// </summary>
    private void OnInterval()
    {
        if (healthProvider != null && healthProvider.IsAlive())
        {
            healthProvider.ApplyHealing(healPerTick);
            OnTick?.Invoke();
        }
        else
        {
            // Target died or became invalid - end effect early
            Cancel();
        }
    }

    /// <summary>
    /// Called when timer finishes naturally
    /// </summary>
    private void OnFinished()
    {
        Cleanup();
    }

    /// <summary>
    /// Cancel the effect early (for dispels, death, etc.)
    /// </summary>
    public void Cancel()
    {
        timer?.Stop();
        Cleanup();
    }

    /// <summary>
    /// Clean up timer and references
    /// </summary>
    private void Cleanup()
    {
        if (timer != null)
        {
            timer.OnInterval -= OnInterval;
            timer.OnTimerFinished -= OnFinished;
            timer = null;
        }

        healthProvider = null;
        OnCompleted?.Invoke();
    }

    /// <summary>
    /// Check if effect is still active
    /// </summary>
    public bool IsActive()
    {
        return timer != null && timer.IsRunning;
    }

    /// <summary>
    /// Get remaining duration
    /// </summary>
    public float GetRemainingDuration()
    {
        if (timer == null) return 0f;
        return duration - timer.CurrentTime;
    }

    /// <summary>
    /// Get effect progress (0-1)
    /// </summary>
    public float GetProgress()
    {
        if (timer == null) return 0f;
        return timer.CurrentTime / duration;
    }
}