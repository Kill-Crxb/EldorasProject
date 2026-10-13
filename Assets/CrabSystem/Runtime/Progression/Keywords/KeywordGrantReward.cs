using UnityEngine;

namespace NinjaGame.Progression
{
    // "Basic attacks gain Sinister": gives the abilities a filter picks a keyword, for this character, while held.
    [CreateAssetMenu(fileName = "Reward_KeywordGrant", menuName = "NinjaGame/Rewards/Keyword Grant")]
    public class KeywordGrantReward : Reward
    {
        [SerializeField] private KeywordDefinition keyword;
        [SerializeField] private AbilityFilter filter;

        public override void Apply(RewardContext context)
        {
            AbilitySystem abilities = context.Brain != null ? context.Brain.Abilities : null;
            if (abilities == null || keyword == null) return;
            abilities.Modifiers.SetTag(context.SourceKey, filter, keyword.keywordId);
        }

        public override void Remove(RewardContext context)
        {
            AbilitySystem abilities = context.Brain != null ? context.Brain.Abilities : null;
            if (abilities != null) abilities.Modifiers.RemoveTag(context.SourceKey);
        }

        public override string Summarise() => $"{filter.Describe()} gain {(keyword != null ? keyword.Label : "a keyword")}";
    }
}
