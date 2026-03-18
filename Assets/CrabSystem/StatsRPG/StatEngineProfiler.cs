using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace NinjaGame.Stats
{
    /// <summary>
    /// Performance profiler for StatEngine operations.
    /// Phase 1.6 Day 6: Performance Profiling
    /// 
    /// Features:
    /// - Measure calculation times per stat
    /// - Track recalculation frequency
    /// - Identify performance bottlenecks
    /// - Export profiling reports
    /// - Runtime performance monitoring
    /// </summary>
    public class StatEngineProfiler
    {
        // Performance metrics
        private Dictionary<string, StatMetrics> statMetrics = new Dictionary<string, StatMetrics>();
        private Dictionary<string, long> calculationTimes = new Dictionary<string, long>(); // In ticks
        private long totalCalculationTime = 0;
        private int totalCalculations = 0;

        private bool isEnabled = false;
        private Stopwatch stopwatch = new Stopwatch();

        public bool IsEnabled => isEnabled;

        public class StatMetrics
        {
            public string statId;
            public int calculationCount;
            public long totalTimeTicks;
            public long minTimeTicks = long.MaxValue;
            public long maxTimeTicks = 0;
            public int dependentCount;

            public double AverageTimeMs => calculationCount > 0 ?
                (totalTimeTicks / (double)calculationCount) / 10000.0 : 0;

            public double MinTimeMs => minTimeTicks / 10000.0;
            public double MaxTimeMs => maxTimeTicks / 10000.0;
        }

        public void Enable()
        {
            isEnabled = true;
            Reset();
            UnityEngine.Debug.Log("[StatEngineProfiler] Profiling enabled");
        }

        public void Disable()
        {
            isEnabled = false;
            UnityEngine.Debug.Log("[StatEngineProfiler] Profiling disabled");
        }

        public void Reset()
        {
            statMetrics.Clear();
            calculationTimes.Clear();
            totalCalculationTime = 0;
            totalCalculations = 0;
        }

        public void BeginCalculation(string statId)
        {
            if (!isEnabled) return;

            stopwatch.Restart();
        }

        public void EndCalculation(string statId, int dependentCount = 0)
        {
            if (!isEnabled) return;

            stopwatch.Stop();
            long elapsed = stopwatch.ElapsedTicks;

            if (!statMetrics.ContainsKey(statId))
            {
                statMetrics[statId] = new StatMetrics { statId = statId };
            }

            var metrics = statMetrics[statId];
            metrics.calculationCount++;
            metrics.totalTimeTicks += elapsed;
            metrics.minTimeTicks = System.Math.Min(metrics.minTimeTicks, elapsed);
            metrics.maxTimeTicks = System.Math.Max(metrics.maxTimeTicks, elapsed);
            metrics.dependentCount = dependentCount;

            totalCalculationTime += elapsed;
            totalCalculations++;
        }

        public string GenerateReport()
        {
            if (statMetrics.Count == 0)
            {
                return "[StatEngineProfiler] No profiling data available. Enable profiling first.";
            }

            var sb = new System.Text.StringBuilder();

            sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║               STAT ENGINE PERFORMANCE REPORT                       ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // Summary
            sb.AppendLine("SUMMARY:");
            sb.AppendLine($"  Total Calculations: {totalCalculations}");
            sb.AppendLine($"  Total Time: {totalCalculationTime / 10000.0:F3} ms");
            sb.AppendLine($"  Average Time/Calc: {(totalCalculationTime / (double)totalCalculations) / 10000.0:F4} ms");
            sb.AppendLine();

            // Top 10 slowest stats
            sb.AppendLine("TOP 10 SLOWEST STATS (by average):");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────");
            sb.AppendLine($"{"Stat ID",-40} {"Avg (ms)",10} {"Count",8} {"Deps",6}");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────");

            var slowestStats = statMetrics.Values
                .OrderByDescending(m => m.AverageTimeMs)
                .Take(10);

            foreach (var metric in slowestStats)
            {
                sb.AppendLine($"{metric.statId,-40} {metric.AverageTimeMs,10:F4} {metric.calculationCount,8} {metric.dependentCount,6}");
            }

            sb.AppendLine();

            // Most frequently calculated stats
            sb.AppendLine("TOP 10 MOST FREQUENTLY CALCULATED:");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────");
            sb.AppendLine($"{"Stat ID",-40} {"Count",10} {"Total (ms)",12}");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────");

            var mostFrequent = statMetrics.Values
                .OrderByDescending(m => m.calculationCount)
                .Take(10);

            foreach (var metric in mostFrequent)
            {
                sb.AppendLine($"{metric.statId,-40} {metric.calculationCount,10} {metric.totalTimeTicks / 10000.0,12:F3}");
            }

            sb.AppendLine();

            // Stats with highest variance (max - min)
            sb.AppendLine("TOP 10 HIGHEST VARIANCE (inconsistent performance):");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────");
            sb.AppendLine($"{"Stat ID",-40} {"Min (ms)",10} {"Max (ms)",10} {"Variance",10}");
            sb.AppendLine("─────────────────────────────────────────────────────────────────────");

            var highestVariance = statMetrics.Values
                .Where(m => m.calculationCount > 1)
                .OrderByDescending(m => m.MaxTimeMs - m.MinTimeMs)
                .Take(10);

            foreach (var metric in highestVariance)
            {
                double variance = metric.MaxTimeMs - metric.MinTimeMs;
                sb.AppendLine($"{metric.statId,-40} {metric.MinTimeMs,10:F4} {metric.MaxTimeMs,10:F4} {variance,10:F4}");
            }

            sb.AppendLine();

            // Performance recommendations
            sb.AppendLine("RECOMMENDATIONS:");

            // Check if any stat takes > 1ms on average
            var slowStats = statMetrics.Values.Where(m => m.AverageTimeMs > 1.0).ToList();
            if (slowStats.Count > 0)
            {
                sb.AppendLine($"  ⚠ {slowStats.Count} stat(s) averaging > 1ms per calculation:");
                foreach (var stat in slowStats.Take(5))
                {
                    sb.AppendLine($"    - {stat.statId}: {stat.AverageTimeMs:F3}ms avg");
                }
                sb.AppendLine("    Consider simplifying these formulas or caching intermediate results.");
            }
            else
            {
                sb.AppendLine("  ✓ All stats calculating in < 1ms (good performance)");
            }

            // Check for frequently recalculated stats
            int avgCalcCount = totalCalculations / statMetrics.Count;
            var overCalculated = statMetrics.Values
                .Where(m => m.calculationCount > avgCalcCount * 3)
                .ToList();

            if (overCalculated.Count > 0)
            {
                sb.AppendLine($"  ⚠ {overCalculated.Count} stat(s) recalculated significantly more than average:");
                foreach (var stat in overCalculated.Take(5))
                {
                    sb.AppendLine($"    - {stat.statId}: {stat.calculationCount} times (avg: {avgCalcCount})");
                }
                sb.AppendLine("    These stats may have many dependents or be in dependency chains.");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        public void LogReport()
        {
            UnityEngine.Debug.Log(GenerateReport());
        }

        public Dictionary<string, StatMetrics> GetAllMetrics()
        {
            return new Dictionary<string, StatMetrics>(statMetrics);
        }

        public StatMetrics GetStatMetrics(string statId)
        {
            return statMetrics.TryGetValue(statId, out var metrics) ? metrics : null;
        }
    }
}