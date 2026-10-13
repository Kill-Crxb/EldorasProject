using UnityEngine;

namespace NinjaGame.Progression
{
    /// <summary>
    /// An amount per point of whatever drives it. "+0.5 Initiative per Spirit" — at Spirit 5
    /// that is 2.5, and it keeps climbing as Spirit does, because the breakpoint is
    /// re-evaluated whenever the driving stat changes.
    /// </summary>
    [CreateAssetMenu(fileName = "Reward_StatRate", menuName = "NinjaGame/Rewards/Stat Rate")]
    public class StatRateReward : Reward
    {
        [Tooltip("Stat ids this adds to, e.g. 'cmb.initiative'.")]
        [IdRef(IdKind.Stat)] [SerializeField] private string[] targetStatIds;

        [Tooltip("Added per point of the driving value.")]
        [SerializeField] private float perPoint = 0.5f;

        public override void Apply(RewardContext context)
        {
            var stats = context.Brain != null ? context.Brain.GetModule<StatSystem>() : null;
            if (stats == null) return;

            float total = perPoint * context.Scale;

            foreach (string statId in targetStatIds)
            {
                if (string.IsNullOrEmpty(statId)) continue;

                if (!stats.HasStat(statId))
                {
                    Debug.LogWarning($"[{name}] '{statId}' is not loaded on {context.Brain.name}", this);
                    continue;
                }

                stats.AddContribution(statId, context.SourceKey, total);
            }
        }

        public override void Remove(RewardContext context)
        {
            var stats = context.Brain != null ? context.Brain.GetModule<StatSystem>() : null;
            if (stats == null) return;

            foreach (string statId in targetStatIds)
            {
                if (!string.IsNullOrEmpty(statId)) stats.RemoveContribution(statId, context.SourceKey);
            }
        }

        public override string Summarise()
        {
            return $"{(perPoint >= 0 ? "+" : "")}{perPoint:0.##} {string.Join(", ", targetStatIds)} per point";
        }
    }
}
