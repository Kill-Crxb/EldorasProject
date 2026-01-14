using UnityEngine;
using System.Collections.Generic;
using NinjaGame.Stats;
using RPG.Factions;
using System;

/// <summary>
/// Damage Manager - Global damage configuration (Singleton)
/// 
/// Architecture Pattern:
/// DamageManager (Global) → DamageSystem (Per-Entity)
/// 
/// Mirrors:
/// - StatsManager → StatSystem
/// - ResourceManager → ResourceSystem
/// 
/// Responsibilities:
/// - Hold active DamageCalculationConfig
/// - Provide damage rules to all entities
/// - Validate stat references at startup
/// - Coordinate with FactionManager for PvP modifiers
/// - Server-authoritative in multiplayer
/// 
/// Benefits vs Hardcoded:
/// - Data-driven damage types
/// - Different configs per game mode (PvE vs PvP)
/// - Hot-reload damage rules at runtime
/// - No hardcoded stat IDs
/// 
/// Priority: 20 (After Stats and Resources)
/// 
/// Phase 1.7b: Universal Systems Consolidation + Manager Coordination
/// Created: January 2026
/// </summary>
public class DamageManager : MonoBehaviour, IGameManager, IManagerDependency
{
    #region Singleton (Simplified)

    private static DamageManager instance;
    public static DamageManager Instance => instance;

    #endregion

    #region IGameManager Implementation

    public string ManagerName => "Damage Manager";
    public int InitializationPriority => 20; // After Stats (0) and Resources (10)
    public bool IsEnabled => enabled;
    public bool IsInitialized { get; private set; }

    // Config versioning for hot-reload sync
    public int ConfigVersion { get; private set; }

    public void Initialize()
    {
        if (IsInitialized) return;

        instance = this;

        // Set active config (use first in list if not set)
        if (activeConfig == null && damageConfigs.Count > 0)
        {
            activeConfig = damageConfigs[0];
        }

        IsInitialized = true;

        if (debugLogging)
        {
            Debug.Log($"[{ManagerName}] Initialized with active config: {(activeConfig != null ? activeConfig.name : "None")}");
        }
    }

    public void LateInitialize()
    {
        // COORDINATION: Validate stat references with StatsManager
        ValidateDamageFormulas();
    }

    public void Shutdown()
    {
        if (debugLogging)
            Debug.Log($"[{ManagerName}] Shutdown complete");
    }

    // IManagerDependency implementation
    public IEnumerable<Type> DependsOn =>
        new[]
        {
            typeof(StatsManager),
            typeof(FactionManager)
        };

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();

        if (activeConfig == null)
        {
            result.IsFatal = true;
            result.Errors.Add("No active damage config assigned");
            return result;
        }

        if (activeConfig.damageTypes == null || activeConfig.damageTypes.Count == 0)
        {
            result.IsFatal = true;
            result.Errors.Add("Active config has no damage types defined");
            return result;
        }

        result.Info.Add($"Active config: {activeConfig.name} ({activeConfig.damageTypes.Count} damage types)");
        return result;
    }

    #endregion

    #region Inspector Fields

    [Header("Damage Configurations")]
    [Tooltip("All available damage calculation configs")]
    [SerializeField] private List<DamageCalculationConfig> damageConfigs = new List<DamageCalculationConfig>();

    [Tooltip("Currently active damage config")]
    [SerializeField] private DamageCalculationConfig activeConfig;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region Public API

    /// <summary>
    /// Get the active damage calculation config
    /// </summary>
    public DamageCalculationConfig ActiveConfig => activeConfig;

    /// <summary>
    /// Get configuration for a specific damage type
    /// </summary>
    public DamageTypeConfig GetDamageTypeConfig(DamageType damageType)
    {
        if (activeConfig == null)
        {
            Debug.LogError("[DamageManager] No active config! Returning default.");
            return new DamageTypeConfig { damageType = damageType };
        }

        return activeConfig.GetConfig(damageType);
    }

    /// <summary>
    /// Set the active damage config (e.g., switch between PvE and PvP configs)
    /// </summary>
    public void SetActiveConfig(DamageCalculationConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[DamageManager] Cannot set null config");
            return;
        }

        activeConfig = config;

        if (debugLogging)
        {
            Debug.Log($"[DamageManager] Active config changed to: {config.name}");
        }

        // Revalidate after config change
        if (IsInitialized)
        {
            ValidateDamageFormulas();
        }
    }

    /// <summary>
    /// Set active config by name
    /// </summary>
    public void SetActiveConfig(string configName)
    {
        var config = damageConfigs.Find(c => c.name == configName);
        if (config != null)
        {
            SetActiveConfig(config);
        }
        else
        {
            Debug.LogError($"[DamageManager] Config '{configName}' not found in available configs");
        }
    }

    #endregion

    #region Coordination: Stat Validation

    /// <summary>
    /// COORDINATION: Validate stat references with StatsManager
    /// </summary>
    private void ValidateDamageFormulas()
    {
        // Get StatsManager through ManagerBrain
        var stats = ManagerBrain.Instance?.Stats;
        if (stats == null)
        {
            Debug.LogError($"[{ManagerName}] StatsManager not available for validation!");
            return;
        }

        if (activeConfig == null)
        {
            Debug.LogWarning($"[{ManagerName}] No active config to validate");
            return;
        }

        bool allValid = true;

        foreach (var damageType in activeConfig.damageTypes)
        {
            if (damageType == null) continue;

            // Validate attacker stats
            foreach (var statId in damageType.attackerStatIds)
            {
                if (string.IsNullOrEmpty(statId)) continue;

                if (!stats.HasStat(statId))
                {
                    Debug.LogError($"[{ManagerName}] {damageType.damageType} damage " +
                                 $"references unknown attacker stat: {statId}");
                    allValid = false;
                }
            }

            // Validate defender stats
            foreach (var statId in damageType.defenderStatIds)
            {
                if (string.IsNullOrEmpty(statId)) continue;

                if (!stats.HasStat(statId))
                {
                    Debug.LogError($"[{ManagerName}] {damageType.damageType} mitigation " +
                                 $"references unknown defender stat: {statId}");
                    allValid = false;
                }
            }
        }

        if (allValid && debugLogging)
        {
            Debug.Log($"[{ManagerName}] All damage formula stat references validated");
        }
    }

    #endregion

    #region Coordination: Faction Damage Modifiers

    /// <summary>
    /// COORDINATION: Get faction-based damage modifier for PvP combat
    /// Queries FactionManager for relationship between factions
    /// </summary>
    /// <param name="attackerFaction">Attacker's faction</param>
    /// <param name="defenderFaction">Defender's faction</param>
    /// <returns>Damage multiplier (1.0 = normal damage)</returns>
    public float GetFactionDamageModifier(FactionType attackerFaction, FactionType defenderFaction)
    {
        var factions = ManagerBrain.Instance?.Factions;
        if (factions == null)
        {
            // FactionManager not available - use normal damage
            return 1f;
        }

        return factions.GetFactionDamageModifier(attackerFaction, defenderFaction);
    }

    #endregion

    #region Context Menu Helpers

    [ContextMenu("Print Active Config")]
    private void PrintActiveConfig()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageManager] Can only print in Play Mode");
            return;
        }

        if (activeConfig == null)
        {
            Debug.Log("[DamageManager] No active config");
            return;
        }

        Debug.Log("=== DamageManager: Active Config ===");
        Debug.Log($"Config: {activeConfig.name}");
        Debug.Log($"Damage Types: {activeConfig.damageTypes.Count}");

        foreach (var damageType in activeConfig.damageTypes)
        {
            Debug.Log($"\n  {damageType.damageType} Damage:");
            Debug.Log($"    Attacker Stats: {string.Join(", ", damageType.attackerStatIds)}");
            Debug.Log($"    Defender Stats: {string.Join(", ", damageType.defenderStatIds)}");
            Debug.Log($"    Can Crit: {damageType.canCrit}");
            Debug.Log($"    Mode: {damageType.calculationMode}");
        }
    }

    [ContextMenu("Validate Damage Formulas")]
    private void ContextValidateDamageFormulas()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageManager] Can only validate in Play Mode");
            return;
        }

        ValidateDamageFormulas();
    }

    #endregion


    #region Server Authority

    /// <summary>
    /// Check if we're running on server (for multiplayer authority)
    /// </summary>
    private bool IsServer()
    {
#if UNITY_SERVER
        return true;
#else
        // For single-player, always act as server
        // When networking added, replace with network check
        return true;
#endif
    }

    #endregion
}