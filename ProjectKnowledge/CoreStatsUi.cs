using UnityEngine;
using TMPro;

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

    [Header("Debug")]
    [SerializeField] private bool debugUI = false;

    private StatSystem statSystem;
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

        if (brain == null)
        {
            Debug.LogError("[CoreStatsUI] Missing ControllerBrain! Cannot initialize.");
            return;
        }

        // Get StatSystem (replaces old RPGCoreStats)
        statSystem = brain.StatSystem;

        if (statSystem == null)
        {
            Debug.LogError("[CoreStatsUI] Missing StatSystem! Cannot initialize.");
            return;
        }

        // Subscribe to stat change events
        statSystem.OnStatChanged += OnStatChanged;

        // Initial display update
        RefreshAllStatDisplays();

        isInitialized = true;

        if (debugUI)
            Debug.Log($"[CoreStatsUI] Initialized successfully. Player: {brain.gameObject.name}");
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (statSystem != null)
            statSystem.OnStatChanged -= OnStatChanged;
    }

    #region Event Handlers

    private void OnStatChanged(string statId, float oldValue, float newValue)
    {
        // Only refresh if it's a core stat
        if (statId.StartsWith("character."))
        {
            RefreshStatDisplay(statId);

            if (debugUI)
                Debug.Log($"[CoreStatsUI] {statId} changed: {oldValue:F0} → {newValue:F0}");
        }
    }

    #endregion

    #region Display Updates

    /// <summary>
    /// Refresh all stat displays
    /// </summary>
    private void RefreshAllStatDisplays()
    {
        if (statSystem == null) return;

        // Using generated properties (from StatSystem_Generated.cs)
        mindAmount?.SetText(statSystem.Mind.ToString("F0"));
        bodyAmount?.SetText(statSystem.Body.ToString("F0"));
        spiritAmount?.SetText(statSystem.Spirit.ToString("F0"));
        insightAmount?.SetText(statSystem.Insight.ToString("F0"));
        enduranceAmount?.SetText(statSystem.Endurance.ToString("F0"));
        resilienceAmount?.SetText(statSystem.Resilience.ToString("F0"));

        // Alternative approach using string access:
        // mindAmount?.SetText(statSystem.GetValue("character.mind").ToString("F0"));
        // bodyAmount?.SetText(statSystem.GetValue("character.body").ToString("F0"));
        // etc...
    }

    /// <summary>
    /// Refresh a specific stat display
    /// </summary>
    private void RefreshStatDisplay(string statId)
    {
        if (statSystem == null) return;

        // Match against full stat ID
        switch (statId)
        {
            case "character.mind":
                mindAmount?.SetText(statSystem.Mind.ToString("F0"));
                break;
            case "character.body":
                bodyAmount?.SetText(statSystem.Body.ToString("F0"));
                break;
            case "character.spirit":
                spiritAmount?.SetText(statSystem.Spirit.ToString("F0"));
                break;
            case "character.insight":
                insightAmount?.SetText(statSystem.Insight.ToString("F0"));
                break;
            case "character.endurance":
                enduranceAmount?.SetText(statSystem.Endurance.ToString("F0"));
                break;
            case "character.resilience":
                resilienceAmount?.SetText(statSystem.Resilience.ToString("F0"));
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