using System;
using UnityEngine;

[System.Serializable]
public class DamageOverTimeEffect
{
    [Header("DoT Configuration")]
    public float duration = 5f;
    public float tickInterval = 1f;
    public float damagePerTick = 10f;
    public DamageType damageType = DamageType.Physical;
    [Tooltip("Can each tick critically hit?")]
    public bool canCrit = false;

    public event Action OnCompleted;

    [System.NonSerialized]
    private IntervalTimer timer;
    [System.NonSerialized]
    private IDamageable currentTarget;
    [System.NonSerialized]
    private DamageModule damageModule;

    public void SetDamageModule(DamageModule module)
    {
        damageModule = module;
    }

    public void Apply(IDamageable target)
    {
        currentTarget = target;
        timer = new IntervalTimer(duration, tickInterval);
        timer.OnInterval += OnInterval;
        timer.OnTimerFinished += OnFinished;
        timer.Start();
    }

    private void OnInterval()
    {
        if (currentTarget != null && damageModule != null)
        {
            CombatDamagePacket packet = damageModule.CalculateOutgoingDamage(
                damagePerTick,
                damageType,
                canCrit
            );
            currentTarget.TakeDamage(packet.finalDamage);
        }
    }

    private void OnFinished()
    {
        Cleanup();
    }

    public void Cancel()
    {
        timer?.Stop();
        Cleanup();
    }

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

    public void Tick(float deltaTime)
    {
        timer?.Tick(deltaTime);
    }
}