namespace NinjaGame.Progression
{
    /// <summary>
    /// Collects additions to named rolls — weapon damage, crit, initiative. Implement this
    /// on whatever resolves rolls and consult it when building the expression.
    ///
    /// Everything is additive: extra dice and flat bonuses join the expression rather than
    /// scaling it, so a katana at 1d8 + 2 with a +1d4 and a +2 reads 1d8 + 1d4 + 4 — still
    /// dice, still a minimum and maximum you can work out in your head.
    /// </summary>
    public interface IRollModifier
    {
        /// <summary>Adds to a roll under a key. Re-adding under the same key replaces.</summary>
        void AddModifier(string rollId, string sourceKey, RollModifier modifier);

        void RemoveModifier(string rollId, string sourceKey);
    }

    /// <summary>Extra dice and a flat amount added to one roll.</summary>
    [System.Serializable]
    public struct RollModifier
    {
        public int diceCount;
        public int diceSides;
        public float flat;

        public bool IsEmpty => diceCount <= 0 && flat == 0f;

        public string Label()
        {
            string dice = diceCount > 0 ? $"{diceCount}d{diceSides}" : "";
            string bonus = flat != 0f ? $"{(flat > 0f ? "+" : "")}{flat:0.##}" : "";

            if (dice.Length == 0) return bonus;
            return bonus.Length == 0 ? dice : $"{dice} {bonus}";
        }
    }
}
