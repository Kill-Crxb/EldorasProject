using UnityEngine;
using System;

public class DamageModule : MonoBehaviour, IBrainModule
{
    [Header("Debug")]
    [SerializeField] private bool debugDamage = false;

    private ControllerBrain brain;
    private ICombatStatsProvider combatStats;
    private IHealthProvider health;
    private IDefenseProvider defense;

    public bool IsEnabled { get; set; } = true;

    public event Action<CombatDamagePacket> OnDamageDealt;
    public event Action<CombatDamagePacket> OnDamageTaken;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        combatStats = brain.GetModuleImplementing<ICombatStatsProvider>();
        health = brain.GetModuleImplementing<IHealthProvider>();
        defense = brain.GetModuleImplementing<IDefenseProvider>();

        if (combatStats == null)
            Debug.LogWarning($"[DamageModule] No ICombatStatsProvider found on {gameObject.name}");
        if (health == null)
            Debug.LogWarning($"[DamageModule] No IHealthProvider found on {gameObject.name}");
    }

    public void UpdateModule() { }

    public CombatDamagePacket CalculateDamage(CombatAttackData attackData)
    {
        // PHASE 1.1: Get weapon damage from equipped weapon (if available)
        float weaponDamage = 0f;
        var weaponBridge = brain?.GetComponentInChildren<EquippedWeaponBridge>();
        if (weaponBridge != null && weaponBridge.HasWeaponEquipped())
        {
            weaponDamage = weaponBridge.GetEquippedWeaponDamage();
        }

        // Priority: Weapon damage > attackData.baseDamage > weaponDamageMultiplier fallback
        float baseDamage = weaponDamage > 0f ? weaponDamage :
                           (attackData.baseDamage > 0f ? attackData.baseDamage :
                            attackData.weaponDamageMultiplier * 10f);

        if (debugDamage && weaponDamage > 0f)
        {
            Debug.Log($"[DamageModule] Using equipped weapon damage: {weaponDamage:F1}");
        }

        // Add attack power from stats
        if (combatStats != null)
        {
            baseDamage += combatStats.GetAttackPower();
        }

        // Apply combo multiplier
        baseDamage *= attackData.comboMultiplier;

        // Roll for critical hit
        bool isCritical = false;
        float critMultiplier = 1f;
        if (combatStats != null)
        {
            float critChance = combatStats.GetCriticalChance();
            if (UnityEngine.Random.Range(0f, 100f) < critChance)
            {
                isCritical = true;
                critMultiplier = combatStats.GetCriticalMultiplier();
                baseDamage *= critMultiplier;
            }
        }

        // Use damage type from attack data
        DamageType damageType = attackData.damageType;

        CombatDamagePacket packet = new CombatDamagePacket(
            baseDamage: weaponDamage > 0f ? weaponDamage : attackData.baseDamage,
            finalDamage: baseDamage,
            isCriticalHit: isCritical,
            criticalMultiplier: critMultiplier,
            damageType: damageType,
            attacker: attackData.attackerTransform,
            attackerId: attackData.attackerTransform?.name ?? "Unknown",
            hitPoint: attackData.hitPoint,
            hitNormal: attackData.hitNormal,
            attackDirection: (attackData.hitPoint - attackData.attackerTransform.position).normalized,
            comboCount: attackData.comboCount,
            isHeavyAttack: attackData.isHeavyAttack,
            weaponId: attackData.weaponId
        );

        OnDamageDealt?.Invoke(packet);

        if (debugDamage)
        {
            Debug.Log($"[DamageModule] {gameObject.name} calculated damage: {baseDamage:F1} " +
                     $"(weapon: {weaponDamage:F1}, base: {packet.baseDamage:F1}, crit: {isCritical})");
        }

        return packet;
    }

    public CombatDamagePacket CalculateOutgoingDamage(float baseDamage, DamageType damageType = DamageType.Physical, bool canCrit = true)
    {
        if (combatStats == null)
        {
            return CombatDamagePacket.CreateSimple(baseDamage, transform, transform.position);
        }

        float attackPower = combatStats.GetAttackPower();
        float critChance = combatStats.GetCriticalChance();
        float critMultiplier = combatStats.GetCriticalMultiplier();
        float armorPen = combatStats.GetArmorPenetration();

        float totalDamage = baseDamage + attackPower;

        // Only roll for crit if canCrit is true
        bool isCritical = canCrit && UnityEngine.Random.Range(0f, 100f) < critChance;
        if (isCritical)
        {
            totalDamage *= critMultiplier;
        }

        CombatDamagePacket packet = new CombatDamagePacket(
            baseDamage: baseDamage,
            finalDamage: totalDamage,
            isCriticalHit: isCritical,
            criticalMultiplier: critMultiplier,
            damageType: damageType,
            attacker: transform,
            attackerId: gameObject.name,
            hitPoint: transform.position,
            hitNormal: Vector3.up,
            attackDirection: Vector3.forward
        );

        OnDamageDealt?.Invoke(packet);

        if (debugDamage)
        {
            Debug.Log($"[DamageModule] {gameObject.name} dealt {totalDamage:F1} damage (base: {baseDamage:F1}, power: {attackPower:F1}, crit: {isCritical})");
        }

        return packet;
    }

    public void TakeDamage(CombatDamagePacket incomingPacket)
    {
        if (!IsEnabled || health == null || !health.IsAlive())
            return;

        float incomingDamage = incomingPacket.finalDamage;

        if (defense != null)
        {
            incomingDamage = defense.ProcessIncomingDamage(incomingDamage, incomingPacket.attackDirection);
        }

        if (combatStats != null)
        {
            float armor = combatStats.GetArmor();
            float armorPen = 0f; // Would need to be in packet
            float effectiveArmor = armor * (1f - armorPen);

            float damageReduction = effectiveArmor / (effectiveArmor + 100f);
            incomingDamage *= (1f - damageReduction);

            if (incomingPacket.damageType == DamageType.Magical)
            {
                float magicResist = combatStats.GetMagicResistance();
                float magicReduction = magicResist / (magicResist + 100f);
                incomingDamage *= (1f - magicReduction);
            }
        }

        health.ApplyDamage(incomingDamage);

        OnDamageTaken?.Invoke(incomingPacket);

        if (debugDamage)
        {
            Debug.Log($"[DamageModule] {gameObject.name} took {incomingDamage:F1} damage (incoming: {incomingPacket.finalDamage:F1}, armor reduction applied)");
        }
    }

    public void TakeDamage(float damage, DamageType damageType = DamageType.Physical)
    {
        CombatDamagePacket packet = CombatDamagePacket.CreateSimple(damage, null, transform.position);
        TakeDamage(packet);
    }

    public bool IsAlive()
    {
        return health != null && health.IsAlive();
    }

    public float GetHealthPercentage()
    {
        return health != null ? health.GetHealthPercentage() : 100f;
    }

    public float GetCurrentHealth() => health?.GetCurrentHealth() ?? 0f;
    public float GetMaxHealth() => health?.GetMaxHealth() ?? 100f;

    public float GetAttackPower() => combatStats?.GetAttackPower() ?? 0f;
    public float GetArmor() => combatStats?.GetArmor() ?? 0f;
    public float GetMagicResistance() => combatStats?.GetMagicResistance() ?? 0f;

    public bool IsDefending()
    {
        bool isBlocking = defense?.IsBlocking() ?? false;
        bool isParrying = defense?.IsParrying() ?? false;
        return isBlocking || isParrying;
    }
}