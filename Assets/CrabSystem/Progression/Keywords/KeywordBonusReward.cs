using UnityEngine;

namespace NinjaGame.Progression
{
    // "Ambushing gains 1d4", "Sinister hits apply Sundered": adds to a keyword while held — to-hit, bonus dice, flat
    // damage, a status to inflict. Scaled by the talent's rank when perRank is on (the status isn't).
    [CreateAssetMenu(fileName = "Reward_KeywordBonus", menuName = "NinjaGame/Rewards/Keyword Bonus")]
    public class KeywordBonusReward : Reward
    {
        [SerializeField] private KeywordDefinition keyword;
        [SerializeField] private float accuracy;
        [SerializeField] private int dice;
        [Tooltip("Die size for the dice. The keyword's own size is used when larger.")]
        [SerializeField] private int dieFaces = 4;
        [SerializeField] private float flat;
        [Tooltip("Also applied to the target when the keyword's hit lands unguarded and its condition holds (Inflict).")]
        [SerializeField] private StatusDefinition inflict;
        [Tooltip("Multiply by the granter's scale — a talent's rank. Off on a stat track.")]
        [SerializeField] private bool perRank = true;

        public override void Apply(RewardContext context)
        {
            KeywordModule keywords = context.Brain != null ? context.Brain.GetModule<KeywordModule>() : null;
            if (keywords == null || keyword == null) return;

            int scale = perRank ? Mathf.Max(1, Mathf.RoundToInt(context.Scale)) : 1;
            keywords.SetBonus(context.SourceKey, new KeywordBonus
            {
                KeywordId = keyword.keywordId,
                Accuracy = accuracy * scale,
                Dice = dice * scale,
                DieFaces = dieFaces,
                Flat = flat * scale,
                Inflict = inflict
            });
        }

        public override void Remove(RewardContext context)
        {
            KeywordModule keywords = context.Brain != null ? context.Brain.GetModule<KeywordModule>() : null;
            if (keywords != null) keywords.RemoveBonus(context.SourceKey);
        }

        public override string Summarise()
        {
            string name = keyword != null ? keyword.Label : "keyword";
            if (inflict != null) return $"{name} hits apply {(string.IsNullOrEmpty(inflict.displayName) ? inflict.id : inflict.displayName)}";
            if (dice > 0) return $"{name}: +{dice}d{dieFaces}";
            if (accuracy != 0f) return $"{name}: +{accuracy:0.#} to hit";
            return $"{name}: +{flat:0.#} damage";
        }
    }
}
