# Core Foundations — Quick Reference

The short version of `Combat_Numbers_Design.md`. If a number here disagrees with that doc, this one is wrong.

## Stat Progression

- Max level **20**. Six core stats, all start at **3**, lifetime cap **20**.
- **Progressive cap: no core may exceed `10 + ⌊Level/2⌋`** — 10 at creation, 20 only at level 20. Maxing early is impossible by design.
- **Level 1 (creation): 18 stat points.**
- **Even level-ups (2–20): +5 stat points** each (10 grants).
- **Odd level-ups (3–19): no stat points** — a talent point, socket, or ability slot instead.
- **Lifetime total: 68 points.** Enough to max two cores (32 each) and push a third to 7.
- Escalating cost per raise: cores 4–10 cost **1**, 11–15 cost **2**, 16–20 cost **3**.
- Build spectrum at level 20: generalist **+5 everywhere** (all cores ~12) vs specialist **+9/+9/+3** (rest at +1).

## The Stats & Their Modifiers

**Modifier = ⌊(Core − 1) / 2⌋.** Core 3 = +1, core 10 = +4, core 20 = +9. The modifier is a flat number added to one specific dice event — no percentages anywhere.

| Core | Secondary | Modifier is added to |
|---|---|---|
| Body | **Force** | Melee damage rolls |
| Endurance | **Precision** | Accuracy roll (d20 vs Defense) |
| Mind | **Arcana** | Spell damage rolls |
| Insight | **Efficacy** | (Spell mastery — parked, number only) |
| Spirit | **Finesse** | Each crit explosion die |
| Resilience | **Initiative** | (Speed — parked, number only) |

How the events work as mechanics:

- **Damage roll**: weapon dice + Force (or Arcana). The Katana is 1d8+2 forever — weapons are sidegrades, growth comes from mods.
- **Crits**: a die rolling its max face **explodes** (roll again, add, can chain). Finesse is added to each explosion die. There is no crit chance stat.
- **Accuracy**: when a swing physically connects, roll **d20 + Precision vs Defense (10 + armor)**. Beat it = full hit; fail = **glancing** (half damage, no explosions). Contact never whiffs entirely.
- Every other point in a core raises its modifier; the points in between raise its resource pool — no dead points.
- Gear sockets and talents add small flat mods (+1 Force) or new rules (e.g. Precision grants armor pen). They stack on top of everything above.

## Resources

Resources key off the **raw core** (not the modifier):

| Resource | Core | Formula | Base (core 3) | Max (core 20) |
|---|---|---|---|---|
| Health | Body | 60 + 5×(Body−3) + (Level−1) | 60 +Lv | 164 at L20 |
| Stamina | Spirit | 30 + 2×(Spirit−3) | 30 | 64 |
| Mana | Mind | 30 + 2×(Mind−3) | 30 | 64 |
| Recovery | Resilience | 20 + (Resilience−3) | 20 | 37 |
| Regeneration | Endurance | 20 + (Endurance−3) | 20 | 37 |
| Recollection | Insight | 20 + (Insight−3) | 20 | 37 |

Balanced melee build across the game:

| Lv | Health | Stamina | Mana | Avg hit | Fight length (all-out) |
|---|---|---|---|---|---|
| 1 | 95 | 36 | 30 | ~10 | ~5 rounds |
| 10 | 129 | 44 | 30 | ~13 | ~5 rounds |
| 20 | 159 | 50 | 30 | ~15 | ~5.5 rounds |

Recovery / Regeneration / Recollection have values but no consuming system yet.

## Combat Frame

- **Round = 4 seconds.** Stamina is the action budget (~8 regen/round). Light attack 4, heavy 7, dodge 5, block 3, parry 4 — roughly two meaningful actions per round.
- Defense layers, outermost first: positioning/dodge → parry (negate) → block (downgrade to glance) → Defense score (may glance anyway) → HP.
- Enemy archetypes reuse player formulas: grunt ×0.5 HP, standard ×1, elite ×2, boss ×5.

## Not Yet Founded (open slots)

- **XP curve** — how fast levels arrive in play-hours. Nothing defined.
- **Talent system** — carries half of all progression (every odd level); needs its budget and first talents.
- **Caster validation** — Arcana path has formulas but no simulated spell loop (mana costs, cast times per round).
- **Efficacy & Initiative** — parked numbers awaiting functions.
- **Regen rules** — stamina in-round regen is set; health/mana regen in and out of combat is not.
- **Enemy stat blocks** — archetype multipliers exist; actual NPC core spreads don't.
