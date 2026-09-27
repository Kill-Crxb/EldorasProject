using UnityEngine;

namespace NinjaGame.Progression
{
    /// <summary>
    /// Talent points for one tree. "1 talent point for spirit-aligned trees."
    ///
    /// Inert until something on the entity implements ITalentPointPool. Authoring the
    /// reward now is still worth it — the balance data is the slow part, and the day the
    /// talent module lands it starts paying out with no change here.
    /// </summary>
    [CreateAssetMenu(fileName = "Reward_TalentPoint", menuName = "NinjaGame/Rewards/Talent Point")]
    public class TalentPointReward : Reward
    {
        [Tooltip("Tree these points may be spent in, e.g. 'spirit'.")]
        [SerializeField] private string treeId;

        [SerializeField] private int points = 1;

        public override void Apply(RewardContext context)
        {
            Pool(context)?.GrantPoints(treeId, context.SourceKey, points);
        }

        public override void Remove(RewardContext context)
        {
            Pool(context)?.RevokePoints(treeId, context.SourceKey);
        }

        public override string Summarise() => $"{points} {treeId} talent point{(points == 1 ? "" : "s")}";

        private static ITalentPointPool Pool(RewardContext context)
        {
            return context.Brain != null ? context.Brain.GetModule<ITalentPointPool>() : null;
        }
    }
}
