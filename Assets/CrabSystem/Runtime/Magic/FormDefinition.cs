using UnityEngine;

namespace NinjaGame.Magic
{
    /// <summary>
    /// Everything slot 2 (Form) decides: how the spell is delivered, and HOW BIG THE DIE IS.
    ///
    /// SIX ASSETS, one per element. Fire is a Cone, Air is a Projectile, Aether is a Beam.
    ///
    /// WHY FORM SETS THE TIER:
    /// Form already decides how many instances and how many ticks a cast produces — which IS the
    /// per-instance magnitude. A Stream that ticks ten times obviously cannot roll d8 per tick;
    /// a Beam that lands once, instantly, precisely, should hit hardest. Authoring the base tier
    /// here just makes explicit something the Form already implies.
    ///
    /// Fire in slot 2 still MEANS Cone. The d6 is a consequence of Cone, not a second meaning the
    /// player has to memorise — which is what keeps the one-concept-per-element rule intact.
    ///
    ///   Beam       one hit, instant, precise      Greater  (d10)
    ///   Projectile one hit, travels               Standard (d8)
    ///   Burst      one hit, area                  Standard (d8)
    ///   Cone       several targets at once        Lesser   (d6)
    ///   Stream     ticks while channelled         Faint    (d4)
    ///   Field      ticks while it lingers         Faint    (d4)
    /// </summary>
    [CreateAssetMenu(fileName = "Form_", menuName = "NinjaGame/Magic/Form Definition")]
    public class FormDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Which element selects this Form when entered in slot 2.")]
        public SpellElement element = SpellElement.Air;

        [Tooltip("Name shown in UI — 'Projectile', 'Cone', 'Beam'.")]
        public string displayName = "Projectile";

        [Header("Delivery")]
        [Tooltip("The ProjectileData this Form fires. SpellcraftSystem swaps the cast ability's " +
                 "projectileData for this one at launch, which is why one archetype prefab and six " +
                 "data assets cover every school.\n\n" +
                 "The visualPrefab on this asset is the Form half of the visual split — the SHAPE. " +
                 "Colour comes from the School.")]
        public ProjectileData projectileData;

        [Tooltip("The rung this Form starts on before Split and Amplify move it. See the class " +
                 "comment for why this is a property of the Form.")]
        public SpellTier baseTier = SpellTier.Standard;

        // Reach is NOT authored here. Distance is speed x lifetime, both of which live on the
        // Form's ProjectileData, and the Range slot multiplies them. A second reach number here
        // would only be a value that could disagree with the one the projectile actually uses.

        [Tooltip("Instances this Form produces from a single cast before Split or Amplify. Almost " +
                 "always 1 — a Cone is one wide projectile, not several.")]
        [Min(1)]
        public int naturalCount = 1;

        /// <summary>
        /// True when this Form has nothing to fire. Checked at cast time so a half-authored
        /// grammar reports which Form is missing instead of silently doing nothing.
        /// </summary>
        public bool IsBuildable => projectileData != null;
    }
}
