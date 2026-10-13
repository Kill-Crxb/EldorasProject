using UnityEngine;

// A self-targeted ability lands here (AbilitySystem, on its effect cue). Melee contact lands in StrikeHandler and
// projectiles in ProjectilePayload; all three read the same effect lists, so delivery never changes what an
// ability does. Over-time damage and healing are statuses (StatusDefinition ticks), applied by ApplyStatuses.

namespace CrabSystem
{
    public partial class AbilityDefinition
    {
        public void ExecuteOnSelf(ControllerBrain caster)
        {
            if (caster == null) return;

            DamageSystem damage = caster.Damage;
            if (damage == null) return;

            ApplyDamage(damage, damage);
            ApplyHealing(caster.Health);
            ApplyKnockback(caster, caster.transform);

            // Statuses land after the hit resolves, so a status this ability applies can't alter the damage that
            // delivered it. Armour shred bites the next hit, not its own.
            ApplyStatuses(caster, caster);
        }

        void ApplyDamage(DamageSystem target, DamageSystem caster)
        {
            foreach (var effect in damageEffects)
            {
                if (effect == null) continue;
                effect.SetDamageSystem(caster);
                effect.Apply(target);
            }
        }

        void ApplyHealing(IHealthProvider target)
        {
            if (target == null) return;
            foreach (var effect in healEffects)
                if (effect != null) effect.Apply(target);
        }

        public void ExecuteMovement(MovementSystem movementSystem)
        {
            if (movementSystem == null) return;

            foreach (var effect in movementEffects)
            {
                effect.SetMovementSystem(movementSystem);
                effect.Apply(movementSystem);
            }
        }
    }
}
