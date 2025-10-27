using UnityEngine;

public class StatsCoordinator : MonoBehaviour, IBrainModule, ISystemCoordinator
{
    [Header("Module Settings")]
    public bool IsEnabled { get; set; } = true;

    private ControllerBrain brain;
    private RPGCoreStats coreStats;
    private RPGSecondaryStats secondaryStats;
    private RPGResources resources;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        coreStats = brain.RPGCoreStats;
        secondaryStats = brain.RPGSecondaryStats;
        resources = brain.RPGResources;
    }

    public void UpdateModule()
    {
    }

    public RPGCoreStats GetCoreStats() => coreStats;
    public RPGSecondaryStats GetSecondaryStats() => secondaryStats;
    public RPGResources GetResources() => resources;

    public int GetLevel() => coreStats?.PlayerLevel ?? 1;

    public float GetCoreStat(string statName) => coreStats?.GetStatFinalValue(statName) ?? 0f;
    public float GetSecondaryStat(string statName) => secondaryStats?.GetSecondaryStatFinalValue(statName) ?? 0f;

    public float GetCurrentHealth() => resources?.CurrentHealth ?? 0f;
    public float GetMaxHealth() => resources?.MaxHealth ?? 100f;
    public float GetHealthPercentage() => resources?.HealthPercentage ?? 0f;

    public float GetCurrentMana() => resources?.CurrentMana ?? 0f;
    public float GetMaxMana() => resources?.MaxMana ?? 50f;
    public float GetManaPercentage() => resources?.ManaPercentage ?? 0f;

    public float GetCurrentStamina() => resources?.CurrentStamina ?? 0f;
    public float GetMaxStamina() => resources?.MaxStamina ?? 100f;
    public float GetStaminaPercentage() => resources?.StaminaPercentage ?? 0f;

    public void ModifyHealth(float amount) => resources?.ModifyHealth(amount);
    public void ModifyMana(float amount) => resources?.ModifyMana(amount);
    public void ModifyStamina(float amount) => resources?.ModifyStamina(amount);
}