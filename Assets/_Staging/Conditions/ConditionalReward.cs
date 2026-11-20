using System.Collections.Generic;
using CrabSystem.Progression;
using UnityEngine;

// Any reward, held only while its conditions hold on this character: "wearing body armour: +1 armour defence",
// "wielding a heavy weapon: +2 damage". One reward type makes every other one conditional, so there is no
// conditional variant per reward. The character's ConditionModule applies and removes the inner reward as the
// conditions change, under this reward's key.
namespace CrabSystem
{
    [CreateAssetMenu(fileName = "Reward_Conditional", menuName = "CrabSystem/Rewards/Conditional")]
    public class ConditionalReward : Reward
    {
        [Tooltip("All must hold, on this character. A held reward has no target, so Target and Behind never hold here.")]
        [SerializeField] private List<Condition> conditions = new();

        [Tooltip("Applied while the conditions hold, removed when they stop.")]
        [SerializeField] private Reward reward;

        public List<Condition> When => conditions;
        public Reward Inner => reward;

        public override void Apply(RewardContext context)
        {
            ConditionModule module = context.Brain != null ? context.Brain.GetModule<ConditionModule>() : null;
            if (module == null)
            {
                Debug.LogError($"[{name}] {context.Brain?.name} has no ConditionModule.", this);
                return;
            }
            module.Set(context, this);
        }

        public override void Remove(RewardContext context)
        {
            ConditionModule module = context.Brain != null ? context.Brain.GetModule<ConditionModule>() : null;
            if (module != null) module.Remove(context.SourceKey);
        }

        public override string Summarise()
        {
            string inner = reward != null ? reward.DisplayName : "nothing";
            return $"{inner} while {Conditions.Describe(conditions)}";
        }
    }
}
