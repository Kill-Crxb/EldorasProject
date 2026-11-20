using CrabSystem.Progression;
using UnityEngine;

// Grants a feat for as long as the reward is held. A trait, a talent node or a race holds one of these to give a
// feat; withdrawing the reward takes back exactly that grant and nothing else.

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "Reward_Feat", menuName = "CrabSystem/Rewards/Feat")]
    public class GrantFeatReward : Reward
    {
        [SerializeField] private FeatDefinition feat;

        public override void Apply(RewardContext context)
        {
            FeatModule feats = context.Brain != null ? context.Brain.GetModule<FeatModule>() : null;
            if (feats == null || feat == null) return;

            feats.Grant(feat, context.SourceKey);
        }

        public override void Remove(RewardContext context)
        {
            FeatModule feats = context.Brain != null ? context.Brain.GetModule<FeatModule>() : null;
            if (feats == null) return;

            feats.Withdraw(context.SourceKey);
        }

        public override string Summarise()
        {
            return feat != null ? $"Grants {feat.displayName}" : "Grants a feat";
        }
    }
}
