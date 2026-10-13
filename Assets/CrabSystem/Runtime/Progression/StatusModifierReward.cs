using UnityEngine;

namespace NinjaGame.Progression
{
    // Resist ("Frightened on you lasts 2 s less"), Immune ("can't be Charmed") and Intensify ("your Sundered lasts
    // 3 s longer and lands an extra stack"): flat changes to the statuses a filter picks, held while the reward is.
    [CreateAssetMenu(fileName = "Reward_StatusModifier", menuName = "NinjaGame/Rewards/Status Modifier")]
    public class StatusModifierReward : Reward
    {
        [SerializeField] private StatusFilter filter;

        [Tooltip("Taken = statuses landing on you (Resist, Immune). Dealt = statuses you apply (Intensify).")]
        [SerializeField] private StatusSide side = StatusSide.Taken;

        [Tooltip("Taken only: the status never lands.")]
        [SerializeField] private bool immune;
        [Tooltip("Seconds added to the status's duration. Negative is shorter; at 0 or below it doesn't land. " +
                 "Statuses with no duration (until removed) are unaffected.")]
        [SerializeField] private float seconds;
        [Tooltip("Extra stacks per application, for statuses that stack.")]
        [SerializeField] private int stacks;

        [Tooltip("Multiply by the granter's scale — a talent's rank. Off on a stat track.")]
        [SerializeField] private bool perRank = true;

        public override void Apply(RewardContext context)
        {
            StatusSystem statuses = context.Brain != null ? context.Brain.GetModule<StatusSystem>() : null;
            if (statuses == null) return;

            int scale = perRank ? Mathf.Max(1, Mathf.RoundToInt(context.Scale)) : 1;
            statuses.Modifiers.Set(context.SourceKey, new StatusModifier
            {
                Filter = filter,
                Side = side,
                Immune = immune && side == StatusSide.Taken,
                Seconds = seconds * scale,
                Stacks = stacks * scale
            });
        }

        public override void Remove(RewardContext context)
        {
            StatusSystem statuses = context.Brain != null ? context.Brain.GetModule<StatusSystem>() : null;
            if (statuses != null) statuses.Modifiers.Remove(context.SourceKey);
        }

        public override string Summarise()
        {
            string target = filter.Describe();
            if (immune && side == StatusSide.Taken) return $"Immune to {target}";

            string who = side == StatusSide.Taken ? "on you" : "you apply";
            if (stacks != 0) return $"{target} {who}: +{stacks} stack";
            return $"{target} {who}: {(seconds >= 0 ? "+" : "")}{seconds:0.##} s";
        }
    }
}
