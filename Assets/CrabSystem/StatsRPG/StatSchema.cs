using UnityEngine;
using System.Collections.Generic;
using NinjaGame.Stats;

namespace NinjaGame.Stats
{
    /// <summary>
    /// Data-driven stat definition schema.
    /// Designers create these as ScriptableObjects to define stats without code changes.
    /// 
    /// Examples:
    /// - RPGCoreStats.asset: character.body, character.mind, character.spirit, etc.
    /// - RPGCombatStats.asset: combat.crit_chance, combat.attack_power, etc.
    /// - RPGResources.asset: character.max_health, character.max_stamina, etc.
    /// 
    /// Hot-reload: Changes in editor automatically update at runtime (F5 to reload)
    /// </summary>
    [CreateAssetMenu(fileName = "New Stat Schema", menuName = "NinjaGame/Stats/Stat Schema")]
    public class StatSchema : ScriptableObject
    {
        [Header("Schema Info")]
        [Tooltip("Namespace for these stats (e.g., 'character', 'combat', 'magic')")]
        public string statNamespace = "character";

        [TextArea(2, 4)]
        public string description = "Define stats for this namespace";

        [Header("Stat Definitions")]
        public List<StatDefinition> stats = new List<StatDefinition>();

        #region Editor Helpers

        [ContextMenu("Add Sample Core Stats")]
        private void AddSampleCoreStats()
        {
            stats.Clear();

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.body",
                displayName = "Body",
                description = "Physical strength and power",
                baseValue = 10f,
                formula = ""
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.mind",
                displayName = "Mind",
                description = "Intelligence and magical power",
                baseValue = 10f,
                formula = ""
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.spirit",
                displayName = "Spirit",
                description = "Agility and dexterity",
                baseValue = 10f,
                formula = ""
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.resilience",
                displayName = "Resilience",
                description = "Defense and damage reduction",
                baseValue = 10f,
                formula = ""
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.endurance",
                displayName = "Endurance",
                description = "Vitality and stamina",
                baseValue = 10f,
                formula = ""
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.insight",
                displayName = "Insight",
                description = "Wisdom and perception",
                baseValue = 10f,
                formula = ""
            });

            Debug.Log($"[StatSchema] Added 6 sample core stats to '{name}'");
        }

        [ContextMenu("Add Sample Resource Stats")]
        private void AddSampleResourceStats()
        {
            stats.Clear();

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.max_health",
                displayName = "Max Health",
                description = "Maximum hit points",
                baseValue = 100f,
                formula = "{character.endurance} * 10 + {character.body} * 5"
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.max_stamina",
                displayName = "Max Stamina",
                description = "Maximum stamina",
                baseValue = 100f,
                formula = "{character.endurance} * 8 + {character.body} * 3"
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.max_mana",
                displayName = "Max Mana",
                description = "Maximum mana",
                baseValue = 50f,
                formula = "{character.mind} * 8 + {character.insight} * 4"
            });

            Debug.Log($"[StatSchema] Added 3 sample resource stats to '{name}'");
        }

        [ContextMenu("Add Sample Combat Stats")]
        private void AddSampleCombatStats()
        {
            stats.Clear();

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.attack_power",
                displayName = "Attack Power",
                description = "Bonus damage added to attacks",
                baseValue = 0f,
                formula = "{character.body} * 2 + {character.spirit} * 1.5"
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.crit_chance",
                displayName = "Critical Chance",
                description = "Chance to land critical hits (%)",
                baseValue = 5f,
                formula = "{character.spirit} * 0.5 + {character.insight} * 0.3"
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.crit_damage",
                displayName = "Critical Damage",
                description = "Critical hit damage multiplier",
                baseValue = 1.5f,
                formula = "1.5 + {character.spirit} * 0.02"
            });

            stats.Add(new StatDefinition
            {
                statId = $"{statNamespace}.armor",
                displayName = "Armor",
                description = "Physical damage reduction",
                baseValue = 0f,
                formula = "{character.resilience} * 3 + {character.body} * 1"
            });

            Debug.Log($"[StatSchema] Added 4 sample combat stats to '{name}'");
        }

        [ContextMenu("Validate All Formulas")]
        private void ValidateFormulas()
        {
            int errors = 0;

            foreach (var stat in stats)
            {
                if (string.IsNullOrEmpty(stat.formula))
                    continue;

                // Check for matching braces
                int openBraces = 0;
                int closeBraces = 0;
                foreach (char c in stat.formula)
                {
                    if (c == '{') openBraces++;
                    if (c == '}') closeBraces++;
                }

                if (openBraces != closeBraces)
                {
                    Debug.LogError($"[StatSchema] Formula error in '{stat.displayName}': Mismatched braces");
                    errors++;
                }
            }

            if (errors == 0)
            {
                Debug.Log($"[StatSchema] All formulas validated successfully in '{name}'");
            }
            else
            {
                Debug.LogWarning($"[StatSchema] Found {errors} formula errors in '{name}'");
            }
        }

        #endregion
    }

    /// <summary>
    /// Individual stat definition within a schema
    /// </summary>
    [System.Serializable]
    public class StatDefinition
    {
        [Header("Identity")]
        [Tooltip("Full namespaced ID (e.g., 'character.max_health')")]
        public string statId;

        [Tooltip("Display name for UI")]
        public string displayName;

        [TextArea(2, 3)]
        [Tooltip("Tooltip description")]
        public string description;

        [Header("Base Configuration")]
        [Tooltip("Base value when formula is empty")]
        public float baseValue = 0f;

        [TextArea(2, 4)]
        [Tooltip("Formula string (e.g., '{character.body} * 5 + {character.endurance} * 2')")]
        public string formula = "";

        [Header("UI Settings (Optional)")]
        public Color displayColor = Color.white;
        public Sprite icon;

        [Header("Metadata")]
        [Tooltip("Should this stat be visible in UI?")]
        public bool isVisible = true;

        [Tooltip("Should this stat be saved?")]
        public bool isSaveable = true;

        [Tooltip("Category for grouping in UI (e.g., 'Core', 'Combat', 'Resource')")]
        public string category = "Uncategorized";
    }
}