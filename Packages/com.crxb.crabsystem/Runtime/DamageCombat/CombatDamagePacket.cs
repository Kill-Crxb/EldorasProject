using UnityEngine;

// Complete damage information sent from DamageOut to DamageIn.
// Contains all data needed for damage application and feedback.
//
// Design:
// - Includes both calculated damage AND contextual information
// - Supports damage types (physical, magical, true)
// - Contains hit location data for feedback systems
// - Immutable after creation (all fields are readonly)

namespace CrabSystem
{
    [System.Serializable]
    public class CombatDamagePacket
    {
        [Header("Damage Values")]
        public readonly float baseDamage;
        public readonly float finalDamage;

        [Header("Damage Type")]
        public readonly string damageType;

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

        // Constructor - creates an immutable damage packet
        public CombatDamagePacket(
            float baseDamage,
            float finalDamage,
            string damageType,
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

        // Create a simple damage packet for testing
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
                damageType: DamageTypeIds.Physical,
                attacker: attacker,
                attackerId: attacker?.name ?? "Unknown",
                hitPoint: hitPoint,
                hitNormal: Vector3.up,
                attackDirection: direction
            );
        }
    }
}
