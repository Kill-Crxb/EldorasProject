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
    [IdRef(IdKind.EquipmentSlot)] public string weaponSlotId = "mainwep";

    [Tooltip("TICK THIS FOR SPELLS. The dice must be supplied by the caller — there is no " +
             "equipped weapon to fall back on.\n\n" +
             "Without it, a spell whose dice failed to arrive silently rolls whatever is in the " +
             "main hand, or the fists when unarmed. That reads as a balance problem for weeks " +
             "before anyone finds the wiring bug. With it, the miss is an error and the effect " +
             "falls back to baseDamage.")]
    public bool requiresSuppliedDice = false;

    [Header("Damage Type")]
    public DamageType damageType = DamageType.Physical;

    [Header("Multipliers")]
    [Tooltip("Multiplier applied to base damage (before external modifiers)")]
    public float baseDamageMultiplier = 1.0f;

    [Tooltip("Multiplier applied to final damage (after all calculations)")]
    public float finalDamageMultiplier = 1.0f;

    [Header("Conditional Requirements (Optional)")]
    [Tooltip("Required blackboard facts on CASTER (empty = no requirements)")]
    [IdRef(IdKind.Fact)] public List<string> requiredCasterFacts = new List<string>();

    [Tooltip("Required blackboard facts on TARGET (empty = no requirements)")]
    [IdRef(IdKind.Fact)] public List<string> requiredTargetFacts = new List<string>();

    public event Action OnCompleted;

    [NonSerialized] private DamageSystem attackerDamageSystem;
    [NonSerialized] private bool isCompleted;

    public void SetDamageSystem(DamageSystem system)
    {
        attackerDamageSystem = system;
    }

    /// <summary>
    /// Applies this effect to a target.
    ///
    /// <paramref name="externalMultiplier"/> scales the final damage for THIS application only.
    /// It exists so a caller can scale damage without writing to the serialized fields on the
    /// shared AbilityDefinition asset — mutating those would corrupt the ability for every
    /// future use. ProjectileRuntime.damageMultiplier arrives through here; crits and buffs
    /// can use the same door.
    ///
    /// <paramref name="weaponOverride"/> supplies the dice for THIS application instead of
    /// looking up the equipped weapon. A thrown weapon is the weapon — a shuriken must roll
    /// its own 1d4, not the katana still sitting in the main-hand slot, and not the fist dice
    /// when the thrower happens to be unarmed.
    ///
    /// Both default to their no-op, so existing callers (StrikeHandler,
    /// AbilityDefinition.ExecuteOnSelf) are unaffected.
    ///
    /// <paramref name="source"/> and <paramref name="contactPoint"/> tell presentation where the
    /// hit came from and where it landed. Without a contact the packet's hit point falls back to
    /// the target's Overhead anchor. The damage number always spawns at Overhead.
    /// </summary>
    public float Apply(DamageSystem target, float externalMultiplier = 1f, DiceProfile weaponOverride = null,
        DamageSource source = DamageSource.Other, Vector3? contactPoint = null)
    {
        isCompleted = false;

        if (target == null)
        {
            Debug.LogWarning("[DamageEffect] BAIL — target is null");
            Complete();
            return 0f;
        }

        if (attackerDamageSystem == null)
        {
            Debug.LogError("[DamageEffect] BAIL — attackerDamageSystem is null (SetDamageSystem not called?)");
            Complete();
            return 0f;
        }

        if (!CheckBlackboardRequirements(attackerDamageSystem, target))
        {
            Complete();
            return 0f;
        }

        float finalDamage = CalculateDamage(attackerDamageSystem, weaponOverride) * externalMultiplier;

        Vector3 numberPoint = target.Brain?.GetModule<VFXSystem>()?.GetAnchorPosition(VFXAnchor.Overhead) ?? target.transform.root.position + Vector3.up * 1.5f;

        CombatAttackData attackData = new CombatAttackData
        {
            baseDamage = finalDamage,
            damageType = damageType,
            attackerTransform = attackerDamageSystem.transform,
            hitPoint = contactPoint ?? numberPoint,
            hitNormal = Vector3.up,
            source = source
        };

        CombatDamagePacket packet = attackerDamageSystem.CalculateDamage(attackData);

        float applied = target.TakeDamage(packet);

        DamageNumberManager.Spawn(applied, numberPoint);

        Complete();
        return applied;
    }

    // The hit before any rolls: weapon dice at their average plus the flat part. Hit-state length
    // scales against it (CF2). Supplied dice aren't known here, so those effects count their base only.
    public float AverageDamage(DamageSystem attacker)
    {
        DiceProfile weapon = requiresSuppliedDice ? null : ResolveWeapon(attacker, null);
        float damage = baseDamage;
        if (weapon != null) damage += weapon.damageDice.Average() + weapon.flatBonus;
        return damage * baseDamageMultiplier * finalDamageMultiplier;
    }

    // The hit before the defender's roll: base, weapon dice and flat bonus, and any bonus damage or dice.
    private float CalculateDamage(DamageSystem attacker, DiceProfile weaponOverride)
    {
        // Resolved ONCE and shared, so a bonus die is the same type as the hit it rides on
        // rather than a second lookup that can disagree with the first.
        DiceProfile weapon = ResolveWeapon(attacker, weaponOverride);

        float damage = baseDamage;
        if (weapon != null) damage += weapon.RollDamage();

        damage *= baseDamageMultiplier;
        damage += GetExternalFlatDamage(attacker, weapon);
        damage *= finalDamageMultiplier;
        return damage;
    }

    /// <summary>
    /// The DiceProfile this application actually rolls — a supplied override, the equipped
    /// weapon, or the fists. Null when this effect rolls no dice at all.
    /// </summary>
    private DiceProfile ResolveWeapon(DamageSystem attacker, DiceProfile weaponOverride)
    {
        if (!useWeaponDamage) return null;

        // A supplied weapon beats the equipment lookup — the thrown thing is the weapon, and
        // a spell's tier dice arrive the same way.
        if (weaponOverride != null) return weaponOverride;

        // A guard, not a fallback. See requiresSuppliedDice.
        if (requiresSuppliedDice)
        {
            Debug.LogError("[DamageEffect] Requires supplied dice, but none arrived — falling back " +
                           "to baseDamage rather than rolling the equipped weapon. Check that the " +
                           "firing path sets ProjectileLaunch.dice.");
            return null;
        }

        var brain = attacker?.Brain;
        if (brain == null) return null;

        // Fists beat the slot: a sheathed weapon is still equipped, so the stance —
        // not the equipment dictionary — decides which dice get rolled.
        var stance = brain.GetModule<CombatStanceModule>();
        if (stance != null && stance.IsUnarmed) return stance.UnarmedWeapon;

        var equipmentSystem = brain.GetModule<EquipmentSystem>();
        if (equipmentSystem == null) return null;

        // An empty slot, or a weapon carrying no DiceProfile, still swings a fist rather
        // than dealing nothing. Null when the entity has no stance module at all.
        var equippedItem = equipmentSystem.GetEquippedItem(weaponSlotId);
        var weaponData = equippedItem?.Definition?.weaponData;

        return weaponData != null ? weaponData : stance?.UnarmedWeapon;
    }

    /// <summary>
    /// Flat damage and extra dice other systems grant this attacker — gear, talents, and the
    /// status layer. All of it arrives as stats, so nothing here knows what a buff is, and
    /// filling this one method makes bonus damage work for melee, thrown and spells at once.
    ///
    /// cmb.bonus_dice rolls the weapon's dice expression directly rather than RollDamage(),
    /// so the weapon's own flat bonus is paid once per hit rather than once per die.
    /// Extra dice need a weapon to copy; flat bonus applies either way.
    /// </summary>
    private float GetExternalFlatDamage(DamageSystem attacker, DiceProfile weapon)
    {
        var stats = attacker?.Brain?.Stats;
        if (stats == null) return 0f;

        float bonus = stats.GetValue("cmb.bonus_damage");
        if (weapon == null) return bonus;

        int extraDice = Mathf.RoundToInt(stats.GetValue("cmb.bonus_dice"));
        for (int i = 0; i < extraDice; i++)
            bonus += weapon.damageDice.Roll();

        return bonus;
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
                if (!casterBoard.GetBool(key)) return false;
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
                if (!targetBoard.GetBool(key)) return false;
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