[System.Serializable]
public struct CharacterCreationData
{
    public string characterName;
    public string accountName;
    public string modelId;

    /// <summary>
    /// Starting core stats. Leave null and SaveManager seeds a flat baseline instead.
    /// </summary>
    public StatBaseOverride[] baseStats;
}
