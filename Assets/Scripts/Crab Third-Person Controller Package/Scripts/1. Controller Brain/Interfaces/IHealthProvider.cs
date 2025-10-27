using UnityEngine;

/// <summary>
/// Manages health state for entities.
/// Implemented by adapters that bridge to specific health/resource systems.
/// 
/// Design:
/// - Decouples damage system from specific health implementations
/// - Works with RPGResources, simple health, or any custom system
/// - Adapters translate between specific systems and this interface
/// </summary>
public interface IHealthProvider
{
    /// <summary>Apply damage to this entity</summary>
    void ApplyDamage(float amount);

    /// <summary>Restore health to this entity</summary>
    void ApplyHealing(float amount);

    /// <summary>Current health value</summary>
    float GetCurrentHealth();

    /// <summary>Maximum health value</summary>
    float GetMaxHealth();

    /// <summary>Health as percentage (0.0 to 1.0)</summary>
    float GetHealthPercentage();

    /// <summary>Is this entity still alive?</summary>
    bool IsAlive();
}