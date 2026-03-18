using UnityEngine;
using System;

/// <summary>
/// UNIVERSAL Identity Handler - works for ALL entity types (Player, NPC, Object)
/// Tracks: name, unique ID, level, entity type, existence time, creation date
/// 
/// Replaces old player-specific IdentityHandler.cs
/// </summary>
public class UniversalIdentityHandler : MonoBehaviour, IIdentityHandler, IIdentityLevel
{
    [Header("Handler Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Basic Identity")]
    [Tooltip("Unique identifier (auto-generated GUID)")]
    [SerializeField] private string entityId;

    [Tooltip("Display name (Hero, Bear, Ancient Chest, etc.)")]
    [SerializeField] private string displayName = "Entity";

    [Tooltip("Entity classification")]
    [SerializeField] private EntityType entityType = EntityType.Entity;

    [Tooltip("Level (0 for objects without levels)")]
    [SerializeField] private int level = 1;

    [Header("Metadata")]
    [Tooltip("When this entity was created/spawned")]
    [SerializeField] private string creationDateString;

    [Tooltip("Total existence time in seconds (playtime for players, lifetime for NPCs/objects)")]
    [SerializeField] private float totalExistenceTime;

    // Internal state
    private IdentitySystem parent;
    private DateTime creationDate;
    private bool isInitialized = false;

    #region Properties (Public API)

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    /// <summary>
    /// Unique entity ID (GUID)
    /// </summary>
    public string EntityId => entityId;

    /// <summary>
    /// Display name (shown to players)
    /// </summary>
    public string DisplayName
    {
        get => displayName;
        set => displayName = value;
    }

    /// <summary>
    /// Entity type classification
    /// </summary>
    public EntityType Type
    {
        get => entityType;
        set => entityType = value;
    }

    /// <summary>
    /// Level (players, NPCs have levels; objects typically 0)
    /// </summary>
    public int Level
    {
        get => level;
        set => level = Mathf.Max(0, value);
    }

    /// <summary>
    /// Total existence time (playtime for players, lifetime for NPCs/objects)
    /// </summary>
    public float ExistenceTime => totalExistenceTime;

    /// <summary>
    /// When this entity was created/spawned
    /// </summary>
    public DateTime CreationDate => creationDate;

    #endregion

    #region IIdentityHandler Implementation

    public void Initialize(IdentitySystem parentSystem)
    {
        parent = parentSystem;

        // Generate ID if new entity
        if (string.IsNullOrEmpty(entityId))
        {
            entityId = System.Guid.NewGuid().ToString();
            creationDate = DateTime.Now;
            creationDateString = creationDate.ToString("yyyy-MM-dd HH:mm:ss");
        }
        else
        {
            // Parse existing creation date
            if (!string.IsNullOrEmpty(creationDateString))
            {
                DateTime.TryParse(creationDateString, out creationDate);
            }
        }

        isInitialized = true;
    }

    public void UpdateHandler()
    {
        if (!isInitialized || !isEnabled) return;

        // Track existence time (playtime for players, lifetime for NPCs/objects)
        totalExistenceTime += Time.deltaTime;
    }

    public string GetHandlerSaveData()
    {
        var saveData = new UniversalIdentitySaveData
        {
            entityId = this.entityId,
            displayName = this.displayName,
            entityType = this.entityType,
            level = this.level,
            creationDate = this.creationDate.ToString("o"), // ISO 8601
            totalExistenceTime = this.totalExistenceTime
        };
        return JsonUtility.ToJson(saveData);
    }

    public void LoadHandlerData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var saveData = JsonUtility.FromJson<UniversalIdentitySaveData>(json);

        entityId = saveData.entityId;
        displayName = saveData.displayName;
        entityType = saveData.entityType;
        level = saveData.level;
        totalExistenceTime = saveData.totalExistenceTime;

        if (DateTime.TryParse(saveData.creationDate, out DateTime parsed))
        {
            creationDate = parsed;
            creationDateString = creationDate.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    public void ResetHandler()
    {
        entityId = System.Guid.NewGuid().ToString();
        displayName = "Entity";
        level = 1;
        totalExistenceTime = 0f;
        creationDate = DateTime.Now;
        creationDateString = creationDate.ToString("yyyy-MM-dd HH:mm:ss");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Increment existence time by specific amount (for manual updates)
    /// </summary>
    public void IncrementExistenceTime(float deltaTime)
    {
        totalExistenceTime += deltaTime;
    }

    /// <summary>
    /// Level up the entity
    /// </summary>
    public void LevelUp()
    {
        level++;
    }

    /// <summary>
    /// Get formatted playtime string (for players)
    /// </summary>
    public string GetFormattedPlaytime()
    {
        int hours = Mathf.FloorToInt(totalExistenceTime / 3600f);
        int minutes = Mathf.FloorToInt((totalExistenceTime % 3600f) / 60f);
        return $"{hours}h {minutes}m";
    }

    #endregion
}

#region Save Data Structure

[System.Serializable]
public class UniversalIdentitySaveData
{
    public string entityId;
    public string displayName;
    public EntityType entityType;
    public int level;
    public string creationDate;
    public float totalExistenceTime;
}

#endregion