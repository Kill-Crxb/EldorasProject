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
    public readonly DamageSource source;

    [Header("Resolution")]
    // The DEFENDER decides full or glancing (glancing halves finalDamage). accuracy is the attacker's
    // Finesse, added to the d20.
    public readonly float accuracy;
    // Set by the attacker (a riposte, GuardModule.TakeRiposte); the defender rolls the d20 twice.
    public readonly bool advantage;

    /// <summary>
    /// Constructor - creates an immutable damage packet
    /// </summary>
    public CombatDamagePacket(
        float baseDamage,
        float finalDamage,
        DamageType damageType,
        Transform attacker,
        string attackerId,
        Vector3 hitPoint,
        Vector3 hitNormal,
        Vector3 attackDirection,
        int comboCount = 0,
        bool isHeavyAttack = false,
        string weaponId = "",
        DamageSource source = DamageSource.Other,
        float accuracy = 0f,
        bool advantage = false)
    {
        this.baseDamage = baseDamage;
        this.finalDamage = finalDamage;
        this.damageType = damageType;
        this.attacker = attacker;
        this.attackerId = attackerId;
        this.hitPoint = hitPoint;
        this.hitNormal = hitNormal;
        this.attackDirection = attackDirection;
        this.comboCount = comboCount;
        this.isHeavyAttack = isHeavyAttack;
        this.weaponId = weaponId;
        this.source = source;
        this.accuracy = accuracy;
        this.advantage = advantage;
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
    True,        // Ignores all defenses
    Fire,       // Magical fire damage
    Lightning,  // Magical lightning damage
    Ice,        // Magical ice damage
    Poison,     // DoT damage type
    Holy,       // Divine damage
    Aether,
    Wind,
    Water,
    Earth,
    Magma,
    Crystal,
    Nature,

    /// <summary>
    /// The other face of Aether. Radiant is Holy above; raw Aether is undifferentiated.
    ///
    /// Only AUTHORED spells can name a face — primitive casting throws Aether and nothing else,
    /// which is the whole difference between the two tiers: not power, precision. An enemy that
    /// shrugs off raw Aether but folds to Shadow is a legible reason to go and learn the spell.
    ///
    /// APPENDED, never inserted — DamageType serializes by index on every ability asset in the
    /// game.
    /// </summary>
    Shadow
}