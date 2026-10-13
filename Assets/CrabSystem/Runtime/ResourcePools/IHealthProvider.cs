using System;

public interface IHealthProvider
{
    float GetCurrentHealth();
    float GetMaxHealth();
    float GetHealthPercentage();

    bool IsAlive();

    void ApplyDamage(float amount);
    void ApplyHealing(float amount);

    event Action<float> OnHealthChanged;
    event Action OnDeath;
}
