using UnityEngine;

/// <summary>
/// AbilityDefinition - Legacy Execute Partial
/// Legacy execution methods - will be replaced by polymorphic effect system in Phase 4
/// </summary>
public partial class AbilityDefinition
{
    // ========================================
    // LEGACY EXECUTE METHODS (To Be Refactored)
    // ========================================

    /// <summary>
    /// Execute ability on a DamageSystem target (Legacy)
    /// NOTE: Will be replaced by polymorphic effect system in Phase 4
    /// </summary>
    public void Execute(
        DamageSystem targetDamage,
        IHealthProvider targetHealth,
        DamageSystem caster,
        Transform attackerTransform,
        EffectManagerModule effectManager = null,
        IResourceProvider resourceProvider = null)
    {
        // Guard clauses
        if (targetDamage == null) return;
        if (caster == null) return;
        if (attackerTransform == null) return;

        // Apply damage effects
        foreach (var effect in damageEffects)
        {
            if (effect == null) continue;

            effect.SetDamageSystem(caster);

            if (effectManager != null)
                effectManager.ApplyDamageEffect(effect, targetDamage);
            else
                effect.Apply(targetDamage);
        }

        // Apply damage over time
        foreach (var effect in damageOverTimeEffects)
        {
            if (effect == null) continue;

            if (effectManager != null)
                effectManager.ApplyDamageOverTimeEffect(effect, targetDamage);
            else
                effect.Apply(targetDamage);
        }

        // Apply healing
        if (targetHealth != null)
        {
            foreach (var effect in healEffects)
            {
                if (effect == null) continue;

                if (effectManager != null)
                    effectManager.ApplyHealEffect(effect, targetHealth);
                else
                    effect.Apply(targetHealth);
            }

            foreach (var effect in healOverTimeEffects)
            {
                if (effect == null) continue;

                if (effectManager != null)
                    effectManager.ApplyHealOverTimeEffect(effect, targetHealth);
                else
                    effect.Apply(targetHealth);
            }
        }

        // Apply knockback
        GameObject targetGO = null;
        if (targetDamage is MonoBehaviour mb)
            targetGO = mb.gameObject;

        if (targetGO != null)
        {
            foreach (var effect in knockbackEffects)
            {
                if (effect == null) continue;

                effect.SetAttacker(attackerTransform);

                if (effectManager != null)
                    effectManager.ApplyKnockbackEffect(effect, attackerTransform, targetGO);
                else
                    effect.Apply(targetGO);
            }
        }
    }

    /// <summary>
    /// Execute ability on self (Legacy)
    /// </summary>
    public void ExecuteOnSelf(ControllerBrain caster, EffectManagerModule effectManager = null, IResourceProvider resourceProvider = null)
    {
        if (caster == null) return;

        var damageSystem = caster.Damage;
        if (damageSystem == null) return;

        var healthProvider = caster.Health;
        if (healthProvider == null) return;

        Execute(damageSystem, healthProvider, damageSystem, caster.transform, effectManager, resourceProvider);
    }

    /// <summary>
    /// Execute ability on target (Legacy)
    /// </summary>
    public void ExecuteOn(ControllerBrain attacker, ControllerBrain target)
    {
        if (attacker == null) return;
        if (target == null) return;

        var attackerDamage = attacker.Damage;
        if (attackerDamage == null) return;

        var targetDamage = target.Damage;
        if (targetDamage == null) return;

        var targetHealth = target.Health;
        if (targetHealth == null) return;

        var effectManager = target.GetModule<EffectManagerModule>();
        var resourceProvider = attacker.Resources;

        Execute(targetDamage, targetHealth, attackerDamage, attacker.transform, effectManager, resourceProvider);
    }

    /// <summary>
    /// Execute movement effects (dash, teleport, etc.)
    /// </summary>
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
