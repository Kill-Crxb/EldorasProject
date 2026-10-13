# Magic System — Setup

Companion to `Magic_System.md`. The code is in; this is what has to be authored before a
sequence does anything.

**Order matters.** Each step depends on the one above it.

---

## 0. What was added

**New — `CrabSystem/Magic/`**

| File | What it is |
|---|---|
| `SpellElement.cs` | the six elements, the six slots, the sequence key |
| `SpellTier.cs` | the ordinal ladder and its table |
| `ElementDefinition.cs` | per-school asset — damage type, colour, tier names, cast ability |
| `FormDefinition.cs` | per-form asset — ProjectileData and base tier |
| `SpellGrammar.cs` | the one asset holding every table |
| `SpellRuntime.cs` | the composed spell, and the resolver |
| `SpellcraftSystem.cs` | the brain module |
| `SpellcraftInput.cs` | temporary keyboard driver |

Everything above is in namespace **`NinjaGame.Magic`** — `ElementDefinition` and
`FormDefinition` are generic enough names to collide with something in `ImportedAssets`.

**New — `CrabSystem/Projectiles/`**

`IProjectileLaunchModifier.cs` — the seam, plus `ProjectileLaunchPlan`. Deliberately in the
**global** namespace and in the projectile folder: it belongs to the projectile system and is
generic. Talents and gear come through the same door.

**Modified — 7 files**

| File | Change |
|---|---|
| `ProjectileTypes.cs` | `tint` on `ProjectileRuntime`; `dice` on `ProjectileLaunch` |
| `ProjectileLauncher.cs` | collects modifiers, builds a plan, fires a fanned volley |
| `ProjectileBrain.cs` | stores `Dice`; passes runtime to the visual |
| `ProjectileVisual.cs` | `Setup(data, runtime)` — tint from the runtime |
| `ProjectilePayload.cs` | dice from `brain.Dice`, not the shared asset |
| `DamageEffect.cs` | `requiresSuppliedDice` — the guard |
| `WeaponData.cs` | `WeaponCategory.Spell` appended |

Every existing projectile behaves identically: the asset's `tint` and `weaponData` seed the
per-cast values, so an unmodified shot resolves exactly as before.

---

## 1. Five dice assets

`Create → Combat → Weapon Data`, into `Database/Resources/ItemDatabase/Weapons/Spells/`.

| Asset | damageDice | flatBonus | category |
|---|---|---|---|
| `Spell_d4` | 1d4 | 0 | Spell |
| `Spell_d6` | 1d6 | 0 | Spell |
| `Spell_d8` | 1d8 | 0 | Spell |
| `Spell_d10` | 1d10 | 0 | Spell |
| `Spell_d12` | 1d12 | 0 | Spell |

Five, not thirty. The die is shared across all six schools — the damage **type** comes from
the cast ability, not from here.

---

## 2. Six Form ProjectileData assets

`Create → NinjaGame → Projectile Data`, into `Database/Resources/ItemDatabase/Weapons/Spells/`.

Copy `ProjectileData_Shuriken` as a starting point. For each: `archetypePrefab` =
`Base_Projectile`, `visualPrefab` = a placeholder sphere, `hitMask` = whatever Shuriken uses.

Only two of the six work today. Build these first:

| Asset | speed | lifetime | collisionRadius | pierceCount | notes |
|---|---|---|---|---|---|
| `Spell_Projectile` | 18 | 3 | 0.4 | 0 | works |
| `Spell_Beam` | — | 0.3 | 0.3 | 3 | `movement.mode` = Hitscan |

The other four are the design's build-cost table. `Cone` and `Burst` need
`scaleOverLifetime` and `SpawnOrigin.Self` respectively; `Stream` and `Field` are new
systems. Author placeholders now if you like — the grammar reports a missing `projectileData`
by name rather than failing silently — but expect Fire, Earth, Water and Nature in slot 2 to
be unsatisfying until those land.

**Leave `weaponData` empty on all six.** The tier supplies the dice.

---

## 3. Six cast abilities

`Create → NinjaGame → Ability Definition`, into
`Database/Resources/AbilityDatabase/Spells/`. One per school:
`Cast_Fire`, `Cast_Water`, `Cast_Air`, `Cast_Earth`, `Cast_Nature`, `Cast_Aether`.

Six rather than one because `DamageEffect.damageType` is a serialized field on a **shared
asset** — it cannot vary per cast without corrupting the asset. Six assets is the data
answer to what would otherwise be a branch.

Identical except for the damage type:

```
abilityId              cast_fire, cast_water, ...      (unique, lowercase)
abilityName            Cast Fire
abilityCategory        Spell                            ← silence blocks it
abilityType            Offensive
resourceCosts          EMPTY                            ← SpellcraftSystem owns the cost
cooldown               0.4
castTime               0
effectTrigger          Effect1                          ← MUST be Effect1/2/3
animationTrigger       Ability
projectileData         Spell_Projectile                 ← any non-null, see below
damageEffects[0]
    useWeaponDamage        TRUE
    requiresSuppliedDice   TRUE                         ← the guard
    damageType             Fire / Water / Wind / Earth / Nature / Aether
    baseDamage             0
```

**Two traps, both silent if you get them wrong:**

- `effectTrigger` must be `Effect1`, `Effect2` or `Effect3`. With anything else
  `AbilitySystem` applies effects directly, `OnAbilityAnimationEvent` never fires, and
  nothing spawns.
- `projectileData` must be **non-null**. `ProjectileLauncher.HandleAnimationEvent` bails on a
  null `projectileData` *before* any modifier runs, so a spell would never get the chance to
  choose its Form. Assign `Spell_Projectile` as a placeholder; the Form overrides it.

The animation clip driving `animationTrigger` needs an `Effect1` animation event on it, the
same as any throw.

`DamageType` already has `Fire`, `Water`, `Wind`, `Earth`, `Nature` and `Aether` — no enum
change needed. `Radiant`/`Shadow` for authored Aether can be appended later.

---

## 4. Six ElementDefinitions

`Create → NinjaGame → Magic → Element Definition`, into `Database/Resources/Magic/`.

| Asset | element | damageType | castAbility | color |
|---|---|---|---|---|
| `Element_Fire` | Fire | Fire | Cast_Fire | orange |
| `Element_Water` | Water | Water | Cast_Water | blue |
| `Element_Air` | Air | **Wind** | Cast_Air | pale cyan |
| `Element_Earth` | Earth | Earth | Cast_Earth | brown |
| `Element_Nature` | Nature | Nature | Cast_Nature | green |
| `Element_Aether` | Aether | Aether | Cast_Aether | violet |

`tierNames` — five entries each, Faint → Grand. This is what the player reads:

| School | Faint | Lesser | Standard | Greater | Grand |
|---|---|---|---|---|---|
| Fire | Ember | Fireball | Flame Lance | Fireblast | Conflagration |
| Water | Droplet | Water Jet | Torrent | Deluge | Maelstrom |
| Air | Gust | Wind Slash | Gale | Cyclone | Tempest |
| Earth | Pebble | Stone Shot | Boulder | Landslide | Cataclysm |
| Nature | Seed | Thorn | Bramble | Overgrowth | Wildwood |
| Aether | Mote | Arcane Bolt | Aether Lance | Rift | Unmaking |

Placeholder art: leave `material`, `overlayPrefab`, `impactVfx` and `statusVfx` empty. Six
tinted spheres exercise the entire real path.

---

## 5. Six FormDefinitions

`Create → NinjaGame → Magic → Form Definition`, into `Database/Resources/Magic/`.

| Asset | element | Form | projectileData | baseTier |
|---|---|---|---|---|
| `Form_Beam` | Aether | Beam | `Spell_Beam` | **Greater** |
| `Form_Projectile` | Air | Projectile | `Spell_Projectile` | **Standard** |
| `Form_Burst` | Earth | Burst | `Spell_Burst` | **Standard** |
| `Form_Cone` | Fire | Cone | `Spell_Cone` | **Lesser** |
| `Form_Stream` | Water | Stream | `Spell_Stream` | **Faint** |
| `Form_Field` | Nature | Field | `Spell_Field` | **Faint** |

`naturalCount` = 1 on all six.

---

## 6. The grammar

`Create → NinjaGame → Magic → Spell Grammar` → `Database/Resources/Magic/SpellGrammar`.

**elements / forms** — drag the twelve assets above in.

**tiers.rows** — five rows:

| tier | displayName | dice | scale | costMultiplier |
|---|---|---|---|---|
| Faint | Faint | `Spell_d4` | 0.5 | 0.6 |
| Lesser | Lesser | `Spell_d6` | 0.75 | 0.8 |
| Standard | Standard | `Spell_d8` | 1.0 | 1.0 |
| Greater | Greater | `Spell_d10` | 1.3 | 1.4 |
| Grand | Grand | `Spell_d12` | 1.6 | 1.9 |

**effects** — six rows. `implemented` is a note to yourself, not a gate:

| element | displayName | stance | implemented |
|---|---|---|---|
| Fire | Damage | Hostile | ✔ |
| Water | Restore | Friendly | ✗ |
| Air | Displace | Hostile | ✗ |
| Earth | Ward | Friendly | ✗ |
| Nature | Bind | Hostile | ✗ |
| Aether | Empower | Friendly | ✗ |

Only Damage lands as intended. The other five resolve their stance correctly and fly — a
`Water·Air·Water` bolt really does seek allies and pass through enemies — but it deals damage
on arrival, because none of those effect systems exist yet. Worth casting anyway: it proves
the stance path end to end.

**modifiers** (slot 4) — six rows, all defaults except:

| element | displayName | tierSteps | extraInstances | spreadDegrees | pierceBonus |
|---|---|---|---|---|---|
| Water | Split | **−1** | **+2** | **20** | 0 |
| Aether | Pierce | 0 | 0 | 0 | **+3** |
| Fire | Ignite | 0 | 0 | 0 | 0 |
| Air | Chain | 0 | 0 | 0 | 0 |
| Earth | Anchor | 0 | 0 | 0 | 0 |
| Nature | Spread | 0 | 0 | 0 | 0 |

Ignite, Chain, Anchor and Spread are inert rows until their behaviours are built. They cost
nothing and change nothing, which is correct — an unbuilt Modify should be a no-op, not an
error.

**ranges** (slot 5) — reach is `speed × lifetime`:

| element | displayName | lifetimeMultiplier | speedMultiplier | redirectToSelf |
|---|---|---|---|---|
| Earth | Short / self | 0.4 | 0.8 | **true** |
| Fire | Medium | 1.0 | 1.0 | false |
| Water | Medium-long | 1.4 | 1.0 | false |
| Air | Long | 2.0 | 1.3 | false |
| Nature | Placed | 1.0 | 0.6 | false |
| Aether | Instant | 0.5 | 3.0 | false |

**amplifiers** (slot 6):

| element | displayName | tierSteps | extraInstances | spread | scale× | speed× | lifetime× | pierce |
|---|---|---|---|---|---|---|---|---|
| Fire | Magnitude | **+1** | 0 | 0 | 1.0 | 1.0 | 1.0 | 0 |
| Earth | Area | 0 | 0 | 0 | **1.5** | 1.0 | 1.0 | 0 |
| Air | Count & speed | 0 | **+2** | **14** | 1.0 | **1.3** | 1.0 | 0 |
| Water | Duration | 0 | 0 | 0 | 1.0 | 1.0 | **1.6** | 0 |
| Nature | Targets | 0 | 0 | 0 | 1.0 | 1.0 | 1.0 | **+2** |
| Aether | Pierce & crit | 0 | 0 | 0 | 1.0 | 1.0 | 1.0 | **+4** |

Split and Amplify·Air use the **same field** and mean opposite things — divide versus add.
That difference lives entirely in the numbers; no code knows about it.

**defaults** — `defaultForm` = Earth (Burst at self), `defaultEffect` = Fire (Damage).

**cost** — `costResource` = `ManaDefinition`, `baseCost` = 4.

**authoredSpells** — leave empty for now. Adding an entry and calling
`SpellcraftSystem.Unlock("fire.air.air")` is the whole of learning a spell.

---

## 7. Wire the player

On `Base_PC`, under `Component_Brain`, next to `ProjectileLauncher`:

1. Add **`SpellcraftSystem`**, assign `SpellGrammar`, tick `debugLogging`.
2. Add **`SpellcraftInput`**.

No edit to `ControllerBrain` and no serialized field on it —
`CacheModuleArrays` walks `GetComponentsInChildren<IBrainModule>(true)` and
`InitializeModules` falls through to anything not in its ordered list, exactly as
`ProjectileLauncher` and `VFXSystem` are already found.

**One constraint from `SaveManager`:** it discovers saveables with
`GetComponents<ISaveable>()` on the brain GameObject plus
`GetComponentsInChildren<ISaveable>()` — **without `includeInactive`**. A `SpellcraftSystem`
on an inactive child is silently skipped and unlocks never persist. Keep it on an active
object. Its save id is `"spellcraft"`, unique against the eleven existing saveables, and it
loads after the ordered ones, which is fine — unlocks depend on nothing.

Default keys are the **numpad**, because the number row is already bound to `Hotbar1-9`:

```
Numpad 1-6      Fire Water Air Earth Nature Aether
Numpad Enter    cast
Numpad -        backspace
Numpad .        clear
```

---

## 8. What to expect

Zoo is the scene for this — `PlayerSpawner` there points at `Base_PC`. Note `Manager_Brain`
is disabled in Zoo; magic does not care, which was the point of it needing no manager.

| Sequence | Expect |
|---|---|
| `Air` | Projectile · Standard · **1d8** · orange? no — Air school, pale cyan, Wind damage |
| `Fire·Air` | a Fireball: orange projectile, Standard, **1d8**, cost 16 |
| `Fire·Air·Fire·Water` | **three** smaller orange orbs, Lesser, **1d6** each, fanned 20° |
| `Fire·Air·Fire·…·Fire` | Flame Lance → Fireblast: bigger orb, **1d10** |
| `Water·Air·Water` | a blue bolt that **flies through enemies and stops on allies** |
| `Fire·Aether` | Aether's Form — a Beam — in Fire's colour, Greater, **1d10** |

With `debugLogging` on, every cast logs the key, the resolved name, the tier, the dice label
and the cost.

**The healing bolt is the test worth doing first.** It proves the stance path — the aim probe
and the hit filter both reading `ProjectileRuntime.targetStance` through
`ProjectileAim.StanceAllows` — which is the single load-bearing claim the whole design rests
on. It will deal damage to the ally it finds, because `Restore` has no effect system yet. That
is expected, and it is still the right test: what is being proven is *who it chose*.

---

## 9. Known gaps

Carried from the audit, unchanged by this pass:

- **No status/buff layer.** `EffectManagerModule` has no named persistent state, no expiry, no
  apply/remove events. The six signature states are build-from-scratch. `Aether` in slot 3
  resolves and flies but cannot apply anything.
- **Four of six Forms are not buildable.** Cone and Burst want `scaleOverLifetime` and
  `SpawnOrigin.Self`; Stream and Field are new systems.
- **`ProjectileLauncher` is on `Base_PC` only.** `Base_NPC` has no launcher, so an enemy
  caster needs the component added first.
- **Five of six Effects land as damage.** Stance is correct; arrival is not.
- **`SpellcraftInput` ignores the UI input router.** Typing in a text field still enters
  elements. It is temporary and should die when the element hotbar page exists.
- **Split clamps at Faint.** Splitting a Stream or Field cannot go below d4, so Split is pure
  upside on the two tick Forms. The cost curve pays for the extra instances; watch it in play.
