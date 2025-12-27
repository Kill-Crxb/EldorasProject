using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug utility for testing the stat allocation system.
/// Attach to a UI panel with a button to trigger level ups.
/// </summary>
public class LevelUpTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ControllerBrain playerBrain;
    [SerializeField] private Button levelUpButton;

    [Header("Auto-Find Player")]
    [SerializeField] private bool autoFindPlayer = true;

    [Header("Settings")]
    [SerializeField] private bool debugLogs = true;

    private StatAllocationSystem statAllocation;
    private bool isInitialized = false;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        if (isInitialized) return;

        // Auto-find player if enabled
        if (autoFindPlayer && playerBrain == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerBrain = player.GetComponentInChildren<ControllerBrain>();
            }
        }

        // Validate brain reference
        if (playerBrain == null)
        {
            Debug.LogError("[LevelUpTester] No player brain assigned! Assign manually or tag player as 'Player'", this);
            enabled = false;
            return;
        }

        // Get stat allocation system
        statAllocation = playerBrain.Stats?.Allocation;
        if (statAllocation == null)
        {
            Debug.LogError("[LevelUpTester] StatAllocationSystem not found on player!", this);
            enabled = false;
            return;
        }

        // Wire up button
        if (levelUpButton == null)
        {
            levelUpButton = GetComponentInChildren<Button>();
        }

        if (levelUpButton != null)
        {
            levelUpButton.onClick.RemoveAllListeners();
            levelUpButton.onClick.AddListener(OnLevelUpButtonClicked);
        }
        else
        {
            Debug.LogWarning("[LevelUpTester] No level up button assigned!", this);
        }

        UpdateButtonState();

        isInitialized = true;

        if (debugLogs)
        {
            Debug.Log($"[LevelUpTester] Initialized - Current Level: {statAllocation.PlayerLevel}/{statAllocation.MaxLevel}");
        }
    }

    void OnLevelUpButtonClicked()
    {
        if (statAllocation == null)
        {
            Debug.LogError("[LevelUpTester] StatAllocationSystem is null!");
            return;
        }

        if (statAllocation.PlayerLevel >= statAllocation.MaxLevel)
        {
            Debug.LogWarning($"[LevelUpTester] Already at max level ({statAllocation.MaxLevel})!");
            return;
        }

        int oldLevel = statAllocation.PlayerLevel;
        bool success = statAllocation.LevelUp();

        if (success)
        {
            Debug.Log($"[LevelUpTester] ✓ Level Up! {oldLevel} → {statAllocation.PlayerLevel}");
            Debug.Log($"[LevelUpTester] Unallocated Points: {statAllocation.UnallocatedPoints}");
            UpdateButtonState();
        }
        else
        {
            Debug.LogWarning("[LevelUpTester] Level up failed!");
        }
    }

    void UpdateButtonState()
    {
        if (levelUpButton == null || statAllocation == null) return;

        bool canLevelUp = statAllocation.PlayerLevel < statAllocation.MaxLevel;
        levelUpButton.interactable = canLevelUp;

        // Update button text - try TextMeshProUGUI first, then UI.Text
        var tmpText = levelUpButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        var uiText = levelUpButton.GetComponentInChildren<UnityEngine.UI.Text>();

        string newText;
        if (canLevelUp)
        {
            newText = $"LEVEL UP ({statAllocation.PlayerLevel}/{statAllocation.MaxLevel})";
        }
        else
        {
            newText = $"MAX LEVEL ({statAllocation.MaxLevel})";
        }

        if (tmpText != null)
        {
            tmpText.text = newText;
        }
        else if (uiText != null)
        {
            uiText.text = newText;
        }
    }

    [ContextMenu("Level Up Player")]
    void ContextLevelUp()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LevelUpTester] Must be in Play mode!");
            return;
        }

        if (!isInitialized)
        {
            Initialize();
        }

        OnLevelUpButtonClicked();
    }

    [ContextMenu("Reset Level to 1")]
    void ContextResetLevel()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LevelUpTester] Must be in Play mode!");
            return;
        }

        if (statAllocation != null)
        {
            statAllocation.SetLevel(1);
            Debug.Log("[LevelUpTester] Reset player to level 1");
            UpdateButtonState();
        }
    }

    [ContextMenu("Set Level to 10")]
    void ContextSetLevel10()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LevelUpTester] Must be in Play mode!");
            return;
        }

        if (statAllocation != null)
        {
            statAllocation.SetLevel(10);
            Debug.Log("[LevelUpTester] Set player to level 10");
            UpdateButtonState();
        }
    }

    [ContextMenu("Print Current Stats")]
    void ContextPrintStats()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LevelUpTester] Must be in Play mode!");
            return;
        }

        if (statAllocation == null || playerBrain == null) return;

        Debug.Log("=== PLAYER STATS ===");
        Debug.Log($"Level: {statAllocation.PlayerLevel}/{statAllocation.MaxLevel}");
        Debug.Log($"Unallocated Points: {statAllocation.UnallocatedPoints}");
        Debug.Log($"Has Pending Changes: {statAllocation.HasPendingChanges}");

        var coreStats = playerBrain.Stats?.CoreStats;
        if (coreStats != null)
        {
            Debug.Log($"\nCore Stats:");
            Debug.Log($"  Mind: {coreStats.Mind.FinalValue}");
            Debug.Log($"  Body: {coreStats.Body.FinalValue}");
            Debug.Log($"  Spirit: {coreStats.Spirit.FinalValue}");
            Debug.Log($"  Resilience: {coreStats.Resilience.FinalValue}");
            Debug.Log($"  Endurance: {coreStats.Endurance.FinalValue}");
            Debug.Log($"  Insight: {coreStats.Insight.FinalValue}");
        }
    }

    void OnValidate()
    {
        // Try to find button if not assigned
        if (levelUpButton == null)
        {
            levelUpButton = GetComponentInChildren<Button>();
        }
    }
}