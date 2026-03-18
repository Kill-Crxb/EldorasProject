using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enhanced Damage Effect - Full damage calculation pipeline
/// 
/// Damage Calculation Flow:
/// 1. Determine Base Damage:
///    - useWeaponDamage? → weapon.damage + baseDamage
///    - else → baseDamage only
/// 2. Apply Base Multiplier:
///    - damage *= baseDamageMultiplier
/// 3. Add External Modifiers (Phase 2.2):
///    - damage += GetExternalFlatDamage()
/// 4. Apply Final Multiplier:
///    - damage *= finalDamageMultiplier
/// 
/// Blackboard Gating:
/// - Effect checks requiredCasterFacts and requiredTargetFacts
/// - If requirements not met, effect is skipped (returns early)
/// - Enables conditional damage (e.g., "Execute" deals bonus vs wounded targets)
/// 
/// Example Configurations:
/// 
/// Basic Attack:
///   useWeaponDamage = true
///   baseDamage = 0
///   baseDamageMultiplier = 1.0
///   → Deals 100% weapon damage
/// 
/// Heavy Strike:
///   useWeaponDamage = true
///   baseDamage = 5
///   baseDamageMultiplier = 1.5
///   → Deals (weapon + 5) * 1.5
/// 
/// Fireball:
///   useWeaponDamage = false
///   baseDamage = 30
///   → Deals 30 static damage
/// 
/// Execute (Primary):
///   useWeaponDamage = true
///   baseDamage = 0
///   → Normal weapon damage
/// 
/// Execute (Secondary):
///   useWeaponDamage = false
///   baseDamage = 50
///   requiredTargetFacts = ["IsWounded"]
///   → Bonus damage if target wounded
/// 
/// Phase 2.1: Weapon Stat Integration
/// Created: January 27, 2026
/// </summary>
[Serializable]
public class DamageEffect
{
    [Header("Base Damage")]
    [Tooltip("Use equipped weapon damage as base? (Unchecked = static ability damage)")]
    public bool useWeaponDamage = false;

    [Tooltip("Base damage value (fallback if no weapon, or bonus if useWeaponDamage = true)")]
    public float baseDamage = 10f;

    [Header("Damage Type")]
    public DamageType damageType = DamageType.Physical;

    [Header("Multipliers")]
    [Tooltip("Multiplier applied to base damage (before external modifiers)")]
    public float baseDamageMultiplier = 1.0f;

    [Tooltip("Multiplier applied to final damage (after all calculations)")]
    public float finalDamageMultiplier = 1.0f;

    [Header("Conditional Requirements (Optional)")]
    [Tooltip("Required blackboard facts on CASTER (empty = no requirements)")]
    public List<string> requiredCasterFacts = new List<string>();

    [Tooltip("Required blackboard facts on TARGET (empty = no requirements)")]
    public List<string> requiredTargetFacts = new List<string>();

    public event Action OnCompleted;

    [NonSerialized] private DamageSystem attackerDamageSystem;
    private bool isCompleted;

    /// <summary>
    /// Set the attacker's DamageSystem (required for damage calculation)
    /// </summary>
    public void SetDamageSystem(DamageSystem system)
    {
        attackerDamageSystem = system;
    }

    /// <summary>
    /// Apply damage to a target DamageSystem
    /// Includes full damage pipeline with weapon integration and blackboard gating
    /// </summary>
    public void Apply(DamageSystem target)
    {
        if (isCompleted)
            return;

        // Guard clauses
        if (target == null)
        {
            Debug.LogWarning("[DamageEffect] Target DamageSystem is null");
            Complete();
            return;
        }

        if (attackerDamageSystem == null)
        {
            Debug.LogError("[DamageEffect] Attacker DamageSystem not set before Apply()");
            Complete();
            return;
        }

        // Check blackboard requirements (skip effect if not met)
        if (!CheckBlackboardRequirements(attackerDamageSystem, target))
        {
            // Requirements not met - skip this effect silently
            Complete();
            return;
        }

        // Calculate final damage using pipeline
        float finalDamage = CalculateDamage(attackerDamageSystem);

        // Create attack data
        CombatAttackData attackData = new CombatAttackData
        {
            baseDamage = finalDamage,
            damageType = damageType,
            attackerTransform = attackerDamageSystem.transform,
            hitPoint = target.transform.position,
            hitNormal = Vector3.up
        };

        // Let DamageSystem calculate final packet (applies armor, resistances, etc.)
        CombatDamagePacket packet = attackerDamageSystem.CalculateDamage(attackData);
        target.TakeDamage(packet);

        Complete();
    }

    /// <summary>
    /// Full damage calculation pipeline
    /// </summary>
    private float CalculateDamage(DamageSystem attacker)
    {
        // Step 1: Determine base damage
        float damage = GetBaseDamage(attacker);

        // Step 2: Apply base multiplier
        damage *= baseDamageMultiplier;

        // Step 3: Add external flat modifiers (Phase 2.2 - stub for now)
        damage += GetExternalFlatDamage(attacker);

        // Step 4: Apply final multiplier
        damage *= finalDamageMultiplier;

        return damage;
    }

    /// <summary>
    /// Get base damage (weapon + ability base)
    /// </summary>
    private float GetBaseDamage(DamageSystem attacker)
    {
        float damage = baseDamage;

        if (useWeaponDamage)
        {
            float weaponDamage = GetWeaponDamage(attacker);
            damage += weaponDamage;
        }

        return damage;
    }

    /// <summary>
    /// Query weapon damage through ControllerBrain
    /// </summary>
    private float GetWeaponDamage(DamageSystem attacker)
    {
        // Get brain from DamageSystem
        var brain = GetBrain(attacker);
        if (brain == null) return 0f;

        // Try equipped weapon (players)
        var equipmentSystem = brain.GetModule<EquipmentSystem>();
        if (equipmentSystem != null)
        {
            // For now, return 0 until API is available
            // return equipmentSystem.GetEquippedWeaponDamage();
        }

        // Try natural weapon (NPCs/animals)
        // TODO Phase 2.1: Add natural weapon support to NPCModule
        // var npcModule = brain.GetModule<NPCModule>();
        // if (npcModule != null && npcModule.naturalWeapon != null)
        //     return npcModule.naturalWeapon.damage;

        return 0f;
    }

    /// <summary>
    /// Get external flat damage modifiers (Phase 2.2)
    /// From gear, buffs, passives, ability tags, etc.
    /// </summary>
    private float GetExternalFlatDamage(DamageSystem attacker)
    {
        // TODO Phase 2.2: Implement modifier aggregation
        // - Get gear bonuses
        // - Get buff/debuff modifiers
        // - Get tag-based bonuses
        return 0f;
    }

    /// <summary>
    /// Check if blackboard requirements are met
    /// Query through ControllerBrain (consistent with all systems)
    /// </summary>
    private bool CheckBlackboardRequirements(DamageSystem attacker, DamageSystem target)
    {
        // Check caster requirements
        if (requiredCasterFacts != null && requiredCasterFacts.Count > 0)
        {
            var casterBrain = GetBrain(attacker);
            if (casterBrain == null) return false;

            var casterBoard = casterBrain.Blackboard;
            if (casterBoard == null) return false;

            foreach (var fact in requiredCasterFacts)
            {
                // Hash string to int key for lookup
                int key = new BlackboardKey(fact).hash;
                if (!casterBoard.GetBool(key))
                    return false;  // Required caster fact not met
            }
        }

        // Check target requirements
        if (requiredTargetFacts != null && requiredTargetFacts.Count > 0)
        {
            var targetBrain = GetBrain(target);
            if (targetBrain == null) return false;

            var targetBoard = targetBrain.Blackboard;
            if (targetBoard == null) return false;

            foreach (var fact in requiredTargetFacts)
            {
                // Hash string to int key for lookup
                int key = new BlackboardKey(fact).hash;
                if (!targetBoard.GetBool(key))
                    return false;  // Required target fact not met
            }
        }

        return true;  // All requirements met
    }

    /// <summary>
    /// Get ControllerBrain from DamageSystem
    /// </summary>
    private ControllerBrain GetBrain(DamageSystem damageSystem)
    {
        return damageSystem?.Brain;
    }

    public void Cancel()
    {
        Complete();
    }

    private void Complete()
    {
        if (isCompleted)
            return;

        isCompleted = true;
        OnCompleted?.Invoke();
    }
}