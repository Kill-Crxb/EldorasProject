using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ability Target Type - Who/what can be targeted
/// </summary>
public enum AbilityTargetType
{
    Self,
    Enemy,
    Ally,
    Ground,
    Direction
}

/// <summary>
/// Ability Definition - fully dynamic, future-proof version
/// Handles resource costs via ResourceDefinition dynamically (no hardcoded strings)
/// Integrates with modern DamageSystem, EffectManagerModule, and movement effects
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "RPG/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string abilityId;
    public string abilityName;
    [TextArea(3, 5)] public string description;
    public Sprite icon;

    [Header("Costs & Cooldown")]
    [Tooltip("Resource used by this ability (dynamic)")]
    public ResourceDefinition resourceDefinition;
    [Tooltip("Amount of resource consumed per cast")]
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

    [Header("Movement Effects")]
    public List<MovementEffect> movementEffects = new List<MovementEffect>();

    [Header("Animation & VFX")]
    public string animationTrigger = "Ability";
    public GameObject castEffectPrefab;
    public GameObject hitEffectPrefab;
    public GameObject projectilePrefab;

    #region Execute Methods (Modern - DamageSystem)

    /// <summary>
    /// Execute ability on a DamageSystem target
    /// </summary>
    public void Execute(DamageSystem target, DamageSystem caster, Transform attackerTransform, EffectManagerModule effectManager = null, IResourceProvider resourceProvider = null)
    {
        if (target == null) return;

        // Check resource dynamically
        if (resourceProvider != null && resourceDefinition != null)
        {
            if (!resourceProvider.HasResource(resourceDefinition, resourceCost)) return;
            resourceProvider.ConsumeResource(resourceDefinition, resourceCost);
        }

        // Apply damage effects
        foreach (var effect in damageEffects)
        {
            effect.SetDamageSystem(caster);
            if (effectManager != null)
                effectManager.ApplyDamageEffect(effect, target);
            else
                effect.Apply(target);
        }

        // Apply DoT effects
        foreach (var effect in damageOverTimeEffects)
        {
            effect.SetDamageSystem(caster);
            if (effectManager != null)
                effectManager.ApplyDamageOverTimeEffect(effect, target);
            else
                effect.Apply(target);
        }

        // Apply healing effects
        foreach (var effect in healEffects)
        {
            if (effectManager != null)
                effectManager.ApplyHealEffect(effect, target);
            else
                effect.Apply(target);
        }

        foreach (var effect in healOverTimeEffects)
        {
            if (effectManager != null)
                effectManager.ApplyHealOverTimeEffect(effect, target);
            else
                effect.Apply(target);
        }

        // Knockback
        foreach (var effect in knockbackEffects)
        {
            effect.SetAttacker(attackerTransform);
            if (effectManager != null)
                effectManager.ApplyKnockbackEffect(effect, attackerTransform, target.gameObject);
            else
                effect.Apply(target.gameObject);
        }
    }

    /// <summary>
    /// Execute ability on self
    /// </summary>
    public void ExecuteOnSelf(DamageSystem caster, EffectManagerModule effectManager = null, IResourceProvider resourceProvider = null)
    {
        Execute(caster, caster, caster.transform, effectManager, resourceProvider);
    }

    #endregion

    #region Execute Methods (Legacy - IDamageable)

    [System.Obsolete("Use DamageSystem overload for modern entities")]
    public void Execute(IDamageable target, DamageSystem caster, IHealthProvider healthProvider, Transform attacker)
    {
        if (target == null) return;

        EffectManagerModule effectManager = null;
        if (target is MonoBehaviour mb) effectManager = mb.GetComponent<EffectManagerModule>();

        // Apply damage
        foreach (var effect in damageEffects)
        {
            effect.SetDamageSystem(caster);
            if (effectManager != null) effectManager.ApplyDamageEffect(effect, target);
            else effect.Apply(target);
        }

        // Apply healing
        foreach (var effect in healEffects) { effect.SetHealthProvider(healthProvider); effect.Apply(target); }
        foreach (var effect in healOverTimeEffects) { effect.SetHealthProvider(healthProvider); effect.Apply(target); }

        // Knockback
        foreach (var effect in knockbackEffects) { effect.SetAttacker(attacker); effect.Apply((target as MonoBehaviour)?.gameObject); }
    }

    #endregion

    #region Movement Effects

    public void ExecuteMovement(MovementSystem movementSystem)
    {
        if (movementSystem == null) return;

        foreach (var effect in movementEffects)
        {
            effect.SetMovementSystem(movementSystem);
            effect.Apply(movementSystem);
        }
    }

    #endregion

    #region Validation

    public bool Validate(out string errorMessage)
    {
        if (string.IsNullOrEmpty(abilityId)) { errorMessage = "Ability ID required"; return false; }
        if (string.IsNullOrEmpty(abilityName)) { errorMessage = "Ability Name required"; return false; }
        if (resourceCost < 0) { errorMessage = "Resource cost cannot be negative"; return false; }
        if (cooldown < 0) { errorMessage = "Cooldown cannot be negative"; return false; }
        if (castTime < 0) { errorMessage = "CastTime cannot be negative"; return false; }

        errorMessage = "";
        return true;
    }

    [ContextMenu("Validate Configuration")]
    private void ValidateConfiguration()
    {
        if (Validate(out string error)) Debug.Log($"[AbilityDefinition] {abilityName} is valid");
        else Debug.LogError($"[AbilityDefinition] {abilityName} validation failed: {error}");
    }

    #endregion
}
