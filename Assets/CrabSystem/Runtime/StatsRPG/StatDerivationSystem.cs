using System.Collections.Generic;
using NinjaGame.Progression;
using NinjaGame.Stats;
using UnityEngine;

/// <summary>
/// Grants what the core stats have earned. Every threshold a stat has passed applies its
/// rewards; anything below the current value is taken back.
///
/// Re-evaluated whenever a driving stat changes, so a per-point reward keeps up as the
/// stat rises and a lost point removes what it paid for. Rewards apply under a key that
/// names the breakpoint, so re-applying replaces rather than stacks.
/// </summary>
public class StatDerivationSystem : MonoBehaviour, IBrainModule
{
    [Header("Table")]
    [Tooltip("Core → reward thresholds. Leave empty and this module does nothing.")]
    [SerializeField] private StatDerivationTable table;

    public bool IsEnabled { get; set; } = true;

    // A reward can raise a stat that drives another derivation. Changes that arrive while
    // draining are queued and picked up by the next pass; the cap stops a cycle that never
    // settles.
    private const int MaxPasses = 32;

    private ControllerBrain brain;
    private IStatProvider stats;
    private bool draining;

    private readonly Dictionary<string, List<StatDerivation>> bySource = new();
    private readonly HashSet<string> dirty = new();
    private readonly List<string> pass = new();

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = controllerBrain.GetModule<StatSystem>();
    }

    // Runs after every module has initialised, so schemas are loaded and any saved core
    // values are in place before the first evaluation.
    public void LateInitialize()
    {
        if (stats == null || table == null) return;

        Index();

        stats.OnStatChanged += HandleStatChanged;

        dirty.UnionWith(bySource.Keys);
        Drain();
    }

    public void UpdateModule() { }

    private void OnDestroy()
    {
        if (stats != null) stats.OnStatChanged -= HandleStatChanged;
    }

    private void Index()
    {
        bySource.Clear();

        foreach (var derivation in table.Derivations)
        {
            if (derivation == null || string.IsNullOrEmpty(derivation.sourceStatId)) continue;

            if (!bySource.ContainsKey(derivation.sourceStatId)) bySource[derivation.sourceStatId] = new List<StatDerivation>();
            bySource[derivation.sourceStatId].Add(derivation);
        }
    }

    private void HandleStatChanged(string statId, float oldValue, float newValue)
    {
        if (!bySource.ContainsKey(statId)) return;

        dirty.Add(statId);
        if (draining) return;

        Drain();
    }

    private void Drain()
    {
        draining = true;

        int passes = 0;
        while (dirty.Count > 0 && passes < MaxPasses)
        {
            pass.Clear();
            pass.AddRange(dirty);
            dirty.Clear();

            foreach (var statId in pass) EvaluateSource(statId);
            passes++;
        }

        if (dirty.Count > 0)
            Debug.LogError($"[StatDerivationSystem] Still changing after {MaxPasses} passes: {string.Join(", ", dirty)}");

        dirty.Clear();
        draining = false;
    }

    private void EvaluateSource(string statId)
    {
        foreach (var derivation in bySource[statId]) Evaluate(derivation);
    }

    private void Evaluate(StatDerivation derivation)
    {
        if (derivation == null || string.IsNullOrEmpty(derivation.sourceStatId)) return;

        float sourceValue = stats.GetValue(derivation.sourceStatId);

        for (int i = 0; i < derivation.breakpoints.Count; i++)
        {
            var breakpoint = derivation.breakpoints[i];
            if (breakpoint == null) continue;

            bool earned = sourceValue >= breakpoint.threshold;

            for (int r = 0; r < breakpoint.rewards.Count; r++)
            {
                var reward = breakpoint.rewards[r];
                if (reward == null) continue;

                // The index keeps two rewards in one breakpoint from sharing a key when
                // they happen to feed the same stat.
                var context = new RewardContext(
                    brain,
                    $"derive:{derivation.sourceStatId}:{breakpoint.threshold}:{r}",
                    sourceValue);

                if (earned) reward.Apply(context);
                else reward.Remove(context);
            }
        }
    }
}
