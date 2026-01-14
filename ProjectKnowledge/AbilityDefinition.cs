using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ability Target Type - Who/what can be targeted
/// </summary>
public enum AbilityTargetType
{
    Self,       // Targets the caster
    Enemy,      // Targets hostile entities
    Ally,       // Targets friendly entities
    Ground,     // Targets a location
    Direction   // Targets a direction (for projectiles/beams)
}

/// <summary>
/// Ability Definition - ScriptableObject defining an ability
/// 
/// Usage:
/// - Create via Assets > Create > RPG > Ability Definition
/// - Configure costs, cooldowns, effects
/// - Reference in AbilityLoadoutModule
/// 
/// Integration:
/// - Works with DamageSystem (modern Brain entities)
/// - Works with EffectManagerModule for effect tracking
/// - Fires animation triggers
/// - Spawns VFX prefabs
/// 
/// Phase 1.7b: Updated to use DamageSystem
/// Updated: January 2026
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "RPG/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Unique identifier for this ability")]
    public string abilityId;

    [Tooltip("Display name shown in UI")]
    public string abilityName;

    [TextArea(3, 5)]
    [Tooltip("Description shown in tooltips")]
    public string description;

    [Tooltip("Icon shown in UI")]
    public Sprite icon;

    [Header("Costs & Cooldown")]
    [Tooltip("Which resource this ability consumes")]
    public ResourceType resourceType = ResourceType.Mana;

    [Tooltip("Amount of resource consumed per cast")]
    public float resourceCost = 10f;

    [Tooltip("Time before ability can be used again (seconds)")]
    public float cooldown = 5f;

    [Tooltip("Time required to cast (0 = instant)")]
    public float castTime = 0.5f;

    [Header("Targeting")]
    [Tooltip("What type of target this ability requires")]
    public AbilityTargetType targetType = AbilityTargetType.Enemy;

    [Tooltip("Maximum range in meters")]
    public float range = 5f;

    [Tooltip("Requires clear line of sight to target")]
    public bool requiresLineOfSight = true;

    [Header("Combat Effects")]
    [Tooltip("Instant damage effects")]
    public List<DamageEffect> damageEffects = new List<DamageEffect>();

    [Tooltip("Damage over time effects")]
    public List<DamageOverTimeEffect> damageOverTimeEffects = new List<DamageOverTimeEffect>();

    [Tooltip("Instant heal effects")]
    public List<HealEffect> healEffects = new List<HealEffect>();

    [Tooltip("Heal over time effects")]
    public List<HealOverTimeEffect> healOverTimeEffects = new List<HealOverTimeEffect>();

    [Tooltip("Knockback/displacement effects")]
    public List<KnockbackEffect> knockbackEffects = new List<KnockbackEffect>();

    [Header("Movement Effects (if movement ability)")]
    [Tooltip("Movement effects (dash, teleport, etc.)")]
    public List<MovementEffect> movementEffects = new List<MovementEffect>();

    [Header("Animation & VFX")]
    [Tooltip("Animation trigger name to play")]
    public string animationTrigger = "Ability";

    [Tooltip("VFX spawned when casting begins")]
    public GameObject castEffectPrefab;

    [Tooltip("VFX spawned on target when hit")]
    public GameObject hitEffectPrefab;

    [Tooltip("Projectile prefab (if projectile ability)")]
    public GameObject projectilePrefab;

    #region Execute Methods (Modern - DamageSystem)

    /// <summary>
    /// Execute ability effects on a DamageSystem target (modern Brain entities)
    /// </summary>
    public void Execute(
        DamageSystem target,
        DamageSystem caster,
        Transform attackerTransform,
        EffectManagerModule effectManager = null)
    {
        if (target == null)
        {
            Debug.LogWarning($"[AbilityDefinition] Cannot execute {abilityName} - target is null");
            return;
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

        // Apply heal effects (target heals self or ally)
        foreach (var effect in healEffects)
        {
            // DamageSystem implements IHealthProvider, so this works
            if (effectManager != null)
                effectManager.ApplyHealEffect(effect, target);
            else
                effect.Apply(target); // Uses IHealthProvider overload
        }

        // Apply HoT effects
        foreach (var effect in healOverTimeEffects)
        {
            // DamageSystem implements IHealthProvider, so this works
            if (effectManager != null)
                effectManager.ApplyHealOverTimeEffect(effect, target);
            else
                effect.Apply(target); // Uses IHealthProvider overload
        }

        // Apply knockback effects
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
    /// Execute ability on caster (self-targeting abilities)
    /// </summary>
    public void ExecuteOnSelf(DamageSystem caster, EffectManagerModule effectManager = null)
    {
        Execute(caster, caster, caster.transform, effectManager);
    }

    #endregion

    #region Execute Methods (Legacy - IDamageable)

    /// <summary>
    /// Execute ability on IDamageable target (legacy compatibility for simple destructibles)
    /// </summary>
    [System.Obsolete("Use Execute(DamageSystem, DamageSystem, Transform, EffectManagerModule) for Brain entities")]
    public void Execute(
        IDamageable target,
        DamageSystem damageSystem,
        IHealthProvider healthProvider,
        Transform attacker)
    {
        if (target == null)
        {
            Debug.LogWarning($"[AbilityDefinition] Cannot execute {abilityName} - target is null");
            return;
        }

        EffectManagerModule effectManager = null;

        if (target is MonoBehaviour mb)
        {
            effectManager = mb.GetComponent<EffectManagerModule>();
        }

        // Damage effects
        foreach (var effect in damageEffects)
        {
            effect.SetDamageSystem(damageSystem);

            if (effectManager != null)
            {
#pragma warning disable CS0618 // Using obsolete method intentionally
                effectManager.ApplyDamageEffect(effect, target);
#pragma warning restore CS0618
            }
            else
            {
#pragma warning disable CS0618 // Using obsolete method intentionally
                effect.Apply(target);
#pragma warning restore CS0618
            }
        }

        // DoT effects - not supported on IDamageable
        if (damageOverTimeEffects.Count > 0)
        {
            Debug.LogWarning($"[AbilityDefinition] {abilityName} has DoT effects but target is IDamageable - DoTs require DamageSystem");
        }

        // Heal effects
        foreach (var effect in healEffects)
        {
            effect.SetHealthProvider(healthProvider);

            if (effectManager != null)
            {
#pragma warning disable CS0618 // Using obsolete method intentionally
                effectManager.ApplyHealEffect(effect, target);
#pragma warning restore CS0618
            }
            else
            {
                effect.Apply(target);
            }
        }

        // HoT effects
        foreach (var effect in healOverTimeEffects)
        {
            effect.SetHealthProvider(healthProvider);

            if (effectManager != null)
            {
#pragma warning disable CS0618 // Using obsolete method intentionally
                effectManager.ApplyHealOverTimeEffect(effect, target);
#pragma warning restore CS0618
            }
            else
            {
                effect.Apply(target);
            }
        }

        // Knockback effects
        foreach (var effect in knockbackEffects)
        {
            effect.SetAttacker(attacker);

            if (effectManager != null)
            {
#pragma warning disable CS0618 // Using obsolete method intentionally
                effectManager.ApplyKnockbackEffect(effect, target);
#pragma warning restore CS0618
            }
            else if (target is MonoBehaviour mb2)
            {
                effect.Apply(mb2.gameObject);
            }
        }
    }

    #endregion

    #region Movement Effects

    /// <summary>
    /// Execute movement effects on MovementSystem
    /// </summary>
    public void ExecuteMovement(MovementSystem movementSystem)
    {
        if (movementSystem == null)
        {
            Debug.LogWarning($"[AbilityDefinition] Cannot execute movement effects for {abilityName} - MovementSystem is null");
            return;
        }

        foreach (var effect in movementEffects)
        {
            effect.SetMovementSystem(movementSystem);
            effect.Apply(movementSystem);
        }
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validate ability configuration (Editor helper)
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        if (string.IsNullOrEmpty(abilityId))
        {
            errorMessage = "Ability ID is required";
            return false;
        }

        if (string.IsNullOrEmpty(abilityName))
        {
            errorMessage = "Ability Name is required";
            return false;
        }

        if (cooldown < 0)
        {
            errorMessage = "Cooldown cannot be negative";
            return false;
        }

        if (castTime < 0)
        {
            errorMessage = "Cast Time cannot be negative";
            return false;
        }

        if (resourceCost < 0)
        {
            errorMessage = "Resource Cost cannot be negative";
            return false;
        }

        errorMessage = "";
        return true;
    }

    #endregion

    #region Context Menu Helpers

    [ContextMenu("Validate Configuration")]
    private void ValidateConfiguration()
    {
        if (Validate(out string error))
        {
            Debug.Log($"[AbilityDefinition] {abilityName} configuration is valid");
        }
        else
        {
            Debug.LogError($"[AbilityDefinition] {abilityName} validation failed: {error}");
        }
    }

    [ContextMenu("Print Summary")]
    private void PrintSummary()
    {
        Debug.Log($"=== {abilityName} ===");
        Debug.Log($"ID: {abilityId}");
        Debug.Log($"Cost: {resourceCost} {resourceType}");
        Debug.Log($"Cooldown: {cooldown}s");
        Debug.Log($"Cast Time: {castTime}s");
        Debug.Log($"Range: {range}m");
        Debug.Log($"Target: {targetType}");
        Debug.Log($"Effects: {damageEffects.Count} damage, {damageOverTimeEffects.Count} DoT, {healEffects.Count} heal, {healOverTimeEffects.Count} HoT");
    }

    #endregion
}