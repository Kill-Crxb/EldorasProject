using UnityEngine;

/// <summary>
/// Player-only system for level-up progression and stat point allocation.
/// NPCs don't use this module - they only use RPGCoreStats directly.
/// Implements ISaveable separately for clean save/load integration.
/// Supports pending changes with confirm/cancel workflow.
/// 
/// SYNC: Automatically syncs level changes to IdentityHandler
/// </summary>
public class StatAllocationSystem : MonoBehaviour, IPlayerModule, ISaveable
{
    [Header("Level Progression")]
    [SerializeField] private int playerLevel = 1;
    [SerializeField] private int maxLevel = 30;
    [SerializeField] private int unallocatedPoints = 0;

    [Header("References")]
    [SerializeField] private RPGCoreStats coreStats;

    [Header("Debug")]
    [SerializeField] private bool debugAllocation = false;

    private ControllerBrain brain;

    // Pending changes tracking
    private bool hasPendingChanges = false;
    private int pendingUnallocatedPoints = 0;
    private float pendingMind = 0f;
    private float pendingBody = 0f;
    private float pendingSpirit = 0f;
    private float pendingResilience = 0f;
    private float pendingEndurance = 0f;
    private float pendingInsight = 0f;

    // Constants
    public const int STARTING_STAT_POINTS = 60;
    public const int POINTS_PER_LEVEL = 3;
    public const int MIN_STAT_VALUE = 8;

    // Properties
    public bool IsEnabled { get; set; } = true;
    public int PlayerLevel => playerLevel;
    public int MaxLevel => maxLevel;
    public int UnallocatedPoints => unallocatedPoints;
    public bool HasPendingChanges => hasPendingChanges;

    // Events
    public System.Action<int, int> OnLevelChanged; // oldLevel, newLevel
    public System.Action<int> OnStatPointsChanged; // unallocatedPoints
    public System.Action OnChangesConfirmed;
    public System.Action OnChangesCancelled;

    #region IPlayerModule Implementation

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Auto-find coreStats if not assigned
        if (coreStats == null)
        {
            coreStats = brain.Stats?.CoreStats;
        }

        // Wire up IdentityHandler level sync
        var playerInfo = brain.GetModule<PlayerInfoModule>();
        if (playerInfo != null && playerInfo.IdentityHandler != null)
        {
            // Subscribe to level changes to keep IdentityHandler in sync
            OnLevelChanged += (oldLevel, newLevel) =>
            {
                playerInfo.IdentityHandler.Level = newLevel;
                if (debugAllocation)
                    Debug.Log($"[StatAllocation] Synced level to IdentityHandler: {newLevel}");
            };

            // Initial sync - set IdentityHandler to match StatAllocation's level
            playerInfo.IdentityHandler.Level = playerLevel;
        }

        if (debugAllocation)
            Debug.Log($"[StatAllocation] Initialized - Level: {playerLevel}, Unallocated: {unallocatedPoints}");
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;
        // Nothing to update per-frame
    }

    #endregion

    #region ISaveable Implementation

    public string GetSaveId() => "StatAllocationSystem";

    public int GetSaveVersion() => 1;

    public string GetSaveData()
    {
        var saveData = new StatAllocationData
        {
            playerLevel = playerLevel,
            unallocatedPoints = unallocatedPoints
        };

        return JsonUtility.ToJson(saveData);
    }

    public void LoadSaveData(string data)
    {
        var saveData = JsonUtility.FromJson<StatAllocationData>(data);

        playerLevel = saveData.playerLevel;
        unallocatedPoints = saveData.unallocatedPoints;

        if (debugAllocation)
            Debug.Log($"[StatAllocation] Data loaded - Level: {playerLevel}, Unallocated: {unallocatedPoints}");
    }

    #endregion

    #region Level Management

    /// <summary>
    /// Directly set the player level (use for loading/debugging)
    /// </summary>
    public void SetLevel(int level)
    {
        int oldLevel = playerLevel;
        playerLevel = Mathf.Clamp(level, 1, maxLevel);
        if (oldLevel != playerLevel)
        {
            OnLevelChanged?.Invoke(oldLevel, playerLevel);
        }
    }

    /// <summary>
    /// Level up the player, granting stat points and entering pending mode
    /// </summary>
    public bool LevelUp()
    {
        if (playerLevel >= maxLevel)
        {
            Debug.LogWarning("[StatAllocation] Already at max level!");
            return false;
        }

        int oldLevel = playerLevel;
        playerLevel++;
        unallocatedPoints += POINTS_PER_LEVEL;

        // Enter pending mode automatically on level up
        BeginPendingChanges();

        OnLevelChanged?.Invoke(oldLevel, playerLevel);
        OnStatPointsChanged?.Invoke(unallocatedPoints);

        if (debugAllocation)
            Debug.Log($"[StatAllocation] Level up! {oldLevel} → {playerLevel}. Gained {POINTS_PER_LEVEL} points. Unallocated: {unallocatedPoints}");

        return true;
    }

    #endregion

    #region Pending Changes System

    /// <summary>
    /// Begin tracking pending changes (store current state)
    /// </summary>
    public void BeginPendingChanges()
    {
        if (coreStats == null) return;

        hasPendingChanges = true;
        pendingUnallocatedPoints = unallocatedPoints;
        pendingMind = coreStats.Mind.calculation.baseValue;
        pendingBody = coreStats.Body.calculation.baseValue;
        pendingSpirit = coreStats.Spirit.calculation.baseValue;
        pendingResilience = coreStats.Resilience.calculation.baseValue;
        pendingEndurance = coreStats.Endurance.calculation.baseValue;
        pendingInsight = coreStats.Insight.calculation.baseValue;

        if (debugAllocation)
            Debug.Log("[StatAllocation] Pending changes mode STARTED - changes can be cancelled");
    }

    /// <summary>
    /// Confirm and commit all pending changes
    /// </summary>
    public void ConfirmChanges()
    {
        if (!hasPendingChanges)
        {
            Debug.LogWarning("[StatAllocation] No pending changes to confirm!");
            return;
        }

        hasPendingChanges = false;

        OnChangesConfirmed?.Invoke();

        if (debugAllocation)
            Debug.Log("[StatAllocation] Changes CONFIRMED - stats locked in");
    }

    /// <summary>
    /// Cancel pending changes and revert to saved state
    /// </summary>
    public void CancelChanges()
    {
        if (!hasPendingChanges)
        {
            Debug.LogWarning("[StatAllocation] No pending changes to cancel!");
            return;
        }

        if (coreStats == null) return;

        unallocatedPoints = pendingUnallocatedPoints;
        coreStats.Mind.calculation.baseValue = pendingMind;
        coreStats.Body.calculation.baseValue = pendingBody;
        coreStats.Spirit.calculation.baseValue = pendingSpirit;
        coreStats.Resilience.calculation.baseValue = pendingResilience;
        coreStats.Endurance.calculation.baseValue = pendingEndurance;
        coreStats.Insight.calculation.baseValue = pendingInsight;

        // Recalculate all stats after reverting
        coreStats.Mind.calculation.RecalculateFinalStat();
        coreStats.Body.calculation.RecalculateFinalStat();
        coreStats.Spirit.calculation.RecalculateFinalStat();
        coreStats.Resilience.calculation.RecalculateFinalStat();
        coreStats.Endurance.calculation.RecalculateFinalStat();
        coreStats.Insight.calculation.RecalculateFinalStat();

        hasPendingChanges = false;

        OnChangesCancelled?.Invoke();
        OnStatPointsChanged?.Invoke(unallocatedPoints);

        if (debugAllocation)
            Debug.Log("[StatAllocation] Changes CANCELLED - reverted to saved state");
    }

    #endregion

    #region Stat Allocation

    /// <summary>
    /// Allocate a stat point to a specific core stat
    /// </summary>
    public bool AllocateStatPoint(string statName)
    {
        if (unallocatedPoints <= 0)
        {
            if (debugAllocation)
                Debug.LogWarning($"[StatAllocation] Cannot allocate {statName} - no points available!");
            return false;
        }

        if (coreStats == null)
        {
            Debug.LogError("[StatAllocation] CoreStats reference is null!");
            return false;
        }

        // Enter pending mode if not already in it
        if (!hasPendingChanges)
        {
            BeginPendingChanges();
        }

        bool success = false;

        switch (statName)
        {
            case "Mind":
                coreStats.Mind.calculation.baseValue += 1f;
                coreStats.Mind.calculation.RecalculateFinalStat();
                success = true;
                break;
            case "Body":
                coreStats.Body.calculation.baseValue += 1f;
                coreStats.Body.calculation.RecalculateFinalStat();
                success = true;
                break;
            case "Spirit":
                coreStats.Spirit.calculation.baseValue += 1f;
                coreStats.Spirit.calculation.RecalculateFinalStat();
                success = true;
                break;
            case "Resilience":
                coreStats.Resilience.calculation.baseValue += 1f;
                coreStats.Resilience.calculation.RecalculateFinalStat();
                success = true;
                break;
            case "Endurance":
                coreStats.Endurance.calculation.baseValue += 1f;
                coreStats.Endurance.calculation.RecalculateFinalStat();
                success = true;
                break;
            case "Insight":
                coreStats.Insight.calculation.baseValue += 1f;
                coreStats.Insight.calculation.RecalculateFinalStat();
                success = true;
                break;
            default:
                Debug.LogWarning($"[StatAllocation] Unknown stat: {statName}");
                return false;
        }

        if (success)
        {
            unallocatedPoints--;
            OnStatPointsChanged?.Invoke(unallocatedPoints);

            if (debugAllocation)
                Debug.Log($"[StatAllocation] +1 {statName}. Remaining points: {unallocatedPoints}");
        }

        return success;
    }

    /// <summary>
    /// Deallocate a stat point from a specific core stat
    /// </summary>
    public bool DeallocateStatPoint(string statName)
    {
        if (coreStats == null)
        {
            Debug.LogError("[StatAllocation] CoreStats reference is null!");
            return false;
        }

        // Enter pending mode if not already in it
        if (!hasPendingChanges)
        {
            BeginPendingChanges();
        }

        bool success = false;
        bool canDeallocate = false;

        switch (statName)
        {
            case "Mind":
                canDeallocate = coreStats.Mind.calculation.baseValue > MIN_STAT_VALUE;
                if (canDeallocate)
                {
                    coreStats.Mind.calculation.baseValue -= 1f;
                    coreStats.Mind.calculation.RecalculateFinalStat();
                    success = true;
                }
                break;
            case "Body":
                canDeallocate = coreStats.Body.calculation.baseValue > MIN_STAT_VALUE;
                if (canDeallocate)
                {
                    coreStats.Body.calculation.baseValue -= 1f;
                    coreStats.Body.calculation.RecalculateFinalStat();
                    success = true;
                }
                break;
            case "Spirit":
                canDeallocate = coreStats.Spirit.calculation.baseValue > MIN_STAT_VALUE;
                if (canDeallocate)
                {
                    coreStats.Spirit.calculation.baseValue -= 1f;
                    coreStats.Spirit.calculation.RecalculateFinalStat();
                    success = true;
                }
                break;
            case "Resilience":
                canDeallocate = coreStats.Resilience.calculation.baseValue > MIN_STAT_VALUE;
                if (canDeallocate)
                {
                    coreStats.Resilience.calculation.baseValue -= 1f;
                    coreStats.Resilience.calculation.RecalculateFinalStat();
                    success = true;
                }
                break;
            case "Endurance":
                canDeallocate = coreStats.Endurance.calculation.baseValue > MIN_STAT_VALUE;
                if (canDeallocate)
                {
                    coreStats.Endurance.calculation.baseValue -= 1f;
                    coreStats.Endurance.calculation.RecalculateFinalStat();
                    success = true;
                }
                break;
            case "Insight":
                canDeallocate = coreStats.Insight.calculation.baseValue > MIN_STAT_VALUE;
                if (canDeallocate)
                {
                    coreStats.Insight.calculation.baseValue -= 1f;
                    coreStats.Insight.calculation.RecalculateFinalStat();
                    success = true;
                }
                break;
            default:
                Debug.LogWarning($"[StatAllocation] Unknown stat: {statName}");
                return false;
        }

        if (!canDeallocate)
        {
            if (debugAllocation)
                Debug.LogWarning($"[StatAllocation] Cannot deallocate {statName} - already at minimum ({MIN_STAT_VALUE})");
            return false;
        }

        if (success)
        {
            unallocatedPoints++;
            OnStatPointsChanged?.Invoke(unallocatedPoints);

            if (debugAllocation)
                Debug.Log($"[StatAllocation] -1 {statName}. Remaining points: {unallocatedPoints}");
        }

        return success;
    }

    /// <summary>
    /// Check if we can allocate more points
    /// </summary>
    public bool CanAllocate()
    {
        return unallocatedPoints > 0;
    }

    /// <summary>
    /// Check if a specific stat can be deallocated
    /// </summary>
    public bool CanDeallocate(string statName)
    {
        if (coreStats == null) return false;

        switch (statName)
        {
            case "Mind": return coreStats.Mind.calculation.baseValue > MIN_STAT_VALUE;
            case "Body": return coreStats.Body.calculation.baseValue > MIN_STAT_VALUE;
            case "Spirit": return coreStats.Spirit.calculation.baseValue > MIN_STAT_VALUE;
            case "Resilience": return coreStats.Resilience.calculation.baseValue > MIN_STAT_VALUE;
            case "Endurance": return coreStats.Endurance.calculation.baseValue > MIN_STAT_VALUE;
            case "Insight": return coreStats.Insight.calculation.baseValue > MIN_STAT_VALUE;
            default: return false;
        }
    }

    #endregion

    #region Utility

    /// <summary>
    /// Get current stat value by name
    /// </summary>
    public float GetStatValue(string statName)
    {
        if (coreStats == null) return 0f;

        switch (statName)
        {
            case "Mind": return coreStats.Mind.FinalValue;
            case "Body": return coreStats.Body.FinalValue;
            case "Spirit": return coreStats.Spirit.FinalValue;
            case "Resilience": return coreStats.Resilience.FinalValue;
            case "Endurance": return coreStats.Endurance.FinalValue;
            case "Insight": return coreStats.Insight.FinalValue;
            default: return 0f;
        }
    }

    #endregion
}