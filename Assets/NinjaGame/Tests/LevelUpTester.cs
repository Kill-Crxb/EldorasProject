using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug utility for testing the RPG progression system.
/// Attach to a UI panel with a button to trigger level ups.
/// Phase 1.7b: Updated to use RPGSystem
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

    private RPGSystem rpgSystem; // Fixed: was statAllocation

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

        // Get RPGSystem module
        rpgSystem = playerBrain.GetModule<RPGSystem>(); // Fixed: was StatAllocationSystem
        if (rpgSystem == null)
        {
            Debug.LogError("[LevelUpTester] RPGSystem not found on player!", this); // Fixed: was StatAllocationSystem
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
            Debug.Log($"[LevelUpTester] Initialized - Current Level: {rpgSystem.CurrentLevel}/{rpgSystem.MaxLevel}"); // Fixed: PlayerLevel -> CurrentLevel
        }
    }

    void OnLevelUpButtonClicked()
    {
        if (rpgSystem == null) // Fixed: was statAllocation
        {
            Debug.LogError("[LevelUpTester] RPGSystem is null!"); // Fixed: was StatAllocationSystem
            return;
        }

        if (rpgSystem.CurrentLevel >= rpgSystem.MaxLevel) // Fixed: PlayerLevel -> CurrentLevel
        {
            Debug.LogWarning($"[LevelUpTester] Already at max level ({rpgSystem.MaxLevel})!");
            return;
        }

        int oldLevel = rpgSystem.CurrentLevel; // Fixed: PlayerLevel -> CurrentLevel

        // RPGSystem uses AddXP instead of LevelUp()
        // Calculate XP needed for next level and add it
        int xpNeeded = rpgSystem.XPToNextLevel - rpgSystem.CurrentXP;
        rpgSystem.AddXP(xpNeeded); // Fixed: Changed from LevelUp() to AddXP()

        if (rpgSystem.CurrentLevel > oldLevel) // Fixed: PlayerLevel -> CurrentLevel
        {
            Debug.Log($"[LevelUpTester] ✓ Level Up! {oldLevel} → {rpgSystem.CurrentLevel}"); // Fixed: PlayerLevel -> CurrentLevel
            Debug.Log($"[LevelUpTester] Unallocated Points: {rpgSystem.UnallocatedPoints}");
            UpdateButtonState();
        }
        else
        {
            Debug.LogWarning("[LevelUpTester] Level up failed!");
        }
    }

    void UpdateButtonState()
    {
        if (levelUpButton == null || rpgSystem == null) return; // Fixed: was statAllocation

        bool canLevelUp = rpgSystem.CurrentLevel < rpgSystem.MaxLevel; // Fixed: PlayerLevel -> CurrentLevel
        levelUpButton.interactable = canLevelUp;

        // Update button text - try TextMeshProUGUI first, then UI.Text
        var tmpText = levelUpButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        var uiText = levelUpButton.GetComponentInChildren<UnityEngine.UI.Text>();

        string newText;
        if (canLevelUp)
        {
            newText = $"LEVEL UP ({rpgSystem.CurrentLevel}/{rpgSystem.MaxLevel})"; // Fixed: PlayerLevel -> CurrentLevel
        }
        else
        {
            newText = $"MAX LEVEL ({rpgSystem.MaxLevel})";
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

        if (rpgSystem != null) // Fixed: was statAllocation
        {
            rpgSystem.SetLevel(1);
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

        if (rpgSystem != null) // Fixed: was statAllocation
        {
            rpgSystem.SetLevel(10);
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

        if (rpgSystem == null || playerBrain == null) return; // Fixed: was statAllocation

        Debug.Log("=== PLAYER STATS ===");
        Debug.Log($"Level: {rpgSystem.CurrentLevel}/{rpgSystem.MaxLevel}"); // Fixed: PlayerLevel -> CurrentLevel
        Debug.Log($"XP: {rpgSystem.CurrentXP}/{rpgSystem.XPToNextLevel}"); // Added XP display
        Debug.Log($"Unallocated Points: {rpgSystem.UnallocatedPoints}");
        Debug.Log($"Has Pending Changes: {rpgSystem.HasPendingChanges}");

        // Use StatSystem API to display core stats
        var statSystem = playerBrain.Stats;
        if (statSystem != null)
        {
            Debug.Log($"\nCore Stats:");
            Debug.Log($"  Mind: {statSystem.Mind}");
            Debug.Log($"  Body: {statSystem.Body}");
            Debug.Log($"  Spirit: {statSystem.Spirit}");
            Debug.Log($"  Resilience: {statSystem.Resilience}");
            Debug.Log($"  Endurance: {statSystem.Endurance}");
            Debug.Log($"  Insight: {statSystem.Insight}");
        }
        else
        {
            Debug.LogWarning("[LevelUpTester] StatSystem not found!");
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