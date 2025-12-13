using System;
using UnityEngine;

[System.Serializable]
public class HealEffect
{
    [Header("Heal Configuration")]
    public float healAmount = 50f;

    public event Action OnCompleted;

    [System.NonSerialized]
    private IHealthProvider healthProvider;

    public void SetHealthProvider(IHealthProvider provider)
    {
        healthProvider = provider;
    }

    public void Apply(IDamageable target)
    {
        healthProvider?.ApplyHealing(healAmount);
        OnCompleted?.Invoke();
    }

    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}