using UnityEngine;

public class NullCombatStats : MonoBehaviour, ICombatStatsProvider
{
    public float GetAttackPower() => 0f;
    public float GetCriticalChance() => 0f;
    public float GetCriticalMultiplier() => 1f;
    public float GetArmorPenetration() => 0f;
    public float GetArmor() => 0f;
    public float GetMagicResistance() => 0f;
}