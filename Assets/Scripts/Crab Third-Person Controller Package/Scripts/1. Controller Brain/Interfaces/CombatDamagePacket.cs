using UnityEngine;

/// <summary>
/// Complete damage information sent from DamageOut to DamageIn.
/// Contains all data needed for damage application and feedback.
/// 
/// Design:
/// - Includes both calculated damage AND contextual information
/// - Supports damage types (physical, magical, true)
/// - Contains hit location data for feedback systems
/// - Immutable after creation (all fields are readonly)
/// </summary>
[System.Serializable]
public class CombatDamagePacket
{
    [Header("Damage Values")]
    public readonly float baseDamage;
    public readonly float finalDamage;
    public readonly bool isCriticalHit;
    public readonly float criticalMultiplier;

    [Header("Damage Type")]
    public readonly DamageType damageType;

    [Header("Source Information")]
    public readonly Transform attacker;
    public readonly string attackerId;

    [Header("Hit Information")]
    public readonly Vector3 hitPoint;
    public readonly Vector3 hitNormal;
    public readonly Vector3 attackDirection;

    [Header("Context")]
    public readonly int comboCount;
    public readonly bool isHeavyAttack;
    public readonly string weaponId;

    /// <summary>
    /// Constructor - creates an immutable damage packet
    /// </summary>
    public CombatDamagePacket(
        float baseDamage,
        float finalDamage,
        bool isCriticalHit,
        float criticalMultiplier,
        DamageType damageType,
        Transform attacker,
        string attackerId,
        Vector3 hitPoint,
        Vector3 hitNormal,
        Vector3 attackDirection,
        int comboCount = 0,
        bool isHeavyAttack = false,
        string weaponId = "")
    {
        this.baseDamage = baseDamage;
        this.finalDamage = finalDamage;
        this.isCriticalHit = isCriticalHit;
        this.criticalMultiplier = criticalMultiplier;
        this.damageType = damageType;
        this.attacker = attacker;
        this.attackerId = attackerId;
        this.hitPoint = hitPoint;
        this.hitNormal = hitNormal;
        this.attackDirection = attackDirection;
        this.comboCount = comboCount;
        this.isHeavyAttack = isHeavyAttack;
        this.weaponId = weaponId;
    }

    /// <summary>Create a simple damage packet for testing</summary>
    public static CombatDamagePacket CreateSimple(float damage, Transform attacker, Vector3 hitPoint)
    {
        Vector3 direction = Vector3.zero;
        if (attacker != null)
        {
            direction = (hitPoint - attacker.position).normalized;
        }

        return new CombatDamagePacket(
            baseDamage: damage,
            finalDamage: damage,
            isCriticalHit: false,
            criticalMultiplier: 1f,
            damageType: DamageType.Physical,
            attacker: attacker,
            attackerId: attacker?.name ?? "Unknown",
            hitPoint: hitPoint,
            hitNormal: Vector3.up,
            attackDirection: direction
        );
    }
}

/// <summary>
/// Types of damage in the game.
/// Different damage types interact with different defensive stats.
/// </summary>
public enum DamageType
{
    Physical,   // Reduced by Armor
    Magical,    // Reduced by Magic Resistance
    True        // Ignores all defenses
}