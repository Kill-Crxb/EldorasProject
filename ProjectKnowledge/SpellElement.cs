using System.Text;

namespace NinjaGame.Magic
{
    /// <summary>
    /// The six elements, and the six positions they can occupy.
    ///
    /// One element means ONE CONCEPT in every slot — Fire is always aggression and magnitude,
    /// Air is always speed and multiplicity. That is what turns 36 mappings into 6 concepts seen
    /// through 6 lenses, and it is the rule that keeps the grammar learnable. Nothing in this
    /// system may special-case an element in one slot and not another.
    ///
    /// The enum order is the canonical order and is used for hotbar layout and for the sequence
    /// key. NEVER reorder it — sequence keys are persisted in save data as strings, but element
    /// indices leak into inspector-authored arrays.
    /// </summary>
    public enum SpellElement
    {
        Fire,
        Water,
        Air,
        Earth,
        Nature,
        Aether,
    }

    /// <summary>
    /// What each position in the sequence decides. Position is meaning: the third element
    /// entered is the Effect regardless of which element it is.
    ///
    /// A slot NEVER changes meaning based on sequence length. Short sequences fall back to the
    /// slot's default (see SpellGrammar), which is what makes every sequence a complete spell
    /// and removes fizzle handling entirely.
    /// </summary>
    public enum SpellSlot
    {
        School = 0,
        Form = 1,
        Effect = 2,
        Modify = 3,
        Range = 4,
        Amplify = 5,
    }

    /// <summary>
    /// Turning a sequence of elements into the dictionary key that authored spells are indexed
    /// by, and back again for save data.
    ///
    /// The key is lowercase element names joined by dots — "fire.air.air". Readable in the
    /// inspector, diffable in a save file, and stable as long as the enum names do not change.
    /// </summary>
    public static class SpellSequence
    {
        /// <summary>The longest sequence the grammar has positions for.</summary>
        public const int MaxLength = 6;

        private static readonly StringBuilder Builder = new StringBuilder(48);

        /// <summary>
        /// Build the lookup key for a sequence. Allocation-free apart from the final string,
        /// which is unavoidable because it is the dictionary key.
        /// </summary>
        public static string ToKey(SpellElement[] elements, int length)
        {
            if (elements == null || length <= 0) return string.Empty;

            Builder.Clear();

            for (int i = 0; i < length && i < elements.Length; i++)
            {
                if (i > 0) Builder.Append('.');
                Builder.Append(Name(elements[i]));
            }

            return Builder.ToString();
        }

        /// <summary>Lowercase name used in keys. Explicit rather than ToString().ToLower() so a
        /// rename of the enum is a compile error here instead of a silent key change.</summary>
        public static string Name(SpellElement element)
        {
            switch (element)
            {
                case SpellElement.Fire:   return "fire";
                case SpellElement.Water:  return "water";
                case SpellElement.Air:    return "air";
                case SpellElement.Earth:  return "earth";
                case SpellElement.Nature: return "nature";
                case SpellElement.Aether: return "aether";
            }

            return "unknown";
        }
    }
}
