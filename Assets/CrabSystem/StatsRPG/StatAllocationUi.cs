using UnityEngine;
using TMPro;

/// <summary>
/// Manages the stat allocation controls panel.
/// Handles +/- buttons, confirm/cancel, and available points display.
/// Disables entire panel when no points are available.
/// Phase 1.7b: Updated to use RPGSystem
/// </summary>
public class StatAllocationUI : MonoBehaviour
{
    [Header("Allocation Panel")]
    [SerializeField] private GameObject allocationPanel;

    [Header("References")]
    [SerializeField] private ControllerBrain brain;
    [SerializeField] private RPGSystem allocation;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI availablePointsText;

    [Header("Debug")]
    [SerializeField] private bool debugUI = false;

    private bool isInitialized = false;

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // Auto-find references if not assigned
        if (brain == null)
        {
            // First try to find in parent (if UI is attached to player)
            brain = GetComponentInParent<ControllerBrain>();

            // If not found, search for player by tag
            if (brain == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    brain = player.GetComponent<ControllerBrain>();
                }
            }

            // Last resort - find any ControllerBrain in scene
            if (brain == null)
            {
                brain = FindFirstObjectByType<ControllerBrain>();
            }
        }

        // Get RPGSystem module
        if (brain != null)
        {
            if (allocation == null)
                allocation = brain.GetModule<RPGSystem>();
        }

        if (allocation == null)
        {
            Debug.LogError("[StatAllocationUI] Missing RPGSystem! Cannot initialize. " +
                          $"Brain: {(brain != null ? "Found" : "NULL")}");
            return;
        }

        // Subscribe to events (FIXED: Only subscribe to events that exist)
        allocation.OnStatPointsChanged += OnUnallocatedPointsChanged;
        allocation.OnLevelChanged += OnLevelChanged;

        // Initial update
        UpdateAllocationPanelState();
        UpdateAvailablePointsDisplay();

        isInitialized = true;

        if (debugUI)
            Debug.Log($"[StatAllocationUI] Initialized successfully. Player: {brain.gameObject.name}");
    }

    void OnDestroy()
    {
        // Unsubscribe from events (FIXED: Only unsubscribe from events we subscribed to)
        if (allocation != null)
        {
            allocation.OnStatPointsChanged -= OnUnallocatedPointsChanged;
            allocation.OnLevelChanged -= OnLevelChanged;
        }
    }

    #region Event Handlers

    private void OnUnallocatedPointsChanged(int points)
    {
        UpdateAvailablePointsDisplay();
        UpdateAllocationPanelState(); // Update panel state when points change

        if (debugUI)
            Debug.Log($"[StatAllocationUI] Unallocated points changed: {points}");
    }

    private void OnLevelChanged(int oldLevel, int newLevel)
    {
        // When level changes, update the panel state (it should show if we have points)
        UpdateAllocationPanelState();
        UpdateAvailablePointsDisplay();

        if (debugUI)
            Debug.Log($"[StatAllocationUI] Level changed: {oldLevel} → {newLevel}. Panel state updated.");
    }

    #endregion

    #region UI Updates

    /// <summary>
    /// Enable/disable entire allocation panel based on available points or pending changes
    /// </summary>
    private void UpdateAllocationPanelState()
    {
        if (allocation == null || allocationPanel == null) return;

        bool shouldShow = allocation.UnallocatedPoints > 0 || allocation.HasPendingChanges;
        allocationPanel.SetActive(shouldShow);

        if (debugUI)
            Debug.Log($"[StatAllocationUI] Allocation panel {(shouldShow ? "ENABLED" : "DISABLED")} " +
                     $"(Points: {allocation.UnallocatedPoints}, Pending: {allocation.HasPendingChanges})");
    }

    /// <summary>
    /// Update the available points display text
    /// </summary>
    private void UpdateAvailablePointsDisplay()
    {
        if (availablePointsText != null && allocation != null)
        {
            availablePointsText.SetText($"Available: {allocation.UnallocatedPoints}");
        }
    }

    #endregion

    #region Public Methods - Allocation (+ buttons)

    public void AllocateMind()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] AllocateMind +");
        allocation?.AllocateStatPoint("character.mind");
    }

    public void AllocateBody()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] AllocateBody +");
        allocation?.AllocateStatPoint("character.body");
    }

    public void AllocateSpirit()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] AllocateSpirit +");
        allocation?.AllocateStatPoint("character.spirit");
    }

    public void AllocateInsight()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] AllocateInsight +");
        allocation?.AllocateStatPoint("character.insight");
    }

    public void AllocateEndurance()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] AllocateEndurance +");
        allocation?.AllocateStatPoint("character.endurance");
    }

    public void AllocateResilience()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] AllocateResilience +");
        allocation?.AllocateStatPoint("character.resilience");
    }

    #endregion

    #region Public Methods - Deallocation (- buttons)

    public void DeallocateMind()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] DeallocateMind -");
        allocation?.DeallocateStatPoint("character.mind");
    }

    public void DeallocateBody()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] DeallocateBody -");
        allocation?.DeallocateStatPoint("character.body");
    }

    public void DeallocateSpirit()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] DeallocateSpirit -");
        allocation?.DeallocateStatPoint("character.spirit");
    }

    public void DeallocateInsight()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] DeallocateInsight -");
        allocation?.DeallocateStatPoint("character.insight");
    }

    public void DeallocateEndurance()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] DeallocateEndurance -");
        allocation?.DeallocateStatPoint("character.endurance");
    }

    public void DeallocateResilience()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] DeallocateResilience -");
        allocation?.DeallocateStatPoint("character.resilience");
    }

    #endregion

    #region Public Methods - Confirm/Cancel

    public void ConfirmAllocation()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] Confirm button pressed");

        if (allocation != null && allocation.HasPendingChanges)
        {
            allocation.ConfirmChanges();
            // OnStatPointsChanged event will trigger UI refresh
            UpdateAllocationPanelState();
        }
    }

    public void CancelAllocation()
    {
        if (debugUI) Debug.Log("[StatAllocationUI] Cancel button pressed");

        if (allocation != null && allocation.HasPendingChanges)
        {
            allocation.CancelChanges();
            // OnStatPointsChanged event will trigger UI refresh
            UpdateAllocationPanelState();
        }
    }

    #endregion

    #region Inspector Helpers

    [ContextMenu("Force Refresh UI")]
    private void ForceRefresh()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StatAllocationUI] Can only refresh in Play Mode!");
            return;
        }

        UpdateAllocationPanelState();
        UpdateAvailablePointsDisplay();
        Debug.Log("[StatAllocationUI] UI refreshed manually");
    }

    #endregion
}