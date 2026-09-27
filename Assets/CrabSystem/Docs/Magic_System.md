# Magic System — Design

**Status:** Design settled, nothing built. Audited against the codebase 2026-08-24.
**Depends on:** the projectile system (`CrabSystem/Projectiles/`), the dice system
(`WeaponData` / `DiceRoll`), `AbilitySystem`, `ControllerBrain`.

---

## Principle

Six elements on their own hotbar. The player taps a **sequence**, presses **E**, and the
sequence *is* the spell. Think Magicka, or Naruto hand signs.

Two tiers, one input:

- **Primitive casting** — the sequence is composed into a spell at runtime. Chaotic, broad,
  available from the start.
- **Authored spells** — specific sequences resolve to a hand-made `AbilityDefinition` instead.
  Unlocked, precise, better.

The sequence is a key. **Look it up; if there's an authored spell, cast that. Otherwise,
compose.** Unlocking a spell is adding a dictionary entry.

---

## The grammar

Position decides what an element means. Six slots:

| # | Slot | Question it answers |
|---|---|---|
| 1 | **School** | what element is it |
| 2 | **Form** | how is it delivered — *and how big the die is* |
| 3 | **Effect** | what does it do on arrival |
| 4 | **Modify** | how does it behave |
| 5 | **Range** | how far |
| 6 | **Amplify** | how much |

`School · Form · Effect` is a complete sentence — *fire, as a projectile, that damages.*
Everything after it is refinement.

### The rule that makes it learnable

Six positions × six elements is **36 meanings**. That is the thing that kills systems like
this — unless each element means the *same concept* in every slot.

| Element | Concept |
|---|---|
| **Fire** | aggression, consumption, magnitude |
| **Water** | flow, restoration, persistence |
| **Air** | speed, distance, multiplicity |
| **Earth** | mass, solidity, area |
| **Nature** | growth, entangling, spread |
| **Aether** | piercing, arcane, meta |

Then it is 6 concepts seen through 6 lenses, not 36 mappings. A player who has learned
*"Air means far and fast"* can predict `Fire·Air·…·Air` without being told.

It also means **every sequence is valid by construction** — no fizzle, no invalid-combination
handling, no resources wasted on a typo.

---

## Damage — spells are weapons

**The law: every spell rolls dice, and the dice come from a `WeaponData` asset.**

This is not a new mechanism. `ProjectilePayload` already hands a `WeaponData` to
`DamageEffect.Apply` as `weaponOverride`, and `DamageEffect.GetBaseDamage` already resolves
it as `weaponOverride.RollDamage() + baseDamage`. A thrown shuriken already rolls its own
1d4 rather than the katana in the main hand. Spells simply stop being the exception.

What this buys:

- **One damage path.** Without it, weapons roll dice and spells roll a flat `baseDamage` —
  two curves to balance against each other forever.
- **Composition without mutation.** A composed spell's damage is *which `WeaponData` it
  points at*, not a number written into a shared asset. `weaponOverride` is already a
  per-application parameter. No `CreateInstance`, no GC, no corrupted assets.
- **Readability.** A player facing a projectile can read its size and know roughly what is
  coming — because size and die come from the same row (below).
- **Legible scaling.** Empowerments add `+2` to the roll via `DamageEffect.baseDamage`.
  Buffs, talents and gear add flat damage or extra dice through
  `DamageEffect.GetExternalFlatDamage()`. Dice stay the unit of power throughout.

### The tier ladder

Magnitude is **ordinal**, because Modify and Amplify have to step along it. That rules out
loose per-tier assets — you cannot do `tier - 1` on an asset reference. So: an enum, and
**one table asset**.

```
SpellTier (enum, ordinal)   Faint → Lesser → Standard → Greater → Grand

SpellTierTable  (ONE asset, ordered rows)
    dice          1d4    1d6    1d8    1d10   1d12
    scale         0.5    0.75   1.0    1.3    1.6
    costMult      0.6    0.8    1.0    1.4    1.9
```

One asset, one inspector, the whole ladder visible in the one place you will actually
balance it. Flatter than six assets, no null path, and stepping the ladder is arithmetic on
an index. **Index clamps at both ends.**

`scale` in the same row is what makes the readability claim structural rather than a promise
the art will quietly break: tier drives `ProjectileRuntime.scale`, so a d10 orb is bigger
than a d4 orb *by construction*. One authored number, two consequences.

### Form sets the base tier

Form already decides how many instances and how many ticks a cast produces — which *is* the
per-instance magnitude, for a mechanical reason rather than an arbitrary mapping. A Stream
that ticks ten times obviously cannot roll d8 per tick.

| Element | Form | Profile | Base tier |
|---|---|---|---|
| Aether | Beam — instant, piercing | one hit, precise | **Greater** (d10) |
| Air | Projectile — travels | one hit | **Standard** (d8) |
| Earth | Burst — erupts at a point | one hit, area | **Standard** (d8) |
| Fire | Cone — breath, short and wide | several at once | **Lesser** (d6) |
| Water | Stream — sustained, flowing | ticks while channelled | **Faint** (d4) |
| Nature | Field — lingers in an area | ticks while it lingers | **Faint** (d4) |

Fire-in-slot-2 still *means* Cone. The d6 is a consequence of Cone, not a second meaning to
memorise.

### Only two things move the tier

- **Modify · Water — Split.** Divides the cast into N instances, each **one tier down**.
  Same total, spread out. A *shape* change.
- **Amplify · Fire — Magnitude.** **One tier up**. A *power* change.

Everything else on the ladder is authored (an authored spell names its tier outright).
Split divides, Amplify adds — learnable in one sentence, and it matches the concepts: Water
is flow (spreading the same thing out), Air is multiplicity (more of it, below).

`Fire·Air` → Projectile, Standard, 1d8. `Fire·Air·Fire·Water` → split, 1d6 each, and each
projectile is visibly smaller. `Fire·Aether·Fire·…·Fire` → Beam at Grand, 1d12, the ceiling.

### Naming falls out of School × Tier

The tier is neutral in data. The **name the player sees** comes from the School row, which
carries five display names — so the ladder reads as flavour, not as numbers:

| School | Faint | Lesser | Standard | Greater | Grand |
|---|---|---|---|---|---|
| Fire | Ember | Fireball | Flame Lance | Fireblast | Conflagration |
| Air | Gust | Wind Slash | Gale | Cyclone | Tempest |
| … | | | | | |

Fire's d6 is a Fireball; Air's d6 is a Wind Slash. Same die, same size, different dressing —
which is exactly what the player should be able to read off the screen.

One extra `string[5]` on the School asset. No code cost.

### Bonus dice keep the packet's type

A packet carries exactly one `damageType`. Bonus dice from Wreathe, talents or gear roll and
**add to the existing type** — they do not spawn a second packet. Wreathe on a Wind Slash
reads as *"your fire attunement makes your attacks hit harder"*, not as a typing puzzle, and
one hit stays one number.

### The guard, not the fallback

Spell `DamageEffect`s run with `useWeaponDamage = true`. If the `weaponOverride` ever
arrives null, today's code falls through to `GetWeaponDamage(attacker)` — which rolls the
equipped katana, or the fists via `CombatStanceModule`. **A fireball silently rolling a
sword, with no error.**

So: `AbilityCategory.Spell` with a null override is an **error**, not a fallback. Log it and
use `baseDamage`. A `Spell` entry on `WeaponCategory` makes that checkable and keeps spell
dice out of any equipment UI that enumerates weapons.

### Weapons keep inline dice

`WeaponData.damageDice` stays an inline `DiceRoll`. A tier asset earns its keep when the
tier carries *more than the dice* — for spells it drives dice, scale and cost together. For
a weapon it would carry only the die, and a d8 is 1–8; there is nothing to rebalance. Making
it an asset would trade two ints for a reference, a load and a new null path where a weapon
silently deals 0.

If a balance ladder view is wanted — every weapon on one axis — that is an editor window
over `DamageMin/Max/Average`. Same benefit, no indirection.

---

## The remaining tables

*Starting point, not settled. The concepts are the commitment; these are the tuning.*

**Slot 3 — Effect** (what it does on arrival)

| Element | Effect | Seeks |
|---|---|---|
| Fire | Damage — direct, burst | hostile |
| Water | Restore — heal, cleanse | friendly |
| Air | Displace — knock, pull, hasten | hostile |
| Earth | Ward — shield, armour, barrier | friendly |
| Nature | Bind — root, slow, entangle | hostile |
| Aether | Empower — apply the School's signature state | friendly |

This slot is what opens the system past damage. `Water·Air·Water` is a healing bolt.
`Earth·Earth·Earth` is a ground-slam that armours allies.

**Every Effect declares which faction stance it seeks.** That one property — not a branch in
the code — is what lets heals, wards and buffs find allies while damage finds enemies. Range
can redirect the seek to self: `Air·?·Air` (Displace) with Earth in Range means you displace
*yourself*, which is a dash.

**The projectile system already accepts this — verified.** `TargetStance`
(`Hostile`/`Friendly`/`Any`) lives in `ProjectileRuntime`, and `ProjectileAim.StanceAllows`
is the single shared rule governing both the aim probe (`ProjectileAim.Probe`) and the hit
filter (`ProjectilePayload.Accepts`). The Effect writes the stance at cast time and a healing
bolt probes for allies using the same code a fireball uses to probe for enemies. No new path,
no special case.

*Drain left this table when Empower took the slot. Steal-and-transfer works better as a Modify
on Damage than as an Effect of its own, and dispel is an authored-spell concern.*

### Signature states — the six buffs

Aether in the Effect slot applies the **School's** signature state. Same rule as everything
else: slot 1 decides which element you get. Six schools, six buffs, no special case.

| School | State | What it does |
|---|---|---|
| Fire | **Wreathe** | ability hits roll an extra die (same type as the hit) |
| Water | **Renew** | health regenerates over time |
| Air | **Quicken** | movement and attack speed |
| Earth | **Stoneskin** | armour, damage reduction |
| Nature | **Thorns** | attackers take damage |
| Aether | **Attune** | spell power, reduced cost |

Buffs inherit the whole grammar for free — Form decides delivery, Range decides reach:

| Sequence | Result |
|---|---|
| `Fire·Air·Aether` | fire buff on a projectile — fired at one ally |
| `Fire·Earth·Aether` | fire buff as a burst — everyone nearby, a party buff |
| `Fire·Fire·Aether` | fire buff as a cone — a group in front of you |
| `Fire·Earth·Aether·…·Earth` | burst at self range — just you |

**Wreathe plugs into the dice system at the one hook built for it.**
`DamageEffect.GetExternalFlatDamage()` is confirmed a `return 0f` stub — that is exactly
where a bonus die belongs, and filling it makes Wreathe work for melee, thrown weapons and
spells at once with no per-path code.

It also composes with Amplify: Fire in slot 6 steps the tier, so an amplified Wreathe rolls
*bigger* dice rather than applying a multiplier. Dice stay the unit of power throughout.

**Slot 4 — Modify** (how it behaves)

| Element | Modify |
|---|---|
| Fire | Ignite — the effect also applies over time |
| Water | **Split — N instances, each one tier down** |
| Air | Chain — jumps from target to nearby target |
| Earth | Anchor — the effect stays where it landed |
| Nature | Spread — propagates outward from whoever it hit |
| Aether | Pierce — ignores resistance, passes through |

**Slot 5 — Range**

| Element | Range |
|---|---|
| Earth | Short / self |
| Fire | Medium |
| Water | Medium-long |
| Air | Long |
| Nature | Placed — target a point |
| Aether | Instant — hitscan, no falloff |

**Slot 6 — Amplify** (how much)

| Element | Amplify |
|---|---|
| Fire | **+1 tier** — bigger dice, bigger projectile |
| Earth | + area |
| Air | **+count** — more instances, *same* tier |
| Water | + duration |
| Nature | + targets affected per instance |
| Aether | + pierce, + crit |

Air (+count) and Nature (+targets) are the closest pair left in the grammar and want
sharper definitions before they mean anything — see Open questions.

---

## Short sequences

**Every slot has a default, so every sequence is already a complete spell.** No slot changes
meaning based on length — that would multiply what the player has to learn and turn the
resolver into special cases.

| Slot | Default when unset |
|---|---|
| Form | Burst at self (Standard tier) |
| Effect | Damage |
| Modify | none |
| Range | the Form's natural range |
| Amplify | base |

`Fire·Air` is not a spell missing four slots. It is a Standard-tier fireball rolling 1d8 at
default range with no modifier — complete, cheap, and the bread and butter of combat.

**Length buys specificity, not magnitude.** Magnitude comes from Form and Amplify only.

---

## Keeping long spells from dominating

No hard cap, so three levers, all three used:

**Cost curve — superlinear, and tier-weighted.** Roughly `base × length² × tier.costMult`.
A six-element spell costs about 9× a two-element one, not 3×, and a Grand-tier cast costs
nearly twice a Standard one on top of that.

**Cast-time exposure — the strongest lever, and free.** Entering six elements takes real
seconds of standing still and readable. A two-element fireball is a snap reaction; a
six-element is a commitment made from cover. Good combat design that needs no balance numbers.

**Slots as sidegrades, not upgrades.** Picking Earth in the Range slot (short) should be
*cheaper and harder-hitting* than picking Air (long). Adding a slot buys a **choice**;
only slot 6 is pure power.

---

## Aether

**In primitive casting, Aether is just Aether.** Neutral, undifferentiated. No polarity, no
special rule, no branch in the resolver — the primitive grammar has **zero element exceptions**,
and that is worth protecting.

**Light and Shadow are the two faces of the same coin, and only authored spells can name one.**

So the difference between primitive and authored is not power, it is **precision**. A raw
caster throws undifferentiated Aether; a trained one has learned to give it a face. Raw Aether
is not weak — it is *unresolved*.

Practical consequence: `DamageType` needs three Aether-family entries. `Aether` for primitive,
plus Radiant (rename `Holy`) and Shadow (new), each with its own row in
`Damage Calculation Config`. That gives a real resistance hook — an enemy that shrugs off raw
Aether but folds to Shadow is a legible reason to go and find the authored spell.

---

## Architecture

**Do not build an `AbilityDefinition` per cast.** `ScriptableObject.CreateInstance` every cast
is the GC trap the projectile system was designed to avoid.

Mirror the seam that already exists:

```
element keys → SpellcraftSystem accumulates a sequence
E pressed
  → look up sequence key ("fire.air.air")
      HIT  → AbilitySystem.UseAbility(authoredAbility)   ← a normal ability, done
      MISS → compose a SpellRuntime struct from the sequence
             resolve tier  = Form base ± Split ± Amplify   (clamped)
             resolve cost  = base × length² × tier.costMult
             consume resources through IResourceProvider
             AbilitySystem.UseAbility("cast_primitive")
  → one authored CastPrimitiveSpell ability plays the animation
  → Effect1 fires → ProjectileLauncher asks its modifiers for data + runtime
                  → SpellcraftSystem returns the Form's ProjectileData,
                    folds SpellRuntime into ProjectileRuntime,
                    and supplies the tier's WeaponData on ProjectileLaunch
  → the projectile fires, and never knows a spell system exists
```

| Piece | Role |
|---|---|
| `SpellcraftSystem` | `IBrainModule` + `ISaveable` on the brain — owns the sequence, the lookup, the tier, the cost |
| `SpellRuntime` (struct) | school, form, effect, modify, range, amplify, tier — flat values, composed at cast |
| `SpellTierTable` | ONE asset — the dice/scale/cost ladder |
| `ElementDefinition` ×6 | per-school: colour, VFX, damage type, tier display names |
| `FormDefinition` ×6 | per-form: `ProjectileData`, base tier, natural range |
| `CastPrimitiveSpell` | ONE authored `AbilityDefinition` — animation, anim events, state |
| authored spells | ordinary `AbilityDefinition` assets, keyed by sequence |

### The three seams that need building

The design's claim that magic "changes nothing outside itself" is *nearly* true. Three
touch-points are real, and all three are generic rather than magic-specific:

**1. `ProjectileLauncher` needs a modifier hook.** `Fire()` currently does
`ProjectileRuntime.FromData(data)` with the data taken straight from
`ability.projectileData` — there is no injection point. Both halves need one: Form decides
*which* `ProjectileData`, and tier/amplify decide the *numbers*. The launcher's own comment
already invites this ("*When modifiers arrive they fill this struct here*").

So: an `IProjectileLaunchModifier` interface that any brain module can implement, collected
in `LateInitialize` via `GetComponentsInChildren`. `SpellcraftSystem` implements it. Talents,
weapon upgrades and buffs use the same door later. **The projectile system learns nothing
about magic.**

**2. The dice reference has to ride the per-cast path.** `ProjectilePayload` reads
`brain.Data.weaponData` — the shared asset. If Form picks the `ProjectileData` and tier picks
the die, a Fire cone at d4 and a Fire cone at d10 would need two assets, and we are back to
the prefab explosion. The override belongs on **`ProjectileLaunch`**, which is already
per-cast context and already carries an `AbilityDefinition` reference — not on
`ProjectileRuntime`, which is documented as keeping the numbers numbers.

**3. `tint` must become per-cast.** Today `tint` and `tintProperty` are on `ProjectileData`,
applied by `ProjectileVisual.Setup(data)` through a `MaterialPropertyBlock`. A composed spell
picks its element at cast time, so `tint` joins `ProjectileRuntime` alongside `targetStance`.
One float4 in the struct, one line in `FromData`, and `ProjectileVisual` reading
`brain.Runtime.tint` instead of `brain.Data.tint`. The asset value stays the default, so every
existing projectile behaves identically.

### It needs no manager

Worth stating plainly, because the instinct is to reach for one. Magic touches `ManagerBrain`
**nowhere**. Sort what it owns into two piles and the reason is obvious:

**Static data** — the six element tables, the tier table, the authored-spell index, the
visuals. Authored once, read by everyone, never written. That is a ScriptableObject
referenced directly by `SpellcraftSystem`, the way `ModelDatabase` is referenced — not a
registry that has to be loaded, warmed and asked. `AbilityManager` exists because abilities
are resolved *by string id* from arbitrary callers; nothing looks up a spell by id from
across the game.

**Per-character state** — which spells this character has unlocked, and the sequence currently
being entered. That belongs to the character, so `SpellcraftSystem` implements `ISaveable` and
serialises its own unlock list, exactly as `FactionSystem` does.

Neither pile is shared mutable world state, which is the only thing a manager is for.

**This is not theoretical.** `Manager_Brain` in `Zoo.unity` is present but renamed
`(HiddenForNow)Manager_Brain` and **set inactive**. Any system that needed it would be dead
in the project's main test scene. Magic works the moment the brain has the module.

### Costing goes through `IResourceProvider`

`AbilitySystem.UseAbility` checks and consumes `ability.resourceCosts` — a fixed array on a
**shared asset**. A composed spell's cost varies by sequence and tier, so it cannot be written
there without corrupting the asset for every future cast.

`SpellcraftSystem` therefore owns the cost: it resolves `IResourceProvider` from the brain
(exactly as `AbilitySystem` itself does), checks affordability, consumes, and only then calls
`UseAbility("cast_primitive")` — whose own `resourceCosts` array is empty. Resolving a
provider through the brain is the codebase's own convention, not a direct system call.

### `SpellcraftSystem` needs no edit to `ControllerBrain`

`CacheModuleArrays` walks `GetComponentsInChildren<IBrainModule>(true)` and
`InitializeModules` falls through to initialising anything not in its explicit ordered list.
So `SpellcraftSystem` is discovered, initialised and updated **without a serialized field on
`ControllerBrain` and without touching that file** — the same way `ProjectileLauncher`,
`VFXSystem`, `CombatStanceModule` and `EffectManagerModule` already are.

---

## Visuals

The obvious approach is one asset per spell, which is 6 schools × 6 forms = **36 prefabs before
a single Modify**. Wrong axis. School and Form are independent, and so are their visuals:

- **Form** decides the *shape* — an orb, a cone, a beam, a lingering field. Geometry, and it is
  element-agnostic.
- **School** decides the *dressing* — colour, material, trailing particles, impact.
- **Tier** decides the *size* — one number from the tier row, applied as scale.

Multiply those at render time rather than at authoring time and 36 becomes **12 assets**, and
the size ladder is free.

There is **one fire projectile**, not one per magnitude. A split fireball is the same prefab
at a smaller scale, and it is smaller *because* it rolls a smaller die.

### Where each half lives

**Form visuals live on the six Form `ProjectileData` assets** — `visualPrefab` on each. This is
already the split the projectile system was built for: the archetype prefab is structure, the
data names the model.

**School visuals get one new asset each:**

```
ElementDefinition         ×6, one per element
    damageType            Fire, Frost, … — the packet's type
    color                 the tint
    material              optional — overrides tint once real shaders exist
    overlayPrefab         optional — embers, mist, motes; parented to the visual
    impactVfx             on contact
    statusVfx             the aura for this element's signature buff
    tierNames  string[5]  Ember, Fireball, Flame Lance, Fireblast, Conflagration
```

`SpellcraftSystem` reads slot 1, hands the `ElementDefinition` to the launcher alongside the
`SpellRuntime`, and the projectile assembles: Form's mesh, School's colour, Tier's size.

### Placeholders are enough

Six tinted spheres and one particle burst exercise the **entire real code path** — resolve,
compose, tint, scale, spawn, impact. Nothing is stubbed out and nothing has to be rewritten
when art arrives; `overlayPrefab` and `material` are the fields that fill in later. Build the
grammar against primitives, and swapping in real effects is an asset job, not a code job.

### Buff VFX is not a projectile concern

A buff aura is attached to a *person*, not fired at one. The projectile delivering
`Fire·Air·Aether` dies on impact; the Wreathe glow that follows belongs to the target.

`VFXSystem` has an `Aura` anchor — **verified present and currently unused on both `Base_PC`
and `Base_NPC`**. That is where `statusVfx` spawns. Six auras, one per element, with no
involvement from the projectile system at all.

---

## What the Forms cost to build

| Form | Status |
|---|---|
| Projectile | ✅ works today |
| Beam | ✅ `Hitscan` mode + linger |
| Cone | ⚠️ projectile at the mouth anchor, low speed, high pierce — needs **scale-over-lifetime** |
| Burst | ⚠️ projectile at the feet, speed 0, big radius, short lifetime — needs **`SpawnOrigin.Self`** |
| Stream | ❌ sustained channel — new |
| Field | ❌ persistent volume that ticks — new |

Four of six are projectile tuning. Two small additions — `scaleOverLifetime` and
`SpawnOrigin.Self` — get four Forms working, which is enough to feel the grammar out before
committing to the hard two.

---

## Codebase audit — 2026-08-24

Checked against `ControllerBrain`, `CrabSystem/Projectiles/`, `CrabSystem/Abilities/`,
`CrabSystem/DamageCombat/`, `Base_PC.prefab`, `Base_NPC.prefab`, `Zoo.unity`, `MenuScene.unity`.

### Confirmed

- `ProjectileRuntime.targetStance` exists and is copied per shot by `FromData`.
- `ProjectileAim.StanceAllows` is the single owner of "may this affect that", used by both
  `ProjectileAim.Probe` and `ProjectilePayload.Accepts`. Heals-find-allies works as designed.
- `DamageEffect.GetExternalFlatDamage()` is a `return 0f` stub — the Wreathe hook is real.
- `DamageEffect.Apply(target, externalMultiplier, weaponOverride)` already takes a per-cast
  `WeaponData`. The spells-are-weapons law needs no new plumbing on the damage side.
- `VFXSystem` exposes an `Aura` anchor; `Base_PC` and `Base_NPC` both have the
  `VFX System → Anchors → {Overhead, CastOrigin, HitSparks, Aura, Back}` transforms wired.
- `tint`/`tintProperty` live on `ProjectileData` and are applied via `MaterialPropertyBlock`
  in `ProjectileVisual` — exactly as described, and exactly why they must move.
- `ControllerBrain` auto-discovers any `IBrainModule` child, so no edit is needed to add one.
- `ManagerBrain` is genuinely optional in practice — it is disabled in `Zoo.unity`.

### Refuted — the design was wrong

- **There is no status/buff layer.** `EffectManagerModule` tracks instant damage, DoTs, heals,
  HoTs and knockback only. It has no concept of a named persistent state, no expiry, no
  application/removal events. The six signature states (Wreathe, Renew, Quicken, Stoneskin,
  Thorns, Attune) are **build-from-scratch**, not a hook-up. This is the single largest piece
  of unbudgeted work in the design.
- **`ProjectileLauncher` is only on `Base_PC`.** `Base_NPC` does not carry it, so NPCs cannot
  fire projectiles at all today. Not blocking for player magic, but any enemy caster needs the
  component added first.

### Gaps the design glossed

- `ProjectileLauncher.Fire()` takes its `ProjectileData` from `ability.projectileData` with no
  override path. Form cannot select a `ProjectileData` without the modifier hook above.
- `AbilitySystem` consumes a fixed cost array from a shared asset — variable cost must be owned
  by `SpellcraftSystem`.
- `ProjectilePayload` sources dice from `brain.Data.weaponData` (the shared asset), not from
  per-cast context.

### Also noticed, unrelated

- `Component_Brain.prefab` in `Z. Entities/PlayerPrefabs/` is serialized against a dead
  "provider coordinator" field layout — every reference is `{fileID: 0}`. It is stale and will
  produce a brain with nothing wired if anyone instantiates it.
- `ControllerBrain.animator` is unassigned on both `Base_PC` and `Base_NPC` (resolved at
  runtime by `ResolveRootReferences`, so this is presumably intentional).
- `Base_NPC` is a hand-copied duplicate of `Base_PC`, not a prefab variant — they share
  fileIDs but not lineage, so fixes have to be applied twice.

---

## Open questions

**Does the sequence display predict the spell, or just show the elements?** Raw element icons
are more incantation-like; a resolved name updating live is far more learnable but risks
feeling like a menu. The School × Tier naming table makes the resolved name genuinely
attractive — *"Fireblast"* under six icons teaches the grammar. Leaning: icons prominent,
resolved name small underneath.

**Air (+count) vs Nature (+targets) in Amplify.** The closest remaining pair. Split (Modify
Water) is now clearly "divide and shrink" and Amplify Air is "add at the same tier", but
Nature's "+targets" still overlaps with Chain and Spread. Needs a sharper definition.

**Does Split clamp or floor out?** A Stream is already Faint (d4), so splitting it clamps —
which makes Split pure upside on the two tick Forms. Either accept it (the cost curve pays
for the extra instances) or add a floor rule. Watch it in play.

**Is there a partial refund on cancel?** Six elements entered and then interrupted is a lot of
resource to lose.

---

## Decided and closed — do not reopen

**No slot selects the tier.** Proposed and rejected. Only Fire (magnitude) and arguably Earth
(mass) have any ordinal reading; the other four would map to d4–d12 arbitrarily, in the one
slot where the player cannot predict the answer from the concept table. It would also create a
second pure-power slot with one correct answer — a tax on the fingers, not a choice. Tier comes
from Form, moved by Split and Amplify.

**No wildcard position.** A slot whose meaning changes is exactly what the grammar rules out;
it also breaks "every sequence is valid by construction", because a wildcard has to know what
it stands in for. And the system is not short of room — 6⁶ is 46,656 sequences. What it is
short of is *authored meaning*, which is what the authored-spell dictionary is for.

**Weapons do not get tier assets.** See *Weapons keep inline dice* above.

---

## Not decided, deliberately

The specific element→meaning tables above are a **starting point**. The commitment is the
*structure*: six positional slots, one concept per element, Form sets magnitude, defaults for
short sequences, lookup-then-compose, and dice as the unit of power. The contents of each
table are tuning, and expected to move.
