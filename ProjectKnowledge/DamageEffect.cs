using System;
using UnityEngine;

/// <summary>
/// Instant Damage Effect
/// Data-driven, DamageSystem-authoritative
/// </summary>
[Serializable]
public class DamageEffect
{
    [Header("Damage Configuration")]
    public float baseDamage = 10f;
    public DamageType damageType = DamageType.Physical;

    public event Action OnCompleted;

    [NonSerialized] private DamageSystem attackerDamageSystem;
    private bool isCompleted;

    /// <summary>
    /// Set the attacker's DamageSystem (required for stat-based damage)
    /// </summary>
    public void SetDamageSystem(DamageSystem system)
    {
        attackerDamageSystem = system;
    }

    /// <summary>
    /// Apply damage to a target DamageSystem
    /// </summary>
    public void Apply(DamageSystem target)
    {
        if (isCompleted)
            return;

        if (target == null)
        {
            Debug.LogWarning("[DamageEffect] Target DamageSystem is null");
            Complete();
            return;
        }

        if (attackerDamageSystem == null)
        {
            Debug.LogError("[DamageEffect] Attacker DamageSystem not set before Apply()");
            Complete();
            return;
        }

        CombatAttackData attackData = new CombatAttackData
        {
            baseDamage = baseDamage,
            damageType = damageType,
            attackerTransform = attackerDamageSystem.transform,
            hitPoint = target.transform.position,
            hitNormal = Vector3.up
        };

        CombatDamagePacket packet = attackerDamageSystem.CalculateDamage(attackData);
        target.TakeDamage(packet);

        Complete();
    }

    public void Cancel()
    {
        Complete();
    }

    private void Complete()
    {
        if (isCompleted)
            return;

        isCompleted = true;
        OnCompleted?.Invoke();
    }
}
