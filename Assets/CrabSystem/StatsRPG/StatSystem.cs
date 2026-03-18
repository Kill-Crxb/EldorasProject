using UnityEngine;
using System;
using System.Collections.Generic;
using NinjaGame.Stats;
using System.Linq;

#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Universal Stat System - Per-Entity Stat Values
/// Follows the same architectural pattern as IdentitySystem, MovementSystem, AnimationSystem.
/// 
/// Phase 1.7b Refactor: Now uses StatsManager for schema definitions
/// 
/// Responsibilities:
/// - Hold RUNTIME stat values for this entity
/// - Initialize StatEngine with schemas from StatsManager
/// - Provide interface for other modules to query/modify stats
/// - Handle stat persistence (ISaveable)
/// - Support hot-reload of stat schemas
/// 
/// NOT Responsible For:
/// - Storing schema definitions (that's StatsManager)
/// - Defining what stats exist (that's StatsManager)
/// 
/// Architecture:
/// StatsManager (Global) → Provides schema definitions
///     ↓
/// StatsSystem (Per-Entity) → Holds runtime values
///     ↓
/// Items/Buffs → Apply modifiers
/// 
/// Integration:
/// - ControllerBrain.Stats (direct property access)
/// - Modules query stats via brain.Stats.GetValue("character.health")
/// - Items/buffs apply modifiers via brain.Stats.AddFlatModifier(...)
/// 
/// Phase 1.7b: System Consolidation
/// Updated: January 09, 2026
/// </summary>
public partial class StatSystem : MonoBehaviour, IBrainModule, ISaveable
{
    [Header("Stat Configuration")]
    [Tooltip("Schema IDs to load from StatsManager (e.g., 'RPGCoreStats', 'RPGCombatStats')")]
    [SerializeField] private List<string> schemaIds = new List<string>();
    [SerializeField] private bool autoLoadSchemas = true;

    [Header("Runtime Settings")]
    [SerializeField] private bool hotReloadEnabled = true;
    [SerializeField] private bool debugLogging = false;

    [Header("NPC Stat Masking (Optional)")]
    [Tooltip("For NPCs, hide stats they don't use (e.g., bears don't show magic stats)")]
    [SerializeField] private StatMaskFlags enabledStats = StatMaskFlags.CalculateAll;

    // Core engine (holds this entity's stat values)
    private StatEngine engine;
    private ControllerBrain brain;

    // Quick access caches (code-generated properties in StatSystem_Generated.cs)
    private Dictionary<string, StatNode> quickAccessCache = new Dictionary<string, StatNode>();

    // Events
    public event Action<string, float, float> OnStatChanged;

    // Properties
    public bool IsEnabled { get; set; } = true;
    public StatEngine Engine => engine;

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Verify StatsManager exists
        if (StatsManager.Instance == null)
        {
            Debug.LogError($"[StatSystem] StatsManager not found! Create a StatsManager under Managers GameObject. Entity: {brain.name}");
            return;
        }

        // Create engine (this entity's runtime stat values)
        engine = new StatEngine(debugLogging);
        engine.OnStatChanged += (statId, oldVal, newVal) => OnStatChanged?.Invoke(statId, oldVal, newVal);

        // Load stat definitions from StatsManager
        if (autoLoadSchemas)
        {
            LoadStatSchemas();
        }

        // Initial calculation
        engine.RecalculateAll();

        if (debugLogging)
            Debug.Log($"[StatSystem] Initialized with {schemaIds.Count} schemas on {brain.name}");
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        // Hot-reload support (editor only)
#if UNITY_EDITOR
        if (hotReloadEnabled)
        {
            // Use new Input System (Unity.InputSystem)
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                ReloadStatSchemas();
            }
        }
#endif
    }

    #endregion

    #region Schema Loading

    /// <summary>
    /// Load all stat definitions from StatsManager
    /// Phase 1.7b: Now pulls schemas from global StatsManager instead of local list
    /// </summary>
    private void LoadStatSchemas()
    {
        if (StatsManager.Instance == null)
        {
            Debug.LogError($"[StatSystem] StatsManager not found! Cannot load schemas. Entity: {brain.name}");
            return;
        }

        if (schemaIds == null || schemaIds.Count == 0)
        {
            Debug.LogWarning($"[StatSystem] No schema IDs assigned to {brain.name}! Assign schema IDs in inspector (e.g., 'RPGCoreStats').");
            return;
        }

        int loadedCount = 0;

        foreach (var schemaId in schemaIds)
        {
            if (string.IsNullOrEmpty(schemaId))
            {
                Debug.LogWarning($"[StatSystem] Empty schema ID in list for {brain.name}, skipping.");
                continue;
            }

            // Get schema from global StatsManager
            var schema = StatsManager.Instance.GetSchema(schemaId);

            if (schema == null)
            {
                Debug.LogWarning($"[StatSystem] Schema '{schemaId}' not found in StatsManager! Entity: {brain.name}");
                continue;
            }

            // Load this schema's stats
            LoadSchema(schema);
            loadedCount++;
        }

        if (debugLogging)
            Debug.Log($"[StatSystem] Loaded {loadedCount}/{schemaIds.Count} schemas from StatsManager for {brain.name}");
    }

    /// <summary>
    /// Load a single stat schema into this entity's engine
    /// </summary>
    private void LoadSchema(StatSchema schema)
    {
        foreach (var statDef in schema.stats)
        {
            // Check stat masking (for NPCs - skip stats they don't use)
            if (!IsStatEnabled(statDef.statId))
            {
                if (debugLogging)
                    Debug.Log($"[StatSystem] Skipping masked stat: {statDef.statId} on {brain.name}");
                continue;
            }

            // Create stat node WITH CATEGORY
            var stat = new StatNode(
                statDef.statId,
                statDef.displayName,
                statDef.baseValue,
                statDef.formula,
                statDef.category
            );

            stat.description = statDef.description;

            // Register with this entity's engine
            engine.RegisterStat(stat);

            // Cache for quick access
            quickAccessCache[statDef.statId] = stat;

           
        }
    }

    /// <summary>
    /// Reload schemas (hot-reload support)
    /// </summary>
    private void ReloadStatSchemas()
    {
        Debug.Log($"[StatSystem] Hot-reloading stat schemas for {brain.name}...");

        // Clear engine
        engine = new StatEngine(debugLogging);
        engine.OnStatChanged += (statId, oldVal, newVal) => OnStatChanged?.Invoke(statId, oldVal, newVal);
        quickAccessCache.Clear();

        // Reload from StatsManager
        LoadStatSchemas();
        engine.RecalculateAll();

        Debug.Log($"[StatSystem] Hot-reload complete for {brain.name}!");
    }

    #endregion

    #region Stat Access

    /// <summary>
    /// Get final value of a stat (after all modifiers)
    /// </summary>
    public float GetValue(string statId, float defaultValue = 0f)
    {
        return engine.GetValue(statId, defaultValue);
    }

    /// <summary>
    /// Get base value of a stat (before modifiers)
    /// </summary>
    public float GetBaseValue(string statId, float defaultValue = 0f)
    {
        return engine.GetBaseValue(statId, defaultValue);
    }

    /// <summary>
    /// Set base value directly
    /// </summary>
    public void SetBaseValue(string statId, float value)
    {
        engine.SetBaseValue(statId, value);
    }

    /// <summary>
    /// Get stat node directly (for advanced use)
    /// </summary>
    public StatNode GetStat(string statId)
    {
        return engine.GetStat(statId);
    }

    /// <summary>
    /// Check if stat exists
    /// </summary>
    public bool HasStat(string statId)
    {
        return engine.HasStat(statId);
    }

    #endregion

    #region Modifier Management

    /// <summary>
    /// Add a flat modifier from a source (item, buff, talent)
    /// Example: AddFlatModifier("combat.damage", "sword_123", 50) → +50 damage
    /// </summary>
    public void AddFlatModifier(string statId, string sourceId, float value)
    {
        engine.AddFlatModifier(statId, sourceId, value);
    }

    /// <summary>
    /// Add a percentage modifier (0.25 = +25%)
    /// Example: AddPercentModifier("combat.damage", "buff_123", 0.25f) → +25% damage
    /// </summary>
    public void AddPercentModifier(string statId, string sourceId, float percent)
    {
        engine.AddPercentModifier(statId, sourceId, percent);
    }

    /// <summary>
    /// Add a contribution bonus (e.g., "+2 crit per point of Insight")
    /// Example: AddContributionBonus("character.insight", "talent_123", "combat.crit_chance", 2f)
    /// </summary>
    public void AddContributionBonus(string statId, string sourceId, string targetStatId, float multiplier)
    {
        engine.AddContributionBonus(statId, sourceId, targetStatId, multiplier);
    }

    /// <summary>
    /// Remove all modifiers from a specific source
    /// Example: RemoveAllModifiersFromSource("sword_123") → Removes all sword modifiers
    /// </summary>
    public void RemoveAllModifiersFromSource(string sourceId)
    {
        engine.RemoveAllModifiersFromSource(sourceId);
    }

    #endregion

    #region Stat Masking (NPC Optimization)

    /// <summary>
    /// Check if a stat should be calculated for this entity
    /// Allows NPCs to skip stats they don't use (e.g., bears don't need magic stats)
    /// </summary>
    private bool IsStatEnabled(string statId)
    {
        // If masking disabled, enable all stats
        if (enabledStats == StatMaskFlags.CalculateAll)
            return true;

        // Map stat IDs to mask flags
        if (statId.Contains("magic") && !enabledStats.HasFlag(StatMaskFlags.MagicalPower))
            return false;

        if (statId.Contains("physical") && !enabledStats.HasFlag(StatMaskFlags.PhysicalPower))
            return false;

        // Add more mappings as needed
        return true;
    }

    #endregion

    #region ISaveable Implementation

    public string GetSaveId() => "stats";

    public int GetSaveVersion() => 1;

    public string GetSaveData()
    {
        var saveData = new StatSystemSaveData();
        saveData.statValues = new Dictionary<string, float>();

        // Save only base values (modifiers come from items/buffs and are re-applied)
        foreach (var statId in engine.GetAllStatIds())
        {
            var stat = engine.GetStat(statId);
            if (stat != null && string.IsNullOrEmpty(stat.formula))
            {
                // Only save stats with no formula (they have independent base values)
                saveData.statValues[statId] = stat.baseValue;
            }
        }

        return JsonUtility.ToJson(saveData);
    }

    public void LoadSaveData(string data)
    {
        var saveData = JsonUtility.FromJson<StatSystemSaveData>(data);

        if (saveData?.statValues != null)
        {
            foreach (var kvp in saveData.statValues)
            {
                engine.SetBaseValue(kvp.Key, kvp.Value);
            }

            // Recalculate after loading
            engine.RecalculateAll();

            if (debugLogging)
                Debug.Log($"[StatSystem] Loaded {saveData.statValues.Count} stat base values from save for {brain.name}");
        }
    }

    [System.Serializable]
    private class StatSystemSaveData
    {
        public Dictionary<string, float> statValues;
    }

    #endregion


    #region Debug & Utilities

    public void EnableDebugLogging(bool enable)
    {
        debugLogging = enable;
        engine?.EnableDebugLogging(enable);
    }

    [ContextMenu("Print All Stats")]
    public void PrintAllStats()
    {
        if (engine != null)
        {
            Debug.Log(engine.GetDebugSummary());
        }
    }

    [ContextMenu("Force Recalculate All")]
    public void ForceRecalculateAll()
    {
        engine?.ForceRecalculateAll();
        Debug.Log($"[StatSystem] Forced recalculation complete for {brain.name}");
    }

    [ContextMenu("Debug: Validate StatsManager Connection")]
    public void DebugValidateStatsManagerConnection()
    {
        Debug.Log($"=== STATS SYSTEM VALIDATION ({brain.name}) ===");

        // Check StatsManager
        if (StatsManager.Instance == null)
        {
            Debug.LogError("❌ StatsManager.Instance is NULL! Create a StatsManager in the scene.");
        }
        else
        {
            Debug.Log("✅ StatsManager.Instance exists");
            Debug.Log($"   Available schemas: {string.Join(", ", StatsManager.Instance.GetSchemaIds())}");
        }

        // Check schema IDs
        if (schemaIds == null || schemaIds.Count == 0)
        {
            Debug.LogError("❌ No schema IDs assigned! Assign schema IDs in inspector.");
        }
        else
        {
            Debug.Log($"✅ {schemaIds.Count} schema IDs assigned");

            foreach (var schemaId in schemaIds)
            {
                if (string.IsNullOrEmpty(schemaId))
                {
                    Debug.LogWarning("⚠️ Empty schema ID in list");
                }
                else if (StatsManager.Instance != null)
                {
                    var schema = StatsManager.Instance.GetSchema(schemaId);
                    if (schema == null)
                        Debug.LogWarning($"⚠️ Schema '{schemaId}' not found in StatsManager");
                    else
                        Debug.Log($"   ✅ '{schemaId}' ({schema.stats.Count} stats)");
                }
            }
        }

        // Check engine
        if (engine == null)
        {
            Debug.LogWarning("⚠️ StatEngine not initialized (call Initialize() first)");
        }
        else
        {
            var allStatIds = engine.GetAllStatIds();
            Debug.Log($"✅ StatEngine initialized with {allStatIds.Count()} stats");
        }

        Debug.Log("=== VALIDATION COMPLETE ===");
    }

    #endregion
}