using UnityEngine;
using System;
using System.Collections.Generic;

namespace NinjaGame.Stats
{
    /// <summary>
    /// Represents a single stat with dependency tracking and modifier management.
    /// Supports formulas, flat modifiers, percentage modifiers, and contribution bonuses.
    /// 
    /// Example:
    /// - character.max_health = "{character.endurance} * 10" + flat modifiers + percentage modifiers
    /// - combat.crit_chance = 5.0 (base) + {character.insight} * 0.2 (contribution) + modifiers
    /// </summary>
    [System.Serializable]
    public class StatNode
    {
        // Identity
        public string statId;           // Full namespaced ID
        public string displayName;      // Human-readable name
        public string description;      // Tooltip description
        public string category = "Uncategorized";  // ← ADD THIS LINE

        // Base configuration
        public float baseValue;
        public string formula;          // Formula string (e.g., "{character.body} * 5 + {character.endurance} * 2")

        // Cached formula results
        private float formulaValue;
        private bool isFormulaDirty = true;

        // Modifiers by source
        private Dictionary<string, float> flatModifiers = new Dictionary<string, float>();      // "item.sword_123": +50 damage
        private Dictionary<string, float> percentModifiers = new Dictionary<string, float>();   // "buff.strength": +25%

        // Contribution bonuses (modify formula relationships)
        // Example: Item adds "+2 crit per point of Insight" → modifies crit formula
        internal Dictionary<string, ContributionBonus> contributionBonuses = new Dictionary<string, ContributionBonus>();

        // Dependencies (stats this formula depends on)
        private HashSet<string> dependencies = new HashSet<string>();

        // Final value cache
        private float cachedFinalValue;
        private bool isFinalValueDirty = true;

        // Events
        public event Action<float, float> OnValueChanged; // (oldValue, newValue)

        /// <summary>
        /// Get the final calculated value with all modifiers applied
        /// </summary>
        public float FinalValue
        {
            get
            {
                if (isFinalValueDirty)
                {
                    RecalculateFinalValue();
                }
                return cachedFinalValue;
            }
        }

        /// <summary>
        /// Get base value (formula result + contributions, no flat/percent modifiers)
        /// </summary>
        public float BaseValue
        {
            get
            {
                if (isFormulaDirty)
                {
                    RecalculateFormula();
                }
                return formulaValue;
            }
        }

        /// <summary>
        /// Get dependencies for this stat's formula
        /// </summary>
        public IEnumerable<string> Dependencies => dependencies;

        /// <summary>
        /// Check if this stat needs recalculation
        /// </summary>
        public bool IsDirty => isFinalValueDirty || isFormulaDirty;

        #region Initialization

        public StatNode(string id, string name, float baseVal = 0f, string formulaStr = "", string cat = "Uncategorized")
        {
            statId = id;
            displayName = name;
            baseValue = baseVal;
            formula = formulaStr;
            category = cat;  // ← ADD THIS LINE

            ParseFormulaDependencies();
        }

        /// <summary>
        /// Parse formula to extract dependencies
        /// Example: "{character.body} * 5" extracts "character.body"
        /// </summary>
        private void ParseFormulaDependencies()
        {
            dependencies.Clear();

            if (string.IsNullOrEmpty(formula))
                return;

            // Find all {stat.name} references
            int startIndex = 0;
            while ((startIndex = formula.IndexOf('{', startIndex)) != -1)
            {
                int endIndex = formula.IndexOf('}', startIndex);
                if (endIndex == -1)
                    break;

                string dependency = formula.Substring(startIndex + 1, endIndex - startIndex - 1);
                dependencies.Add(dependency);

                startIndex = endIndex + 1;
            }
        }

        #endregion

        #region Modifier Management

        /// <summary>
        /// Add a flat modifier from a source (e.g., item, buff, talent)
        /// </summary>
        public void AddFlatModifier(string sourceId, float value)
        {
            flatModifiers[sourceId] = value;
            MarkDirty();
        }

        /// <summary>
        /// Add a percentage modifier from a source (0.25 = +25%)
        /// </summary>
        public void AddPercentModifier(string sourceId, float percent)
        {
            percentModifiers[sourceId] = percent;
            MarkDirty();
        }

        /// <summary>
        /// Add a contribution bonus (modifies formula relationships)
        /// Example: "+2 crit per point of Insight"
        /// </summary>
        public void AddContributionBonus(string sourceId, string targetStatId, float multiplier)
        {
            contributionBonuses[sourceId] = new ContributionBonus
            {
                targetStatId = targetStatId,
                multiplier = multiplier
            };

            // Add to dependencies if not already there
            dependencies.Add(targetStatId);
            isFormulaDirty = true;
            MarkDirty();
        }

        /// <summary>
        /// Remove all modifiers from a specific source (e.g., item unequipped)
        /// </summary>
        public void RemoveAllModifiersFromSource(string sourceId)
        {
            bool hadModifier = false;

            if (flatModifiers.Remove(sourceId))
                hadModifier = true;

            if (percentModifiers.Remove(sourceId))
                hadModifier = true;

            if (contributionBonuses.Remove(sourceId))
            {
                hadModifier = true;
                isFormulaDirty = true;
            }

            if (hadModifier)
                MarkDirty();
        }

        /// <summary>
        /// Clear all modifiers (useful for reset/debug)
        /// </summary>
        public void ClearAllModifiers()
        {
            flatModifiers.Clear();
            percentModifiers.Clear();
            contributionBonuses.Clear();
            MarkDirty();
        }

        #endregion

        #region Calculation

        /// <summary>
        /// Mark this stat as needing recalculation
        /// </summary>
        public void MarkDirty()
        {
            isFinalValueDirty = true;
        }

        /// <summary>
        /// Mark formula as needing recalculation (when dependencies change)
        /// </summary>
        public void MarkFormulaDirty()
        {
            isFormulaDirty = true;
            isFinalValueDirty = true;
        }

        /// <summary>
        /// Recalculate formula result (Stage 1: formula + contributions)
        /// </summary>
        private void RecalculateFormula()
        {
            if (string.IsNullOrEmpty(formula))
            {
                formulaValue = baseValue;
            }
            else
            {
                // Formula evaluation happens in StatEngine
                // This is just the cached result
                // StatEngine will call SetFormulaResult() after evaluation
            }

            // Apply contribution bonuses to formula result
            foreach (var bonus in contributionBonuses.Values)
            {
                // Contribution bonuses are applied during formula evaluation
                // Example: If this is crit_chance and bonus says "+2 per Insight"
                // StatEngine evaluates: base_formula + (insight_value * 2)
            }

            isFormulaDirty = false;
        }

        /// <summary>
        /// Set formula result from StatEngine (called after formula evaluation)
        /// </summary>
        public void SetFormulaResult(float result)
        {
            formulaValue = result;
            isFormulaDirty = false;
            isFinalValueDirty = true;
        }

        /// <summary>
        /// Recalculate final value (Stage 2: formula + flat + percent)
        /// </summary>
        private void RecalculateFinalValue()
        {
            float oldValue = cachedFinalValue;

            // Start with formula result
            float value = BaseValue;

            // Add flat modifiers
            foreach (var modifier in flatModifiers.Values)
            {
                value += modifier;
            }

            // Apply percentage modifiers
            float percentTotal = 0f;
            foreach (var percent in percentModifiers.Values)
            {
                percentTotal += percent;
            }

            if (percentTotal != 0f)
            {
                value *= (1f + percentTotal);
            }

            cachedFinalValue = value;
            isFinalValueDirty = false;

            // Fire event if value changed
            if (!Mathf.Approximately(oldValue, cachedFinalValue))
            {
                OnValueChanged?.Invoke(oldValue, cachedFinalValue);
            }
        }

        /// <summary>
        /// Force immediate recalculation (useful for debugging)
        /// </summary>
        public void ForceRecalculate()
        {
            isFormulaDirty = true;
            isFinalValueDirty = true;
            RecalculateFormula();
            RecalculateFinalValue();
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"{displayName} ({statId}): {FinalValue:F2} " +
                   $"[Base: {BaseValue:F2}, Flat: {GetTotalFlatModifiers():F2}, Percent: {GetTotalPercentModifiers():F2}%]";
        }

        private float GetTotalFlatModifiers()
        {
            float total = 0f;
            foreach (var modifier in flatModifiers.Values)
                total += modifier;
            return total;
        }

        private float GetTotalPercentModifiers()
        {
            float total = 0f;
            foreach (var percent in percentModifiers.Values)
                total += percent;
            return total * 100f;
        }

        #endregion
    }

    /// <summary>
    /// Represents a contribution bonus that modifies formula relationships
    /// Example: "+2 crit chance per point of Insight"
    /// </summary>
    [System.Serializable]
    public class ContributionBonus
    {
        public string targetStatId;     // Which stat contributes (e.g., "character.insight")
        public float multiplier;        // How much per point (e.g., 2.0 for "+2 crit per insight")
    }
}