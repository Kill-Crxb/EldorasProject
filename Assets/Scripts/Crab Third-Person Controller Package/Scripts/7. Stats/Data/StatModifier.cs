using UnityEngine;

/// <summary>
/// Data structure for stat modifiers with duration tracking.
/// Used for buffs, debuffs, and temporary effects.
/// </summary>
[System.Serializable]
public class StatModifier
{
    public string sourceId;     // Equipment ID, buff ID, etc.
    public string sourceName;   // For display purposes
    public float value;
    public float duration;      // -1 for permanent
    public float appliedTime;

    public bool IsExpired => duration > 0 && Time.time - appliedTime >= duration;
    public bool IsPermanent => duration <= 0;
}