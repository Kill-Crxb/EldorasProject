using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// RPG Progression System - Level, XP, and Stat Allocation
/// Phase 1.7b FINAL (Revised + Fixed)
/// </summary>
public class RPGSystem : MonoBehaviour, IBrainModule, ISaveable
{
    #region Constants

    private const int POINTS_PER_LEVEL = 3;
    public const int MIN_STAT_VALUE = 8;

    #endregion

    #region Inspector Fields

    [Header("Configuration")]
    [SerializeField] private bool enableStatAllocation = true;
    [SerializeField] private bool enableXPGain = true;
    [SerializeField] private bool autoAllocateStats = false;

    [Header("Level Progression")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int maxLevel = 30;
    [SerializeField] private int currentXP = 0;

    [Header("Stat Points")]
    [SerializeField] private int unallocatedPoints = 0;

    [Header("XP Curve")]
    [SerializeField] private int baseXPRequired = 100;
    [SerializeField] private float xpExponent = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region Private Fields

    private ControllerBrain brain;
    private StatSystem statsSystem;
    private IIdentityLevel identityHandler;

    private int xpToNextLevel;

    // Core stat discovery
    private List<string> coreStatIds = new();

    // Pending preview system (CORE STATS ONLY)
    private bool hasPendingChanges = false;
    private int pendingPointsSpent = 0;

    private readonly Dictionary<string, float> savedCoreStatValues = new();
    private readonly Dictionary<string, float> previewCoreStatValues = new();

    #endregion

    #region Properties

    public bool IsEnabled { get; set; } = true;

    public int CurrentLevel => currentLevel;
    public int MaxLevel => maxLevel;  // ✅ FIXED: Exposed MaxLevel property
    public int CurrentXP => currentXP;
    public int XPToNextLevel => xpToNextLevel;
    public int UnallocatedPoints => unallocatedPoints;
    public bool HasPendingChanges => hasPendingChanges;

    #endregion

    #region Events

    public event System.Action<int, int> OnLevelChanged;
    public event System.Action<int> OnXPChanged;
    public event System.Action<int> OnStatPointsChanged;

    // Optional UI hooks
    public event System.Action OnPreviewStarted;
    public event System.Action OnPreviewConfirmed;
    public event System.Action OnPreviewCancelled;

    #endregion

    #region Initialization

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        statsSystem = brain.Stats;

        if (statsSystem == null)
        {
            Debug.LogError($"[RPGSystem] StatsSystem missing on {brain.name}");
            return;
        }

        coreStatIds = DiscoverCoreStats();
        xpToNextLevel = CalculateXPForLevel(currentLevel + 1);

        var identitySystem = brain.Identity;
        if (identitySystem != null)
        {
            identityHandler = identitySystem.Identity as IIdentityLevel;
            if (identityHandler != null)
            {
                OnLevelChanged = null;
                OnLevelChanged += SyncIdentityLevel;
                identityHandler.Level = currentLevel;  // ✅ FIXED: Direct property assignment
            }
        }

        if (debugLogging)
            Debug.Log($"[RPGSystem] Initialized ({coreStatIds.Count} core stats)");
    }

    public void UpdateModule() { }

    private void SyncIdentityLevel(int _, int newLevel)
    {
        if (identityHandler != null)
            identityHandler.Level = newLevel;  // ✅ FIXED: Direct property assignment
    }

    #endregion

    #region Core Stat Discovery

    private List<string> DiscoverCoreStats()
    {
        var result = new List<string>();
        var ids = statsSystem.Engine.GetAllStatIds();

        // Preferred: category-based
        foreach (var id in ids)
        {
            var stat = statsSystem.Engine.GetStat(id);
            if (stat != null && stat.category == "Core")
                result.Add(id);
        }

        // Fallback: formula-less stats
        if (result.Count == 0)
        {
            foreach (var id in ids)
            {
                var stat = statsSystem.Engine.GetStat(id);
                if (stat != null && string.IsNullOrEmpty(stat.formula) && stat.baseValue > 0)
                    result.Add(id);
            }
        }

        return result;
    }

    #endregion

    #region XP & Leveling

    public void AddXP(int amount)
    {
        if (!enableXPGain || amount <= 0) return;

        currentXP += amount;
        OnXPChanged?.Invoke(currentXP);

        while (currentXP >= xpToNextLevel && currentLevel < maxLevel)
            LevelUp();
    }

    private void LevelUp()
    {
        int oldLevel = currentLevel;
        currentLevel++;

        currentXP -= xpToNextLevel;
        xpToNextLevel = CalculateXPForLevel(currentLevel + 1);

        unallocatedPoints += POINTS_PER_LEVEL;

        if (autoAllocateStats)
        {
            AutoDistributeStatPoints();
        }
        else if (enableStatAllocation && !hasPendingChanges)
        {
            BeginPendingChanges();
        }

        OnLevelChanged?.Invoke(oldLevel, currentLevel);
        OnStatPointsChanged?.Invoke(unallocatedPoints);
    }

    public void SetLevel(int level)
    {
        if (level < 1 || level > maxLevel) return;

        int oldLevel = currentLevel;
        currentLevel = level;
        currentXP = 0;

        xpToNextLevel = CalculateXPForLevel(currentLevel + 1);
        unallocatedPoints = (currentLevel - 1) * POINTS_PER_LEVEL;

        if (autoAllocateStats)
            AutoDistributeStatPoints();
        else if (enableStatAllocation)
            BeginPendingChanges();

        OnLevelChanged?.Invoke(oldLevel, currentLevel);
        OnXPChanged?.Invoke(currentXP);
        OnStatPointsChanged?.Invoke(unallocatedPoints);
    }

    private int CalculateXPForLevel(int level)
    {
        if (level <= 1) return 0;
        return Mathf.RoundToInt(baseXPRequired * Mathf.Pow(level, xpExponent));
    }

    public float GetLevelProgress()
    {
        if (currentLevel >= maxLevel) return 1f;
        return Mathf.Clamp01((float)currentXP / xpToNextLevel);
    }

    #endregion

    #region Stat Allocation (Transactional)

    public void BeginPendingChanges()
    {
        if (hasPendingChanges) return;

        savedCoreStatValues.Clear();
        previewCoreStatValues.Clear();
        pendingPointsSpent = 0;

        foreach (var id in coreStatIds)
        {
            float value = statsSystem.GetBaseValue(id);
            savedCoreStatValues[id] = value;
            previewCoreStatValues[id] = value;
        }

        hasPendingChanges = true;
        OnPreviewStarted?.Invoke();
    }

    public bool AllocateStatPoint(string statId)
    {
        if (!enableStatAllocation || unallocatedPoints <= 0) return false;
        if (!coreStatIds.Contains(statId)) return false;

        if (!hasPendingChanges)
            BeginPendingChanges();

        float current = previewCoreStatValues[statId];
        previewCoreStatValues[statId] = current + 1f;
        statsSystem.SetBaseValue(statId, current + 1f);

        unallocatedPoints--;
        pendingPointsSpent++;

        OnStatPointsChanged?.Invoke(unallocatedPoints);
        return true;
    }

    public bool DeallocateStatPoint(string statId)
    {
        if (!hasPendingChanges || !coreStatIds.Contains(statId)) return false;

        float current = statsSystem.GetBaseValue(statId);
        float saved = savedCoreStatValues[statId];

        if (current <= saved || current - 1 < MIN_STAT_VALUE) return false;

        previewCoreStatValues[statId] = current - 1f;
        statsSystem.SetBaseValue(statId, current - 1f);

        unallocatedPoints++;
        pendingPointsSpent--;

        OnStatPointsChanged?.Invoke(unallocatedPoints);
        return true;
    }

    public void ConfirmChanges()
    {
        if (!hasPendingChanges) return;

        hasPendingChanges = false;
        savedCoreStatValues.Clear();
        previewCoreStatValues.Clear();
        pendingPointsSpent = 0;

        OnPreviewConfirmed?.Invoke();
    }

    public void CancelChanges()
    {
        if (!hasPendingChanges) return;

        foreach (var kvp in savedCoreStatValues)
            statsSystem.SetBaseValue(kvp.Key, kvp.Value);

        unallocatedPoints += pendingPointsSpent;

        hasPendingChanges = false;
        pendingPointsSpent = 0;
        savedCoreStatValues.Clear();
        previewCoreStatValues.Clear();

        OnStatPointsChanged?.Invoke(unallocatedPoints);
        OnPreviewCancelled?.Invoke();
    }

    private void AutoDistributeStatPoints()
    {
        if (unallocatedPoints <= 0 || coreStatIds.Count == 0) return;

        int perStat = unallocatedPoints / coreStatIds.Count;
        int remainder = unallocatedPoints % coreStatIds.Count;

        foreach (var id in coreStatIds)
            statsSystem.SetBaseValue(id, statsSystem.GetBaseValue(id) + perStat);

        for (int i = 0; i < remainder; i++)
            statsSystem.SetBaseValue(coreStatIds[i],
                statsSystem.GetBaseValue(coreStatIds[i]) + 1);

        unallocatedPoints = 0;
        OnStatPointsChanged?.Invoke(unallocatedPoints);
    }

    #endregion

    #region Save / Load

    public string GetSaveId() => "RPGSystem";
    public int GetSaveVersion() => 1;

    public string GetSaveData()
    {
        return JsonUtility.ToJson(new SaveData
        {
            level = currentLevel,
            xp = currentXP,
            points = unallocatedPoints
        });
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<SaveData>(json);

        currentLevel = data.level;
        currentXP = data.xp;
        unallocatedPoints = data.points;

        xpToNextLevel = CalculateXPForLevel(currentLevel + 1);

        if (identityHandler != null)
            identityHandler.Level = currentLevel;  // ✅ FIXED: Direct property assignment
    }

    [System.Serializable]
    private struct SaveData
    {
        public int level;
        public int xp;
        public int points;
    }

    #endregion
}