using UnityEngine;

namespace NinjaGame.Progression
{
    // "Dodge costs 2 less stamina", "Techniques cool down 1 s faster": flat changes to the abilities a filter picks,
    // held while the reward is.
    [CreateAssetMenu(fileName = "Reward_AbilityModifier", menuName = "NinjaGame/Rewards/Ability Modifier")]
    public class AbilityModifierReward : Reward
    {
        [SerializeField] private AbilityFilter filter;

        [Tooltip("Resource whose cost changes. Empty = no cost change.")]
        [SerializeField] private ResourceDefinition resource;
        [Tooltip("Added to that cost. Negative is cheaper.")]
        [SerializeField] private float costDelta;
        [Tooltip("Seconds added to the cooldown. Negative is faster.")]
        [SerializeField] private float cooldownDelta;
        [Tooltip("Seconds added to the cast time. Negative is faster.")]
        [SerializeField] private float castTimeDelta;

        [Tooltip("Multiply by the granter's scale — a talent's rank. Leave off on a stat track, where the scale is the " +
                 "stat's whole value (equip.load 8 would make +1 into +8).")]
        [SerializeField] private bool perRank = true;

        public override void Apply(RewardContext context)
        {
            AbilitySystem abilities = context.Brain != null ? context.Brain.Abilities : null;
            if (abilities == null) return;

            float scale = perRank ? context.Scale : 1f;
            abilities.Modifiers.Set(context.SourceKey, new AbilityModifier
            {
                Filter = filter,
                Resource = resource,
                CostDelta = costDelta * scale,
                CooldownDelta = cooldownDelta * scale,
                CastTimeDelta = castTimeDelta * scale
            });
        }

        public override void Remove(RewardContext context)
        {
            AbilitySystem abilities = context.Brain != null ? context.Brain.Abilities : null;
            if (abilities != null) abilities.Modifiers.Remove(context.SourceKey);
        }

        public override string Summarise()
        {
            string target = filter.Describe();
            if (resource != null && costDelta != 0f) return $"{target}: {Signed(costDelta)} {resource.displayName} cost";
            if (cooldownDelta != 0f) return $"{target}: {Signed(cooldownDelta)} s cooldown";
            return $"{target}: {Signed(castTimeDelta)} s cast time";
        }

        private static string Signed(float value) => $"{(value >= 0 ? "+" : "")}{value:0.##}";
    }
}
