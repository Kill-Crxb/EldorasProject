using UnityEngine;

namespace CrabSystem.Progression
{
    // "Your Sinister abilities become Shadow Strike": every ability the filter picks plays the replacement instead,
    // for this character, while held. The replacement keeps the filter's tag (Shadow Strike is still Sinister).
    [CreateAssetMenu(fileName = "Reward_AbilityReplace", menuName = "CrabSystem/Rewards/Ability Replace")]
    public class AbilityReplaceReward : Reward
    {
        [SerializeField] private AbilityFilter filter;
        [SerializeField] private AbilityDefinition replacement;

        public override void Apply(RewardContext context)
        {
            AbilitySystem abilities = context.Brain != null ? context.Brain.Abilities : null;
            if (abilities == null || replacement == null) return;

            if (abilities.GetAbility(replacement.abilityId) == null) abilities.AddAbility(replacement);
            abilities.Modifiers.SetReplacement(context.SourceKey, filter, replacement);
        }

        public override void Remove(RewardContext context)
        {
            AbilitySystem abilities = context.Brain != null ? context.Brain.Abilities : null;
            if (abilities != null) abilities.Modifiers.RemoveReplacement(context.SourceKey);
        }

        public override string Summarise()
            => $"{filter.Describe()} becomes {(replacement != null ? replacement.abilityName : "another ability")}";
    }
}
