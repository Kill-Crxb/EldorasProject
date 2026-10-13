using UnityEngine;

namespace CrabSystem.Progression
{
    // Something granted by reaching a threshold: a stat bonus, a talent point, a dice
    // modifier. Authored as an asset so the same reward can be referenced by a stat
    // breakpoint, a talent node and an item affix without being described three times.
    //
    // Rewards are applied and removed by key. Applying twice under one key replaces
    // rather than stacks, so re-evaluating a threshold is always safe.
    public abstract class Reward : ScriptableObject
    {
        [Tooltip("Shown in UI. Leave empty to use the generated summary.")]
        [SerializeField] private string displayName;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? Summarise() : displayName;

        public abstract void Apply(RewardContext context);

        public abstract void Remove(RewardContext context);

        // One line describing what this gives, for tooltips and debug output.
        public abstract string Summarise();
    }

    // Who is being rewarded, under what key, and by how much when the reward scales.
    public readonly struct RewardContext
    {
        // The entity receiving it.
        public readonly ControllerBrain Brain;

        // Identifies the granter so removal is exact — "derive:core.spirit:10",
        // "talent:shadowstep". Re-applying under the same key replaces the last amount.
        public readonly string SourceKey;

        // What a per-point reward multiplies. The driving stat's value when a stat
        // breakpoint granted this; a rank for a talent; 1 where nothing scales.
        public readonly float Scale;

        public RewardContext(ControllerBrain brain, string sourceKey, float scale = 1f)
        {
            Brain = brain;
            SourceKey = sourceKey;
            Scale = scale;
        }
    }
}
