using UnityEngine;
using System.Collections.Generic;

namespace NinjaGame.Stats
{


    /// <summary>
    /// Data-driven configuration for damage calculation.
    /// Defines which stats affect which damage types and how armor/resistances work.
    /// 
    /// This replaces hardcoded damage logic in DamageModule with data-driven rules.
    /// 
    /// Examples:
    /// - Physical damage: Uses character.attack_power, affected by armor
    /// - Fire damage: Uses magic.spell_power, affected by fire_resistance, ignores armor
    /// - True damage: Uses character.attack_power, ignores ALL defenses
    /// 
    /// Used by DamageModule for Stage 2 & 3 calculations (Phase 1.7 integration)
    /// </summary>
    [CreateAssetMenu(fileName = "Damage Calculation Config", menuName = "NinjaGame/Stats/Damage Calculation Config")]
    public class DamageCalculationConfig : ScriptableObject
    {


        [Header("Damage Type Definitions")]
        public List<DamageTypeConfig> damageTypes = new List<DamageTypeConfig>();

        [Header("Default Settings")]
        [Tooltip("Fallback config if damage type not found")]
        public DamageTypeConfig defaultConfig;

        /// <summary>
        /// Get configuration for a specific damage type
        /// </summary>
        public DamageTypeConfig GetConfig(DamageType damageType)
        {
            foreach (var config in damageTypes)
            {
                if (config.damageType == damageType)
                    return config;
            }

            // Fallback to default
            return defaultConfig ?? new DamageTypeConfig { damageType = damageType };
        }

        /// <summary>
        /// Query which stats to use for attacker bonuses
        /// </summary>
        public List<string> GetAttackerStatIds(DamageType damageType)
        {
            var config = GetConfig(damageType);
            return config.attackerStatIds;
        }

        /// <summary>
        /// Query which stats to use for defender mitigation
        /// </summary>
        public List<string> GetDefenderStatIds(DamageType damageType)
        {
            var config = GetConfig(damageType);
            return config.defenderStatIds;
        }

        /// <summary>
        /// Check if damage type can crit
        /// </summary>
        public bool CanCrit(DamageType damageType)
        {
            var config = GetConfig(damageType);
            return config.canCrit;
        }

        /// <summary>
        /// Get damage calculation mode
        /// </summary>
        public DamageCalculationMode GetCalculationMode(DamageType damageType)
        {
            var config = GetConfig(damageType);
            return config.calculationMode;
        }

        #region Editor Helpers

        [ContextMenu("Add Default Damage Types")]
        private void AddDefaultDamageTypes()
        {
            damageTypes.Clear();

            // Physical damage
            damageTypes.Add(new DamageTypeConfig
            {
                damageType = DamageType.Physical,
                displayName = "Physical",
                description = "Standard physical damage affected by armor",
                canCrit = true,
                calculationMode = DamageCalculationMode.Standard,
                attackerStatIds = new List<string> { "combat.attack_power", "combat.penetration" },
                defenderStatIds = new List<string> { "combat.armor" },
                attackerStatMultipliers = new List<float> { 1.0f, 0.5f },
                defenderStatMultipliers = new List<float> { 1.0f }
            });

            // Fire damage
            damageTypes.Add(new DamageTypeConfig
            {
                damageType = DamageType.Fire,
                displayName = "Fire",
                description = "Magical fire damage that ignores armor",
                canCrit = true,
                calculationMode = DamageCalculationMode.Standard,
                attackerStatIds = new List<string> { "magic.spell_power" },
                defenderStatIds = new List<string> { "combat.resistance", "magic.fire_resistance" },
                attackerStatMultipliers = new List<float> { 1.0f },
                defenderStatMultipliers = new List<float> { 0.5f, 1.0f }
            });

            // Lightning damage
            damageTypes.Add(new DamageTypeConfig
            {
                damageType = DamageType.Lightning,
                displayName = "Lightning",
                description = "Magical lightning damage with high crit chance",
                canCrit = true,
                calculationMode = DamageCalculationMode.Standard,
                attackerStatIds = new List<string> { "magic.spell_power" },
                defenderStatIds = new List<string> { "combat.resistance", "magic.lightning_resistance" },
                attackerStatMultipliers = new List<float> { 1.0f },
                defenderStatMultipliers = new List<float> { 0.5f, 1.0f }
            });

            // True damage
            damageTypes.Add(new DamageTypeConfig
            {
                damageType = DamageType.True,
                displayName = "True",
                description = "Pure damage that ignores ALL defenses",
                canCrit = false,
                calculationMode = DamageCalculationMode.IgnoreAllDefenses,
                attackerStatIds = new List<string> { "combat.attack_power" },
                defenderStatIds = new List<string>(), // No defense stats apply
                attackerStatMultipliers = new List<float> { 1.0f },
                defenderStatMultipliers = new List<float>()
            });

            Debug.Log($"[DamageCalculationConfig] Added {damageTypes.Count} default damage types to '{name}'");
        }

        [ContextMenu("Validate Configuration")]
        private void ValidateConfiguration()
        {
            int warnings = 0;

            foreach (var config in damageTypes)
            {
                if (config.attackerStatIds.Count != config.attackerStatMultipliers.Count)
                {
                    Debug.LogWarning($"[DamageCalculationConfig] {config.displayName}: Attacker stat count mismatch");
                    warnings++;
                }

                if (config.defenderStatIds.Count != config.defenderStatMultipliers.Count)
                {
                    Debug.LogWarning($"[DamageCalculationConfig] {config.displayName}: Defender stat count mismatch");
                    warnings++;
                }
            }

            if (warnings == 0)
            {
                Debug.Log($"[DamageCalculationConfig] Configuration validated successfully in '{name}'");
            }
            else
            {
                Debug.LogWarning($"[DamageCalculationConfig] Found {warnings} configuration issues in '{name}'");
            }
        }

        #endregion
    }

    /// <summary>
    /// Configuration for a specific damage type
    /// </summary>
    [System.Serializable]
    public class DamageTypeConfig
    {
        [Header("Identity")]
        public DamageType damageType = DamageType.Physical;
        public string displayName = "Physical";

        [TextArea(2, 3)]
        public string description = "Standard physical damage";

        [Header("Calculation Settings")]
        [Tooltip("Can this damage type crit?")]
        public bool canCrit = true;

        [Tooltip("How is this damage calculated?")]
        public DamageCalculationMode calculationMode = DamageCalculationMode.Standard;

        [Header("Attacker Bonuses")]
        [Tooltip("Stat IDs that increase damage (e.g., 'combat.attack_power')")]
        public List<string> attackerStatIds = new List<string>();

        [Tooltip("Multipliers for each attacker stat (1.0 = 100%, 0.5 = 50%)")]
        public List<float> attackerStatMultipliers = new List<float>();

        [Header("Defender Mitigation")]
        [Tooltip("Stat IDs that reduce damage (e.g., 'combat.armor')")]
        public List<string> defenderStatIds = new List<string>();

        [Tooltip("Multipliers for each defender stat (1.0 = 100%, 0.5 = 50%)")]
        public List<float> defenderStatMultipliers = new List<float>();

        [Header("Visual/Audio")]
        public Color damageColor = Color.white;
        public GameObject impactVFX;
        public AudioClip impactSound;
    }

    /// <summary>
    /// How damage is calculated
    /// </summary>
    public enum DamageCalculationMode
    {
        /// <summary>
        /// Normal calculation: base + attacker bonuses - defender mitigation
        /// </summary>
        Standard,

        /// <summary>
        /// Ignore armor (but not resistances)
        /// </summary>
        IgnoreArmor,

        /// <summary>
        /// Ignore all defenses (true damage)
        /// </summary>
        IgnoreAllDefenses,

        /// <summary>
        /// Percentage-based (e.g., 10% of max health)
        /// </summary>
        PercentageBased
    }
}