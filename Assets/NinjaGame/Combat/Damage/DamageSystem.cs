using NinjaGame.Stats;
using System;
using System.Collections.Generic;
using UnityEngine;

public class DamageSystem : MonoBehaviour
{
    [SerializeField] private bool isEnabled = true;

    private ControllerBrain brain;
    private StatSystem stats;
    private IHealthProvider health;
    private IDefenseProvider defense;

    private bool isDead;

    public event Action<float> OnHealthChanged;
    public event Action<float, float> OnHealthChangedDetailed;
    public event Action OnDeath;
    public event Action<CombatDamagePacket> OnDamageDealt;
    public event Action<CombatDamagePacket> OnDamageTaken;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = brain.Stats;
        health = brain.GetModule<IHealthProvider>();
        defense = brain.GetModule<IDefenseProvider>();

        if (stats == null || health == null)
        {
            isEnabled = false;
            Debug.LogError("[DamageSystem] Missing dependencies on " + brain.name);
            return;
        }

        health.OnDeath += Die;
        health.OnHealthChanged += HandleHealthChanged;
    }

    private void HandleHealthChanged(float current)
    {
        OnHealthChanged?.Invoke(current);
        OnHealthChangedDetailed?.Invoke(current, health.GetMaxHealth());
    }

    public CombatDamagePacket CalculateDamage(CombatAttackData attackData)
    {
        var config = DamageManager.Instance?.ActiveConfig?.GetConfig(attackData.damageType);
        float damage = attackData.baseDamage + GetDynamicStat(config?.attackerStatIds);

        bool crit = false;
        float critMult = 1f;

        if (config != null && config.allowsCrits)
        {
            float chance = GetDynamicStat(config.critChanceStatIds);
            if (UnityEngine.Random.value * 100f < chance)
            {
                crit = true;
                critMult = 1f + GetDynamicStat(config.critMultiplierStatIds);
                damage *= critMult;
            }
        }

        float finalDamage = ApplyMitigation(damage, config);

        var packet = new CombatDamagePacket(
            attackData.baseDamage,
            finalDamage,
            crit,
            critMult,
            attackData.damageType,
            attackData.attackerTransform,
            attackData.attackerTransform?.name ?? "Unknown",
            attackData.hitPoint,
            attackData.hitNormal,
            attackData.hitPoint - (attackData.attackerTransform?.position ?? Vector3.zero)
        );

        OnDamageDealt?.Invoke(packet);
        return packet;
    }

    public void TakeDamage(CombatDamagePacket packet)
    {
        if (!isEnabled || isDead) return;

        float dmg = packet.finalDamage;

        if (defense != null)
            dmg = defense.ProcessIncomingDamage(dmg, packet.attackDirection);

        health.ApplyDamage(dmg);
        OnDamageTaken?.Invoke(packet);
    }

    private float ApplyMitigation(float dmg, DamageTypeConfig config)
    {
        if (config == null) return dmg;

        float mitigation = GetDynamicStat(config.defenderStatIds);
        float reduction = mitigation / (mitigation + 100f);
        return dmg * (1f - reduction);
    }

    private float GetDynamicStat(List<string> statIds)
    {
        if (stats == null || statIds == null) return 0f;
        float total = 0f;
        foreach (var id in statIds)
            total += stats.GetValue(id);
        return total;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDeath?.Invoke();
    }
}
