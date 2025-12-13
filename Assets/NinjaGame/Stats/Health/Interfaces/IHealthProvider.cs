using System;

public interface IHealthProvider
{
    float GetCurrentHealth();
    float GetMaxHealth();
    float GetHealthPercentage();
    bool IsAlive();

    void ApplyDamage(float damage);
    void ApplyHealing(float healing);
    void SetHealth(float value);
    void SetHealthToMax();

    event Action<float> OnHealthChanged;
    event Action OnDeath;
}