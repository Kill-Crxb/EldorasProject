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

    private ControllerBrain brain;
    private IStatProvider stats;
    private bool evaluating;

    private readonly Dictionary<string, StatDerivation> bySource = new();

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

        foreach (var derivation in table.Derivations) Evaluate(derivation);
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

            bySource[derivation.sourceStatId] = derivation;
        }
    }

    private void HandleStatChanged(string statId, float oldValue, float newValue)
    {
        // Applying a reward changes a stat, which raises this event again. A stat that is
        // both a source and a target would otherwise recurse.
        if (evaluating) return;

        if (!bySource.TryGetValue(statId, out var derivation)) return;

        Evaluate(derivation);
    }

    private void Evaluate(StatDerivation derivation)
    {
        if (derivation == null || string.IsNullOrEmpty(derivation.sourceStatId)) return;

        float sourceValue = stats.GetValue(derivation.sourceStatId);

        evaluating = true;

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

        evaluating = false;
    }
}
