using CrabSystem.Progression;
using UnityEngine;

// Installs an organ for as long as the reward is held. This is how a trait grants its eyes: the trait's only reward.
// Withdrawing the reward takes back exactly the organ it installed, and nothing it didn't.

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "Reward_Organ", menuName = "CrabSystem/Rewards/Organ")]
    public class GrantOrganReward : Reward
    {
        [SerializeField] private OrganDefinition organ;

        public override void Apply(RewardContext context)
        {
            OrganModule organs = context.Brain != null ? context.Brain.GetModule<OrganModule>() : null;
            if (organs == null || organ == null) return;

            organs.Install(organ.organId, context.SourceKey);
        }

        public override void Remove(RewardContext context)
        {
            OrganModule organs = context.Brain != null ? context.Brain.GetModule<OrganModule>() : null;
            if (organs == null) return;

            organs.Withdraw(context.SourceKey);
        }

        public override string Summarise()
        {
            return organ != null ? $"Grants {organ.displayName}" : "Grants an organ";
        }
    }
}
