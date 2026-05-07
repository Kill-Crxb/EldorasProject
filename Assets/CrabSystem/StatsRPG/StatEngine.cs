using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NinjaGame.Stats
{
    /// <summary>
    /// Per-entity stat calculation engine. Supports formula-driven stats, flat/percent
    /// modifiers, dependency cascades, and O(K) source removal via reverse index.
    /// Formula parser uses simple string substitution — stat IDs must be namespace-qualified
    /// (e.g. "character.strength") to avoid substring collisions.
    /// </summary>
    public class StatEngine
    {
        // All registered stats by ID
        private Dictionary<string, StatNode> stats = new Dictionary<string, StatNode>(StringComparer.OrdinalIgnoreCase);

        // Dependency graph for efficient updates
        // Key: stat that others depend ON
        // Value: list of stats that depend on it
        private Dictionary<string, HashSet<string>> dependents = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // 🟢 Phase 1.6 Day 6: Reverse index for O(1) modifier removal
        // Key: sourceId (item, buff, talent)
        // Value: list of statIds that have modifiers from this source
        private Dictionary<string, HashSet<string>> sourceIndex = new Dictionary<string, HashSet<string>>();

        // 🟢 Phase 1.6 Day 6: Performance profiler
        private StatEngineProfiler profiler = new StatEngineProfiler();

        // Debug settings
        private bool debugLogging = false;

        //lazy validation
        private bool validationDirty = false;
        private bool validated = false;

        // Events
        public event Action<string, float, float> OnStatChanged; // (statId, oldValue, newValue)

        // Properties
        public StatEngineProfiler Profiler => profiler;

        #region Initialization

        public StatEngine(bool enableDebug = false)
        {
            debugLogging = enableDebug;
        }

        /// <summary>
        /// Register a stat in the engine
        /// </summary>
        public void RegisterStat(StatNode stat)
        {
            if (stats.ContainsKey(stat.statId))
            {
                Debug.LogWarning($"[StatEngine] Stat '{stat.statId}' already registered. Replacing.");
            }

            stats[stat.statId] = stat;
            UpdateDependencyGraph(stat);

            validationDirty = true; // 🔑 mark for later

            stat.OnValueChanged += (oldVal, newVal) =>
                OnStatChanged?.Invoke(stat.statId, oldVal, newVal);

            if (debugLogging)
                Debug.Log($"[StatEngine] Registered stat: {stat.statId}");
        }

        public void ValidateAllStats()
        {
            foreach (var stat in stats.Values)
            {
                ValidateStatFormula(stat);
            }
        }
        /// <summary>
        /// Validate stat formula and dependencies before registration
        /// </summary>
        private void ValidateStatFormula(StatNode stat)
        {
            if (string.IsNullOrEmpty(stat.formula))
                return;

            // Check for basic formula syntax errors
            int openBraces = 0;
            int closeBraces = 0;
            foreach (char c in stat.formula)
            {
                if (c == '{') openBraces++;
                if (c == '}') closeBraces++;
            }

            if (openBraces != closeBraces)
            {
                Debug.LogError($"[StatEngine] Invalid formula for '{stat.statId}': Mismatched braces. Formula: {stat.formula}");
                return;
            }

            // 🟠 CRITICAL: Warn about missing dependencies
            foreach (var dependency in stat.Dependencies)
            {
                if (!stats.ContainsKey(dependency))
                {
                    // Extract namespace from dependency (e.g., "def" from "def.aegis")
                    string depNamespace = dependency.Contains('.') ? dependency.Substring(0, dependency.IndexOf('.')) : dependency;

                    Debug.LogWarning($"[StatEngine] Stat '{stat.statId}' references unknown stat '{dependency}'. " +
                                    $"This usually means a schema containing '{depNamespace}.*' stats is not loaded. " +
                                    $"Check that your StatSystem has all required schema IDs assigned in the inspector.");
                }
            }
        }

        /// <summary>
        /// Register a stat with simple base value
        /// </summary>
        public void RegisterStat(string statId, string displayName, float baseValue)
        {
            var stat = new StatNode(statId, displayName, baseValue);
            RegisterStat(stat);
        }

        /// <summary>
        /// Register a stat with formula
        /// </summary>
        public void RegisterStat(string statId, string displayName, string formula)
        {
            var stat = new StatNode(statId, displayName, 0f, formula);
            RegisterStat(stat);
        }

        /// <summary>
        /// Update dependency graph when stat formula changes
        /// </summary>
        private void UpdateDependencyGraph(StatNode stat)
        {
            // Remove old dependencies
            foreach (var dependencyList in dependents.Values)
            {
                dependencyList.Remove(stat.statId);
            }

            // Add new dependencies
            foreach (var dependency in stat.Dependencies)
            {
                if (!dependents.ContainsKey(dependency))
                {
                    dependents[dependency] = new HashSet<string>();
                }

                dependents[dependency].Add(stat.statId);
            }
        }

        #endregion

        #region Stat Access

        /// <summary>
        /// Get a stat by ID
        /// </summary>
        public StatNode GetStat(string statId)
        {
            return stats.TryGetValue(statId, out var stat) ? stat : null;
        }

        /// <summary>
        /// Get final value of a stat (convenience method)
        /// </summary>
        public float GetValue(string statId, float defaultValue = 0f)
        {
            EnsureValidated();
            var stat = GetStat(statId);
            return stat?.FinalValue ?? defaultValue;
        }

        /// <summary>
        /// Get base value of a stat (before modifiers)
        /// </summary>
        public float GetBaseValue(string statId, float defaultValue = 0f)
        {
            EnsureValidated();
            var stat = GetStat(statId);
            return stat?.BaseValue ?? defaultValue;
        }

        /// <summary>
        /// Set base value directly (for stats without formulas)
        /// </summary>
        public void SetBaseValue(string statId, float value)
        {
            var stat = GetStat(statId);
            if (stat != null)
            {
                stat.baseValue = value;
                stat.MarkFormulaDirty();
                RecalculateWithDependents(statId);
            }
        }

        /// <summary>
        /// Check if stat exists
        /// </summary>
        public bool HasStat(string statId)
        {
            return stats.ContainsKey(statId);
        }

        /// <summary>
        /// Get all registered stat IDs
        /// </summary>
        public IEnumerable<string> GetAllStatIds()
        {
            return stats.Keys;
        }

        #endregion
        #region StatHandle Support (Fast Lookups)

        /// <summary>
        /// Get final value of a stat by handle (FAST - no string compare)
        /// Use this for runtime performance-critical code
        /// </summary>
        /// <param name="handle">StatHandle resolved at initialization</param>
        /// <param name="defaultValue">Value to return if stat not found</param>
        /// <returns>Final calculated stat value</returns>
        public float GetValue(StatHandle handle, float defaultValue = 0f)
        {
            EnsureValidated();
            if (!handle.IsValid)
                return defaultValue;

            // Fast int-based lookup via stats dictionary
            // StatHandle.Id is the hash of the stat ID string
            // We still use string lookup internally, but callers cache handles
            // This saves string allocation and comparison at call sites

            // Get the stat ID from StatsManager's reverse lookup
            var statsManager = StatsManager.Instance;
            if (statsManager == null)
                return defaultValue;

            string statId = statsManager.GetStatIdByHandle(handle);
            if (string.IsNullOrEmpty(statId))
                return defaultValue;

            var stat = GetStat(statId);
            return stat?.FinalValue ?? defaultValue;
        }

        /// <summary>
        /// Get base value of a stat by handle (FAST)
        /// </summary>
        public float GetBaseValue(StatHandle handle, float defaultValue = 0f)
        {
            EnsureValidated();
            if (!handle.IsValid)
                return defaultValue;

            var statsManager = StatsManager.Instance;
            if (statsManager == null)
                return defaultValue;

            string statId = statsManager.GetStatIdByHandle(handle);
            if (string.IsNullOrEmpty(statId))
                return defaultValue;

            var stat = GetStat(statId);
            return stat?.BaseValue ?? defaultValue;
        }

        /// <summary>
        /// Set base value by handle (FAST)
        /// </summary>
        public void SetBaseValue(StatHandle handle, float value)
        {
            EnsureValidated();
            if (!handle.IsValid)
                return;

            var statsManager = StatsManager.Instance;
            if (statsManager == null)
                return;

            string statId = statsManager.GetStatIdByHandle(handle);
            if (string.IsNullOrEmpty(statId))
                return;

            SetBaseValue(statId, value);
        }

        /// <summary>
        /// Check if stat exists by handle
        /// </summary>
        public bool HasStat(StatHandle handle)
        {
            EnsureValidated();
            if (!handle.IsValid)
                return false;

            var statsManager = StatsManager.Instance;
            if (statsManager == null)
                return false;

            string statId = statsManager.GetStatIdByHandle(handle);
            return !string.IsNullOrEmpty(statId) && HasStat(statId);
        }

        #endregion

        #region Modifier Management

        public void AddFlatModifier(string statId, string sourceId, float value)
        {
            var stat = GetStat(statId);
            if (stat == null) return;

            stat.AddFlatModifier(sourceId, value);
            TrackSource(sourceId, statId);
            RecalculateTracked(statId);

            if (debugLogging)
                Debug.Log($"[StatEngine] AddFlatModifier {statId}: {sourceId} = {value}");
        }

        public void AddPercentModifier(string statId, string sourceId, float percent)
        {
            var stat = GetStat(statId);
            if (stat == null) return;

            stat.AddPercentModifier(sourceId, percent);
            TrackSource(sourceId, statId);
            RecalculateTracked(statId);

            if (debugLogging)
                Debug.Log($"[StatEngine] AddPercentModifier {statId}: {sourceId} = {percent * 100}%");
        }

        public void AddContributionBonus(string statId, string sourceId, string targetStatId, float multiplier)
        {
            var stat = GetStat(statId);
            if (stat == null) return;

            stat.AddContributionBonus(sourceId, targetStatId, multiplier);
            UpdateDependencyGraph(stat);
            validationDirty = true;
            TrackSource(sourceId, statId);
            RecalculateTracked(statId);

            if (debugLogging)
                Debug.Log($"[StatEngine] AddContributionBonus {statId}: {sourceId} = +{multiplier} per {targetStatId}");
        }

        // O(K) removal — only iterates stats affected by this source.
        public void RemoveAllModifiersFromSource(string sourceId)
        {
            if (!sourceIndex.TryGetValue(sourceId, out var affectedStats))
                return;

            foreach (var statId in affectedStats)
            {
                var stat = GetStat(statId);
                if (stat != null)
                {
                    stat.RemoveAllModifiersFromSource(sourceId);
                    RecalculateTracked(statId);
                }
            }

            // Clean up reverse index
            sourceIndex.Remove(sourceId);

            if (debugLogging)
                Debug.Log($"[StatEngine] Removed modifiers from source '{sourceId}' ({affectedStats.Count} stats affected)");
        }

        /// <summary>
        /// Enable or disable performance profiling.
        /// When enabled, all stat calculations are timed and logged.
        /// </summary>
        public void EnableProfiling(bool enable)
        {
            if (enable) profiler.Enable();
            else profiler.Disable();
        }

        public void ExportPerformanceReport() => profiler.LogReport();

        // Records sourceId → statId in the reverse index so removal is O(K).
        private void TrackSource(string sourceId, string statId)
        {
            if (!sourceIndex.ContainsKey(sourceId))
                sourceIndex[sourceId] = new HashSet<string>();
            sourceIndex[sourceId].Add(statId);
        }

        // Recalculates statId with profiler instrumentation when enabled.
        private void RecalculateTracked(string statId)
        {
            if (profiler.IsEnabled)
            {
                profiler.BeginCalculation(statId);
                RecalculateWithDependents(statId);
                int depCount = dependents.ContainsKey(statId) ? dependents[statId].Count : 0;
                profiler.EndCalculation(statId, depCount);
            }
            else
            {
                RecalculateWithDependents(statId);
            }
        }

        #endregion

        #region Formula Evaluation

        /// <summary>
        /// Evaluate a formula string
        /// Example: "{character.body} * 5 + {character.endurance} * 2"
        /// </summary>
        private float EvaluateFormula(string formula, StatNode stat)
        {
            // Start with base value
            float result = stat.baseValue;

            // Add formula result if formula exists
            if (!string.IsNullOrEmpty(formula))
            {
                string processedFormula = formula;

                // Replace all {stat.id} references with actual values
                // 🔴 CRITICAL: Use InvariantCulture to avoid locale issues (comma vs period)
                foreach (var dependency in stat.Dependencies)
                {
                    // 🟠 VALIDATION: Check if dependency exists (case-insensitive now)
                    if (!stats.ContainsKey(dependency))
                    {
                        // Extract namespace for helpful error message
                        string depNamespace = dependency.Contains('.') ? dependency.Substring(0, dependency.IndexOf('.')) : dependency;
                        Debug.LogError($"[StatEngine] Formula for '{stat.statId}' references missing stat '{dependency}'. " +
                                      $"This usually means a schema containing '{depNamespace}.*' stats was not loaded. " +
                                      $"Using 0 as default. Check StatSystem schema IDs.");
                        processedFormula = processedFormula.Replace($"{{{dependency}}}", "0");
                        continue;
                    }

                    float dependencyValue = GetValue(dependency, 0f);
                    processedFormula = processedFormula.Replace($"{{{dependency}}}", dependencyValue.ToString(CultureInfo.InvariantCulture));
                }

                // Evaluate mathematical expression and ADD to base value
                result += EvaluateMathExpression(processedFormula);
            }

            // Add contribution bonuses ("+X per Y" mechanics)
            foreach (var bonus in stat.contributionBonuses.Values)
            {
                float targetStatValue = GetValue(bonus.targetStatId, 0f);
                float contributionAmount = targetStatValue * bonus.multiplier;
                result += contributionAmount;
            }

            return result;
        }

        /// <summary>
        /// Simple math expression evaluator
        /// Supports: +, -, *, /, parentheses
        /// Uses simple recursive descent parser (Unity compatible)
        /// 
        /// KNOWN LIMITATIONS:
        /// - No unary minus support (e.g., "-5 + 3" may fail)
        /// - No operator precedence inside parentheses edge cases
        /// - O(n²) worst case due to substring creation
        /// - Not suitable for complex formulas with deep nesting
        /// 
        /// For production use, consider:
        /// - Tokenization-based parser
        /// - Expression tree compilation
        /// - NCalc or similar library
        /// </summary>
        private float EvaluateMathExpression(string expression)
        {
            try
            {
                // Remove whitespace
                expression = expression.Replace(" ", "");

                if (string.IsNullOrEmpty(expression))
                    return 0f;

                // Simple expression evaluator
                return EvaluateExpression(expression);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatEngine] Failed to evaluate formula '{expression}': {e.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// Recursive descent parser for simple math expressions
        /// </summary>
        private float EvaluateExpression(string expr)
        {
            // Handle parentheses first
            while (expr.Contains("("))
            {
                int start = expr.LastIndexOf('(');
                int end = expr.IndexOf(')', start);
                if (end == -1)
                    throw new System.Exception("Mismatched parentheses");

                string subExpr = expr.Substring(start + 1, end - start - 1);
                float subResult = EvaluateExpression(subExpr);
                expr = expr.Substring(0, start) + subResult.ToString() + expr.Substring(end + 1);
            }

            // Handle addition and subtraction (lowest precedence)
            for (int i = expr.Length - 1; i >= 0; i--)
            {
                if (expr[i] == '+' && i > 0)
                {
                    float left = EvaluateExpression(expr.Substring(0, i));
                    float right = EvaluateExpression(expr.Substring(i + 1));
                    return left + right;
                }
                else if (expr[i] == '-' && i > 0)
                {
                    float left = EvaluateExpression(expr.Substring(0, i));
                    float right = EvaluateExpression(expr.Substring(i + 1));
                    return left - right;
                }
            }

            // Handle multiplication and division
            for (int i = expr.Length - 1; i >= 0; i--)
            {
                if (expr[i] == '*')
                {
                    float left = EvaluateExpression(expr.Substring(0, i));
                    float right = EvaluateExpression(expr.Substring(i + 1));
                    return left * right;
                }
                else if (expr[i] == '/')
                {
                    float left = EvaluateExpression(expr.Substring(0, i));
                    float right = EvaluateExpression(expr.Substring(i + 1));
                    if (Mathf.Approximately(right, 0f))
                        throw new System.Exception("Division by zero");
                    return left / right;
                }
            }

            // Base case: parse as number
            if (float.TryParse(expr, out float result))
                return result;

            throw new System.Exception($"Invalid expression: {expr}");
        }

        #endregion

        #region Recalculation

        /// <summary>
        /// Recalculate a specific stat and all its dependents
        /// </summary>
        public void RecalculateWithDependents(string statId)
        {
            EnsureValidated();
            RecalculateWithDependents(statId, new HashSet<string>());
        }

        /// <summary>
        /// Internal recursive implementation with cycle detection.
        /// Calculates dependencies FIRST, then the stat itself, then its dependents.
        /// </summary>
        private void RecalculateWithDependents(string statId, HashSet<string> visited)
        {
            var stat = GetStat(statId);
            if (stat == null)
                return;

            // 🔴 CRITICAL: Cycle detection (only for dependency chain UPWARD)
            if (!visited.Add(statId))
            {
                Debug.LogError($"[StatEngine] Circular dependency detected at '{statId}'! Dependency chain: {string.Join(" → ", visited)} → {statId}");
                return;
            }

            // 🟢 STEP 1: Recursively calculate all dependencies FIRST (upward in tree)
            // Uses SAME visited set to detect true cycles in dependency chain
            foreach (var dependency in stat.Dependencies)
            {
                var dependencyStat = GetStat(dependency);
                if (dependencyStat != null && dependencyStat.IsDirty)
                {
                    RecalculateWithDependents(dependency, visited);
                }
            }

            // 🟢 STEP 2: Now calculate this stat (all dependencies are fresh)
            if (stat.IsDirty)
            {
                float formulaResult = EvaluateFormula(stat.formula, stat);
                stat.SetFormulaResult(formulaResult);
            }

            // Remove from visited BEFORE cascading to dependents
            visited.Remove(statId);

            // 🟢 STEP 3: Cascade to dependents (downward in tree)
            // Uses FRESH visited set for each dependent to avoid false cycle detection
            if (dependents.TryGetValue(statId, out var dependentList))
            {
                foreach (var dependentId in dependentList)
                {
                    var dependent = GetStat(dependentId);
                    if (dependent != null)
                    {
                        dependent.MarkFormulaDirty();
                        // Fresh visited set for each dependent branch
                        RecalculateWithDependents(dependentId, new HashSet<string>());
                    }
                }
            }
        }

        /// <summary>
        /// Recalculate all stats using a multi-pass approach to handle dependencies.
        /// Uses iterative calculation until all stats are stable.
        /// 
        /// This approach:
        /// 1. Doesn't require dependency graph traversal
        /// 2. Handles circular dependencies gracefully
        /// 3. Always converges (with max iteration limit)
        /// </summary>
        public void RecalculateAll()
        {
            EnsureValidated();

            // Mark all stats as needing recalculation
            foreach (var stat in stats.Values)
            {
                stat.MarkFormulaDirty();
            }

            // Multi-pass calculation: keep recalculating until nothing changes
            // This handles dependencies without recursive traversal
            const int maxPasses = 10; // Safety limit
            int pass = 0;
            bool anyChanged = true;

            while (anyChanged && pass < maxPasses)
            {
                anyChanged = false;
                pass++;

                foreach (var stat in stats.Values)
                {
                    if (stat.IsDirty)
                    {
                        float oldValue = stat.FinalValue;
                        float formulaResult = EvaluateFormula(stat.formula, stat);
                        stat.SetFormulaResult(formulaResult);

                        // Check if value changed significantly
                        if (!Mathf.Approximately(oldValue, stat.FinalValue))
                        {
                            anyChanged = true;

                            // Mark dependents as dirty
                            if (dependents.TryGetValue(stat.statId, out var dependentList))
                            {
                                foreach (var dependentId in dependentList)
                                {
                                    var dependent = GetStat(dependentId);
                                    dependent?.MarkFormulaDirty();
                                }
                            }
                        }
                    }
                }

                if (debugLogging && pass > 1)
                    Debug.Log($"[StatEngine] RecalculateAll pass {pass}, {(anyChanged ? "continuing" : "stable")}");
            }

            if (pass >= maxPasses)
            {
                Debug.LogWarning($"[StatEngine] RecalculateAll reached max passes ({maxPasses}). " +
                                "This might indicate circular dependencies or unstable formulas.");
            }
        }

        /// <summary>
        /// Force recalculation of all stats (useful for debugging)
        /// </summary>
        public void ForceRecalculateAll()
        {
            foreach (var stat in stats.Values)
            {
                stat.MarkFormulaDirty();
            }

            RecalculateAll();

            if (debugLogging)
                Debug.Log("[StatEngine] Forced recalculation of all stats");
        }

        #endregion

        #region Debug

        public void EnableDebugLogging(bool enable)
        {
            debugLogging = enable;
        }

        public string GetDebugSummary()
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("=== STAT ENGINE DEBUG ===");
            summary.AppendLine($"Total Stats: {stats.Count}");
            summary.AppendLine($"Total Dependencies: {dependents.Count}");
            summary.AppendLine();

            foreach (var stat in stats.Values)
            {
                summary.AppendLine(stat.ToString());
            }

            return summary.ToString();
        }
        private void EnsureValidated()
        {
            if (!validationDirty || validated)
                return;

            foreach (var stat in stats.Values)
            {
                ValidateStatFormula(stat);
            }

            validated = true;
            validationDirty = false;

            if (debugLogging)
                Debug.Log("[StatEngine] Stat validation completed");
        }

        public void LogStatTree(string rootStatId)
        {
            var stat = GetStat(rootStatId);
            if (stat == null)
            {
                Debug.LogWarning($"[StatEngine] Stat '{rootStatId}' not found");
                return;
            }

            Debug.Log($"=== STAT TREE: {rootStatId} ===");
            Debug.Log($"Value: {stat.FinalValue:F2}");
            Debug.Log($"Formula: {stat.formula}");
            Debug.Log($"Dependencies: {string.Join(", ", stat.Dependencies)}");

            if (dependents.TryGetValue(rootStatId, out var dependentList))
            {
                Debug.Log($"Dependents: {string.Join(", ", dependentList)}");
            }
        }

        #endregion
    }
}