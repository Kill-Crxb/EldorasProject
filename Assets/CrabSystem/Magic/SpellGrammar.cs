using System;
using System.Collections.Generic;
using UnityEngine;

namespace NinjaGame.Magic
{
    /// <summary>
    /// What an Effect does on arrival, and — the part that matters — WHO IT SEEKS.
    ///
    /// The stance is not a branch in the code. It is one property on a row, and it is what lets
    /// heals, wards and buffs find allies while damage finds enemies, through the exact same
    /// path: ProjectileAim.StanceAllows governs both the aim probe and the hit filter, so a
    /// healing bolt probes for allies using the code a fireball uses to probe for enemies.
    /// </summary>
    [Serializable]
    public class SpellEffectRow
    {
        [Tooltip("Which element selects this Effect when entered in slot 3.")]
        public SpellElement element = SpellElement.Fire;

        [Tooltip("Name shown in UI — 'Damage', 'Restore', 'Ward'.")]
        public string displayName = "Damage";

        [Tooltip("Who this effect is for. Anything it should not affect is a PASS-THROUGH, not an " +
                 "impact — an ally in a doorway does not eat a fireball, and a healing bolt does " +
                 "not stop on an enemy.")]
        public TargetStance stance = TargetStance.Hostile;

        [Tooltip("Restore, Ward, Bind and Empower need effect systems that do not exist yet. Only " +
                 "Damage is wired end to end today; the rest resolve their stance and fly, but land " +
                 "as damage until their systems are built. Ticked here so the gap is visible in the " +
                 "inspector rather than surprising at runtime.")]
        public bool implemented = false;
    }

    /// <summary>
    /// One row of adjustment to a composed spell. Used by THREE tables — Modify, Range and
    /// Amplify — because all three answer the same kind of question: given the spell the first
    /// three slots described, what changes?
    ///
    /// One row shape rather than three near-identical ones. The difference between "Split" and
    /// "+count" is entirely in the numbers:
    ///
    ///   Modify  Water  Split       tierSteps -1, extraInstances +2   divide and shrink
    ///   Amplify Air    +count      tierSteps  0, extraInstances +2   add at the same tier
    ///
    /// Same field, opposite meaning, no code knows the difference. That is what "data driven"
    /// has to mean to be worth the name.
    /// </summary>
    [Serializable]
    public class SpellAdjustRow
    {
        [Tooltip("Which element selects this row in its slot.")]
        public SpellElement element = SpellElement.Fire;

        [Tooltip("Name shown in UI — 'Split', 'Magnitude', 'Long'.")]
        public string displayName = "";

        [Header("Magnitude")]
        [Tooltip("Steps along the tier ladder. Split is -1, Amplify's Fire is +1, everything else " +
                 "is 0. Clamped at both ends of the ladder.")]
        public int tierSteps = 0;

        [Header("Instances")]
        [Tooltip("Extra projectiles beyond the Form's natural count. Split pairs this with " +
                 "tierSteps -1 (divide); Amplify's Air uses it alone (add).")]
        public int extraInstances = 0;

        [Tooltip("Total fan angle across all instances, in degrees. 0 stacks them exactly, which " +
                 "is almost never what you want above one instance.")]
        public float spreadDegrees = 0f;

        [Header("Multipliers — 1 leaves the value alone")]
        public float scaleMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float lifetimeMultiplier = 1f;

        [Header("Additive")]
        [Tooltip("Extra targets this instance can pass through after the first.")]
        public int pierceBonus = 0;

        [Tooltip("Added to homing strength, clamped 0..1 by the projectile system. Lets a Range or " +
                 "Modify grant tracking without any code knowing it did.")]
        public float homingBonus = 0f;

        [Header("Targeting")]
        [Tooltip("Redirects the effect to the caster. This is what makes Displace at self range a " +
                 "dash rather than a shove.")]
        public bool redirectToSelf = false;
    }

    /// <summary>
    /// An authored spell: the sequence that summons it, and the hand-made ability it resolves to.
    ///
    /// The sequence is PICKED, not typed. It used to be a hand-written key string — "fire.air.air"
    /// — compared exactly, which meant "Nature.Water" silently never matched "nature.water" and
    /// the spell just quietly composed primitively instead. A dropdown per slot cannot be
    /// misspelled, cannot be miscapitalised, and shows you the grammar while you author it.
    ///
    /// The string key still exists at the persistence boundary, where it has to: unlocks are
    /// saved by key. It is derived from this array rather than authored beside it, so the two
    /// can never disagree.
    /// </summary>
    [Serializable]
    public class AuthoredSpellEntry
    {
        [Tooltip("The sequence, in order: School, Form, Effect, Modify, Range, Amplify. Two to " +
                 "six entries — as many slots as this spell cares about.")]
        public SpellElement[] sequence = new SpellElement[0];

        [Tooltip("The ability cast instead of composing. An ordinary AbilityDefinition — nothing " +
                 "about it knows the spell system exists.")]
        public AbilityDefinition ability;

        [Tooltip("Known to everyone from the start, without being learned.\n\n" +
                 "Tick it to TEST a spell without wiring progression, and leave it ticked for any " +
                 "spell that is meant to be common knowledge. Untick it and the sequence composes " +
                 "primitively until something calls SpellcraftSystem.Unlock — which is what a " +
                 "trainer, a tome or a quest reward does.")]
        public bool unlockedByDefault = false;

        /// <summary>The persistence key. Derived, so it can never disagree with the sequence.</summary>
        public string Key => SpellSequence.ToKey(sequence, sequence != null ? sequence.Length : 0);

        public int Length => sequence != null ? sequence.Length : 0;

        /// <summary>
        /// Element-by-element compare against what the player has entered. No string is built,
        /// so this is free to call from a UI that previews the sequence every frame.
        /// </summary>
        public bool Matches(SpellElement[] entered, int length)
        {
            if (sequence == null || entered == null) return false;
            if (sequence.Length != length) return false;

            for (int i = 0; i < length; i++)
            {
                if (sequence[i] != entered[i]) return false;
            }

            return true;
        }
    }

    /// <summary>
    /// THE grammar. One asset holding every table, because this is where balance happens and six
    /// tables side by side in one inspector is legible in a way thirty loose assets are not.
    ///
    /// Referenced directly by SpellcraftSystem, the way ModelDatabase is referenced — static data
    /// authored once and read by everyone. NOT a manager: nothing is written here at runtime,
    /// nothing has to be loaded or warmed, and magic therefore works in any scene the moment the
    /// brain has the module. ManagerBrain is disabled in Zoo.unity, so anything that depended on
    /// it would already be dead in the project's main test scene.
    ///
    /// Every lookup scans six rows rather than indexing by (int)element. Six iterations per cast
    /// is nothing, and it means a mis-ordered or half-filled table reports which row is missing
    /// instead of throwing IndexOutOfRange or silently returning the wrong element.
    /// </summary>
    [CreateAssetMenu(fileName = "SpellGrammar", menuName = "NinjaGame/Magic/Spell Grammar")]
    public class SpellGrammar : ScriptableObject
    {
        [Header("Slot 1 — School")]
        [Tooltip("Six ElementDefinitions. Damage type, colour, tier names, and the per-school cast " +
                 "ability.")]
        public ElementDefinition[] elements = new ElementDefinition[0];

        [Header("Slot 2 — Form")]
        [Tooltip("Six FormDefinitions. Delivery, and the base tier.")]
        public FormDefinition[] forms = new FormDefinition[0];

        [Header("Slot 3 — Effect")]
        public SpellEffectRow[] effects = new SpellEffectRow[0];

        [Header("Slot 4 — Modify")]
        public SpellAdjustRow[] modifiers = new SpellAdjustRow[0];

        [Header("Slot 5 — Range")]
        public SpellAdjustRow[] ranges = new SpellAdjustRow[0];

        [Header("Slot 6 — Amplify")]
        public SpellAdjustRow[] amplifiers = new SpellAdjustRow[0];

        [Header("Tier ladder")]
        public SpellTierTable tiers = new SpellTierTable();

        [Header("Defaults for short sequences")]
        [Tooltip("Every slot has a default, so every sequence is already a complete spell. No slot " +
                 "changes meaning based on length.\n\nForm default: Earth — Burst at self.")]
        public SpellElement defaultForm = SpellElement.Earth;

        [Tooltip("Effect default: Fire — Damage.")]
        public SpellElement defaultEffect = SpellElement.Fire;

        [Header("Presentation")]
        [Tooltip("ONE burst, played at the casting hand each time an element is entered, tinted " +
                 "to that element's colour.\n\n" +
                 "Shared rather than one per school for the same reason the projectile visuals " +
                 "are: shape is shape, colour is colour, and multiplying them at runtime beats " +
                 "authoring six near-identical prefabs that then have to be kept in sync.\n\n" +
                 "This is the readability contract for the input half of the system — an onlooker " +
                 "should be able to count the signs and read their colours before the spell lands.")]
        public GameObject drawVfxPrefab;

        [Tooltip("Shader colour property on the draw burst's renderers. Particles and lights are " +
                 "tinted regardless; this only matters for mesh-based effects.")]
        public string drawVfxColorProperty = "_BaseColor";

        [Header("Cost")]
        [Tooltip("Which resource a primitive cast consumes.")]
        public ResourceDefinition costResource;

        [Tooltip("Cost of a one-element cast at Standard tier. The full curve is " +
                 "base x length^2 x tier.costMultiplier — superlinear, so a six-element spell costs " +
                 "about 9x a two-element one rather than 3x.")]
        public float baseCost = 4f;

        [Header("Authored spells")]
        [Tooltip("Sequence key to hand-made ability. Unlocking a spell is adding an entry.")]
        public AuthoredSpellEntry[] authoredSpells = new AuthoredSpellEntry[0];

        // =========================================================================
        // Lookups
        // =========================================================================

        public ElementDefinition GetElement(SpellElement element)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i] != null && elements[i].element == element)
                    return elements[i];
            }

            Debug.LogError($"[SpellGrammar] '{name}' has no ElementDefinition for {element}.", this);
            return null;
        }

        /// <summary>
        /// Null is a LEGITIMATE answer here, unlike GetElement. Four of the six Forms need
        /// projectile features that do not exist yet, so a half-authored forms list is the
        /// normal state of this asset for a while. SpellRuntime falls back to the default Form
        /// rather than refusing the cast — an unbuilt Form must not break "every sequence is
        /// valid by construction". Use Validate Grammar to see the whole list of holes at once.
        /// </summary>
        public FormDefinition GetForm(SpellElement element)
        {
            for (int i = 0; i < forms.Length; i++)
            {
                if (forms[i] != null && forms[i].element == element)
                    return forms[i];
            }

            return null;
        }

        public SpellEffectRow GetEffect(SpellElement element)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null && effects[i].element == element)
                    return effects[i];
            }

            Debug.LogError($"[SpellGrammar] '{name}' has no Effect row for {element}.", this);
            return null;
        }

        /// <summary>
        /// A row from any of the three adjust tables. Null is a legitimate answer — an unset
        /// Modify, Range or Amplify slot simply does not adjust anything, which is what makes
        /// short sequences complete rather than incomplete.
        /// </summary>
        public SpellAdjustRow GetAdjust(SpellAdjustRow[] table, SpellElement element)
        {
            if (table == null) return null;

            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] != null && table[i].element == element)
                    return table[i];
            }

            return null;
        }

        public AbilityDefinition GetAuthored(string sequenceKey)
        {
            AuthoredSpellEntry entry = FindAuthored(sequenceKey);

            return entry != null ? entry.ability : null;
        }

        /// <summary>
        /// The entry for what the player has actually entered. Allocation-free — this is the
        /// one called on every cast and potentially every frame by a preview.
        ///
        /// Only returns entries that have an ability: a sequence pointing at nothing is an
        /// authoring mistake, not a spell, and treating it as one would swallow the sequence.
        /// </summary>
        public AuthoredSpellEntry FindAuthored(SpellElement[] entered, int length)
        {
            if (entered == null || length <= 0) return null;

            for (int i = 0; i < authoredSpells.Length; i++)
            {
                var entry = authoredSpells[i];

                if (entry != null && entry.ability != null && entry.Matches(entered, length))
                    return entry;
            }

            return null;
        }

        /// <summary>
        /// By key. Only for the persistence boundary — unlocks are saved as strings — so the
        /// per-entry string build here is paid on load and on Unlock, not on every cast.
        /// </summary>
        public AuthoredSpellEntry FindAuthored(string sequenceKey)
        {
            if (string.IsNullOrEmpty(sequenceKey)) return null;

            for (int i = 0; i < authoredSpells.Length; i++)
            {
                var entry = authoredSpells[i];

                if (entry != null && entry.ability != null && entry.Key == sequenceKey)
                    return entry;
            }

            return null;
        }

        // =========================================================================
        // Validation
        // =========================================================================

        /// <summary>
        /// Report EVERY hole in the grammar at once, in one log.
        ///
        /// Worth having because the runtime can only ever tell you about the one row the cast you
        /// just tried happened to need. Thirty assets have to agree with each other before a
        /// sequence resolves end to end, and finding that out one cast at a time is a bad evening.
        ///
        /// Right-click the asset → Validate Grammar. Same pattern as
        /// DamageCalculationConfig.ValidateConfiguration.
        /// </summary>
        [ContextMenu("Validate Grammar")]
        public void ValidateGrammar()
        {
            var problems = new List<string>();

            ValidateElements(problems);
            ValidateForms(problems);
            ValidateEffects(problems);
            ValidateAdjustTable("Modify", modifiers, problems);
            ValidateAdjustTable("Range", ranges, problems);
            ValidateAdjustTable("Amplify", amplifiers, problems);
            ValidateTiers(problems);
            ValidateAuthored(problems);

            if (costResource == null)
                problems.Add("Cost: no costResource — every spell will be free.");

            if (problems.Count == 0)
            {
                Debug.Log($"[SpellGrammar] '{name}' validated clean.", this);
                return;
            }

            Debug.LogWarning($"[SpellGrammar] '{name}' — {problems.Count} issue(s):\n  • " +
                             string.Join("\n  • ", problems), this);
        }

        private void ValidateElements(List<string> problems)
        {
            for (int i = 0; i < 6; i++)
            {
                var element = (SpellElement)i;
                ElementDefinition def = GetElement(element);

                if (def == null)
                {
                    problems.Add($"School {element}: no ElementDefinition — this school cannot be cast at all.");
                    continue;
                }

                if (def.castAbility == null)
                {
                    problems.Add($"School {element}: no castAbility.");
                    continue;
                }

                ValidateCastAbility(element, def, problems);
            }
        }

        /// <summary>
        /// The cast ability has five ways to be silently wrong, and every one of them looks
        /// identical from the game: nothing happens.
        /// </summary>
        private void ValidateCastAbility(SpellElement element, ElementDefinition def, List<string> problems)
        {
            AbilityDefinition ability = def.castAbility;
            string tag = $"School {element} → '{ability.name}'";

            bool triggerFires = ability.effectTrigger == NinjaGame.Animation.AnimationEventType.Effect1 ||
                                ability.effectTrigger == NinjaGame.Animation.AnimationEventType.Effect2 ||
                                ability.effectTrigger == NinjaGame.Animation.AnimationEventType.Effect3;

            if (!triggerFires)
                problems.Add($"{tag}: effectTrigger is {ability.effectTrigger}, not Effect1/2/3 — " +
                             $"ProjectileLauncher never hears it and nothing spawns.");

            if (ability.projectileData == null)
                problems.Add($"{tag}: projectileData is empty. ProjectileLauncher bails on null BEFORE " +
                             $"any modifier runs, so the spell never gets to choose its Form. " +
                             $"Assign any ProjectileData as a placeholder.");

            // The nastiest of the five, because it half-works: the projectile still launches, but
            // AbilitySystem ALSO runs the DamageEffect through ExecuteOnSelf on the same animation
            // event — so the caster damages themselves, with no dice, and the projectile's own hit
            // looks fine. Set it to Direction: a free-aimed spell's damage is delivered by the
            // projectile, never by the ability executing on the caster.
            if (ability.targetType == AbilityTargetType.Self)
                problems.Add($"{tag}: targetType is Self. AbilitySystem will apply the DamageEffect to " +
                             $"THE CASTER on the effect trigger, dice-less, as well as firing the " +
                             $"projectile. Set it to Direction.");

            if (string.IsNullOrEmpty(ability.abilityId))
                problems.Add($"{tag}: no abilityId — AbilitySystem cannot register or resolve it.");

            if (ability.resourceCosts != null && ability.resourceCosts.Length > 0)
                problems.Add($"{tag}: resourceCosts is not empty. SpellcraftSystem owns the variable " +
                             $"cost; a fixed cost here is charged on top of it.");

            if (ability.damageEffects == null || ability.damageEffects.Count == 0)
            {
                problems.Add($"{tag}: no DamageEffect — the spell will deal no damage.");
                return;
            }

            DamageEffect damage = ability.damageEffects[0];

            if (!damage.useWeaponDamage)
                problems.Add($"{tag}: DamageEffect.useWeaponDamage is off — the tier's dice are ignored " +
                             $"and every tier deals the same flat baseDamage.");

            if (!damage.requiresSuppliedDice)
                problems.Add($"{tag}: DamageEffect.requiresSuppliedDice is off — if the dice ever fail to " +
                             $"arrive this rolls the EQUIPPED WEAPON instead, silently.");

            if (damage.damageType != def.damageType)
                problems.Add($"{tag}: DamageEffect.damageType is {damage.damageType} but the element says " +
                             $"{def.damageType}. They are two halves of one decision.");

            // The Unity zero-init trap again, and here it is fatal rather than cosmetic:
            // CalculateDamage multiplies by both of these, so a zero on either deals no damage.
            if (damage.baseDamageMultiplier <= 0f)
                problems.Add($"{tag}: DamageEffect.baseDamageMultiplier is {damage.baseDamageMultiplier} — " +
                             $"THIS SPELL DEALS ZERO DAMAGE. Set it to 1.");

            if (damage.finalDamageMultiplier <= 0f)
                problems.Add($"{tag}: DamageEffect.finalDamageMultiplier is {damage.finalDamageMultiplier} — " +
                             $"THIS SPELL DEALS ZERO DAMAGE. Set it to 1.");
        }

        private void ValidateForms(List<string> problems)
        {
            for (int i = 0; i < 6; i++)
            {
                var element = (SpellElement)i;
                FormDefinition form = GetForm(element);

                if (form == null)
                {
                    problems.Add($"Form {element}: not in the forms list — sequences naming it fall back " +
                                 $"to {defaultForm}.");
                    continue;
                }

                if (form.projectileData == null)
                    problems.Add($"Form {element} ('{form.name}'): no ProjectileData — falls back to {defaultForm}.");
                else if (form.projectileData.archetypePrefab == null)
                    problems.Add($"Form {element} ('{form.name}'): its ProjectileData " +
                                 $"'{form.projectileData.name}' has no archetypePrefab — it cannot fire.");
            }

            // Duplicates are the quiet one: copy Form_Projectile three times to make Cone, Stream
            // and Field, forget to change `element`, and you have four Airs and three gaps.
            for (int i = 0; i < forms.Length; i++)
            {
                if (forms[i] == null) continue;

                for (int j = i + 1; j < forms.Length; j++)
                {
                    if (forms[j] == null) continue;

                    if (forms[i].element == forms[j].element)
                        problems.Add($"Forms: '{forms[i].name}' and '{forms[j].name}' both claim " +
                                     $"{forms[i].element}. Only the first is ever used.");
                }
            }
        }

        private void ValidateEffects(List<string> problems)
        {
            for (int i = 0; i < 6; i++)
            {
                if (GetEffect((SpellElement)i) == null)
                    problems.Add($"Effect {(SpellElement)i}: no row — sequences naming it cannot be composed.");
            }
        }

        /// <summary>
        /// Catches the Unity zero-init signature: a row added with the inspector's + button gets
        /// memset to zero, because C# field initialisers do not run for [Serializable] class
        /// instances created that way. SpellRuntime treats a non-positive multiplier as unset so
        /// this is no longer destructive — but a row of three zeroes almost always means the
        /// designer meant 1 and never saw the field.
        /// </summary>
        private void ValidateAdjustTable(string label, SpellAdjustRow[] table, List<string> problems)
        {
            if (table == null) return;

            for (int i = 0; i < table.Length; i++)
            {
                SpellAdjustRow row = table[i];
                if (row == null) continue;

                if (row.scaleMultiplier <= 0f && row.speedMultiplier <= 0f && row.lifetimeMultiplier <= 0f)
                    problems.Add($"{label} {row.element} ('{row.displayName}'): all three multipliers are 0. " +
                                 $"Unity zero-fills rows added with '+'. Treated as 1, but set them to 1.");

                for (int j = i + 1; j < table.Length; j++)
                {
                    if (table[j] != null && table[j].element == row.element)
                        problems.Add($"{label}: two rows claim {row.element}. Only the first is ever used.");
                }
            }
        }

        /// <summary>
        /// An authored spell that never resolves is invisible: the sequence simply composes
        /// primitively and looks like it worked. Every failure mode here is silent in play,
        /// which is exactly why they are worth reporting.
        /// </summary>
        private void ValidateAuthored(List<string> problems)
        {
            if (authoredSpells == null) return;

            for (int i = 0; i < authoredSpells.Length; i++)
            {
                AuthoredSpellEntry entry = authoredSpells[i];

                if (entry == null)
                {
                    problems.Add($"Authored spell {i}: empty row.");
                    continue;
                }

                string label = entry.Length > 0 ? entry.Key : $"row {i}";

                if (entry.ability == null)
                {
                    problems.Add($"Authored '{label}': no ability — the sequence will compose " +
                                 $"primitively instead, silently.");
                }
                else
                {
                    if (string.IsNullOrEmpty(entry.ability.abilityId))
                        problems.Add($"Authored '{label}': '{entry.ability.name}' has no abilityId. " +
                                     $"AbilitySystem keys on it, so the spell can never be cast.");

                    // An authored spell does NOT go through the launch modifier — that only fires
                    // for the six school cast abilities — so it never receives tier dice.
                    if (entry.ability.damageEffects != null)
                    {
                        for (int d = 0; d < entry.ability.damageEffects.Count; d++)
                        {
                            DamageEffect effect = entry.ability.damageEffects[d];

                            if (effect != null && effect.requiresSuppliedDice)
                                problems.Add($"Authored '{label}': DamageEffect {d} requires supplied " +
                                             $"dice, but authored spells never get tier dice — only " +
                                             $"composed ones do. Give its ProjectileData a DiceProfile, " +
                                             $"or untick requiresSuppliedDice and use baseDamage.");
                        }
                    }
                }

                if (entry.Length == 0)
                    problems.Add($"Authored row {i}: empty sequence — it can never match anything.");
                else if (entry.Length == 1)
                    problems.Add($"Authored '{label}': a single element. That shadows every " +
                                 $"sequence starting with that School, because it is checked " +
                                 $"before composing.");

                if (entry.Length > SpellSequence.MaxLength)
                    problems.Add($"Authored '{label}': {entry.Length} elements, but only " +
                                 $"{SpellSequence.MaxLength} can ever be entered.");

                for (int j = i + 1; j < authoredSpells.Length; j++)
                {
                    AuthoredSpellEntry other = authoredSpells[j];

                    if (other != null && other.Length == entry.Length && other.Key == entry.Key)
                        problems.Add($"Authored '{label}': two rows claim it. Only the first is used.");
                }
            }
        }

        private void ValidateTiers(List<string> problems)
        {
            for (int i = 0; i <= (int)SpellTier.Grand; i++)
            {
                var tier = (SpellTier)i;
                SpellTierRow row = tiers != null ? tiers.Get(tier) : null;

                if (row == null)
                {
                    problems.Add($"Tier {tier}: no row — any spell landing on this rung refuses to cast.");
                    continue;
                }

                if (row.dice == null)
                    problems.Add($"Tier {tier}: no dice asset — spells at this rung deal no dice damage.");

                if (row.scale <= 0f)
                    problems.Add($"Tier {tier}: scale is {row.scale} — the projectile will be INVISIBLE.");

                if (row.costMultiplier <= 0f)
                    problems.Add($"Tier {tier}: costMultiplier is {row.costMultiplier} — spells at this rung are free.");
            }
        }
    }
}
