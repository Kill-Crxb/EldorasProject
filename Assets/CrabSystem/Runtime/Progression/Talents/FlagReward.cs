using UnityEngine;

namespace NinjaGame.Progression
{
    // Holds a blackboard fact true for as long as the reward is held: a capability grant (SprintGranted,
    // DoubleJumpGranted) or a standing boon. Claims under the reward's source key on the blackboard's ledger,
    // so a status raising the same fact and expiring can't clear the talent's grant, and re-applying is safe.
    [CreateAssetMenu(fileName = "Reward_Flag", menuName = "NinjaGame/Rewards/Flag")]
    public class FlagReward : Reward
    {
        [IdRef(IdKind.Fact)] [SerializeField] private string fact;

        public override void Apply(RewardContext context)
        {
            Blackboard blackboard = context.Brain != null ? context.Brain.Blackboard : null;
            if (blackboard == null || string.IsNullOrEmpty(fact)) return;

            blackboard.Claim(new BlackboardKey(fact).hash, context.SourceKey);
        }

        public override void Remove(RewardContext context)
        {
            Blackboard blackboard = context.Brain != null ? context.Brain.Blackboard : null;
            if (blackboard == null || string.IsNullOrEmpty(fact)) return;

            blackboard.Release(new BlackboardKey(fact).hash, context.SourceKey);
        }

        public override string Summarise() => $"Grants {fact}";
    }
}
