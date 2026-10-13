using UnityEngine;

namespace CrabSystem.Progression
{
    // Grants an ability for as long as the reward is held. Works from a stat breakpoint,
    // a talent node or an item — RuntimeAbilityManager already tracks abilities by source,
    // so withdrawing the reward removes exactly the one it added.
    [CreateAssetMenu(fileName = "Reward_Ability", menuName = "CrabSystem/Rewards/Ability")]
    public class AbilityReward : Reward
    {
        [SerializeField] private AbilityDefinition ability;

        [Tooltip("Lifecycle this ability follows. Equipment is removed on unequip; Talent for talents (each " +
                 "grant is its own instance, so two sources of one ability don't take it from each other); " +
                 "Permanent survives until the reward itself is withdrawn.")]
        [SerializeField] private AbilitySource source = AbilitySource.Permanent;

        public override void Apply(RewardContext context)
        {
            var abilities = Abilities(context);
            if (abilities == null || ability == null) return;

            abilities.AddAbility(ability, source, context.SourceKey);
        }

        public override void Remove(RewardContext context)
        {
            var abilities = Abilities(context);
            if (abilities == null) return;

            abilities.RemoveBySource(source, context.SourceKey);
        }

        public override string Summarise()
        {
            return ability != null ? $"Grants {ability.abilityName}" : "Grants an ability";
        }

        private static RuntimeAbilityManager Abilities(RewardContext context)
        {
            return context.Brain != null ? context.Brain.GetModule<RuntimeAbilityManager>() : null;
        }
    }
}
