using System.Collections.Generic;

[System.Serializable]
public struct CharacterCreationData
{
    public string characterName;
    public string accountName;
    public string modelId;

    // One option per appearance group of the chosen model. Written to model.json at creation.
    public List<AppearanceChoice> appearance;

    /// <summary>
    /// Starting core stats. Leave null and SaveManager seeds a flat baseline instead.
    /// </summary>
    public StatBaseOverride[] baseStats;
}
