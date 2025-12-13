using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pure calculation engine for stat modifiers and final value computation.
/// Handles flat bonuses (items, talents, buffs) and percentage multipliers.
/// Formula: (Base + CoreStats + Items + Talents + Buffs) × (1 + PercentageModifiers)
/// </summary>
[System.Serializable]
public class StatCalculation
{
    [Header("Base Values")]
    public float baseValue;

    [Header("Calculated Components (Read-Only)")]
    [SerializeField, ReadOnly] private float coreStatsBonus;
    [SerializeField, ReadOnly] private float itemsBonus;
    [SerializeField, ReadOnly] private float talentsBonus;
    [SerializeField, ReadOnly] private float buffsBonus;
    [SerializeField, ReadOnly] private float percentageMultiplier = 1f;
    [SerializeField, ReadOnly] private float finalStat;

    // Modifier storage
    private Dictionary<string, float> itemModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> talentModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> buffModifiers = new Dictionary<string, float>();
    private Dictionary<string, float> percentageModifiers = new Dictionary<string, float>();

    // Properties
    public float CoreStatsBonus => coreStatsBonus;
    public float ItemsBonus => itemsBonus;
    public float TalentsBonus => talentsBonus;
    public float BuffsBonus => buffsBonus;
    public float PercentageMultiplier => percentageMultiplier;
    public float FinalStat => finalStat;

    // Events
    public System.Action<float, float> OnStatChanged; // oldValue, newValue

    #region Core Stats Bonus

    public void SetCoreStatsBonus(float bonus)
    {
        if (coreStatsBonus != bonus)
        {
            coreStatsBonus = bonus;
            RecalculateFinalStat();
        }
    }

    #endregion

    #region Item Modifiers

    public void AddItemModifier(string itemId, float value)
    {
        itemModifiers[itemId] = value;
        RecalculateItemsBonus();
    }

    public void RemoveItemModifier(string itemId)
    {
        if (itemModifiers.Remove(itemId))
        {
            RecalculateItemsBonus();
        }
    }

    private void RecalculateItemsBonus()
    {
        float sum = 0f;
        foreach (var kvp in itemModifiers)
        {
            sum += kvp.Value;
        }

        if (itemsBonus != sum)
        {
            itemsBonus = sum;
            RecalculateFinalStat();
        }
    }

    #endregion

    #region Talent Modifiers

    public void AddTalentModifier(string talentId, float value)
    {
        talentModifiers[talentId] = value;
        RecalculateTalentsBonus();
    }

    public void RemoveTalentModifier(string talentId)
    {
        if (talentModifiers.Remove(talentId))
        {
            RecalculateTalentsBonus();
        }
    }

    private void RecalculateTalentsBonus()
    {
        float sum = 0f;
        foreach (var kvp in talentModifiers)
        {
            sum += kvp.Value;
        }

        if (talentsBonus != sum)
        {
            talentsBonus = sum;
            RecalculateFinalStat();
        }
    }

    #endregion

    #region Buff Modifiers

    public void AddBuffModifier(string buffId, float value)
    {
        buffModifiers[buffId] = value;
        RecalculateBuffsBonus();
    }

    public void RemoveBuffModifier(string buffId)
    {
        if (buffModifiers.Remove(buffId))
        {
            RecalculateBuffsBonus();
        }
    }

    private void RecalculateBuffsBonus()
    {
        float sum = 0f;
        foreach (var kvp in buffModifiers)
        {
            sum += kvp.Value;
        }

        if (buffsBonus != sum)
        {
            buffsBonus = sum;
            RecalculateFinalStat();
        }
    }

    #endregion

    #region Percentage Modifiers

    public void AddPercentageModifier(string sourceId, float percentage)
    {
        percentageModifiers[sourceId] = percentage;
        RecalculatePercentageMultiplier();
    }

    public void RemovePercentageModifier(string sourceId)
    {
        if (percentageModifiers.Remove(sourceId))
        {
            RecalculatePercentageMultiplier();
        }
    }

    private void RecalculatePercentageMultiplier()
    {
        float sum = 0f;
        foreach (var kvp in percentageModifiers)
        {
            sum += kvp.Value;
        }

        float newMultiplier = 1f + sum;

        if (Mathf.Abs(percentageMultiplier - newMultiplier) > 0.001f)
        {
            percentageMultiplier = newMultiplier;
            RecalculateFinalStat();
        }
    }

    #endregion

    #region Remove All From Source

    public void RemoveAllModifiersFromSource(string sourceId)
    {
        bool changed = false;

        changed |= itemModifiers.Remove(sourceId);
        changed |= talentModifiers.Remove(sourceId);
        changed |= buffModifiers.Remove(sourceId);
        changed |= percentageModifiers.Remove(sourceId);

        if (changed)
        {
            RecalculateItemsBonus();
            RecalculateTalentsBonus();
            RecalculateBuffsBonus();
            RecalculatePercentageMultiplier();
        }
    }

    #endregion

    #region Final Stat Calculation

    public void RecalculateFinalStat()
    {
        float oldValue = finalStat;

        // Formula: (Base + CoreStats + Items + Talents + Buffs) × (1 + PercentageModifiers)
        float flatTotal = baseValue + coreStatsBonus + itemsBonus + talentsBonus + buffsBonus;
        finalStat = flatTotal * percentageMultiplier;

        if (Mathf.Abs(oldValue - finalStat) > 0.001f)
        {
            OnStatChanged?.Invoke(oldValue, finalStat);
        }
    }

    #endregion

    #region Utility

    public string GetBreakdown()
    {
        var breakdown = new System.Text.StringBuilder();
        breakdown.AppendLine($"Base: {baseValue:F1}");
        if (coreStatsBonus > 0) breakdown.AppendLine($"Core Stats: +{coreStatsBonus:F1}");
        if (itemsBonus > 0) breakdown.AppendLine($"Items: +{itemsBonus:F1}");
        if (talentsBonus > 0) breakdown.AppendLine($"Talents: +{talentsBonus:F1}");
        if (buffsBonus > 0) breakdown.AppendLine($"Buffs: +{buffsBonus:F1}");

        float flatTotal = baseValue + coreStatsBonus + itemsBonus + talentsBonus + buffsBonus;
        breakdown.AppendLine($"Subtotal: {flatTotal:F1}");

        if (percentageMultiplier != 1f)
        {
            float percentageBonus = (percentageMultiplier - 1f) * 100f;
            breakdown.AppendLine($"Percentage: ×{percentageMultiplier:F2} (+{percentageBonus:F1}%)");
        }

        breakdown.AppendLine($"FINAL: {finalStat:F1}");
        return breakdown.ToString();
    }

    #endregion
}
