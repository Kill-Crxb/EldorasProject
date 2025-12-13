using UnityEngine;

/// <summary>
/// Manages the 6 core RPG stats: Mind, Body, Spirit, Resilience, Endurance, Insight.
/// Handles stat calculations, modifiers, and persistence.
/// Works for both NPCs and Players - allocation logic is separate in StatAllocationSystem.
/// </summary>
public class RPGCoreStats : MonoBehaviour, IPlayerModule, ISaveable
{
    [Header("Core Stats")]
    [SerializeField]
    private CoreStatDefinition mind = new CoreStatDefinition(
        "Mind", "Intelligence and magical power", Color.cyan, 10f
    );

    [SerializeField]
    private CoreStatDefinition body = new CoreStatDefinition(
        "Body", "Physical strength and power", Color.red, 10f
    );

    [SerializeField]
    private CoreStatDefinition spirit = new CoreStatDefinition(
        "Spirit", "Agility and dexterity", Color.green, 10f
    );

    [SerializeField]
    private CoreStatDefinition resilience = new CoreStatDefinition(
        "Resilience", "Defense and damage reduction", Color.blue, 10f
    );

    [SerializeField]
    private CoreStatDefinition endurance = new CoreStatDefinition(
        "Endurance", "Vitality and stamina", Color.yellow, 10f
    );

    [SerializeField]
    private CoreStatDefinition insight = new CoreStatDefinition(
        "Insight", "Wisdom and perception", Color.magenta, 10f
    );

    [Header("Debug")]
    [SerializeField] private bool debugStats = false;

    private ControllerBrain brain;

    // Properties
    public bool IsEnabled { get; set; } = true;
    public CoreStatDefinition Mind => mind;
    public CoreStatDefinition Body => body;
    public CoreStatDefinition Spirit => spirit;
    public CoreStatDefinition Resilience => resilience;
    public CoreStatDefinition Endurance => endurance;
    public CoreStatDefinition Insight => insight;

    // Events
    public System.Action<string, float, float> OnStatChanged;

    #region IPlayerModule Implementation

    void Awake()
    {
        // Initialize calculations
        mind.calculation.RecalculateFinalStat();
        body.calculation.RecalculateFinalStat();
        spirit.calculation.RecalculateFinalStat();
        resilience.calculation.RecalculateFinalStat();
        endurance.calculation.RecalculateFinalStat();
        insight.calculation.RecalculateFinalStat();
    }

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

        // Force initial stat calculations
        mind.calculation.RecalculateFinalStat();
        body.calculation.RecalculateFinalStat();
        spirit.calculation.RecalculateFinalStat();
        resilience.calculation.RecalculateFinalStat();
        endurance.calculation.RecalculateFinalStat();
        insight.calculation.RecalculateFinalStat();

        if (debugStats)
            Debug.Log($"[RPGCoreStats] Initialized - Body: {body.FinalValue}, Endurance: {endurance.FinalValue}");
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;
        // Handle any time-based stat updates here if needed
    }

    #endregion

    #region ISaveable Implementation

    public string GetSaveId() => "RPGCoreStats";

    public int GetSaveVersion() => 1;

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
            insightBase = insight.calculation.baseValue
        };

        return JsonUtility.ToJson(saveData);
    }

    public void LoadSaveData(string data)
    {
        var saveData = JsonUtility.FromJson<RPGCoreStatsData>(data);

        mind.calculation.baseValue = saveData.mindBase;
        body.calculation.baseValue = saveData.bodyBase;
        spirit.calculation.baseValue = saveData.spiritBase;
        resilience.calculation.baseValue = saveData.resilienceBase;
        endurance.calculation.baseValue = saveData.enduranceBase;
        insight.calculation.baseValue = saveData.insightBase;

        // Recalculate after loading
        mind.calculation.RecalculateFinalStat();
        body.calculation.RecalculateFinalStat();
        spirit.calculation.RecalculateFinalStat();
        resilience.calculation.RecalculateFinalStat();
        endurance.calculation.RecalculateFinalStat();
        insight.calculation.RecalculateFinalStat();

        if (debugStats)
            Debug.Log("[RPGCoreStats] Data loaded from save");
    }

    #endregion

    #region Stat Access

    public CoreStatDefinition GetCoreStat(string statName)
    {
        return statName.ToLower() switch
        {
            "mind" => mind,
            "body" => body,
            "spirit" => spirit,
            "resilience" => resilience,
            "endurance" => endurance,
            "insight" => insight,
            _ => null
        };
    }

    public float GetStatFinalValue(string statName)
    {
        var stat = GetCoreStat(statName);
        return stat?.FinalValue ?? 0f;
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