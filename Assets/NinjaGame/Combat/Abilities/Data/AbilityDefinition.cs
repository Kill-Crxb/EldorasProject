using System.Collections.Generic;
using UnityEngine;

public enum AbilityTargetType
{
    Self,
    Enemy,
    Ally,
    Ground,
    Direction
}

[CreateAssetMenu(fileName = "New Ability", menuName = "RPG/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string abilityId;
    public string abilityName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;


    [Header("Costs & Cooldown")]
    public ResourceType resourceType = ResourceType.Mana;
    public float resourceCost = 10f;
    public float cooldown = 5f;
    public float castTime = 0.5f;

    [Header("Targeting")]
    public AbilityTargetType targetType = AbilityTargetType.Enemy;
    public float range = 5f;
    public bool requiresLineOfSight = true;

    [Header("Combat Effects")]
    public List<DamageEffect> damageEffects = new List<DamageEffect>();
    public List<DamageOverTimeEffect> damageOverTimeEffects = new List<DamageOverTimeEffect>();
    public List<HealEffect> healEffects = new List<HealEffect>();
    public List<HealOverTimeEffect> healOverTimeEffects = new List<HealOverTimeEffect>();
    public List<KnockbackEffect> knockbackEffects = new List<KnockbackEffect>();

    [Header("Movement Effects (if movement ability)")]
    public List<MovementEffect> movementEffects = new List<MovementEffect>();

    [Header("Animation & VFX")]
    public string animationTrigger = "Ability";
    public GameObject castEffectPrefab;
    public GameObject hitEffectPrefab;
    public GameObject projectilePrefab;

    public void Execute(IDamageable target, DamageModule damageModule, IHealthProvider healthProvider, Transform attacker)
    {
        EffectManagerModule effectManager = null;

        if (target is MonoBehaviour mb)
        {
            effectManager = mb.GetComponent<EffectManagerModule>();
        }

        foreach (var effect in damageEffects)
        {
            effect.SetDamageModule(damageModule);

            if (effectManager != null)
                effectManager.ApplyDamageEffect(effect, target);
            else
                effect.Apply(target);
        }

        foreach (var effect in damageOverTimeEffects)
        {
            effect.SetDamageModule(damageModule);

            if (effectManager != null)
                effectManager.ApplyDamageOverTimeEffect(effect, target);
            else
                effect.Apply(target);
        }

        foreach (var effect in healEffects)
        {
            effect.SetHealthProvider(healthProvider);

            if (effectManager != null)
                effectManager.ApplyHealEffect(effect, target);
            else
                effect.Apply(target);
        }

        foreach (var effect in healOverTimeEffects)
        {
            effect.SetHealthProvider(healthProvider);

            if (effectManager != null)
                effectManager.ApplyHealOverTimeEffect(effect, target);
            else
                effect.Apply(target);
        }

        foreach (var effect in knockbackEffects)
        {
            effect.SetAttacker(attacker);

            if (effectManager != null)
                effectManager.ApplyKnockbackEffect(effect, target);
            else
                effect.Apply(target);
        }
    }

    public void ExecuteMovement(IMovementProvider movement)
    {
        foreach (var effect in movementEffects)
        {
            effect.SetMovementProvider(movement);
            effect.Apply(movement);
        }
    }
}