using System;
using UnityEngine;

/// <summary>
/// Damage Over Time Effect
/// Fully dynamic, DamageSystem-driven
/// </summary>
[Serializable]
public class DamageOverTimeEffect
{
    [Header("DoT Configuration")]
    public float duration = 5f;
    public float tickInterval = 1f;
    public float damagePerTick = 10f;
    public DamageType damageType = DamageType.Poison;

    public event Action OnCompleted;
    public event Action OnTick;

    [NonSerialized] private IntervalTimer timer;
    [NonSerialized] private DamageSystem targetDamage;
    [NonSerialized] private IHealthProvider targetHealth;
    [NonSerialized] private DamageSystem attackerDamage;

    public void SetAttacker(DamageSystem attacker)
    {
        attackerDamage = attacker;
    }

    public void Apply(DamageSystem target)
    {
        if (target == null)
        {
            Debug.LogWarning("[DamageOverTimeEffect] Target DamageSystem is null");
            Finish();
            return;
        }

        if (attackerDamage == null)
        {
            Debug.LogWarning("[DamageOverTimeEffect] No attacker DamageSystem set");
            Finish();
            return;
        }

        targetDamage = target;
        targetHealth = target.GetComponent<IHealthProvider>();

        if (targetHealth == null)
        {
            Debug.LogWarning("[DamageOverTimeEffect] Target has no IHealthProvider");
            Finish();
            return;
        }

        timer = new IntervalTimer(duration, tickInterval);
        timer.OnInterval += TickDamage;
        timer.OnTimerFinished += Finish;
        timer.Start();
    }

    public void Tick(float deltaTime)
    {
        timer?.Tick(deltaTime);
    }

    private void TickDamage()
    {
        if (targetHealth == null || !targetHealth.IsAlive())
        {
            Cancel();
            return;
        }

        CombatAttackData attackData = new CombatAttackData
        {
            baseDamage = damagePerTick,
            damageType = damageType,
            attackerTransform = attackerDamage.transform,
            hitPoint = targetDamage.transform.position,
            hitNormal = Vector3.up
        };

        CombatDamagePacket packet = attackerDamage.CalculateDamage(attackData);
        targetDamage.TakeDamage(packet);

        OnTick?.Invoke();
    }

    private void Finish()
    {
        Cleanup();
        OnCompleted?.Invoke();
    }

    public void Cancel()
    {
        timer?.Stop();
        Cleanup();
        OnCompleted?.Invoke();
    }

    private void Cleanup()
    {
        if (timer != null)
        {
            timer.OnInterval -= TickDamage;
            timer.OnTimerFinished -= Finish;
            timer = null;
        }

        targetDamage = null;
        targetHealth = null;
    }

    public bool IsActive => timer != null && timer.IsRunning;

    public float GetRemainingDuration()
        => timer == null ? 0f : duration - timer.CurrentTime;

    public float GetProgress()
        => timer == null ? 0f : timer.CurrentTime / duration;
}
