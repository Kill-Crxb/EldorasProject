using UnityEngine;
using TMPro;

/// <summary>
/// Displays the 6 core stat values in the CoreStats panel.
/// Updates automatically when stats change.
/// </summary>
public class CoreStatsUI : MonoBehaviour
{
    [Header("Stat Value Displays")]
    [SerializeField] private TextMeshProUGUI mindAmount;
    [SerializeField] private TextMeshProUGUI bodyAmount;
    [SerializeField] private TextMeshProUGUI spiritAmount;
    [SerializeField] private TextMeshProUGUI insightAmount;
    [SerializeField] private TextMeshProUGUI enduranceAmount;
    [SerializeField] private TextMeshProUGUI resilienceAmount;

    [Header("References")]
    [SerializeField] private ControllerBrain brain;
    [SerializeField] private RPGCoreStats coreStats;

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
            // Try parent first
            brain = GetComponentInParent<ControllerBrain>();

            // Search by tag
            if (brain == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    brain = player.GetComponent<ControllerBrain>();
                }
            }

            // Find any in scene
            if (brain == null)
            {
                brain = FindFirstObjectByType<ControllerBrain>();
            }
        }

        if (brain != null)
        {
            if (coreStats == null)
                coreStats = brain.Stats?.CoreStats;
        }

        if (coreStats == null)
        {
            Debug.LogError("[CoreStatsUI] Missing RPGCoreStats! Cannot initialize.");
            return;
        }

        // Subscribe to stat change events
        coreStats.OnStatChanged += OnCoreStatChanged;

        // Initial display update
        RefreshAllStatDisplays();

        isInitialized = true;

        if (debugUI)
            Debug.Log($"[CoreStatsUI] Initialized successfully. Player: {brain.gameObject.name}");
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (coreStats != null)
            coreStats.OnStatChanged -= OnCoreStatChanged;
    }

    #region Event Handlers

    private void OnCoreStatChanged(string statName, float oldValue, float newValue)
    {
        RefreshStatDisplay(statName);

        if (debugUI)
            Debug.Log($"[CoreStatsUI] {statName} changed: {oldValue:F0} → {newValue:F0}");
    }

    #endregion

    #region Display Updates

    /// <summary>
    /// Refresh all stat displays
    /// </summary>
    private void RefreshAllStatDisplays()
    {
        if (coreStats == null) return;

        mindAmount?.SetText(coreStats.Mind.FinalValue.ToString("F0"));
        bodyAmount?.SetText(coreStats.Body.FinalValue.ToString("F0"));
        spiritAmount?.SetText(coreStats.Spirit.FinalValue.ToString("F0"));
        insightAmount?.SetText(coreStats.Insight.FinalValue.ToString("F0"));
        enduranceAmount?.SetText(coreStats.Endurance.FinalValue.ToString("F0"));
        resilienceAmount?.SetText(coreStats.Resilience.FinalValue.ToString("F0"));
    }

    /// <summary>
    /// Refresh a specific stat display
    /// </summary>
    private void RefreshStatDisplay(string statName)
    {
        if (coreStats == null) return;

        switch (statName.ToLower())
        {
            case "mind":
                mindAmount?.SetText(coreStats.Mind.FinalValue.ToString("F0"));
                break;
            case "body":
                bodyAmount?.SetText(coreStats.Body.FinalValue.ToString("F0"));
                break;
            case "spirit":
                spiritAmount?.SetText(coreStats.Spirit.FinalValue.ToString("F0"));
                break;
            case "insight":
                insightAmount?.SetText(coreStats.Insight.FinalValue.ToString("F0"));
                break;
            case "endurance":
                enduranceAmount?.SetText(coreStats.Endurance.FinalValue.ToString("F0"));
                break;
            case "resilience":
                resilienceAmount?.SetText(coreStats.Resilience.FinalValue.ToString("F0"));
                break;
        }
    }

    #endregion

    #region Inspector Helpers

    [ContextMenu("Force Refresh Display")]
    private void ForceRefresh()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CoreStatsUI] Can only refresh in Play Mode!");
            return;
        }

        RefreshAllStatDisplays();
        Debug.Log("[CoreStatsUI] Display refreshed manually");
    }

    #endregion
}