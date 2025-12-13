using System;
using UnityEngine;

[System.Serializable]
public class HealOverTimeEffect
{
    [Header("HoT Configuration")]
    public float duration = 5f;
    public float tickInterval = 1f;
    public float healPerTick = 10f;

    public event Action OnCompleted;

    [System.NonSerialized]
    private IntervalTimer timer;
    [System.NonSerialized]
    private IHealthProvider healthProvider;

    public void SetHealthProvider(IHealthProvider provider)
    {
        healthProvider = provider;
    }

    public void Apply(IDamageable target)
    {
        timer = new IntervalTimer(duration, tickInterval);
        timer.OnInterval += OnInterval;
        timer.OnTimerFinished += OnFinished;
        timer.Start();
    }

    private void OnInterval()
    {
        healthProvider?.ApplyHealing(healPerTick);
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
        OnCompleted?.Invoke();
    }

    public void Tick(float deltaTime)
    {
        timer?.Tick(deltaTime);
    }
}