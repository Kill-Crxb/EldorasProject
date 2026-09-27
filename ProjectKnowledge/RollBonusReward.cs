using UnityEngine;

namespace NinjaGame.Progression
{
    /// <summary>
    /// Adds dice, a flat amount, or both to a named roll. "+1d4 to crit rolls",
    /// "+2 weapon damage", or an ability upgrade that does both.
    ///
    /// Purely additive by design — a katana at 1d8 + 2 with this reward reads 1d8 + 1d4 + 2,
    /// which still has a minimum and a maximum you can work out in your head.
    ///
    /// Inert until something on the entity implements IRollModifier.
    /// </summary>
    [CreateAssetMenu(fileName = "Reward_RollBonus", menuName = "NinjaGame/Rewards/Roll Bonus")]
    public class RollBonusReward : Reward
    {
        [Tooltip("Roll this modifies, e.g. 'weapon', 'crit'.")]
        [SerializeField] private string rollId = "weapon";

        [SerializeField] private RollModifier modifier = new RollModifier { diceCount = 0, diceSides = 6, flat = 1f };

        public override void Apply(RewardContext context)
        {
            var roller = Roller(context);
            if (roller == null) return;

            roller.AddModifier(rollId, context.SourceKey, modifier);
        }

        public override void Remove(RewardContext context)
        {
            var roller = Roller(context);
            if (roller == null) return;

            roller.RemoveModifier(rollId, context.SourceKey);
        }

        public override string Summarise() => $"{modifier.Label()} to {rollId} rolls";

        private static IRollModifier Roller(RewardContext context)
        {
            return context.Brain != null ? context.Brain.GetModule<IRollModifier>() : null;
        }
    }
}
