using UnityEngine;

/// <summary>
/// Context information about an attack before damage calculation.
/// Passed from combat system to DamageOut.
/// 
/// Design:
/// - Contains all information needed to calculate damage
/// - Lightweight struct that's easy to create
/// - Provides factory methods for common scenarios
/// </summary>
[System.Serializable]
public class CombatAttackData
{
    [Header("Attack Type")]
    public bool isHeavyAttack;
    public bool isChargedAttack;

    [Header("Combat State")]
    public int comboCount;
    public float comboMultiplier = 1f;

    [Header("Source Information")]
    public Transform attackerTransform;
    public Vector3 hitPoint;
    public Vector3 hitNormal;

    [Header("Weapon Information")]
    public string weaponId;
    public float weaponDamageMultiplier = 1f;

    /// <summary>Create basic attack data with minimal information</summary>
    public static CombatAttackData CreateBasic(Transform attacker, Vector3 hitPoint)
    {
        return new CombatAttackData
        {
            attackerTransform = attacker,
            hitPoint = hitPoint,
            comboCount = 0,
            comboMultiplier = 1f,
            weaponDamageMultiplier = 1f
        };
    }

    /// <summary>Create attack data with combo information</summary>
    public static CombatAttackData CreateWithCombo(Transform attacker, Vector3 hitPoint, int combo, float comboMultiplier)
    {
        return new CombatAttackData
        {
            attackerTransform = attacker,
            hitPoint = hitPoint,
            comboCount = combo,
            comboMultiplier = comboMultiplier,
            weaponDamageMultiplier = 1f
        };
    }
}