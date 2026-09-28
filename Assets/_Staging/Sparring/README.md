# Sparring partner — staging

A dummy that hits back on a timer, for testing the player's side of combat before real AI (roadmap A1).
Delete this folder to remove it.

## Set up (editor, ~2 minutes)

1. Right-click `Database/Characters/TargetDummy/TargetDummy.prefab` → **Create → Prefab Variant**.
   Name it `Sparring Dummy`. On its `PersistentNPCConfigurator` set **entityId** `sparring_dummy`
   and **displayNameOverride** `Sparring Dummy`.
2. Under `Component_Brain`, add an empty child `Sparring_System` and put **SparringPartner** on it.
   No references to fill — it finds its brain, the player and its own HitFlash (if the dummy has the
   Juice set) by itself.
3. Drop it in the Zoo a few metres from where you spawn.

Defaults: a 1d6 physical hit every ~3 s (2.5 s rest + 0.6 s wind-up) when you're within 2.5 m,
starting its wind-up when you're within 6 m. The dummy turns to face you and flashes at wind-up start.

## What it tests

- **Hit roll and soak on the player** — with `HitTrace` in the scene you get the full breakdown.
- **Juice, target side** — `View_Hit` shake and chromatic, hit flash, hit-stop on you.
- **Player hit reactions** — only once `HitReactionSystem` is on `Base_PC` (roadmap P3).
- **Statuses on an NPC** — Cleave (Cripple) to 3 stacks raises `IsStunned`, and the dummy stops swinging.
- **Block / parry** — ⚠ not yet. Block reaches the damage path only after CF3; see Known Issues B11.

## Knobs

`interval`, `windup`, `aggroRange`, `strikeRange`, `damageDice`, `damageType`, `windupTrigger`
(an animator trigger to fire at wind-up, if the dummy's controller has one), `windupFlash`.
