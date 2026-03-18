using UnityEngine;

/// <summary>
/// Context information about an attack before damage calculation.
/// Passed from combat system to DamageModule.
/// 
/// Design:
/// - Contains all information needed to calculate damage
/// - Lightweight class that's easy to create
/// - Provides factory methods for common scenarios
/// </summary>
[System.Serializable]
public class CombatAttackData
{
    [Header("Base Damage")]
    public float baseDamage = 10f;

    [Header("Attack Type")]
    public bool isHeavyAttack;
    public bool isChargedAttack;
    public float heavyAttackMultiplier = 2.0f;

    [Header("Combat State")]
    public int comboCount;
    public float comboMultiplier = 1f;

    [Header("Source Information")]
    public Transform attackerTransform;
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public Vector3 attackDirection;

    [Header("Weapon Information")]
    public string weaponId;
    public float weaponDamageMultiplier = 1f;

    [Header("Damage Type")]
    public DamageType damageType = DamageType.Physical;

    /// <summary>Create basic attack data with minimal information</summary>
    public static CombatAttackData CreateBasic(Transform attacker, Vector3 hitPoint)
    {
        Vector3 direction = Vector3.zero;
        if (attacker != null)
        {
            direction = (hitPoint - attacker.position).normalized;
        }

        return new CombatAttackData
        {
            attackerTransform = attacker,
            hitPoint = hitPoint,
            hitNormal = Vector3.up,
            attackDirection = direction,
            baseDamage = 10f,
            comboCount = 0,
            comboMultiplier = 1f,
            weaponDamageMultiplier = 1f,
            heavyAttackMultiplier = 2.0f,
            damageType = DamageType.Physical
        };
    }

    /// <summary>Create attack data with combo information</summary>
    public static CombatAttackData CreateWithCombo(Transform attacker, Vector3 hitPoint, int combo, float comboMult)
    {
        var data = CreateBasic(attacker, hitPoint);
        data.comboCount = combo;
        data.comboMultiplier = comboMult;
        return data;
    }
}