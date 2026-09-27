using UnityEngine;

namespace NinjaGame.Progression
{
    /// <summary>
    /// Something granted by reaching a threshold: a stat bonus, a talent point, a dice
    /// modifier. Authored as an asset so the same reward can be referenced by a stat
    /// breakpoint, a talent node and an item affix without being described three times.
    ///
    /// Rewards are applied and removed by key. Applying twice under one key replaces
    /// rather than stacks, so re-evaluating a threshold is always safe.
    /// </summary>
    public abstract class Reward : ScriptableObject
    {
        [Tooltip("Shown in UI. Leave empty to use the generated summary.")]
        [SerializeField] private string displayName;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? Summarise() : displayName;

        public abstract void Apply(RewardContext context);

        public abstract void Remove(RewardContext context);

        /// <summary>One line describing what this gives, for tooltips and debug output.</summary>
        public abstract string Summarise();
    }

    /// <summary>
    /// Who is being rewarded, under what key, and by how much when the reward scales.
    /// </summary>
    public readonly struct RewardContext
    {
        /// <summary>The entity receiving it.</summary>
        public readonly ControllerBrain Brain;

        /// <summary>
        /// Identifies the granter so removal is exact — "derive:core.spirit:10",
        /// "talent:shadowstep". Re-applying under the same key replaces the last amount.
        /// </summary>
        public readonly string SourceKey;

        /// <summary>
        /// What a per-point reward multiplies. The driving stat's value when a stat
        /// breakpoint granted this; a rank for a talent; 1 where nothing scales.
        /// </summary>
        public readonly float Scale;

        public RewardContext(ControllerBrain brain, string sourceKey, float scale = 1f)
        {
            Brain = brain;
            SourceKey = sourceKey;
            Scale = scale;
        }
    }
}
