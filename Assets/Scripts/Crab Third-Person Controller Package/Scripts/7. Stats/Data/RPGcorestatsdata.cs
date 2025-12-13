using UnityEngine;

/// <summary>
/// Save data structure for RPGCoreStats.
/// Stores only base values - modifiers are managed by equipment/talents/buffs separately.
/// </summary>
[System.Serializable]
public class RPGCoreStatsData
{
    public float mindBase;
    public float bodyBase;
    public float spiritBase;
    public float resilienceBase;
    public float enduranceBase;
    public float insightBase;
}
