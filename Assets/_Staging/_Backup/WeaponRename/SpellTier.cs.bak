using System;
using UnityEngine;

namespace NinjaGame.Magic
{
    /// <summary>
    /// Magnitude. ORDINAL on purpose — Split steps down and Amplify steps up, so the tier has to
    /// be something you can do arithmetic on. That is why this is an enum indexing a table rather
    /// than a set of loose assets.
    ///
    /// Never reorder. Split and Amplify move by ±1 along this order.
    /// </summary>
    public enum SpellTier
    {
        Faint = 0,
        Lesser = 1,
        Standard = 2,
        Greater = 3,
        Grand = 4,
    }

    /// <summary>
    /// One rung of the ladder. Dice, size and cost move together, which is what makes
    /// "a bigger projectile hits harder" true by construction rather than by an artist
    /// remembering to match them.
    /// </summary>
    [Serializable]
    public class SpellTierRow
    {
        [Tooltip("Which rung this row describes. Must match its index in the table.")]
        public SpellTier tier = SpellTier.Standard;

        [Tooltip("Display name for tooltips and debug — 'Standard'. The name the PLAYER sees comes " +
                 "from the School's tierNames, not from here.")]
        public string displayName = "Standard";

        [Tooltip("THE DICE. A WeaponData asset — spells are weapons, so a spell rolls dice through " +
                 "the same weaponOverride path a thrown shuriken uses. Shared across all six schools: " +
                 "the damage TYPE comes from the School's cast ability, not from here.")]
        public WeaponData dice;

        [Tooltip("Uniform scale applied to the projectile. This is the readability contract — a d10 " +
                 "orb is visibly bigger than a d4 orb because both come off this one number.")]
        public float scale = 1f;

        [Tooltip("Multiplies the sequence cost. A Grand cast costs nearly twice a Standard one on " +
                 "top of the length curve.")]
        public float costMultiplier = 1f;
    }

    /// <summary>
    /// The ladder. ONE table, authored in one inspector, because this is where balance actually
    /// happens — five rows side by side is legible in a way five separate assets are not.
    ///
    /// Lives inline on SpellGrammar rather than as its own asset for the same reason
    /// ProjectileMovement lives inline on ProjectileData: once it is rows rather than behaviour,
    /// a separate asset only buys indirection.
    /// </summary>
    [Serializable]
    public class SpellTierTable
    {
        [Tooltip("Five rows, Faint through Grand, in order.")]
        public SpellTierRow[] rows = new SpellTierRow[0];

        /// <summary>
        /// The row for a tier, or null. Scans rather than indexing, so a mis-ordered or short
        /// array returns null instead of throwing IndexOutOfRange mid-cast.
        ///
        /// Silent on a miss: the caller has the context to say something useful, and the
        /// grammar validator calls this in a loop where an error per rung would be noise.
        /// </summary>
        public SpellTierRow Get(SpellTier tier)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && rows[i].tier == tier)
                    return rows[i];
            }

            return null;
        }

        /// <summary>
        /// Move along the ladder, CLAMPED at both ends. Splitting an already-Faint Form does not
        /// fall off the bottom, and amplifying a Grand cast does not fall off the top.
        /// </summary>
        public static SpellTier Step(SpellTier tier, int steps)
        {
            int index = (int)tier + steps;

            if (index < (int)SpellTier.Faint) index = (int)SpellTier.Faint;
            if (index > (int)SpellTier.Grand) index = (int)SpellTier.Grand;

            return (SpellTier)index;
        }
    }
}
