using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enhanced Damage Effect - Full damage calculation pipeline
/// </summary>
[Serializable]
public class DamageEffect
{
    [Header("Base Damage")]
    [Tooltip("Use equipped weapon dice roll as base? (Unchecked = static ability damage)")]
    public bool useWeaponDamage = false;

    [Tooltip("Added to weapon dice roll when useWeaponDamage is true. Use negative for off-hand penalties.")]
    public float baseDamage = 10f;

    [Tooltip("Slot ID to read weapon from (default: mainwep). Override for off-hand abilities.")]
    public string weaponSlotId = "mainwep";

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
    [NonSerialized] private bool isCompleted;

    public void SetDamageSystem(DamageSystem system)
    {
        attackerDamageSystem = system;
    }

    public void Apply(DamageSystem target)
    {
        isCompleted = false;

        if (target == null)
        {
            Debug.LogWarning("[DamageEffect] BAIL — target is null");
            Complete();
            return;
        }

        if (attackerDamageSystem == null)
        {
            Debug.LogError("[DamageEffect] BAIL — attackerDamageSystem is null (SetDamageSystem not called?)");
            Complete();
            return;
        }

        if (!CheckBlackboardRequirements(attackerDamageSystem, target))
        {
            Debug.LogWarning("[DamageEffect] BAIL — blackboard requirements not met");
            Complete();
            return;
        }

        float finalDamage = CalculateDamage(attackerDamageSystem);

        CombatAttackData attackData = new CombatAttackData
        {
            baseDamage = finalDamage,
            damageType = damageType,
            attackerTransform = attackerDamageSystem.transform,
            hitPoint = target.Brain?.GetModule<VFXSystem>()?.GetAnchorPosition(VFXAnchor.Overhead) ?? target.transform.root.position + Vector3.up * 1.5f,
            hitNormal = Vector3.up
        };

        CombatDamagePacket packet = attackerDamageSystem.CalculateDamage(attackData);

        Debug.Log($"[DamageEffect] Dealing {packet.finalDamage:F1} {damageType} to {target.name}");

        target.TakeDamage(packet);

        DamageNumberManager.Spawn(packet.finalDamage, attackData.hitPoint);

        Complete();
    }

    private float CalculateDamage(DamageSystem attacker)
    {
        float damage = GetBaseDamage(attacker);
        damage *= baseDamageMultiplier;
        damage += GetExternalFlatDamage(attacker);
        damage *= finalDamageMultiplier;
        return damage;
    }

    private float GetBaseDamage(DamageSystem attacker)
    {
        if (!useWeaponDamage)
            return baseDamage;

        return GetWeaponDamage(attacker) + baseDamage;
    }

    private float GetWeaponDamage(DamageSystem attacker)
    {
        var brain = attacker?.Brain;
        if (brain == null) return 0f;

        var equipmentSystem = brain.GetModule<EquipmentSystem>();
        if (equipmentSystem == null) return 0f;

        var equippedItem = equipmentSystem.GetEquippedItem(weaponSlotId);
        if (equippedItem == null) return 0f;

        var weaponData = equippedItem.Definition?.weaponData;
        if (weaponData == null) return 0f;

        return weaponData.RollDamage();
    }

    private float GetExternalFlatDamage(DamageSystem attacker)
    {
        return 0f;
    }

    private bool CheckBlackboardRequirements(DamageSystem attacker, DamageSystem target)
    {
        if (requiredCasterFacts != null && requiredCasterFacts.Count > 0)
        {
            var casterBrain = attacker?.Brain;
            if (casterBrain == null)
            {
                Debug.LogWarning("[DamageEffect] CheckBlackboard — casterBrain is null");
                return false;
            }

            var casterBoard = casterBrain.Blackboard;
            if (casterBoard == null)
            {
                Debug.LogWarning("[DamageEffect] CheckBlackboard — caster has no Blackboard");
                return false;
            }

            foreach (var fact in requiredCasterFacts)
            {
                int key = new BlackboardKey(fact).hash;
                if (!casterBoard.GetBool(key))
                {
                    Debug.LogWarning($"[DamageEffect] CheckBlackboard — caster missing fact '{fact}'");
                    return false;
                }
            }
        }

        if (requiredTargetFacts != null && requiredTargetFacts.Count > 0)
        {
            var targetBrain = target?.Brain;
            if (targetBrain == null)
            {
                Debug.LogWarning("[DamageEffect] CheckBlackboard — targetBrain is null");
                return false;
            }

            var targetBoard = targetBrain.Blackboard;
            if (targetBoard == null)
            {
                Debug.LogWarning("[DamageEffect] CheckBlackboard — target has no Blackboard");
                return false;
            }

            foreach (var fact in requiredTargetFacts)
            {
                int key = new BlackboardKey(fact).hash;
                if (!targetBoard.GetBool(key))
                {
                    Debug.LogWarning($"[DamageEffect] CheckBlackboard — target missing fact '{fact}'");
                    return false;
                }
            }
        }

        return true;
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