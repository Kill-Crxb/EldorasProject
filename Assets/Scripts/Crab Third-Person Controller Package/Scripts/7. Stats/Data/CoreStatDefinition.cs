using UnityEngine;

/// <summary>
/// Wrapper for a single core stat with display information and unified calculation.
/// Used by RPGCoreStats to manage individual stats (Mind, Body, Spirit, etc.)
/// </summary>
[System.Serializable]
public class CoreStatDefinition
{
    [Header("Basic Info")]
    public string displayName;
    public string description;
    public Color displayColor = Color.white;

    [Header("Unified Calculation")]
    public StatCalculation calculation = new StatCalculation();

    // Convenience property
    public float FinalValue => calculation.FinalStat;

    public CoreStatDefinition() { }

    public CoreStatDefinition(string name, string desc, Color color, float baseValue)
    {
        displayName = name;
        description = desc;
        displayColor = color;
        calculation = new StatCalculation { baseValue = baseValue };
    }
}