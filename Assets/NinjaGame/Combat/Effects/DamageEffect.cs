using System;
using UnityEngine;

[System.Serializable]
public class DamageEffect
{
    [Header("Damage Configuration")]
    public float baseDamage = 10f;
    public DamageType damageType = DamageType.Physical;
    [Tooltip("Can this damage critically hit?")]
    public bool canCrit = true;

    public event Action OnCompleted;

    [System.NonSerialized]
    private DamageModule damageModule;

    public void SetDamageModule(DamageModule module)
    {
        damageModule = module;
    }

    public void Apply(IDamageable target)
    {
        if (damageModule != null)
        {
            CombatDamagePacket packet = damageModule.CalculateOutgoingDamage(
                baseDamage,
                damageType,
                canCrit
            );
            target.TakeDamage(packet.finalDamage);
        }

        OnCompleted?.Invoke();
    }

    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}