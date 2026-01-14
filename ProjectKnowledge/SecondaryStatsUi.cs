using UnityEngine;
using TMPro;
using System.Collections.Generic;


public class SecondaryStatsUI : MonoBehaviour
{
    [Header("Content Container")]
    [SerializeField] private Transform contentContainer;

    [Header("Stat Entry Prefab")]
    [SerializeField] private GameObject statEntryPrefab;

    [Header("References")]
    [SerializeField] private ControllerBrain brain;

    [Header("Display Settings")]
    [SerializeField] private bool showCombatStats = true;
    [SerializeField] private bool showDefenseStats = true;
    [SerializeField] private bool showResourceStats = true;

    [Header("Debug")]
    [SerializeField] private bool debugUI = false;

    private StatSystem statSystem;
    private bool isInitialized = false;
    private Dictionary<string, TextMeshProUGUI> statDisplays = new Dictionary<string, TextMeshProUGUI>();

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
            brain = GetComponentInParent<ControllerBrain>();

            if (brain == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    brain = player.GetComponent<ControllerBrain>();
                }
            }

            if (brain == null)
            {
                brain = FindFirstObjectByType<ControllerBrain>();
            }
        }

        if (brain == null)
        {
            Debug.LogError("[SecondaryStatsUI] Missing ControllerBrain! Cannot initialize.");
            return;
        }

        // Get StatSystem (replaces old RPGSecondaryStats)
        statSystem = brain.StatSystem;

        if (statSystem == null)
        {
            Debug.LogError("[SecondaryStatsUI] Missing StatSystem! Cannot initialize.");
            return;
        }

        // Subscribe to stat change events
        statSystem.OnStatChanged += OnStatChanged;

        // Create stat displays
        CreateStatDisplays();

        // Initial refresh
        RefreshAllStats();

        isInitialized = true;

        if (debugUI)
            Debug.Log($"[SecondaryStatsUI] Initialized successfully. Player: {brain.gameObject.name}");
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
        // Refresh specific stat if it's being displayed
        if (statDisplays.ContainsKey(statId))
        {
            RefreshStat(statId);

            if (debugUI)
                Debug.Log($"[SecondaryStatsUI] {statId} changed: {oldValue:F1} → {newValue:F1}");
        }
    }

    #endregion

    #region Stat Display Creation

    /// <summary>
    /// Create UI entries for all secondary stats
    /// </summary>
    private void CreateStatDisplays()
    {
        if (contentContainer == null)
        {
            Debug.LogError("[SecondaryStatsUI] Content Container not assigned!");
            return;
        }

        // Clear existing entries
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }
        statDisplays.Clear();

        // Combat Stats
        if (showCombatStats)
        {
            CreateStatEntry("Attack Power", "combat.attack_power");
            CreateStatEntry("Critical Chance", "combat.crit_chance");
            CreateStatEntry("Critical Damage", "combat.crit_damage");
        }

        // Defense Stats
        if (showDefenseStats)
        {
            CreateStatEntry("Armor", "combat.armor");
        }

        // Resource Stats
        if (showResourceStats)
        {
            CreateStatEntry("Max Health", "character.max_health");
            CreateStatEntry("Max Stamina", "character.max_stamina");
            CreateStatEntry("Max Mana", "character.max_mana");
        }
    }

    /// <summary>
    /// Create a single stat entry in the list
    /// </summary>
    private void CreateStatEntry(string displayName, string statId)
    {
        GameObject entry;

        // Use prefab if assigned, otherwise create simple default
        if (statEntryPrefab != null)
        {
            entry = Instantiate(statEntryPrefab, contentContainer);
        }
        else
        {
            entry = CreateDefaultStatEntry(displayName);
        }

        // Find the value text (assumes child TMP named "Value" or second TMP component)
        TextMeshProUGUI valueText = null;
        TextMeshProUGUI[] texts = entry.GetComponentsInChildren<TextMeshProUGUI>();

        if (texts.Length >= 2)
        {
            texts[0].SetText(displayName); // Label
            valueText = texts[1]; // Value
        }
        else if (texts.Length == 1)
        {
            valueText = texts[0];
        }

        if (valueText != null)
        {
            statDisplays[statId] = valueText;
        }
    }

    /// <summary>
    /// Create a default stat entry if no prefab is assigned
    /// </summary>
    private GameObject CreateDefaultStatEntry(string displayName)
    {
        GameObject entry = new GameObject($"{displayName}_Entry");
        entry.transform.SetParent(contentContainer, false);

        // Add horizontal layout
        var layout = entry.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 10f;

        // Create label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(entry.transform, false);
        var labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.SetText(displayName);
        labelText.fontSize = 14;

        // Create value
        GameObject valueObj = new GameObject("Value");
        valueObj.transform.SetParent(entry.transform, false);
        var valueText = valueObj.AddComponent<TextMeshProUGUI>();
        valueText.SetText("0");
        valueText.fontSize = 14;
        valueText.color = Color.yellow;

        return entry;
    }

    #endregion

    #region Display Updates

    /// <summary>
    /// Refresh all stat displays
    /// </summary>
    private void RefreshAllStats()
    {
        if (statSystem == null) return;

        foreach (var kvp in statDisplays)
        {
            RefreshStat(kvp.Key);
        }
    }

    /// <summary>
    /// Refresh a specific stat display
    /// </summary>
    private void RefreshStat(string statId)
    {
        if (statSystem == null || !statDisplays.ContainsKey(statId)) return;

        float value = statSystem.GetValue(statId);
        statDisplays[statId].SetText(value.ToString("F1"));
    }

    #endregion

    #region Inspector Helpers

    [ContextMenu("Rebuild Stat Displays")]
    private void RebuildDisplays()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SecondaryStatsUI] Can only rebuild in Play Mode!");
            return;
        }

        CreateStatDisplays();
        RefreshAllStats();
        Debug.Log("[SecondaryStatsUI] Stat displays rebuilt");
    }

    [ContextMenu("Force Refresh Stats")]
    private void ForceRefresh()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SecondaryStatsUI] Can only refresh in Play Mode!");
            return;
        }

        RefreshAllStats();
        Debug.Log("[SecondaryStatsUI] Stats refreshed manually");
    }

    #endregion
}