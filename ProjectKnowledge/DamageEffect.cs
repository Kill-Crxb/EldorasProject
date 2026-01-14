using System;
using UnityEngine;

/// <summary>
/// Instant Damage Effect
/// 
/// Usage:
/// - Called by abilities to apply damage to targets
/// - Integrates with DamageSystem for calculation
/// - Fires completion event for effect chaining
/// 
/// Example:
/// var effect = new DamageEffect { baseDamage = 50f, damageType = DamageType.Fire };
/// effect.SetDamageSystem(brain.Damage);
/// effect.Apply(targetBrain.Damage);
/// 
/// Updated: January 2026 - Uses DamageSystem instead of DamageModule
/// </summary>
[System.Serializable]
public class DamageEffect
{
    [Header("Damage Configuration")]
    [Tooltip("Base damage before modifiers")]
    public float baseDamage = 10f;

    [Tooltip("Type of damage (Physical, Magical, Fire, etc.)")]
    public DamageType damageType = DamageType.Physical;

    [Tooltip("Can this damage critically hit?")]
    public bool canCrit = true;

    /// <summary>Fired when effect completes</summary>
    public event Action OnCompleted;

    [System.NonSerialized]
    private DamageSystem damageSystem;

    /// <summary>
    /// Set the DamageSystem that will calculate outgoing damage
    /// Call this before Apply()
    /// </summary>
    public void SetDamageSystem(DamageSystem system)
    {
        damageSystem = system;
    }

    /// <summary>
    /// Apply damage to a target's DamageSystem
    /// </summary>
    public void Apply(DamageSystem target)
    {
        if (target == null)
        {
            Debug.LogWarning("[DamageEffect] Target DamageSystem is null!");
            OnCompleted?.Invoke();
            return;
        }

        if (damageSystem != null)
        {
            // Calculate damage using attacker's DamageSystem
            CombatDamagePacket packet = damageSystem.CalculateOutgoingDamage(
                baseDamage,
                damageType,
                canCrit
            );

            // Apply to target's DamageSystem
            target.TakeDamage(packet);
        }
        else
        {
            // Fallback: apply raw damage if no DamageSystem set
            Debug.LogWarning("[DamageEffect] No DamageSystem set - applying raw damage");
            target.TakeDamage(baseDamage, damageType);
        }

        OnCompleted?.Invoke();
    }

    /// <summary>
    /// Apply damage to legacy IDamageable target (for compatibility)
    /// </summary>
    [Obsolete("Use Apply(DamageSystem) instead for Brain-based entities")]
    public void Apply(IDamageable target)
    {
        if (target == null)
        {
            Debug.LogWarning("[DamageEffect] Target IDamageable is null!");
            OnCompleted?.Invoke();
            return;
        }

        if (damageSystem != null)
        {
            CombatDamagePacket packet = damageSystem.CalculateOutgoingDamage(
                baseDamage,
                damageType,
                canCrit
            );
            target.TakeDamage(packet.finalDamage);
        }
        else
        {
            target.TakeDamage(baseDamage);
        }

        OnCompleted?.Invoke();
    }

    /// <summary>
    /// Cancel the effect (for interruptible abilities)
    /// </summary>
    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}