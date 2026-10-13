using UnityEngine;
using System.Collections.Generic;
using CrabSystem.Stats;
using CrabSystem.Factions;
using System;

// Damage Manager - Global damage configuration (Singleton)
//
// Architecture Pattern:
// DamageManager (Global) → DamageSystem (Per-Entity)
//
// Mirrors:
// - StatsManager → StatSystem
// - ResourceManager → ResourceSystem
//
// Responsibilities:
// - Hold active DamageCalculationConfig
// - Provide damage rules to all entities
// - Validate stat references at startup
// - Coordinate with FactionManager for PvP modifiers
// - Server-authoritative in multiplayer
//
// Benefits vs Hardcoded:
// - Data-driven damage types
// - Different configs per game mode (PvE vs PvP)
// - Hot-reload damage rules at runtime
// - No hardcoded stat IDs
//
// Priority: 20 (After Stats and Resources)
//
// Phase 1.7b: Universal Systems Consolidation + Manager Coordination

namespace CrabSystem
{
    public class DamageManager : MonoBehaviour, IGameManager, IManagerDependency
    {
        private static DamageManager instance;
        public static DamageManager Instance => instance;

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

        [Header("Damage Configurations")]
        [Tooltip("All available damage calculation configs")]
        [SerializeField] private List<DamageCalculationConfig> damageConfigs = new List<DamageCalculationConfig>();

        [Tooltip("Currently active damage config")]
        [SerializeField] private DamageCalculationConfig activeConfig;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        public DamageCalculationConfig ActiveConfig => activeConfig;

        // Set the active damage config (e.g., switch between PvE and PvP configs)
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

        // COORDINATION: Validate stat references with StatsManager
        private void ValidateDamageFormulas()
        {
            var stats = StatsManager.Instance;
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
                        Debug.LogError($"[{ManagerName}] {damageType.id} damage " +
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
                        Debug.LogError($"[{ManagerName}] {damageType.id} mitigation " +
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

        // COORDINATION: Get faction-based damage modifier for combat.
        // Reads the damage multiplier from the relationship entry in FactionRelationships.
        public float GetFactionDamageModifier(FactionDefinition attackerFaction, FactionDefinition defenderFaction)
        {
            return FactionManager.GetDamageModifier(attackerFaction, defenderFaction);
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

        // Check if we're running on server (for multiplayer authority)
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
    }
}
