using System;
using UnityEngine;

public class InvincibleHealthProvider : MonoBehaviour, IHealthProvider
{
    [SerializeField] private float maxHealth = 100f;

    public float GetCurrentHealth() => maxHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercentage() => 100f;
    public bool IsAlive() => true;

    public void ApplyDamage(float damage) { }
    public void ApplyHealing(float healing) { }
    public void SetHealth(float value) { }
    public void SetHealthToMax() { }

    public event Action<float> OnHealthChanged;
    public event Action OnDeath;
}