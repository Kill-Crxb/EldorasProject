using System.Collections.Generic;
using UnityEngine;
using NinjaGame.Animation;

// ========================================
// ENUMS
// ========================================

/// <summary>
/// Ability Type - Determines execution mode
/// </summary>
public enum AbilityType
{
    Offensive,  // Damage, knockback, status effects → Execute immediately
    Defensive,  // Block, parry, dodge → Activate defense state
    Utility     // Movement, buffs, utility → Various execution modes
}

/// <summary>
/// Ability Category - Determines default blackboard requirements
/// </summary>
public enum AbilityCategory
{
    Spell,      // Magical abilities (blocked by silence)
    Physical,   // Weapon attacks (blocked by disarm)
    Movement,   // Mobility skills (blocked by root)
    Defense,    // Blocking/parrying (blocked by disarm for weapon-based)
    Natural,    // Innate abilities (can't be disarmed - bear claws, etc.)
    Utility     // Generic abilities (minimal restrictions)
}

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

// ========================================
// RESOURCE COST STRUCTURE
// ========================================

/// <summary>
/// Resource cost entry for multi-resource abilities
/// </summary>
[System.Serializable]
public class ResourceCostEntry
{
    [Tooltip("Resource consumed by this ability")]
    public ResourceDefinition resource;

    [Tooltip("Amount consumed on activation")]
    public float cost = 10f;

    [Tooltip("Amount drained per second while active (defensive abilities)")]
    public float drain = 0f;

    [Tooltip("Amount refunded on successful use (e.g., parry refund)")]
    public float refund = 0f;
}

// ========================================
// ABILITY DEFINITION
// ========================================

/// <summary>
/// Unified Ability Definition - Works for all ability types
/// 
/// Architecture:
/// - Offensive: Execute effects immediately
/// - Defensive: Enter defense state, process incoming damage
/// - Utility: Various execution modes
/// 
/// Integration:
/// - State Machine: Transitions entity state
/// - Blackboard: Validates semantic requirements
/// - Animation Events: Timing control via Effect1/2/3, AnimUnlocked, ComboWindow
/// - Resource System: Multi-resource costs
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "NinjaGame/Ability Definition")]
public class AbilityDefinition : ScriptableObject
{
    // ========================================
    // BASIC INFO
    // ========================================

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

    // ========================================
    // CLASSIFICATION
    // ========================================

    [Header("Classification")]
    [Tooltip("Type determines execution mode (Offensive/Defensive/Utility)")]
    public AbilityType abilityType = AbilityType.Offensive;

    [Tooltip("Category determines default blackboard requirements")]
    public AbilityCategory abilityCategory = AbilityCategory.Physical;

    // ========================================
    // STATE INTEGRATION
    // ========================================

    [Header("State Integration")]
    [Tooltip("Upper body state to transition to when using this ability")]
    public UpperBodyState setsUpperBodyState = UpperBodyState.Idle;

    // ========================================
    // ANIMATION CONTROL
    // ========================================

    [Header("Animation Control")]
    [Tooltip("Which animation event triggers ability effects (Effect1/2/3, or set to PlayEffect for immediate)")]
    public AnimationEventType effectTrigger = AnimationEventType.Effect1;

    [Tooltip("Wait for AnimUnlocked event before completing ability?")]
    public bool waitForAnimUnlock = true;

    [Tooltip("Safety timeout - force complete if AnimUnlocked never fires (0 = no timeout)")]
    public float maxDuration = 2.0f;

    // ========================================
    // COSTS & COOLDOWN
    // ========================================

    [Header("Costs & Cooldown")]
    [Tooltip("Resource costs (can have multiple - mana + stamina, etc.)")]
    public ResourceCostEntry[] resourceCosts = new ResourceCostEntry[0];

    [Tooltip("Cooldown duration in seconds")]
    public float cooldown = 5f;

    [Tooltip("Cast time before execution (0 = instant)")]
    public float castTime = 0f;

    // ========================================
    // BLACKBOARD REQUIREMENTS
    // ========================================

    [Header("Blackboard Requirements")]
    [Tooltip("Additional required facts (ANY logic - ability usable if ANY fact is true)")]
    public List<string> requiredFactsAny = new List<string>();

    [Tooltip("Additional required facts (ALL logic - ability requires ALL facts true)")]
    public List<string> requiredFactsAll = new List<string>();

    [Tooltip("Additional forbidden facts (blocked if ALL forbidden facts are true)")]
    public List<string> forbiddenFactsAll = new List<string>();

    [Tooltip("Override category defaults? (Use custom requirements instead)")]
    public bool overrideDefaults = false;

    [Tooltip("Custom required facts (when overriding defaults)")]
    public List<string> customRequiredFacts = new List<string>();

    [Tooltip("Custom forbidden facts (when overriding defaults)")]
    public List<string> customForbiddenFacts = new List<string>();

    // ========================================
    // TARGETING
    // ========================================

    [Header("Targeting")]
    [Tooltip("Who/what can be targeted")]
    public AbilityTargetType targetType = AbilityTargetType.Enemy;

    [Tooltip("Maximum targeting range")]
    public float range = 5f;

    [Tooltip("Requires line of sight to target?")]
    public bool requiresLineOfSight = true;

    // ========================================
    // COMBAT EFFECTS (Legacy - Transitioning to Polymorphic)
    // ========================================

    [Header("Combat Effects")]
    public List<DamageEffect> damageEffects = new List<DamageEffect>();
    public List<DamageOverTimeEffect> damageOverTimeEffects = new List<DamageOverTimeEffect>();
    public List<HealEffect> healEffects = new List<HealEffect>();
    public List<HealOverTimeEffect> healOverTimeEffects = new List<HealOverTimeEffect>();
    public List<KnockbackEffect> knockbackEffects = new List<KnockbackEffect>();

    [Header("Movement Effects")]
    public List<MovementEffect> movementEffects = new List<MovementEffect>();

    // TODO Phase 4: Migrate to polymorphic effect list
    // [SerializeReference]
    // public List<AbilityEffect> effects = new List<AbilityEffect>();

    // ========================================
    // ANIMATION & VFX
    // ========================================

    [Header("Animation & VFX")]
    [Tooltip("Animation trigger parameter name")]
    public string animationTrigger = "Ability";

    [Tooltip("VFX spawned on cast start")]
    public GameObject castEffectPrefab;

    [Tooltip("VFX spawned on hit/impact")]
    public GameObject hitEffectPrefab;

    [Tooltip("Projectile prefab (if projectile ability)")]
    public GameObject projectilePrefab;

    // ========================================
    // DEFENSIVE MECHANICS (AbilityType.Defensive)
    // ========================================

    [Header("Defense Mechanics (Defensive Abilities Only)")]
    [Tooltip("Damage reduction when blocking (0.7 = 70% reduction)")]
    [Range(0f, 1f)]
    public float blockDamageReduction = 0.5f;

    [Tooltip("Block arc in degrees (180 = half circle)")]
    public float blockAngle = 120f;

    [Tooltip("Perfect parry window duration (seconds from defense start)")]
    public float parryWindowDuration = 0.2f;

    [Tooltip("Damage reduction during parry window (1.0 = 100% blocked)")]
    [Range(0f, 1f)]
    public float parryDamageReduction = 1.0f;

    [Tooltip("Successful parry opens counter-attack window?")]
    public bool parryEnablesCounter = true;

    [Tooltip("Counter-attack window duration after successful parry")]
    public float counterWindowDuration = 1.0f;

    [Tooltip("Startup time before defense becomes active")]
    public float blockStartupTime = 0.1f;

    // ========================================
    // CHAINING (Combo System)
    // ========================================

    [Header("Chaining")]
    [Tooltip("Next ability in combo chain (optional)")]
    public AbilityDefinition nextInChain;

    [Tooltip("Time window after ComboWindowStart event to chain to next ability")]
    public float chainWindow = 0.5f;

    // ========================================
    // METHODS
    // ========================================

    #region Category Default Requirements

    /// <summary>
    /// Get blackboard requirements for this ability (includes category defaults)
    /// Returns: (requiredFactsAny, requiredFactsAll, forbiddenFactsAll)
    /// </summary>
    public (List<string> requiredAny, List<string> requiredAll, List<string> forbiddenAll) GetBlackboardRequirements()
    {
        // If overriding defaults, use custom requirements only
        if (overrideDefaults)
        {
            return (
                new List<string>(customRequiredFacts),
                new List<string>(),  // Custom doesn't distinguish ANY/ALL yet
                new List<string>(customForbiddenFacts)
            );
        }

        // Start with category defaults
        var requiredAny = new List<string>();
        var requiredAll = new List<string>();
        var forbiddenAll = new List<string>(GetCategoryDefaultForbiddenFacts());

        // Add ability-specific requirements
        requiredAny.AddRange(requiredFactsAny);
        requiredAll.AddRange(requiredFactsAll);
        forbiddenAll.AddRange(forbiddenFactsAll);

        return (requiredAny, requiredAll, forbiddenAll);
    }

    /// <summary>
    /// Get default forbidden facts for this ability's category
    /// </summary>
    private List<string> GetCategoryDefaultForbiddenFacts()
    {
        switch (abilityCategory)
        {
            case AbilityCategory.Spell:
                return new List<string> { "IsSilenced", "IsStunned" };

            case AbilityCategory.Physical:
                return new List<string> { "IsDisarmed", "IsStunned" };

            case AbilityCategory.Movement:
                return new List<string> { "IsRooted", "IsStunned" };

            case AbilityCategory.Defense:
                return new List<string> { "IsDisarmed", "IsStunned" };

            case AbilityCategory.Natural:
                return new List<string> { "IsStunned" };  // Can't be disarmed

            case AbilityCategory.Utility:
                return new List<string>();  // Minimal restrictions

            default:
                return new List<string>();
        }
    }

    #endregion

    #region Execute Methods (Legacy - To Be Refactored)

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

    #endregion

    #region Movement Effects

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

    #endregion

    #region Validation

    /// <summary>
    /// Validate ability configuration
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        if (string.IsNullOrEmpty(abilityId))
        {
            errorMessage = "Ability ID required";
            return false;
        }

        if (string.IsNullOrEmpty(abilityName))
        {
            errorMessage = "Ability Name required";
            return false;
        }

        if (resourceCosts != null)
        {
            foreach (var cost in resourceCosts)
            {
                if (cost.cost < 0)
                {
                    errorMessage = "Resource cost cannot be negative";
                    return false;
                }
            }
        }

        if (cooldown < 0)
        {
            errorMessage = "Cooldown cannot be negative";
            return false;
        }

        if (castTime < 0)
        {
            errorMessage = "Cast time cannot be negative";
            return false;
        }

        if (maxDuration < 0)
        {
            errorMessage = "Max duration cannot be negative";
            return false;
        }

        errorMessage = "";
        return true;
    }

    [ContextMenu("Validate Configuration")]
    private void ValidateConfiguration()
    {
        if (Validate(out string error))
            Debug.Log($"[AbilityDefinition] {abilityName} is valid");
        else
            Debug.LogError($"[AbilityDefinition] {abilityName} validation failed: {error}");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get total resource cost for a specific resource
    /// </summary>
    public float GetResourceCost(ResourceDefinition resource)
    {
        if (resourceCosts == null || resource == null) return 0f;

        foreach (var entry in resourceCosts)
        {
            if (entry.resource == resource)
                return entry.cost;
        }

        return 0f;
    }

    /// <summary>
    /// Check if this ability uses a specific resource
    /// </summary>
    public bool UsesResource(ResourceDefinition resource)
    {
        if (resourceCosts == null || resource == null) return false;

        foreach (var entry in resourceCosts)
        {
            if (entry.resource == resource)
                return true;
        }

        return false;
    }

    #endregion
}