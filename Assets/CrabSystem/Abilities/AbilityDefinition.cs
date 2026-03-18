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
/// Split into partial classes by domain for clarity and maintainability
/// 
/// Partials:
/// - AbilityDefinition.cs (this file): Core identity, targeting, effects
/// - AbilityDefinition.Animation.cs: Animation triggers and VFX
/// - AbilityDefinition.Costs.cs: Resource costs and cooldowns
/// - AbilityDefinition.Blackboard.cs: Semantic requirements (optimized)
/// - AbilityDefinition.Defense.cs: Block/parry policy helpers
/// - AbilityDefinition.Chaining.cs: Combo policy helpers
/// - AbilityDefinition.LegacyExecute.cs: Legacy execution methods
/// - AbilityDefinition.Validation.cs: Editor validation
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "NinjaGame/Ability Definition")]
public partial class AbilityDefinition : ScriptableObject
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
}