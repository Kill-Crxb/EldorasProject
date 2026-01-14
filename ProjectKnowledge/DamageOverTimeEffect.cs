using System;
using UnityEngine;

/// <summary>
/// Damage Over Time (DoT) Effect
/// 
/// Usage:
/// - Called by abilities to apply periodic damage
/// - Integrates with DamageSystem for calculation
/// - Requires manual Tick() calls (from EffectManager or ability)
/// 
/// Example:
/// var effect = new DamageOverTimeEffect 
/// { 
///     duration = 5f, 
///     tickInterval = 1f, 
///     damagePerTick = 10f,
///     damageType = DamageType.Poison
/// };
/// effect.SetDamageSystem(brain.Damage);
/// effect.Apply(targetBrain.Damage);
/// 
/// // In Update loop:
/// effect.Tick(Time.deltaTime);
/// 
/// Updated: January 2026 - Uses DamageSystem instead of DamageModule
/// </summary>
[System.Serializable]
public class DamageOverTimeEffect
{
    [Header("DoT Configuration")]
    [Tooltip("Total duration of the effect (seconds)")]
    public float duration = 5f;

    [Tooltip("Time between damage ticks (seconds)")]
    public float tickInterval = 1f;

    [Tooltip("Damage dealt per tick")]
    public float damagePerTick = 10f;

    [Tooltip("Type of damage (Poison, Fire, etc.)")]
    public DamageType damageType = DamageType.Poison;

    [Tooltip("Can each tick critically hit?")]
    public bool canCrit = false;

    /// <summary>Fired when effect completes</summary>
    public event Action OnCompleted;

    /// <summary>Fired on each damage tick (for VFX)</summary>
    public event Action OnTick;

    [System.NonSerialized]
    private IntervalTimer timer;

    [System.NonSerialized]
    private DamageSystem currentTarget;

    [System.NonSerialized]
    private DamageSystem damageSystem;

    /// <summary>
    /// Set the DamageSystem that will calculate outgoing damage
    /// Call this before Apply()
    /// </summary>
    public void SetDamageSystem(DamageSystem system)
    {
        damageSystem = system;
    }

    /// <summary>
    /// Apply DoT to a target's DamageSystem
    /// </summary>
    public void Apply(DamageSystem target)
    {
        if (target == null)
        {
            Debug.LogWarning("[DamageOverTimeEffect] Target DamageSystem is null!");
            OnCompleted?.Invoke();
            return;
        }

        currentTarget = target;
        timer = new IntervalTimer(duration, tickInterval);
        timer.OnInterval += OnInterval;
        timer.OnTimerFinished += OnFinished;
        timer.Start();
    }

    /// <summary>
    /// Update the DoT effect (call from Update or EffectManager)
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
        if (currentTarget != null && currentTarget.IsAlive())
        {
            if (damageSystem != null)
            {
                // Calculate damage using attacker's DamageSystem
                CombatDamagePacket packet = damageSystem.CalculateOutgoingDamage(
                    damagePerTick,
                    damageType,
                    canCrit
                );

                // Apply to target
                currentTarget.TakeDamage(packet);
            }
            else
            {
                // Fallback: apply raw damage
                currentTarget.TakeDamage(damagePerTick, damageType);
            }

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

        currentTarget = null;
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