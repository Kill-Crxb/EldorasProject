using UnityEngine;

namespace NinjaGame.Progression
{
    /// <summary>A flat amount added to one or more stats. "+1 Armor and Evasion".</summary>
    [CreateAssetMenu(fileName = "Reward_StatBonus", menuName = "NinjaGame/Rewards/Stat Bonus")]
    public class StatBonusReward : Reward
    {
        [Tooltip("Stat ids this adds to, e.g. 'scr.armor'.")]
        [IdRef(IdKind.Stat)] [SerializeField] private string[] targetStatIds;

        [SerializeField] private float amount = 1f;

        public override void Apply(RewardContext context)
        {
            var stats = context.Brain != null ? context.Brain.GetModule<StatSystem>() : null;
            if (stats == null) return;

            foreach (string statId in targetStatIds)
            {
                if (string.IsNullOrEmpty(statId)) continue;

                if (!stats.HasStat(statId))
                {
                    Debug.LogWarning($"[{name}] '{statId}' is not loaded on {context.Brain.name}", this);
                    continue;
                }

                stats.AddContribution(statId, context.SourceKey, amount);
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
            return $"{(amount >= 0 ? "+" : "")}{amount:0.##} {string.Join(", ", targetStatIds)}";
        }
    }
}
