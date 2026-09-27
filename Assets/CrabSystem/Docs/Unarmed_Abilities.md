# Unarmed Abilities

Six `AbilityDefinition` assets in `Database/Resources/AbilityDatabase/UnarmedAbilities/`, one per
Any-State trigger on the Humanoid animator.

## What's in the animator

Bool `IsUnarmed` and six triggers, all wired Any-State → state on **Full Body Actions**
(layer 1) — not Upper Body Combat, despite the name of the layer they were described as being on.
Upper Body Combat holds the spell states and duplicate BasicAttack1/2/3.

| State | Trigger | Clip | Length |
|---|---|---|---|
| Punch1 | `Punch1` | `Punch.anim` | 0.40s |
| Punch2 | `Punch2` | `Punch.anim` — same clip | 0.40s |
| Kick1 | `Kick1` | `KickSLam.anim` | 1.07s |
| Kick2 | `Kick2` | `CleaveKick.anim` | 0.75s |
| Uppercut | `SPunch` | FBX sub-clip | unverified |
| Stomp | `SKick` | `Stomp.anim` | 1.97s |

`ForwardKick.anim` (0.70s) exists but no state uses it.

## The assets

| Asset | abilityId | Trigger | CD | Stamina | Damage |
|---|---|---|---|---|---|
| `Punch1` | `unarmed_punch1` | Punch1 | 0 | 5 | 1d4+2 ×1.00 |
| `Punch2` | `unarmed_punch2` | Punch2 | 0 | 5 | 1d4+2 ×1.15 |
| `Kick1` | `unarmed_kick1` | Kick1 | 0.5 | 8 | 1d4+2 ×1.40 |
| `Kick2` | `unarmed_kick2` | Kick2 | 0.5 | 8 | 1d4+2 ×1.25 |
| `Uppercut` | `unarmed_uppercut` | SPunch | 6 | 15 | 1d4+2 ×1.60 |
| `Stomp` | `unarmed_stomp` | SKick | 8 | 20 | 1d4+2 ×2.00 |

All six: `animationLayer: FullBodyActions`, `useWeaponDamage: 1`, `weaponSlotId: mainwep`, range
2.5, same stamina resource the Katana abilities use. `useWeaponDamage` is what routes them through
`CombatStanceModule.RollUnarmedDamage()` — the slot id is ignored while unarmed.

`Punch1.nextInChain → Punch2` is set as groundwork. It's inert until combo-window events exist.

Numbers are a starting curve, not a balance pass.

## The blocker: no animation events

**Every H2H clip has `m_Events: []`.** Nothing else matters until this is fixed, because:

- No `HitboxStart` / `HitboxEnd` → `AbilitySystem` never enables the fist hitbox → **zero damage**.
- No `AnimUnlocked` → with `waitForAnimUnlock: 1` the ability only ends via the `maxDuration`
  safety timeout, logging *"timed out — AnimUnlocked never fired!"* every swing. While it runs,
  `CanUseAbility` returns false for everything, so the player is locked out for that whole window.

`maxDuration` on each asset is set just above its clip length as a stopgap, so the lockout is
roughly the animation length rather than an arbitrary 2s. It's a crutch, not a fix.

### Adding the events

Open each clip in the Animation window and add events calling these parameterless methods on
`AnimationEventForwarder` (it lives next to the Animator on the model):

| Method | When |
|---|---|
| `OnHitboxStart` | fist/foot starts moving through the target |
| `OnHitboxEnd` | contact frame passes |
| `OnAnimUnlocked` | recovery ends — the ability completes here |
| `OnComboWindowStart` / `OnComboWindowEnd` | optional, enables `nextInChain` |

Rough placement for `Punch.anim` (0.40s): HitboxStart ~0.15, HitboxEnd ~0.22, AnimUnlocked ~0.33.
Once `OnAnimUnlocked` fires, `maxDuration` stops mattering and can go back to a flat 2.

## Registering them

`Resources.LoadAll` scans subfolders, so `AbilityManager` picks all six up automatically — that
covers saved hotbar slots and debug tools.

But `AbilitySystem.CanUseAbility` and `HotbarSystem.ResolveSlotAbility` read the **per-entity**
`AbilitySystem.abilities` list, not the global registry. So they must also be dragged onto
`Base_PC > Component_Brain > Ability_System > Abilities`, or nothing can cast them.

(This is the "should `ResolveSlotAbility` fall back to `AbilityManager`?" question already noted
as open in `Item_Database.md`. Same root cause.)

## Then

Put them on `Page_Unarmed`'s bars once the page assets exist. The page is what makes them
available only while unarmed — the abilities themselves carry no stance requirement, deliberately,
since availability is the hotbar's job.

## Loose ends

- **Punch2 reuses Punch.anim.** Fine as a placeholder; the two punches are currently identical
  animations with different damage.
- **`ForwardKick.anim` is unused.** Either wire a seventh state or drop it.
- **Uppercut's clip length is unverified** — it resolves to an FBX sub-clip, not a standalone
  `.anim`. If `maxDuration: 1.2` cuts it short or lags, adjust.
- **Icons are unset** on all six, so hotbar slots will render blank.
