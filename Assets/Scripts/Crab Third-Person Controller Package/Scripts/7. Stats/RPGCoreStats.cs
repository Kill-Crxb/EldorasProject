// RPG Core Stats - Foundation stats with unified calculation system + Save support
using UnityEngine;
using System.Collections.Generic;

// Custom attribute for read-only fields in inspector
public class ReadOnlyAttribute : PropertyAttribute { }

[System.Serializable]
public class StatModifier
{
    public string sourceId; // Equipment ID, buff ID, etc.
    public string sourceName; // For display
    public float value;
    public float duration; // -1 for permanent
    public float appliedTime;

    public bool IsExpired => duration > 0 && Time.time - appliedTime >= duration;
    public bool IsPermanent => duration <= 0;
}

// Base class for all stat calculations
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

    public void SetCoreStatsBonus(float bonus)
    {
        if (coreStatsBonus != bonus)
        {
            coreStatsBonus = bonus;
            RecalculateFinalStat();
        }
    }

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

    public void RemoveAllModifiersFromSource(string sourceId)
    {
        bool changed = false;
        changed |= itemModifiers.Remove(sourceId);
        changed |= talentModifiers.Remove(sourceId);
        changed |= buffModifiers.Remove(sourceId);
        changed |= percentageModifiers.Remove(sourceId);

        if (changed)
        {
            RecalculateAllComponents();
        }
    }

    void RecalculateItemsBonus()
    {
        float newBonus = 0f;
        foreach (var modifier in itemModifiers.Values)
        {
            newBonus += modifier;
        }

        if (itemsBonus != newBonus)
        {
            itemsBonus = newBonus;
            RecalculateFinalStat();
        }
    }

    void RecalculateTalentsBonus()
    {
        float newBonus = 0f;
        foreach (var modifier in talentModifiers.Values)
        {
            newBonus += modifier;
        }

        if (talentsBonus != newBonus)
        {
            talentsBonus = newBonus;
            RecalculateFinalStat();
        }
    }

    void RecalculateBuffsBonus()
    {
        float newBonus = 0f;
        foreach (var modifier in buffModifiers.Values)
        {
            newBonus += modifier;
        }

        if (buffsBonus != newBonus)
        {
            buffsBonus = newBonus;
            RecalculateFinalStat();
        }
    }

    void RecalculatePercentageMultiplier()
    {
        float newMultiplier = 1f;
        foreach (var modifier in percentageModifiers.Values)
        {
            newMultiplier += modifier * 0.01f; // Convert percentage to decimal
        }

        if (percentageMultiplier != newMultiplier)
        {
            percentageMultiplier = newMultiplier;
            RecalculateFinalStat();
        }
    }

    void RecalculateAllComponents()
    {
        RecalculateItemsBonus();
        RecalculateTalentsBonus();
        RecalculateBuffsBonus();
        RecalculatePercentageMultiplier();
    }

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
}

[System.Serializable]
public class CoreStatWithCalculation
{
    [Header("Basic Info")]
    public string displayName;
    public string description;
    public Color displayColor = Color.white;

    [Header("Unified Calculation")]
    public StatCalculation calculation = new StatCalculation();

    // Convenience property
    public float FinalValue => calculation.FinalStat;
}

public class RPGCoreStats : MonoBehaviour, IPlayerModule, ISaveable
{
    [Header("Core Stats")]
    [SerializeField]
    private CoreStatWithCalculation mind = new CoreStatWithCalculation
    {
        displayName = "Mind",
        description = "Intelligence and magical power",
        displayColor = Color.cyan,
        calculation = new StatCalculation { baseValue = 10f }
    };

    [SerializeField]
    private CoreStatWithCalculation body = new CoreStatWithCalculation
    {
        displayName = "Body",
        description = "Physical strength and power",
        displayColor = Color.red,
        calculation = new StatCalculation { baseValue = 10f }
    };

    [SerializeField]
    private CoreStatWithCalculation spirit = new CoreStatWithCalculation
    {
        displayName = "Spirit",
        description = "Agility and dexterity",
        displayColor = Color.green,
        calculation = new StatCalculation { baseValue = 10f }
    };

    [SerializeField]
    private CoreStatWithCalculation resilience = new CoreStatWithCalculation
    {
        displayName = "Resilience",
        description = "Defense and damage reduction",
        displayColor = Color.blue,
        calculation = new StatCalculation { baseValue = 10f }
    };

    [SerializeField]
    private CoreStatWithCalculation endurance = new CoreStatWithCalculation
    {
        displayName = "Endurance",
        description = "Vitality and stamina",
        displayColor = Color.yellow,
        calculation = new StatCalculation { baseValue = 10f }
    };

    [SerializeField]
    private CoreStatWithCalculation insight = new CoreStatWithCalculation
    {
        displayName = "Insight",
        description = "Wisdom and perception",
        displayColor = Color.magenta,
        calculation = new StatCalculation { baseValue = 10f }
    };

    [Header("Player Level")]
    [SerializeField] private int playerLevel = 1;
    [SerializeField] private int maxLevel = 30;

    [Header("Debug")]
    [SerializeField] private bool debugStats = false;

    private ControllerBrain brain;
    private bool isFullyInitialized = false;

    // Properties
    public bool IsEnabled { get; set; } = true;
    public CoreStatWithCalculation Mind => mind;
    public CoreStatWithCalculation Body => body;
    public CoreStatWithCalculation Spirit => spirit;
    public CoreStatWithCalculation Resilience => resilience;
    public CoreStatWithCalculation Endurance => endurance;
    public CoreStatWithCalculation Insight => insight;
    public int PlayerLevel => playerLevel;

    // Events
    public System.Action<string, float, float> OnStatChanged;
    public System.Action<int, int> OnLevelChanged;

    #region IPlayerModule Implementation

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Subscribe to stat change events
        mind.calculation.OnStatChanged += (old, newVal) => OnStatChanged?.Invoke("Mind", old, newVal);
        body.calculation.OnStatChanged += (old, newVal) => OnStatChanged?.Invoke("Body", old, newVal);
        spirit.calculation.OnStatChanged += (old, newVal) => OnStatChanged?.Invoke("Spirit", old, newVal);
        resilience.calculation.OnStatChanged += (old, newVal) => OnStatChanged?.Invoke("Resilience", old, newVal);
        endurance.calculation.OnStatChanged += (old, newVal) => OnStatChanged?.Invoke("Endurance", old, newVal);
        insight.calculation.OnStatChanged += (old, newVal) => OnStatChanged?.Invoke("Insight", old, newVal);

        isFullyInitialized = true;
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;
        // Handle any time-based stat updates here
    }

    #endregion

    #region ISaveable Implementation

    public string GetSaveId() => "RPGCoreStats";

    public string GetSaveData()
    {
        var saveData = new RPGCoreStatsData
        {
            // Save only base values - modifiers come from equipment/talents/buffs
            mindBase = mind.calculation.baseValue,
            bodyBase = body.calculation.baseValue,
            spiritBase = spirit.calculation.baseValue,
            resilienceBase = resilience.calculation.baseValue,
            enduranceBase = endurance.calculation.baseValue,
            insightBase = insight.calculation.baseValue,
            playerLevel = playerLevel
        };

        return JsonUtility.ToJson(saveData, true);
    }

    public void LoadSaveData(string json)
    {
        var saveData = JsonUtility.FromJson<RPGCoreStatsData>(json);
        if (saveData != null)
        {
            // Load base values
            mind.calculation.baseValue = saveData.mindBase;
            body.calculation.baseValue = saveData.bodyBase;
            spirit.calculation.baseValue = saveData.spiritBase;
            resilience.calculation.baseValue = saveData.resilienceBase;
            endurance.calculation.baseValue = saveData.enduranceBase;
            insight.calculation.baseValue = saveData.insightBase;

            // Set level
            SetPlayerLevel(saveData.playerLevel);

            // Trigger recalculation if we're initialized
            if (isFullyInitialized)
            {
                mind.calculation.RecalculateFinalStat();
                body.calculation.RecalculateFinalStat();
                spirit.calculation.RecalculateFinalStat();
                resilience.calculation.RecalculateFinalStat();
                endurance.calculation.RecalculateFinalStat();
                insight.calculation.RecalculateFinalStat();
            }
        }
    }

    public int GetSaveVersion() => 1;

    #endregion

    #region Core Stat Access

    public CoreStatWithCalculation GetCoreStat(string statName)
    {
        switch (statName.ToLower())
        {
            case "mind": return mind;
            case "body": return body;
            case "spirit": return spirit;
            case "resilience": return resilience;
            case "endurance": return endurance;
            case "insight": return insight;
            default: return null;
        }
    }

    public float GetStatFinalValue(string statName)
    {
        var stat = GetCoreStat(statName);
        return stat?.calculation.FinalStat ?? 0f;
    }

    public float GetStatBaseValue(string statName)
    {
        var stat = GetCoreStat(statName);
        return stat?.calculation.baseValue ?? 0f;
    }

    public void SetStatBaseValue(string statName, float value)
    {
        var stat = GetCoreStat(statName);
        if (stat != null)
        {
            stat.calculation.baseValue = value;
            stat.calculation.RecalculateFinalStat();
        }
    }

    public void SetPlayerLevel(int level)
    {
        int oldLevel = playerLevel;
        playerLevel = Mathf.Clamp(level, 1, maxLevel);
        if (oldLevel != playerLevel)
        {
            OnLevelChanged?.Invoke(oldLevel, playerLevel);
        }
    }

    #endregion

    #region Modifier Management

    public void AddItemModifier(string statName, string itemId, float value)
    {
        var stat = GetCoreStat(statName);
        if (stat != null)
        {
            stat.calculation.AddItemModifier(itemId, value);
        }
    }

    public void AddTalentModifier(string statName, string talentId, float value)
    {
        var stat = GetCoreStat(statName);
        if (stat != null)
        {
            stat.calculation.AddTalentModifier(talentId, value);
        }
    }

    public void AddBuffModifier(string statName, string buffId, float value)
    {
        var stat = GetCoreStat(statName);
        if (stat != null)
        {
            stat.calculation.AddBuffModifier(buffId, value);
        }
    }

    public void AddPercentageModifier(string statName, string sourceId, float percentage)
    {
        var stat = GetCoreStat(statName);
        if (stat != null)
        {
            stat.calculation.AddPercentageModifier(sourceId, percentage);
        }
    }

    public void RemoveAllModifiersFromSource(string sourceId)
    {
        mind.calculation.RemoveAllModifiersFromSource(sourceId);
        body.calculation.RemoveAllModifiersFromSource(sourceId);
        spirit.calculation.RemoveAllModifiersFromSource(sourceId);
        resilience.calculation.RemoveAllModifiersFromSource(sourceId);
        endurance.calculation.RemoveAllModifiersFromSource(sourceId);
        insight.calculation.RemoveAllModifiersFromSource(sourceId);
    }

    #endregion

    #region Public API

    public string GetStatsSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine("=== CORE STATS ===");
        summary.AppendLine($"Level: {playerLevel}");
        summary.AppendLine($"Mind: {mind.FinalValue:F1}");
        summary.AppendLine($"Body: {body.FinalValue:F1}");
        summary.AppendLine($"Spirit: {spirit.FinalValue:F1}");
        summary.AppendLine($"Resilience: {resilience.FinalValue:F1}");
        summary.AppendLine($"Endurance: {endurance.FinalValue:F1}");
        summary.AppendLine($"Insight: {insight.FinalValue:F1}");

        return summary.ToString();
    }

    #endregion

    #region Debug

    void OnGUI()
    {
        if (!debugStats) return;

        GUILayout.BeginArea(new Rect(10, 10, 350, Screen.height - 20));
        GUILayout.Label("=== RPG CORE STATS ===");
        GUILayout.Label($"Level: {playerLevel}");
        GUILayout.Label($"Mind: {mind.FinalValue:F1}");
        GUILayout.Label($"Body: {body.FinalValue:F1}");
        GUILayout.Label($"Spirit: {spirit.FinalValue:F1}");
        GUILayout.Label($"Resilience: {resilience.FinalValue:F1}");
        GUILayout.Label($"Endurance: {endurance.FinalValue:F1}");
        GUILayout.Label($"Insight: {insight.FinalValue:F1}");

        GUILayout.EndArea();
    }

    #endregion
}

//==========================================
// SAVE DATA STRUCTURE
//==========================================

[System.Serializable]
public class RPGCoreStatsData
{
    public float mindBase;
    public float bodyBase;
    public float spiritBase;
    public float resilienceBase;
    public float enduranceBase;
    public float insightBase;
    public int playerLevel;
}