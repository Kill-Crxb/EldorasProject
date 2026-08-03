using System;
using UnityEngine;

[Serializable]
public class CharacterConfigData
{
    [Header("Identity")]
    public string characterId;
    public string displayName;
    public string factionId = "faction_neutral";
    public int level = 1;

    [Header("Visuals")]
    public string modelId;

    [Header("Stats")]
    public StatBaseOverride[] baseStatOverrides;

    [Header("Persistence (for unique NPCs)")]
    public bool isPersistent = false;
    public string saveFilePath;

    public CharacterConfigData() { }

    public CharacterConfigData(string displayName, string factionId, string modelId, StatBaseOverride[] baseStatOverrides, int level)
    {
        this.displayName = displayName;
        this.factionId = factionId;
        this.modelId = modelId;
        this.baseStatOverrides = baseStatOverrides;
        this.level = level;
    }
}