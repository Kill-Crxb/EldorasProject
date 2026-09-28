using System;
using System.Collections.Generic;
using UnityEngine;

namespace NinjaGame.Magic
{
    /// <summary>
    /// Save payload. JsonUtility needs a concrete class and a plain array.
    ///
    /// Carries its own version, matching StatSystem, ResourceSystem and HotbarSystem — a file
    /// whose shape has to change later cannot be migrated if it never recorded which shape it
    /// was written in.
    /// </summary>
    [Serializable]
    public class SpellcraftSaveData
    {
        public int version;
        public string[] unlockedSpells = new string[0];
    }

    /// <summary>
    /// The magic system. Owns the sequence being entered, the authored-spell lookup, the tier and
    /// cost resolve, and the fold into a launch.
    ///
    /// AUTO-DISCOVERED. ControllerBrain.CacheModuleArrays walks GetComponentsInChildren
    /// &lt;IBrainModule&gt; and InitializeModules falls through to anything not in its explicit
    /// ordered list, so this needs NO serialized field on ControllerBrain and no edit to it —
    /// the same way ProjectileLauncher, VFXSystem and CombatStanceModule are already wired.
    ///
    /// NO MANAGER. Sort what this owns into two piles and the reason is obvious. The grammar is
    /// static data, authored once and read by everyone, referenced directly the way ModelDatabase
    /// is. The unlock list is per-character state, so it saves itself through ISaveable exactly as
    /// FactionSystem does. Neither is shared mutable world state, which is the only thing a manager
    /// is for. This is not academic: Manager_Brain is DISABLED in Zoo.unity, so anything depending
    /// on it would already be dead in the project's main test scene.
    ///
    /// NO INPUT. This exposes PushElement/Cast and nothing more. Whatever drives it — the element
    /// hotbar, SpellcraftInput, a test script, an AI — is somebody else's component, the same way
    /// IAbilityControlSource keeps ability input out of AbilitySystem.
    ///
    /// SETUP: add to any child of the ControllerBrain (next to ProjectileLauncher is the obvious
    /// home), assign the grammar, done.
    /// </summary>
    public class SpellcraftSystem : MonoBehaviour, IBrainModule, ISaveable,
                                    IProjectileLaunchModifier, IProjectileArrivalHandler
    {
        [Header("Module Settings")]
        [SerializeField] private bool isEnabled = true;

        [Header("Grammar")]
        [Tooltip("The tables. One asset, shared by every caster — never written at runtime.")]
        [SerializeField] private SpellGrammar grammar;

        [Header("Silence")]
        [Tooltip("Colour of the draw burst when an element fizzles.")]
        [SerializeField] private Color fizzleColor = new Color(0.45f, 0.45f, 0.45f, 1f);

        [Header("Cast-Time Exposure")]
        [Tooltip("Seconds one hand sign takes. THIS IS THE MAIN BALANCE LEVER on long spells, and " +
                 "it costs nothing to tune.\n\n" +
                 "The design's argument: a two-element fireball is a snap reaction, a six-element " +
                 "spell is a commitment made from cover — because entering six signs takes real " +
                 "seconds of standing still and readable. Without this, six elements cost six " +
                 "frames and the whole cost curve is balanced against a price nobody pays.")]
        [SerializeField] private float drawInterval = 0.35f;

        [Tooltip("Stat that makes a caster quicker with their hands. Efficacy is the offensive " +
                 "stat drawn from Endurance — the one that means 'you can keep doing this'.")]
        [SerializeField] private string drawSpeedStatId = "cmb.efficacy";

        [Tooltip("Speed gained per point of that stat. At 0.01, 50 Efficacy is a 1.5x faster " +
                 "draw and 99 is just under 2x. Deliberately gentle: this should feel like " +
                 "mastery, not like skipping the cost.")]
        [SerializeField] private float drawSpeedPerPoint = 0.01f;

        [Tooltip("Optional animator FLOAT parameter set to the same speed multiplier, so the hand " +
                 "sign visibly plays faster. Skipped silently when the animator has no such " +
                 "parameter, so it is safe to leave set.")]
        [SerializeField] private string drawSpeedParameter = "DrawSpeed";

        [Tooltip("Named socket on the model's rig where the hand-sign burst plays — the casting " +
                 "hand. Authored on the model's ModelSocketProvider under Named Sockets.")]
        [SerializeField] private string drawSocketId = "hand_r";

        [Tooltip("Where the burst plays when the model has no such socket. CastOrigin is chest " +
                 "height and forward of the body — not the hand, but not nowhere.")]
        [SerializeField] private VFXAnchor drawFallbackAnchor = VFXAnchor.CastOrigin;

        [Header("Debug")]
        [Tooltip("Log every compose: the sequence, the resolved name, the tier and the cost.")]
        [SerializeField] private bool debugLogging = false;

        private ControllerBrain brain;
        private AbilitySystem abilities;
        private AnimationSystem animation;
        private VFXSystem vfx;
        private IResourceProvider resources;
        private IStatProvider stats;
        private float nextDrawTime;

        private readonly SpellElement[] sequence = new SpellElement[SpellSequence.MaxLength];
        private readonly HashSet<string> unlocked = new HashSet<string>();

        private SpellRuntime pending;
        private bool hasPending;

        public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

        // ── Sequence state, for UI ────────────────────────────────────────────

        private int sequenceLength;

        // The single place the hands-up fact is written. Every path that changes the sequence —
        // draw, pop, clear, consume on cast — goes through here.
        public int SequenceLength
        {
            get => sequenceLength;
            private set
            {
                sequenceLength = value;
                brain?.Blackboard?.SetBool(BlackboardKey.IsDrawingSigns, value > 0);
            }
        }
        public IReadOnlyList<SpellElement> Sequence => sequence;
        public SpellGrammar Grammar => grammar;

        /// <summary>Fires whenever the sequence changes — pushed, cleared or cast.</summary>
        public event Action OnSequenceChanged;

        /// <summary>
        /// One element was just entered. Carries WHICH, because OnSequenceChanged deliberately
        /// does not — a UI redrawing the whole row does not care, but anything reacting to the
        /// individual sign does. Hand-sign VFX and per-element audio both hang off this.
        /// </summary>
        public event Action<SpellElement> OnElementDrawn;
        // A sign drawn while silenced. Hook fizzle sound and UI here.
        public event Action<SpellElement> OnElementFizzled;

        /// <summary>The spell that was cast, for floating text and the sequence display.</summary>
        public event Action<SpellRuntime> OnSpellCast;

        /// <summary>Why a cast did not happen. Never fires for a "wrong" sequence — there is no such thing.</summary>
        public event Action<string> OnCastFailed;

        // =========================================================================
        // IBrainModule
        // =========================================================================

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
        }

        public void LateInitialize()
        {
            abilities = brain.GetModule<AbilitySystem>();
            resources = brain.GetProvider<IResourceProvider>();

            // Concrete rather than IAnimationProvider because layer weights are not on the
            // interface — the same reason AbilitySystem does `animationProvider is AnimationSystem`.
            animation = brain.GetModule<AnimationSystem>();
            vfx = brain.GetModule<VFXSystem>();

            // Through the interface, not StatSystem — nothing here needs the store, only a
            // number. Resolved via GetModule because ControllerBrain registers StatSystem by its
            // concrete type only, so GetProvider<IStatProvider> would miss.
            stats = brain.GetModule<IStatProvider>();

            if (grammar == null)
            {
                isEnabled = false;
                Debug.LogError($"[SpellcraftSystem] No SpellGrammar assigned on {brain.name} — magic is off.", this);
                return;
            }

            if (abilities == null)
            {
                isEnabled = false;
                Debug.LogError($"[SpellcraftSystem] No AbilitySystem on {brain.name} — nothing can be cast.", this);
                return;
            }

            RegisterSpellAbilities();
        }

        // Hands up / down is the animator's job now: SequenceLength publishes IsDrawingSigns, the
        // fact bridge mirrors it to IsDrawing, and the sign-hold state follows it.
        public void UpdateModule() { }

        /// <summary>
        /// AbilitySystem resolves abilities BY STRING ID out of its own list, so anything this
        /// system might ask it to cast has to be in that list first. Two sets qualify: the six
        /// per-school cast abilities, and every authored spell in the grammar.
        ///
        /// Registered here rather than asked of every caster prefab, because the grammar already
        /// knows the full set and a prefab that forgot one would fail only for the sequence that
        /// needed it — a bug that hides until someone happens to type those elements.
        /// </summary>
        private void RegisterSpellAbilities()
        {
            for (int i = 0; i < grammar.elements.Length; i++)
            {
                ElementDefinition element = grammar.elements[i];
                if (element == null) continue;

                if (element.castAbility == null)
                {
                    Debug.LogError($"[SpellcraftSystem] Element '{element.displayName}' has no cast ability — " +
                                   $"that school cannot be cast primitively.", element);
                    continue;
                }

                Register(element.castAbility);
            }

            // An authored spell is an ORDINARY ability, which is the whole point — but ordinary
            // abilities have to be registered like any other, and nothing else was going to do it.
            for (int i = 0; i < grammar.authoredSpells.Length; i++)
            {
                AuthoredSpellEntry entry = grammar.authoredSpells[i];
                if (entry == null || entry.ability == null) continue;

                Register(entry.ability);
            }
        }

        /// <summary>
        /// Add one ability, refusing an unusable one loudly. AbilitySystem keys on abilityId, so
        /// an empty id registers nothing and every cast of it is refused as "not ready" — with no
        /// hint that the id is the problem.
        /// </summary>
        private void Register(AbilityDefinition ability)
        {
            if (string.IsNullOrEmpty(ability.abilityId))
            {
                Debug.LogError($"[SpellcraftSystem] '{ability.name}' has no abilityId, so AbilitySystem " +
                               $"cannot register or resolve it. Every attempt to cast it will be " +
                               $"refused.", ability);
                return;
            }

            abilities.AddAbility(ability);
        }

        // =========================================================================
        // Entering a sequence
        // =========================================================================

        /// <summary>
        /// Add an element. Refuses past the last slot rather than shuffling — there is nothing
        /// after Amplify, and silently dropping the School would be worse than doing nothing.
        /// </summary>
        /// <summary>
        /// Add an element. Refuses past the last slot rather than shuffling — there is nothing
        /// after Amplify, and silently dropping the School would be worse than doing nothing.
        ///
        /// Also refuses while the previous hand sign is still being drawn. That refusal IS the
        /// balance lever: length costs seconds of standing still, so a six-element spell is a
        /// commitment rather than a keystroke. Silent, because a player mashing keys does not
        /// need to be told they are mashing.
        /// </summary>
        public void PushElement(SpellElement element)
        {
            if (!isEnabled) return;
            if (SequenceLength >= SpellSequence.MaxLength) return;
            if (Time.time < nextDrawTime) return;
            if (HasFact(BlackboardKey.CannotAct)) return;

            // CannotCast: the hand sign still plays and still costs its draw time, but the element
            // never enters the sequence.
            if (HasFact(BlackboardKey.CannotCast))
            {
                Fizzle(element);
                return;
            }

            sequence[SequenceLength] = element;
            SequenceLength++;
            nextDrawTime = Time.time + CurrentDrawInterval;

            PlayDraw(element);

            OnElementDrawn?.Invoke(element);
            OnSequenceChanged?.Invoke();
        }

        // Silenced: the hands still move — the sign is seen and its time is spent — but the element
        // never enters the sequence, and the burst comes out grey.
        private void Fizzle(SpellElement element)
        {
            nextDrawTime = Time.time + CurrentDrawInterval;

            ElementDefinition def = grammar.GetElement(element);
            if (def != null)
            {
                PlayDrawBurst(fizzleColor);
                PlayDrawAnimation(def);
            }

            OnElementFizzled?.Invoke(element);
        }

        private bool HasFact(int key) => brain?.Blackboard?.GetBool(key) ?? false;

        /// <summary>How fast this caster's hands are, 1 at no stat. Above 1 is faster.</summary>
        public float DrawSpeedMultiplier
        {
            get
            {
                if (stats == null || string.IsNullOrEmpty(drawSpeedStatId)) return 1f;

                return 1f + stats.GetValue(drawSpeedStatId) * drawSpeedPerPoint;
            }
        }

        /// <summary>Seconds this caster takes per element, after stats.</summary>
        public float CurrentDrawInterval
        {
            get
            {
                float speed = DrawSpeedMultiplier;

                return speed > 0f ? drawInterval / speed : drawInterval;
            }
        }

        /// <summary>
        /// Seconds a sequence of this length would take to enter. For a UI that wants to show
        /// what a spell is about to cost in exposure, not just in mana.
        /// </summary>
        public float DrawTimeFor(int length) => length * CurrentDrawInterval;

        /// <summary>Drop the last element — the backspace.</summary>
        public void PopElement()
        {
            if (!isEnabled) return;
            if (SequenceLength <= 0) return;

            SequenceLength--;

            OnSequenceChanged?.Invoke();
        }

        /// <summary>Abandon the sequence. Hands come down.</summary>
        public void ClearSequence()
        {
            if (SequenceLength == 0) return;

            SequenceLength = 0;
            OnSequenceChanged?.Invoke();
        }

        /// <summary>
        /// Clear because the sequence was SPENT, not abandoned. The cast's own trigger takes the
        /// hands from the sign hold straight into the cast animation.
        /// </summary>
        private void ConsumeSequence()
        {
            SequenceLength = 0;
            OnSequenceChanged?.Invoke();
        }

        // ── Draw animation ────────────────────────────────────────────────

        /// <summary>
        /// The whole visible response to one element being entered: the hand sign, and the burst
        /// of that element's colour at the casting hand.
        ///
        /// Both live here rather than in a listener, because "play the draw" is one idea and
        /// AbilitySystem already sets the precedent — it spawns an ability's castEffectPrefab
        /// through VFXSystem the same way. The generic halves (socket resolution, parenting,
        /// recolouring) belong to VFXSystem and VfxTint; what is left here is the only part that
        /// is actually about magic: which prefab, and which colour.
        /// </summary>
        private void PlayDraw(SpellElement element)
        {
            ElementDefinition def = grammar.GetElement(element);
            if (def == null) return;

            PlayDrawBurst(def.color);
            PlayDrawAnimation(def);
        }

        /// <summary>
        /// THE READABILITY CONTRACT for the input half of the system. Everything else makes a
        /// spell readable once it is airborne; this makes it readable while it is being made, so
        /// an onlooker can count the signs and read their colours and know roughly what is
        /// coming. That is what turns a six-element cast into a commitment someone can punish
        /// rather than a keystroke nobody saw.
        ///
        /// One prefab for all six schools, recoloured at spawn — the same economy as the
        /// projectile visuals.
        /// </summary>
        private void PlayDrawBurst(Color color)
        {
            if (vfx == null || grammar.drawVfxPrefab == null) return;

            GameObject burst = vfx.SpawnAtSocket(grammar.drawVfxPrefab, drawSocketId, drawFallbackAnchor);

            if (burst != null)
                VfxTint.ApplyOnce(burst, color, grammar.drawVfxColorProperty);
        }

        private void PlayDrawAnimation(ElementDefinition def)
        {
            if (animation == null) return;
            if (string.IsNullOrEmpty(def.drawTrigger)) return;

            // The clip plays at the same speed the hands actually move, so a fast caster looks
            // fast rather than looking normal and finishing early. Skipped silently when the
            // animator has no such parameter — SetFloat on a missing one logs a Unity warning
            // every single press, which would bury the console.
            if (!string.IsNullOrEmpty(drawSpeedParameter) && animation.HasParameter(drawSpeedParameter))
                animation.SetFloat(drawSpeedParameter, DrawSpeedMultiplier);

            animation.TriggerCombatAnimation(def.drawTrigger);
        }

        /// <summary>The lookup key for what is currently entered.</summary>
        public string CurrentKey => SpellSequence.ToKey(sequence, SequenceLength);

        /// <summary>
        /// What the sequence would cast, without casting it. For the live name under the icons.
        /// Returns empty for an empty sequence; never throws on a partial one, because a partial
        /// sequence is a complete spell.
        /// </summary>
        public string PreviewName()
        {
            if (SequenceLength == 0) return string.Empty;

            AbilityDefinition authored = ResolveAuthored();
            if (authored != null) return authored.abilityName;

            if (!SpellRuntime.TryCompose(grammar, sequence, SequenceLength, out SpellRuntime spell))
                return string.Empty;

            return spell.DisplayName;
        }

        // =========================================================================
        // Casting
        // =========================================================================

        /// <summary>
        /// Resolve and cast. Look the sequence up; if this character has learned an authored spell
        /// for it, cast that as an ordinary ability. Otherwise compose one.
        ///
        /// The difference between the two is not power, it is precision.
        /// </summary>
        public void Cast()
        {
            if (!isEnabled) return;
            if (SequenceLength == 0) return;

            // NOT cleared here. A refused cast — on cooldown, mid-animation — must not wipe the
            // spell the PREVIOUS cast is still waiting to launch: the projectile fires on an
            // animation event several frames after UseAbility returns, and a mashed cast key
            // would otherwise leave it to fire as the cast ability's placeholder projectile.
            // pending is replaced only when a new cast actually commits.
            AbilityDefinition authored = ResolveAuthored();

            if (authored != null)
            {
                CastAuthored(authored);
                return;
            }

            CastPrimitive();
        }

        private void CastAuthored(AbilityDefinition ability)
        {
            // An authored spell is a normal ability: its own costs, its own cooldown, its own
            // everything. Nothing here needs to know it came from a sequence.
            if (!abilities.CanUseAbility(ability.abilityId))
            {
                Fail($"'{ability.abilityName}' {NotReadyReason(ability)}");
                return;
            }

            // An authored cast supersedes any composed spell still waiting to launch. It fires a
            // different ability asset, so the fold would not have applied to it anyway — this
            // just stops a stale spell surviving until the next primitive cast.
            hasPending = false;

            abilities.UseAbility(ability.abilityId);
            ConsumeSequence();
        }

        private void CastPrimitive()
        {
            if (!SpellRuntime.TryCompose(grammar, sequence, SequenceLength, out SpellRuntime spell))
            {
                Fail("the grammar is incomplete — see the console");
                ClearSequence();
                return;
            }

            AbilityDefinition cast = spell.schoolDef.castAbility;

            if (cast == null)
            {
                Fail($"{spell.schoolDef.displayName} has no cast ability");
                ClearSequence();
                return;
            }

            // Readiness before cost, always. Charging for a cast that a cooldown then refuses is
            // the one failure the player will never forgive.
            if (!abilities.CanUseAbility(cast.abilityId))
            {
                Fail("not ready");
                return;
            }

            if (!HasResourceFor(spell.cost))
            {
                Fail("not enough " + ResourceName());
                return;
            }

            ConsumeFor(spell.cost);

            // Held rather than passed, because the launch happens later — on the animation event
            // the cast ability names, not on this call. Cleared at the top of the next Cast, so a
            // multi-projectile spell can fold every instance from the same composed values.
            pending = spell;
            hasPending = true;

            abilities.UseAbility(cast.abilityId);

            if (debugLogging)
                Debug.Log($"[SpellcraftSystem] {CurrentKey} → {spell.DisplayName} " +
                          $"({spell.tierRow.displayName}, {DiceLabel(spell)}, x{spell.instanceCount}) " +
                          $"cost {spell.cost:F1}", this);

            OnSpellCast?.Invoke(spell);
            ConsumeSequence();
        }

        private void Fail(string reason)
        {
            if (debugLogging)
                Debug.Log($"[SpellcraftSystem] cast refused — {reason}", this);

            OnCastFailed?.Invoke(reason);
        }

        /// <summary>
        /// Why AbilitySystem said no, in the terms a person can act on.
        ///
        /// "Not ready" covers a cooldown, a busy caster, a blackboard fact and an ability that
        /// was never registered — and those want completely different fixes. The last one used
        /// to be indistinguishable from a cooldown that never expires, which is a bad hour.
        /// </summary>
        private string NotReadyReason(AbilityDefinition ability)
        {
            if (abilities.GetAbility(ability.abilityId) == null)
                return $"is not registered with AbilitySystem (abilityId '{ability.abilityId}') — " +
                       $"it is probably missing from the grammar";

            if (abilities.IsAbilityOnCooldown(ability.abilityId))
                return $"is on cooldown ({abilities.GetAbilityCooldownRemaining(ability.abilityId):F1}s)";

            if (abilities.IsExecuting)
                return "cannot start while another ability is running";

            return "was refused — a blackboard requirement or its own resource cost";
        }

        private static string DiceLabel(SpellRuntime spell)
        {
            return spell.Dice != null ? spell.Dice.DamageLabel() : "no dice";
        }

        // =========================================================================
        // Cost
        // =========================================================================

        /// <summary>
        /// The cost lives here, not on the cast ability, because it varies by sequence and tier
        /// and the ability is a SHARED ASSET — writing a per-cast cost into resourceCosts would
        /// corrupt it for every future cast. The six cast abilities carry an empty cost array.
        ///
        /// Resolved through IResourceProvider off the brain, which is the codebase's own
        /// convention — it is what AbilitySystem itself does.
        /// </summary>
        private bool HasResourceFor(float cost)
        {
            if (cost <= 0f) return true;
            if (resources == null || grammar.costResource == null) return true;

            return resources.HasResource(grammar.costResource, cost);
        }

        private void ConsumeFor(float cost)
        {
            if (cost <= 0f) return;
            if (resources == null || grammar.costResource == null) return;

            resources.ConsumeResource(grammar.costResource, cost);
        }

        private string ResourceName()
        {
            return grammar.costResource != null ? grammar.costResource.displayName : "resource";
        }

        // =========================================================================
        // IProjectileLaunchModifier — the fold
        // =========================================================================

        /// <summary>
        /// Turn the composed spell into a launch. This is the only place the spell system touches
        /// the projectile system, and it does so through a door the projectile system opened for
        /// anyone — talents and gear will come through the same one.
        ///
        /// Form decides WHICH projectile, School decides its colour and damage type, Tier decides
        /// its dice and its size. Everything else is multipliers the sequence accumulated.
        /// </summary>
        public void ModifyLaunch(AbilityDefinition ability, ref ProjectileLaunchPlan plan)
        {
            if (!hasPending) return;
            if (pending.schoolDef == null || pending.formDef == null) return;
            if (ability != pending.schoolDef.castAbility) return;

            // Form chose a different projectile than the ability named, so the numbers have to be
            // re-seeded from it. Whoever swaps the data owns this.
            plan.data = pending.formDef.projectileData;
            plan.runtime = ProjectileRuntime.FromData(plan.data);

            plan.runtime.targetStance = pending.stance;
            plan.runtime.tint = pending.schoolDef.color;

            plan.runtime.scale *= pending.tierRow.scale * pending.scaleMultiplier;
            plan.runtime.speed *= pending.speedMultiplier;
            plan.runtime.lifetime *= pending.lifetimeMultiplier;
            plan.runtime.pierceCount += pending.pierceBonus;
            plan.runtime.homingStrength = Mathf.Clamp01(plan.runtime.homingStrength + pending.homingBonus);

            plan.dice = pending.tierRow.dice;
            plan.count = pending.instanceCount;
            plan.spreadDegrees = pending.spreadDegrees;
        }

        // =========================================================================
        // IProjectileArrivalHandler — what the spell DOES when it lands
        // =========================================================================

        /// <summary>
        /// The Effect slot, finally cashed in. Everything up to here has been about getting the
        /// right projectile to the right target; this decides what happens to them.
        ///
        /// Returns false for Damage, which is not a gap — it means "the payload's normal path is
        /// already correct", and the normal path already rolls the tier's dice with the school's
        /// damage type. Adding a special case to reach the same result would be worse code.
        /// </summary>
        public bool HandleArrival(AbilityDefinition ability, ControllerBrain target, ProjectileHitInfo hit,
                                  DiceProfile dice, float damageMultiplier)
        {
            if (!hasPending) return false;
            if (pending.schoolDef == null) return false;
            if (ability != pending.schoolDef.castAbility) return false;

            // Ignite rides ON TOP of whatever the Effect does, because Modify answers "how does
            // it behave", not "what is it". A burning heal is nonsense, but that is the grammar's
            // problem to express, not this method's to prevent.
            if (pending.HasModify && pending.modify == SpellElement.Fire)
                ApplyIgnite(ability, target);

            if (pending.effect == SpellElement.Water)
                return ApplyRestore(ability, target);

            // Displace, Ward, Bind and Empower need systems that do not exist yet — knockback
            // wiring, and the whole status layer. They fall through to damage, which is honest:
            // the projectile found the right person, and that half is proven.
            return false;
        }

        /// <summary>
        /// Heal the thing we hit. The stance already guaranteed it is an ally — ProjectileAim
        /// filtered enemies out as pass-throughs before this was ever called.
        /// </summary>
        private bool ApplyRestore(AbilityDefinition ability, ControllerBrain target)
        {
            if (ability.healEffects == null || ability.healEffects.Count == 0)
            {
                Debug.LogWarning($"[SpellcraftSystem] '{ability.name}' has no HealEffect, so a Restore " +
                                 $"spell has nothing to restore. Add one to the cast ability.", this);
                return false;
            }

            IHealthProvider health = target.GetProvider<IHealthProvider>();
            if (health == null) return false;

            EffectManagerModule effects = target.GetModule<EffectManagerModule>();

            for (int i = 0; i < ability.healEffects.Count; i++)
            {
                HealEffect heal = ability.healEffects[i];
                if (heal == null) continue;

                if (effects != null) effects.ApplyHealEffect(heal, health);
                else heal.Apply(health);
            }

            return true;
        }

        /// <summary>
        /// Modify · Fire — the effect also applies over time.
        ///
        /// Applies a COPY of the template rather than the asset's own instance.
        /// DamageOverTimeEffect holds its timer in a NonSerialized field, so handing the same
        /// instance to two burning targets would have them share one timer and one of them would
        /// silently stop ticking. The shared asset is a template; each application needs its own.
        /// </summary>
        private void ApplyIgnite(AbilityDefinition ability, ControllerBrain target)
        {
            if (ability.damageOverTimeEffects == null || ability.damageOverTimeEffects.Count == 0) return;

            DamageSystem targetDamage = target.GetModule<DamageSystem>();
            DamageSystem sourceDamage = brain.GetModule<DamageSystem>();
            EffectManagerModule effects = target.GetModule<EffectManagerModule>();

            if (targetDamage == null || sourceDamage == null || effects == null) return;

            for (int i = 0; i < ability.damageOverTimeEffects.Count; i++)
            {
                DamageOverTimeEffect template = ability.damageOverTimeEffects[i];
                if (template == null) continue;

                var dot = new DamageOverTimeEffect
                {
                    duration = template.duration,
                    tickInterval = template.tickInterval,
                    damagePerTick = template.damagePerTick,
                    tickDice = template.tickDice,

                    // The school's type, not the template's — a Nature spell that ignites should
                    // burn as Nature, because Ignite says "over time", not "as fire".
                    damageType = pending.schoolDef.damageType,
                };

                dot.SetAttacker(sourceDamage);
                effects.ApplyDamageOverTimeEffect(dot, targetDamage);
            }
        }

        // =========================================================================
        // Unlocks
        // =========================================================================

        /// <summary>
        /// An authored spell this character has learned. A grammar entry that is not unlocked
        /// composes primitively instead — the sequence still works, it is just less precise.
        ///
        /// Two ways to know a spell: the grammar says everyone knows it, or this character
        /// learned it. Kept as an OR rather than seeding the unlock set at startup, because
        /// LoadSaveData replaces that set wholesale — a seeded default would vanish on the
        /// first load and reappear on a fresh character, which is the kind of bug that takes a
        /// week to notice.
        /// </summary>
        private AbilityDefinition ResolveAuthored()
        {
            AuthoredSpellEntry entry = grammar.FindAuthored(sequence, SequenceLength);

            if (entry == null) return null;
            if (entry.unlockedByDefault) return entry.ability;

            return unlocked.Contains(entry.Key) ? entry.ability : null;
        }

        public bool IsUnlocked(string sequenceKey)
        {
            if (unlocked.Contains(sequenceKey)) return true;

            AuthoredSpellEntry entry = grammar != null ? grammar.FindAuthored(sequenceKey) : null;

            return entry != null && entry.unlockedByDefault;
        }

        /// <summary>Learning a spell is adding a dictionary entry. Returns false if already known.</summary>
        public bool Unlock(string sequenceKey)
        {
            if (string.IsNullOrEmpty(sequenceKey)) return false;
            if (grammar == null || grammar.GetAuthored(sequenceKey) == null)
            {
                Debug.LogWarning($"[SpellcraftSystem] No authored spell for '{sequenceKey}' in " +
                                 $"'{grammar?.name}' — unlocking it will do nothing.", this);
            }

            return unlocked.Add(sequenceKey);
        }

        public IReadOnlyCollection<string> UnlockedSpells => unlocked;

        // =========================================================================
        // ISaveable
        // =========================================================================

        public string GetSaveId() => "spellcraft";

        public int GetSaveVersion() => 1;

        public string GetSaveData()
        {
            var data = new SpellcraftSaveData { version = GetSaveVersion() };

            // Only what was LEARNED. Spells the grammar marks unlockedByDefault are not written:
            // they belong to the game's content, not to this character, and baking them into a
            // save would freeze a design decision the moment anyone pressed save.
            data.unlockedSpells = new string[unlocked.Count];
            unlocked.CopyTo(data.unlockedSpells);

            return JsonUtility.ToJson(data);
        }

        public void LoadSaveData(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<SpellcraftSaveData>(json);
            if (data == null || data.unlockedSpells == null) return;

            unlocked.Clear();

            for (int i = 0; i < data.unlockedSpells.Length; i++)
            {
                string key = data.unlockedSpells[i];
                if (!string.IsNullOrEmpty(key)) unlocked.Add(key);
            }
        }
    }
}
