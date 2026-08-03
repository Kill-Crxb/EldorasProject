using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace NinjaGame.Stats
{
    public class StatEngine
    {
        private Dictionary<string, StatNode> stats = new Dictionary<string, StatNode>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, HashSet<string>> dependents = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, HashSet<string>> sourceIndex = new Dictionary<string, HashSet<string>>();
        private StatEngineProfiler profiler = new StatEngineProfiler();

        public event Action<string, float, float> OnStatChanged;
        public StatEngineProfiler Profiler => profiler;

        public StatEngine(bool enableDebug = false) { }

        #region Registration

        public void RegisterStat(StatNode stat)
        {
            stats[stat.statId] = stat;
            UpdateDependencyGraph(stat);
            stat.OnValueChanged += (oldVal, newVal) => OnStatChanged?.Invoke(stat.statId, oldVal, newVal);
        }

        public void RegisterStat(string statId, string displayName, float baseValue)
        {
            RegisterStat(new StatNode(statId, displayName, baseValue));
        }

        public void RegisterStat(string statId, string displayName, string formula)
        {
            RegisterStat(new StatNode(statId, displayName, 0f, formula));
        }

        private void UpdateDependencyGraph(StatNode stat)
        {
            foreach (var dependencyList in dependents.Values)
                dependencyList.Remove(stat.statId);

            foreach (var dependency in stat.Dependencies)
            {
                if (!dependents.ContainsKey(dependency))
                    dependents[dependency] = new HashSet<string>();

                dependents[dependency].Add(stat.statId);
            }
        }

        #endregion

        #region Stat Access

        public StatNode GetStat(string statId)
        {
            return stats.TryGetValue(statId, out var stat) ? stat : null;
        }

        public float GetValue(string statId, float defaultValue = 0f)
        {
            var stat = GetStat(statId);
            return stat?.FinalValue ?? defaultValue;
        }

        public float GetBaseValue(string statId, float defaultValue = 0f)
        {
            var stat = GetStat(statId);
            return stat?.BaseValue ?? defaultValue;
        }

        public void SetBaseValue(string statId, float value)
        {
            var stat = GetStat(statId);
            if (stat == null) return;

            stat.baseValue = value;
            stat.MarkFormulaDirty();
            RecalculateWithDependents(statId);
        }

        public bool HasStat(string statId)
        {
            return stats.ContainsKey(statId);
        }

        public IEnumerable<string> GetAllStatIds()
        {
            return stats.Keys;
        }

        #endregion

        #region Handle Access

        public float GetValue(StatHandle handle, float defaultValue = 0f)
        {
            if (!handle.IsValid) return defaultValue;

            string statId = StatsManager.Instance?.GetStatIdByHandle(handle);
            if (string.IsNullOrEmpty(statId)) return defaultValue;

            return GetValue(statId, defaultValue);
        }

        public float GetBaseValue(StatHandle handle, float defaultValue = 0f)
        {
            if (!handle.IsValid) return defaultValue;

            string statId = StatsManager.Instance?.GetStatIdByHandle(handle);
            if (string.IsNullOrEmpty(statId)) return defaultValue;

            return GetBaseValue(statId, defaultValue);
        }

        public void SetBaseValue(StatHandle handle, float value)
        {
            if (!handle.IsValid) return;

            string statId = StatsManager.Instance?.GetStatIdByHandle(handle);
            if (string.IsNullOrEmpty(statId)) return;

            SetBaseValue(statId, value);
        }

        public bool HasStat(StatHandle handle)
        {
            if (!handle.IsValid) return false;

            string statId = StatsManager.Instance?.GetStatIdByHandle(handle);
            return !string.IsNullOrEmpty(statId) && HasStat(statId);
        }

        #endregion

        #region Modifiers

        public void AddFlatModifier(string statId, string sourceId, float value)
        {
            var stat = GetStat(statId);
            if (stat == null) return;

            stat.AddFlatModifier(sourceId, value);
            TrackSource(sourceId, statId);
            RecalculateTracked(statId);
        }

        public void AddPercentModifier(string statId, string sourceId, float percent)
        {
            var stat = GetStat(statId);
            if (stat == null) return;

            stat.AddPercentModifier(sourceId, percent);
            TrackSource(sourceId, statId);
            RecalculateTracked(statId);
        }

        public void AddContributionBonus(string statId, string sourceId, string targetStatId, float multiplier)
        {
            var stat = GetStat(statId);
            if (stat == null) return;

            stat.AddContributionBonus(sourceId, targetStatId, multiplier);
            UpdateDependencyGraph(stat);
            TrackSource(sourceId, statId);
            RecalculateTracked(statId);
        }

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

            sourceIndex.Remove(sourceId);
        }

        private void TrackSource(string sourceId, string statId)
        {
            if (!sourceIndex.ContainsKey(sourceId))
                sourceIndex[sourceId] = new HashSet<string>();

            sourceIndex[sourceId].Add(statId);
        }

        #endregion

        #region Profiling

        public void EnableProfiling(bool enable)
        {
            if (enable) profiler.Enable();
            else profiler.Disable();
        }

        public void ExportPerformanceReport() => profiler.LogReport();

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

        #region Recalculation

        public void RecalculateWithDependents(string statId)
        {
            var scope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectDownwardClosure(statId, scope);
            RecalcScope(scope);
        }

        public void RecalculateAll()
        {
            RecalcScope(new HashSet<string>(stats.Keys, StringComparer.OrdinalIgnoreCase));
        }

        public void ForceRecalculateAll() => RecalculateAll();

        private void CollectDownwardClosure(string statId, HashSet<string> scope)
        {
            if (!scope.Add(statId)) return;

            if (dependents.TryGetValue(statId, out var deps))
                foreach (var dependent in deps)
                    CollectDownwardClosure(dependent, scope);
        }

        private void RecalcScope(HashSet<string> scope)
        {
            var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var inProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var statId in scope)
                RecalcOrdered(statId, scope, done, inProgress);
        }

        private void RecalcOrdered(string statId, HashSet<string> scope, HashSet<string> done, HashSet<string> inProgress)
        {
            if (done.Contains(statId)) return;

            if (!inProgress.Add(statId))
            {
                Debug.LogError($"[StatEngine] Circular dependency at '{statId}': {string.Join(" → ", inProgress)} → {statId}");
                return;
            }

            var stat = GetStat(statId);
            if (stat != null)
            {
                foreach (var dependency in stat.Dependencies)
                    if (scope.Contains(dependency))
                        RecalcOrdered(dependency, scope, done, inProgress);

                stat.MarkFormulaDirty();
                stat.SetFormulaResult(EvaluateFormula(stat.formula, stat));
            }

            inProgress.Remove(statId);
            done.Add(statId);
        }

        #endregion

        #region Formula Evaluation

        private float EvaluateFormula(string formula, StatNode stat)
        {
            float result = stat.baseValue;

            if (!string.IsNullOrEmpty(formula))
            {
                string processedFormula = formula;

                foreach (var dependency in stat.Dependencies)
                {
                    if (!stats.ContainsKey(dependency))
                    {
                        Debug.LogError($"[StatEngine] Formula for '{stat.statId}' references missing stat '{dependency}'. Using 0.");
                        processedFormula = processedFormula.Replace($"{{{dependency}}}", "0");
                        continue;
                    }

                    float dependencyValue = GetValue(dependency, 0f);
                    processedFormula = processedFormula.Replace($"{{{dependency}}}", dependencyValue.ToString(CultureInfo.InvariantCulture));
                }

                result += EvaluateMathExpression(processedFormula);
            }

            foreach (var bonus in stat.contributionBonuses.Values)
            {
                float targetStatValue = GetValue(bonus.targetStatId, 0f);
                result += targetStatValue * bonus.multiplier;
            }

            return result;
        }

        private float EvaluateMathExpression(string expression)
        {
            try
            {
                expression = expression.Replace(" ", "");
                if (string.IsNullOrEmpty(expression))
                    return 0f;

                return EvaluateExpression(expression);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatEngine] Failed to evaluate formula '{expression}': {e.Message}");
                return 0f;
            }
        }

        private float EvaluateExpression(string expr)
        {
            while (expr.Contains("("))
            {
                int start = expr.LastIndexOf('(');
                int end = expr.IndexOf(')', start);
                if (end == -1)
                    throw new Exception("Mismatched parentheses");

                string subExpr = expr.Substring(start + 1, end - start - 1);
                float subResult = EvaluateExpression(subExpr);
                expr = expr.Substring(0, start) + subResult.ToString(CultureInfo.InvariantCulture) + expr.Substring(end + 1);
            }

            for (int i = expr.Length - 1; i >= 0; i--)
            {
                if (expr[i] == '+' && i > 0)
                    return EvaluateExpression(expr.Substring(0, i)) + EvaluateExpression(expr.Substring(i + 1));

                if (expr[i] == '-' && i > 0)
                    return EvaluateExpression(expr.Substring(0, i)) - EvaluateExpression(expr.Substring(i + 1));
            }

            for (int i = expr.Length - 1; i >= 0; i--)
            {
                if (expr[i] == '*')
                    return EvaluateExpression(expr.Substring(0, i)) * EvaluateExpression(expr.Substring(i + 1));

                if (expr[i] == '/')
                {
                    float right = EvaluateExpression(expr.Substring(i + 1));
                    if (Mathf.Approximately(right, 0f))
                        throw new Exception("Division by zero");

                    return EvaluateExpression(expr.Substring(0, i)) / right;
                }
            }

            if (float.TryParse(expr, NumberStyles.Any, CultureInfo.InvariantCulture, out float result))
                return result;

            throw new Exception($"Invalid expression: {expr}");
        }

        #endregion

        #region Debug

        public void EnableDebugLogging(bool enable) { }

        public string GetDebugSummary()
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("=== STAT ENGINE DEBUG ===");
            summary.AppendLine($"Total Stats: {stats.Count}");
            summary.AppendLine($"Total Dependencies: {dependents.Count}");
            summary.AppendLine();

            foreach (var stat in stats.Values)
                summary.AppendLine(stat.ToString());

            return summary.ToString();
        }

        #endregion
    }
}