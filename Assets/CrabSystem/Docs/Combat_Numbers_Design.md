# Combat Numbers Design

Dice-first combat model. One rule everywhere: **stats never use percentages — every stat is a flat modifier added to a specific dice event.**

## Pillars

1. Weapon dice are the heart of damage. Stats support the dice, never drown them (target: dice ≈ 40–70% of a hit).
2. Combat is long, slow, methodical. Rounds are 4 seconds; action economy is limited; defense is a choice, not a stat check.
3. No hidden breakpoints. Every point spent does something visible (+1 to a roll). Scarcity comes from allocation cost, not from math.
4. **Flat power.** A Katana is 1d8+2 at level 1 and at level 30. Variants are sidegrades (different dice shapes, elements), never tiers. All growth lives in small single-digit modifiers.
5. Gear grants dice variants, sockets, and flat mods (+1 Force). The old "X per Y stat" formulas are retired.

## Round & Action Economy

- **Round = 4 seconds** (a balancing window, combat stays real-time).
- **Stamina is the action budget.** Regen ~8 stamina/round in combat.
- Costs: Light attack 4 · Heavy attack 7 · Dodge roll 5 · Block (reaction) 3 · Parry (reaction) 4.
- Consequence: ~2 meaningful actions per round. All-out offense = 2 attacks and no defense. Fighting defensively = 1 attack + 1 defense. This tradeoff *is* the combat pacing.

## Core Stats

All six cores start at **3** (baseline, not spendable), lifetime cap **20**. Max level **20**.

**Progressive cap: no core may exceed `10 + ⌊Level/2⌋`.** At level 1 this is the creation cap (10); it reaches 20 exactly at level 20. This is what makes rushing impossible — no point total can max a core early. A specialist riding the cap sits at 15 (+7) at level 10 and reaches 20 (+9) only at endgame.

**Stat points are not a per-level drip — they're front-loaded and interspersed:**

| When | Reward |
|---|---|
| Level 1 (creation) | **18 stat points** — progressive cap applies (max core 10) |
| Even level-ups (2, 4, … 20) | **+5 stat points** (10 grants) |
| Odd level-ups (3, 5, … 19) | Something else: talent point, socket, ability slot (9 grants) |

Lifetime stat points: **68** — sized so a specialist can **max two cores (64) and push a third a little** (e.g. to 7). At creation, 18 points buys two cores at 10 (+4 each) with change, or all six to 6 (+2 across the board). The progressive cap keeps day-one characters strong but bounded (+4 max mod). Odd levels always feel like a level-up without inflating the stat curve — talents and sockets carry half of progression.

**Escalating allocation cost** (soft-cap without touching roll math):

| Raising a core to | Cost per point |
|---|---|
| 4–10 | 1 |
| 11–15 | 2 |
| 16–20 | 3 |

Maxing one core (3→20) costs 32 points. The specialist arc: ride the progressive cap in two cores (both at 15/+7 around level 10, both at 20/+9 at endgame), leaving ~4 points for a third core at 7 (+3). Everything else stays at base 3 (+1) — that's the specialist's price.

**Even-spread (generalist) reference** — all six cores raised equally: level 1 all cores 6 (+2), level 10 all cores 10 (+4), level 20 all cores ~12 (+5), with every resource pool growing. Generalist +5 everywhere vs specialist +9/+9/+3.

## Secondary Stats

**Modifier = ⌊(Core − 1) / 2⌋.** Base stats matter: a fresh level-1 character is **+1** across the board, not +0. Hard ceiling **+9** (core 20); a balanced build ends around +5 to +8 in its main stats.

The half-step is a deliberate, legible breakpoint: **every other point raises your modifier, the points in between raise your resource pools** (resources key off the raw core, below) — so every single point buys something.

| Secondary | Dice event | Core | Status |
|---|---|---|---|
| Force | + melee damage roll | Body | Active |
| Precision | + accuracy roll (d20 vs Defense) | Endurance | Active |
| Arcana | + spell damage roll | Mind | Active |
| Efficacy | (spell mastery — number only for now) | Insight | Parked |
| Finesse | + each crit explosion die | Spirit | Active |
| Initiative | (speed — number only for now) | Resilience | Parked |

Secondaries have **baseValue 0** and exactly one formula: `core − 3`. Talents and sockets add flat modifiers on top (`+1 Force`), or new functionality (e.g. "Precision also grants armor penetration"). Percent modifiers are retired.

## The Rolls

**Damage roll** = weapon dice + Force (or Arcana for spells) + socket/talent mods.

**Crits are exploding dice, not a % stat.** Max face → roll that die again and add; chains. **Finesse is added to each explosion die.** Crit rate is the dice's own probability (1-in-8 on a d8) — no crit chance stat exists.

**Accuracy roll** (Precision's function): when a swing physically connects, roll **d20 + Precision vs Defense**.
- Meet or beat → **full hit** (dice can explode).
- Fail → **glancing hit**: half damage, no explosions. Never a whiff — contact always costs something.

**Defense = 10 + Armor (gear) + deflection (talents).** Armor is a to-be-hit-through score, D&D style — the old `armor/(armor+100)` mitigation is deleted.

## Defense Layers (outermost first)

1. **Positioning / dodge** — no contact, no roll. Player skill.
2. **Parry** (active, 4 stam, strict timing) — negates the hit, opens riposte.
3. **Block** (active, 3 stam, lenient timing) — downgrades the hit to glancing.
4. **Defense score** (passive) — accuracy roll may downgrade to glancing anyway.
5. **HP** — the final buffer.

## Resources

All six resources use the **raw core** (full-step), so odd allocation points still pay out.

| Resource | Core | Formula | L1 | L20 maxed driver |
|---|---|---|---|---|
| **Health** | Body | `60 + 5×(Body−3) + 1×(Level−1)` | 60 | 164 |
| **Stamina** | Spirit | `30 + 2×(Spirit−3)` | 30 | 64 |
| **Mana** | Mind | `30 + 2×(Mind−3)` | 30 | 64 |
| Recovery | Resilience | `20 + 1×(Resilience−3)` | 20 | 37 |
| Regeneration | Endurance | `20 + 1×(Endurance−3)` | 20 | 37 |
| Recollection | Insight | `20 + 1×(Insight−3)` | 20 | 37 |

Health keeps a tiny level term (+1) so no build is one-shot at 20; Body remains the dominant driver. Recovery/Regeneration/Recollection have numbers but **no consuming system yet** — candidate roles: out-of-combat heal pool (Recovery), passive HP regen rate (Regeneration), spell-memory/cooldown pool (Recollection).

**Balanced melee build** (45% Body / 35% Endurance / 20% Spirit) at various levels:

| Lv | Health | Stamina | Mana | Recovery | Regeneration | Recollection |
|---|---|---|---|---|---|---|
| 1 | 95 | 36 | 30 | 20 | 26 | 20 |
| 5 | 109 | 40 | 30 | 20 | 28 | 20 |
| 10 | 129 | 44 | 30 | 20 | 31 | 20 |
| 15 | 144 | 46 | 30 | 20 | 32 | 20 |
| 20 | 159 | 50 | 30 | 20 | 34 | 20 |

(Mana/Recovery/Recollection stay at base because this build never invests in Mind/Resilience/Insight — untouched stats mean untouched pools.)

## Gear (no tiers)

Weapons are defined by their dice shape, and shapes are **sidegrades**:

| Weapon | Dice | Character |
|---|---|---|
| Katana | 1d8+2 | The baseline: swingy, explodes 1-in-8 |
| Heavy variant | 1d10+1 | Bigger ceiling, rarer explosions |
| Fast/precise variant | 2d4+2 | Consistent, explodes often but small |
| Special/named | e.g. 1d8+2 fire | Different damage type or rider, same budget |

All variants live on roughly the same average (~6.5); what changes is variance, explosion feel, and type. Armor likewise: **+0 to +4 Defense** total across the whole game (quality/enchant, not tiers). Sockets: a socketed gem = one named flat modifier on one secondary (`+1 Force`), stacking with everything — and at these scales +1 is a real decision.

## Progression Table (simulated)

### How allocation is modeled

The tables don't assume every point lands in one stat — they simulate a **build profile**. At level L the player has `18 + 5×(even level-ups so far)` points (18 at L1, 43 at L10, 68 at L20). The balanced melee profile splits that budget **45% Body / 35% Endurance / 20% Spirit**, and each core is raised until the next raise's escalating cost would exceed that core's share of the budget.

Worked example, level 10 (43 points): Body gets ~19 points → 4–10 cost 1 each (7), 11–15 cost 2 each (10), 2 left over → **Body 15**. Endurance ~15 points → **Endurance 14**. Spirit ~8 points → **Spirit 10**. The specialist profile instead has Body already maxed at 20 (+9 Force) by level 10 — with Spirit still at 3 and 30 stamina to show for it.

**All numbers in this doc are bare-character math**: base Katana 1d8+2, no talents, no sockets, no gear stat mods (armor's 0–4 Defense is the only gear effect modeled). Sockets and talents stack on top, so live endgame numbers will run a few points hotter than these tables — the sim is the floor, and that headroom is the talent system's budget.

### The curve

Balanced melee build, Katana 1d8+2 throughout, vs same-level opponent:

| Lv | Body | Force | Prec | Fin | Def | Hit% | Avg hit | HP | TTK all-out | Hits (defended) |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 10 | +4 | +4 | +2 | 10 | 75% | 9.9 | 95 | 4.8 rounds | ~12 |
| 5 | 12 | +5 | +5 | +3 | 11 | 75% | 10.9 | 109 | 5.0 | ~12 |
| 10 | 15 | +7 | +6 | +4 | 12 | 75% | 12.7 | 129 | 5.1 | ~13 |
| 15 | 17 | +8 | +7 | +5 | 13 | 75% | 13.7 | 144 | 5.3 | ~13 |
| 20 | 19 | +9 | +8 | +6 | 14 | 75% | 14.7 | 159 | 5.4 | ~13 |

Read: an even fight is **~5 rounds (~20s) if both go all-out**, and **8–12+ rounds** with active defense — flat across the entire level range. A level-20 character hits for ~13, not ~40; the biggest number anyone sees is a lucky multi-explosion in the 30s. No one-shots, no sponges, and a level-1 grunt still chips a level-20 player.

## Enemy Archetypes

Same formulas as the player, scaled:

| Archetype | HP × | Damage × | Purpose |
|---|---|---|---|
| Grunt | 0.5 | 0.7 | Dies in 2–3 rounds, teaches spacing |
| Standard | 1.0 | 1.0 | The even fight above |
| Elite | 2.0 | 1.3 | Forces defensive play |
| Boss | 5.0 | 1.6 | Full stamina-management fight |

## Tuning Levers (change these, not the structure)

- Fight length: HP base (60) and Body multiplier (5).
- Action density: stamina regen per round (8) and attack costs.
- Hit/glance ratio: armor/enchant values (0–4 range).
- Crit frequency: die size (bigger dice explode less often).
- Stat dominance: allocation cost bands, the core cap (20), and the modifier divisor (2).

## Migration Checklist

- [ ] Offensive schema: `cmb.*` baseValues 5 → **0**; formulas → `⌊({core.x} - 1) / 2⌋` per mapping table (note Precision→Endurance, Finesse→Spirit, Initiative→Resilience differ from current wiring). **Check `StatEngine`'s formula parser supports floor/integer division** — if not, add a `floor()` function or compute the modifier in code.
- [ ] Rename/repoint: `cmb.finesse` core (was endurance), `cmb.precision` core (was resilience); add `cmb.efficacy` (Insight).
- [ ] Delete `combat.crit_chance` / `combat.crit_damage` reads in `DamageSystem` — replace crit block with exploding-dice logic in `DiceRoll.Roll()` (+ Finesse rider).
- [ ] Replace `ApplyMitigation` (armor/(armor+100)) with the d20 accuracy roll → full/glancing.
- [ ] Damage Calculation Config: Physical attacker stats = `cmb.force` ×1.0 only (remove precision-as-damage).
- [ ] `BasicAttack1.asset`: `useWeaponDamage: 1`, `baseDamage: 0` (match Cleave/Slam/Thrust/Whirlwind). Delete stray `BasicAttack1 1.asset`.
- [ ] Resources schema: health/stamina/mana formulas per Resources table; park the other three.
- [ ] `RPGSystem`: replace flat `POINTS_PER_LEVEL` with the reward track — 18 points at creation (creation cap: core ≤ 10), +5 on even level-ups, talent/socket/ability grant on odd level-ups; `maxLevel` 30 → **20**; implement escalating cost (1/2/3) and core cap 20 in `RPGSystem.AllocatePoint`.
- [ ] Retire percent modifiers and X-per-Y gear stats; gear/talents apply named flat modifiers to secondaries.
