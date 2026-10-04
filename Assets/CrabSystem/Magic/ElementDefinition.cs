using UnityEngine;

namespace NinjaGame.Magic
{
    /// <summary>
    /// Everything slot 1 (School) decides: what damage type the spell deals, what it looks like,
    /// and what the player calls it at each rung of the tier ladder.
    ///
    /// SIX ASSETS, one per element. This is the School half of the visual split — Form decides
    /// the shape (an orb, a cone, a beam), School decides the dressing (colour, particles,
    /// impact). Multiplying those at render time is what turns 6 schools x 6 forms = 36 prefabs
    /// into 12 assets.
    ///
    /// WHY castAbility LIVES HERE, and why there are six of them:
    /// DamageEffect.damageType is a serialized field on the shared AbilityDefinition asset. It
    /// cannot vary per cast without writing to that asset and corrupting it for every future use.
    /// So instead of one CastPrimitiveSpell with a runtime-swapped damage type, there are six —
    /// identical except for the damage type on their DamageEffect. Data, not a branch.
    ///
    /// SETUP for each cast ability:
    ///   - damageEffects[0].useWeaponDamage      = TRUE  (the dice arrive via weaponOverride)
    ///   - damageEffects[0].requiresSuppliedDice = TRUE  (arms the guard — without it a spell whose
    ///                                                    dice went missing silently rolls the
    ///                                                    equipped katana)
    ///   - damageEffects[0].damageType           = this element's damageType
    ///   - damageEffects[0].baseDamage           = 0     (flat empowerment goes here later)
    ///   - abilityCategory                       = Spell (so silence blocks it)
    ///   - resourceCosts                         = EMPTY (SpellcraftSystem owns the variable cost)
    ///   - effectTrigger                         = Effect1/2/3, or the launcher never fires
    ///   - projectileData                   = ANY non-null ProjectileData
    ///       ProjectileLauncher.HandleAnimationEvent bails when projectileData is null, before
    ///       any modifier gets a chance to run. Assign the Projectile form's data as a default;
    ///       SpellcraftSystem swaps it for the Form the sequence actually chose.
    /// </summary>
    [CreateAssetMenu(fileName = "Element_", menuName = "NinjaGame/Magic/Element Definition")]
    public class ElementDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Which element this describes. Must be unique across the grammar's element list.")]
        public SpellElement element = SpellElement.Fire;

        [Tooltip("Name shown in UI — 'Fire'.")]
        public string displayName = "Fire";

        [Tooltip("Icon for the element hotbar and the sequence display.")]
        public Sprite icon;

        [Tooltip("Animator trigger played when this element is ENTERED — the hand sign. Optional; " +
                 "leave empty for no draw animation.\n\n" +
                 "Separate from the cast ability's own trigger: entering an element and casting the " +
                 "sequence are different moments, and the whole readability of the system rests on " +
                 "an onlooker being able to see which signs you are making before the spell lands.")]
        [IdRef(IdKind.AnimatorParam)] public string drawTrigger = "";

        [Header("Damage")]
        [Tooltip("The packet's damage type for spells of this school. Must match the damageType on " +
                 "castAbility's DamageEffect — they are two halves of the same decision.")]
        public DamageType damageType = DamageType.Fire;

        [Tooltip("The CastPrimitiveSpell for this school. One per element, because DamageEffect's " +
                 "damage type lives on the shared asset and cannot vary per cast. See the class " +
                 "comment for the full setup checklist.")]
        public AbilityDefinition castAbility;

        [Header("Naming")]
        [Tooltip("What the player calls this school's spell at each tier, Faint through Grand — " +
                 "Ember, Fireball, Flame Lance, Fireblast, Conflagration.\n\n" +
                 "Five entries. This is what makes the ladder read as flavour rather than as " +
                 "numbers: Fire's d6 is a Fireball, Air's d6 is a Wind Slash.")]
        public string[] tierNames = new string[5];

        [Header("Visuals")]
        [Tooltip("The tint applied to the Form's mesh at cast time, through ProjectileRuntime. " +
                 "The Form supplies the shape; this supplies the colour.")]
        public Color color = Color.white;

        [Tooltip("Optional. Overrides the tint once real shaders exist. Leave empty while working " +
                 "against placeholder primitives.")]
        public Material material;

        [Tooltip("Optional. Embers, mist, motes — parented to the projectile's visual.")]
        public GameObject overlayPrefab;

        [Tooltip("Spawned on contact, in this element's flavour.")]
        public GameObject impactVfx;

        [Tooltip("The persistent aura for this element's signature buff (Wreathe, Renew, ...). " +
                 "Spawns at the VFXSystem Aura anchor on the TARGET, never on the projectile — " +
                 "the projectile dies on impact, the buff does not.")]
        public GameObject statusVfx;

        /// <summary>
        /// The player-facing name for this school at a given tier. Falls back to the plain
        /// display name when the table is unfilled, so a half-authored grammar still runs.
        /// </summary>
        public string TierName(SpellTier tier)
        {
            int index = (int)tier;

            if (tierNames == null || index < 0 || index >= tierNames.Length)
                return displayName;

            return string.IsNullOrEmpty(tierNames[index]) ? displayName : tierNames[index];
        }
    }
}
