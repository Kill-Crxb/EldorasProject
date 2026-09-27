using UnityEngine;

namespace NinjaGame.Magic
{
    /// <summary>
    /// One composed spell. The magic-system equivalent of ProjectileRuntime, one level up: the
    /// sequence resolved once at cast time into values the rest of the game can read without
    /// knowing a grammar exists.
    ///
    /// A STRUCT ON PURPOSE, and composed by value, for the same reason ProjectileRuntime is:
    /// nothing downstream can reach back and write to the shared grammar asset. And critically,
    /// composing a spell allocates NOTHING — no ScriptableObject.CreateInstance per cast, which
    /// is the GC trap the projectile system was built to avoid.
    ///
    /// It carries three resolved asset references alongside the numbers. That is deliberate: the
    /// alternative is re-scanning the grammar in the launcher, and the launcher is the wrong
    /// place to know what a School is.
    /// </summary>
    public struct SpellRuntime
    {
        // ── What the sequence said ────────────────────────────────────────────

        public SpellElement school;
        public SpellElement form;
        public SpellElement effect;

        /// <summary>
        /// The later three slots, kept as ELEMENTS rather than only as folded numbers.
        ///
        /// Most of what these slots do is already baked into the multipliers below, but not all
        /// of it — Ignite has to be recognised on arrival, and a UI has to name what the player
        /// typed. A slot that resolved to no row leaves its field at the School, which reads as
        /// "unset" only in combination with length; check length before trusting them.
        /// </summary>
        public SpellElement modify;
        public SpellElement range;
        public SpellElement amplify;

        /// <summary>How many elements were entered. Drives the cost curve.</summary>
        public int length;

        public bool HasModify => length > (int)SpellSlot.Modify;
        public bool HasRange => length > (int)SpellSlot.Range;
        public bool HasAmplify => length > (int)SpellSlot.Amplify;

        // ── Resolved definitions ──────────────────────────────────────────────

        /// <summary>Slot 1. Damage type, colour, tier names, and the cast ability to invoke.</summary>
        public ElementDefinition schoolDef;

        /// <summary>Slot 2. The ProjectileData to fire, and the base tier.</summary>
        public FormDefinition formDef;

        /// <summary>The rung the ladder landed on — dice, scale and cost multiplier.</summary>
        public SpellTierRow tierRow;

        public SpellTier tier;

        // ── Composed values ───────────────────────────────────────────────────

        /// <summary>Who the projectile is for. Written straight into ProjectileRuntime.</summary>
        public TargetStance stance;

        /// <summary>Projectiles this cast produces. Split divides into these; Amplify adds to them.</summary>
        public int instanceCount;

        /// <summary>Total fan angle across every instance. Meaningless at instanceCount 1.</summary>
        public float spreadDegrees;

        public float scaleMultiplier;
        public float speedMultiplier;
        public float lifetimeMultiplier;

        public int pierceBonus;
        public float homingBonus;

        /// <summary>Range redirected the effect to the caster — a Displace at self range is a dash.</summary>
        public bool redirectToSelf;

        /// <summary>Resource consumed. base x length^2 x tier multiplier.</summary>
        public float cost;

        // ── Convenience ───────────────────────────────────────────────────────

        /// <summary>What the player calls this spell — "Fireball", "Wind Slash". School x Tier.</summary>
        public string DisplayName => schoolDef != null ? schoolDef.TierName(tier) : "Unknown";

        /// <summary>The dice this spell rolls. Null means the grammar is missing a tier row.</summary>
        public DiceProfile Dice => tierRow != null ? tierRow.dice : null;

        // =========================================================================
        // Composition
        // =========================================================================

        /// <summary>
        /// Resolve a sequence into a spell. Returns false only when the grammar is incomplete —
        /// never because the player entered something wrong, because EVERY SEQUENCE IS VALID BY
        /// CONSTRUCTION. There is no fizzle, no invalid-combination path, and no resource wasted
        /// on a typo.
        ///
        /// Slots past the end of the sequence fall back to their defaults, which is what makes
        /// `Fire·Air` a complete spell rather than one missing four slots. Length buys
        /// specificity, not completeness — and not magnitude, which comes from Form and Amplify.
        /// </summary>
        public static bool TryCompose(SpellGrammar grammar, SpellElement[] sequence, int length,
                                      out SpellRuntime spell)
        {
            spell = default;

            if (grammar == null || sequence == null || length <= 0) return false;

            // TryCompose is public and the array is the caller's. Trusting `length` over the
            // array it indexes is how you get an IndexOutOfRange in the middle of a cast.
            if (length > sequence.Length) length = sequence.Length;
            if (length > SpellSequence.MaxLength) length = SpellSequence.MaxLength;

            spell.length = length;
            spell.school = sequence[0];
            spell.form = length > (int)SpellSlot.Form ? sequence[(int)SpellSlot.Form] : grammar.defaultForm;
            spell.effect = length > (int)SpellSlot.Effect ? sequence[(int)SpellSlot.Effect] : grammar.defaultEffect;

            // Unset slots hold the School as an inert placeholder. Nothing reads these without
            // checking HasModify / HasRange / HasAmplify first.
            spell.modify = length > (int)SpellSlot.Modify ? sequence[(int)SpellSlot.Modify] : spell.school;
            spell.range = length > (int)SpellSlot.Range ? sequence[(int)SpellSlot.Range] : spell.school;
            spell.amplify = length > (int)SpellSlot.Amplify ? sequence[(int)SpellSlot.Amplify] : spell.school;

            spell.schoolDef = grammar.GetElement(spell.school);

            SpellEffectRow effectRow = grammar.GetEffect(spell.effect);

            // A missing School IS fatal — it names the cast ability, so there is nothing to
            // invoke and nothing sensible to fall back to.
            if (spell.schoolDef == null || effectRow == null) return false;

            // A missing or unbuilt FORM is not fatal. EVERY SEQUENCE IS VALID BY CONSTRUCTION is
            // the design's core promise, and it has to survive a half-authored grammar too —
            // four of the six Forms need projectile features that do not exist yet, and a player
            // pressing Water in slot 2 should get a spell, not a refusal. Fall back to the
            // default Form and say so.
            spell.formDef = Buildable(grammar.GetForm(spell.form));

            if (spell.formDef == null)
            {
                spell.form = grammar.defaultForm;
                spell.formDef = Buildable(grammar.GetForm(spell.form));

                Debug.LogWarning($"[SpellRuntime] '{SpellSequence.ToKey(sequence, length)}' asks for a Form " +
                                 $"that is missing or has no ProjectileData — falling back to " +
                                 $"{grammar.defaultForm}. Run 'Validate Grammar' on the grammar asset.", grammar);
            }

            // The default Form being unbuildable too means the grammar has nothing to fire at all.
            if (spell.formDef == null) return false;

            spell.stance = effectRow.stance;
            spell.instanceCount = spell.formDef.naturalCount;
            spell.spreadDegrees = 0f;
            spell.scaleMultiplier = 1f;
            spell.speedMultiplier = 1f;
            spell.lifetimeMultiplier = 1f;

            // Modify, Range and Amplify are the same operation against three different tables.
            // A row that is absent — because the sequence was short, or the table is unfilled —
            // simply does not adjust anything.
            int tierSteps = 0;

            tierSteps += Apply(RowAt(grammar, grammar.modifiers, sequence, length, SpellSlot.Modify), ref spell);
            tierSteps += Apply(RowAt(grammar, grammar.ranges, sequence, length, SpellSlot.Range), ref spell);
            tierSteps += Apply(RowAt(grammar, grammar.amplifiers, sequence, length, SpellSlot.Amplify), ref spell);

            spell.tier = SpellTierTable.Step(spell.formDef.baseTier, tierSteps);
            spell.tierRow = grammar.tiers.Get(spell.tier);

            if (spell.tierRow == null)
            {
                Debug.LogError($"[SpellRuntime] The tier ladder has no '{spell.tier}' row, so " +
                               $"'{SpellSequence.ToKey(sequence, length)}' has no dice and no size. " +
                               $"Run 'Validate Grammar' on the grammar asset.", grammar);
                return false;
            }

            if (spell.instanceCount < 1) spell.instanceCount = 1;

            spell.cost = grammar.baseCost * length * length * spell.tierRow.costMultiplier;

            return true;
        }

        /// <summary>
        /// Fold one adjust row into the spell. Returns its tier steps rather than applying them,
        /// because the ladder has to be stepped once at the end — three separate clamps would let
        /// a Split at Faint eat an Amplify that should have cancelled it.
        /// </summary>
        private static int Apply(SpellAdjustRow row, ref SpellRuntime spell)
        {
            if (row == null) return 0;

            spell.instanceCount += row.extraInstances;

            if (row.spreadDegrees > spell.spreadDegrees)
                spell.spreadDegrees = row.spreadDegrees;

            spell.scaleMultiplier = Scale(spell.scaleMultiplier, row.scaleMultiplier);
            spell.speedMultiplier = Scale(spell.speedMultiplier, row.speedMultiplier);
            spell.lifetimeMultiplier = Scale(spell.lifetimeMultiplier, row.lifetimeMultiplier);

            spell.pierceBonus += row.pierceBonus;
            spell.homingBonus += row.homingBonus;

            if (row.redirectToSelf) spell.redirectToSelf = true;

            return row.tierSteps;
        }

        /// <summary>
        /// Multiply, treating a non-positive multiplier as UNSET rather than as zero.
        ///
        /// Unity does not run C# field initialisers when you add an element to a [Serializable]
        /// class array with the inspector's + button — it zero-fills the new row. So every
        /// `= 1f` default on SpellAdjustRow is a lie for exactly the rows a designer creates by
        /// hand, and a plain `*=` would annihilate the projectile: scale 0 is invisible, speed 0
        /// is motionless, lifetime 0 expires on the first frame. Silently, and looking for all
        /// the world like the spell system does not work.
        ///
        /// Zero is never a wanted multiplier here either way. A projectile that should not move
        /// has speed 0 on its ProjectileData; it does not have every Range row multiplying its
        /// speed to nothing.
        /// </summary>
        private static float Scale(float current, float multiplier)
        {
            return multiplier > 0f ? current * multiplier : current;
        }

        /// <summary>A Form only counts if it has something to fire. Null in, null out.</summary>
        private static FormDefinition Buildable(FormDefinition form)
        {
            return form != null && form.IsBuildable ? form : null;
        }

        /// <summary>
        /// The row a slot selects, or null when the sequence never reached that slot. Null is the
        /// slot's default: no adjustment. That is the whole of short-sequence handling.
        /// </summary>
        private static SpellAdjustRow RowAt(SpellGrammar grammar, SpellAdjustRow[] table,
                                            SpellElement[] sequence, int length, SpellSlot slot)
        {
            int index = (int)slot;

            if (index >= length) return null;

            return grammar.GetAdjust(table, sequence[index]);
        }
    }
}
