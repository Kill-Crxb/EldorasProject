using UnityEngine;
using System;

/// <summary>
/// Tracks basic character identity: name, level, creation date, playtime
/// </summary>
public class IdentityHandler : MonoBehaviour, IPlayerInfoHandler
{
    [Header("Character Identity")]
    [SerializeField] private string characterName = "Adventurer";
    [SerializeField] private int characterLevel = 1;
    [SerializeField] private string characterId;

    [Header("Metadata")]
    [SerializeField] private string creationDateString; // For inspector display
    [SerializeField] private float totalPlaytime;

    private PlayerInfoModule parent;
    private DateTime creationDate;
    private bool isInitialized = false;

    #region Properties (Public API)

    /// <summary>
    /// Character's display name
    /// </summary>
    public string CharacterName
    {
        get => characterName;
        set => characterName = value;
    }

    /// <summary>
    /// Character's unique ID (GUID)
    /// </summary>
    public string CharacterId => characterId;

    /// <summary>
    /// Character's current level
    /// </summary>
    public int Level
    {
        get => characterLevel;
        set => characterLevel = value;
    }

    /// <summary>
    /// Total playtime in seconds
    /// </summary>
    public float TotalPlaytime => totalPlaytime;

    /// <summary>
    /// Character creation date
    /// </summary>
    public DateTime CreationDate => creationDate;

    public bool IsEnabled { get; set; } = true;

    #endregion

    #region IPlayerInfoHandler Implementation

    public void Initialize(PlayerInfoModule parentModule)
    {
        parent = parentModule;

        // Generate ID if new character
        if (string.IsNullOrEmpty(characterId))
        {
            characterId = System.Guid.NewGuid().ToString();
            creationDate = DateTime.Now;
            creationDateString = creationDate.ToString("yyyy-MM-dd HH:mm:ss");
        }

        isInitialized = true;
        Debug.Log($"[IdentityHandler] Initialized: {characterName} (Level {characterLevel})");
    }

    public void UpdateHandler()
    {
        if (!isInitialized) return;

        // Track playtime
        totalPlaytime += Time.deltaTime;
    }

    public string GetHandlerSaveData()
    {
        var saveData = new IdentitySaveData
        {
            characterName = this.characterName,
            characterLevel = this.characterLevel,
            characterId = this.characterId,
            creationDate = this.creationDate.ToString("o"), // ISO 8601 format
            totalPlaytime = this.totalPlaytime
        };
        return JsonUtility.ToJson(saveData);
    }

    public void LoadHandlerData(string json)
    {
        var saveData = JsonUtility.FromJson<IdentitySaveData>(json);

        characterName = saveData.characterName;
        characterLevel = saveData.characterLevel;
        characterId = saveData.characterId;
        totalPlaytime = saveData.totalPlaytime;

        if (DateTime.TryParse(saveData.creationDate, out DateTime parsed))
        {
            creationDate = parsed;
            creationDateString = creationDate.ToString("yyyy-MM-dd HH:mm:ss");
        }

        Debug.Log($"[IdentityHandler] Loaded: {characterName} (Level {characterLevel})");
    }

    public void ResetHandler()
    {
        characterName = "Adventurer";
        characterLevel = 1;
        characterId = System.Guid.NewGuid().ToString();
        creationDate = DateTime.Now;
        creationDateString = creationDate.ToString("yyyy-MM-dd HH:mm:ss");
        totalPlaytime = 0f;
    }

    #endregion

    #region Public Methods (Alternative API)

    /// <summary>
    /// Get character name (alternative to property)
    /// </summary>
    public string GetCharacterName() => characterName;

    /// <summary>
    /// Get character level (alternative to property)
    /// </summary>
    public int GetCharacterLevel() => characterLevel;

    /// <summary>
    /// Get character ID (alternative to property)
    /// </summary>
    public string GetCharacterId() => characterId;

    /// <summary>
    /// Get total playtime (alternative to property)
    /// </summary>
    public float GetTotalPlaytime() => totalPlaytime;

    /// <summary>
    /// Set character name
    /// </summary>
    public void SetCharacterName(string name)
    {
        characterName = name;
    }

    /// <summary>
    /// Level up the character
    /// </summary>
    public void LevelUp()
    {
        characterLevel++;
        Debug.Log($"[IdentityHandler] Level Up! Now level {characterLevel}");
    }

    /// <summary>
    /// Set character level directly
    /// </summary>
    public void SetLevel(int level)
    {
        characterLevel = Mathf.Max(1, level);
    }

    /// <summary>
    /// Add playtime in seconds
    /// </summary>
    public void AddPlaytime(float seconds)
    {
        totalPlaytime += seconds;
    }

    #endregion
}

#region Save Data Structure

[System.Serializable]
public class IdentitySaveData
{
    public string characterName;
    public int characterLevel;
    public string characterId;
    public string creationDate;
    public float totalPlaytime;
}

#endregion