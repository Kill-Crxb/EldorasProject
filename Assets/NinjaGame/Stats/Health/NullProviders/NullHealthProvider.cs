using System;
using UnityEngine;

public class NullHealthProvider : MonoBehaviour, IHealthProvider
{
    public float GetCurrentHealth() => 0f;
    public float GetMaxHealth() => 0f;
    public float GetHealthPercentage() => 0f;
    public bool IsAlive() => true;

    public void ApplyDamage(float damage) { }
    public void ApplyHealing(float healing) { }
    public void SetHealth(float value) { }
    public void SetHealthToMax() { }

    public event Action<float> OnHealthChanged;
    public event Action OnDeath;
}