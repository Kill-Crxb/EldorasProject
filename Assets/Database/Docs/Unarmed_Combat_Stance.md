# Unarmed Combat Stance

**R toggles the player between wielding the melee weapon and fighting bare-handed.**

## The idea

Nothing is unequipped. The item stays in `mainwep`, so stats, tooltips, the paperdoll and the
save file never see a stance change. A single new module owns one bit — armed or unarmed — and
five systems read it.

```
R  →  InputSystem.ToggleStancePressed
        ↓
      CombatStanceModule.ToggleStance()
        ├─ AbilityLoadoutModule   basic-attack chain + defense swap
        ├─ ModelModule            weapon reparents hand ⇄ sheath socket
        ├─ AnimationSystem        "IsUnarmed" bool
        ├─ Blackboard             "IsUnarmed" fact
        └─ (read on demand)
             DamageEffect         rolls 1d4+2 instead of the weapon dice
             WeaponHitbox         blade refuses to fire; fists do
```

## Why the stance has to gate hitboxes

`AbilitySystem.EnableAbilityHitboxes()` calls `GetComponentsInChildren<WeaponHitbox>(true)` and
enables **every one of them**. Add fist hitboxes to the rig and a punch would also swing the
sheathed katana. `WeaponHitbox` now carries a `stanceFilter` and refuses `Enable()` when the
stance doesn't match. Entities without a `CombatStanceModule` — every NPC, every natural
weapon — pass unconditionally, so nothing existing changes behaviour.

## Why the stance has to gate damage

`DamageEffect.GetWeaponDamage()` read the equipment dictionary directly and returned `0f` on an
empty slot. Since a sheathed weapon is still in the slot, the equipment dictionary is now the
wrong source of truth. Stance is checked first; an empty slot or a weapon with no `WeaponData`
also falls through to the unarmed profile rather than dealing nothing.

## Changes

| File | Change |
|---|---|
| `DamageCombat/CombatStanceModule.cs` | **New.** Owns the stance, the R toggle and all five hookups. |
| `DamageCombat/DamageEffect.cs` | Stance checked before equipment; unarmed fallback on an empty slot. |
| `Input/InputSystem.cs` | Reads the new `ToggleStance` action. |
| `Input/IInputProvider.cs` | `ToggleStancePressed`. |
| `ItemInventoryEquipment/.../WeaponHitbox.cs` | `HitboxStanceFilter` — Any / ArmedOnly / UnarmedOnly. |
| `ModelVisual/ModelSocketProvider.cs` | `namedSockets` list — sockets that aren't equipment slots. |
| `ModelVisual/ModelModule.cs` | `GetNamedSocket()`, `MoveSocketContents()`. |
| `PlayerInputControls.inputactions` | `ToggleStance` action bound to `<Keyboard>/r`. |

## Setup

1. **Author the unarmed profile.** Right-click → `Combat/Weapon Data`. Name it `Unarmed_Fists`,
   set `damageDice` to **1d4**, `flatBonus` to **2**, `weaponType` to `Unarmed`,
   `category` to `Natural`. Tooltip label comes out as `1d4 + 2`.
2. **Add `CombatStanceModule`** next to the other brain modules on the player. Assign
   `Unarmed_Fists`. No `ControllerBrain` edit needed — modules are auto-discovered by
   `GetComponentsInChildren<IBrainModule>`.
3. **Add a sheath socket.** On the model prefab's `ModelSocketProvider`, add a **Named Sockets**
   entry `mainwep_sheathed` pointing at a back or hip bone. Without it the weapon just hides on
   sheathe — playable, but it looks like a placeholder.
4. **Fist hitboxes.** Add a `WeaponHitbox` to each knuckle bone with `stanceFilter =
   UnarmedOnly`. Set the katana prefab's blade hitbox to `ArmedOnly`.
5. **Unarmed moveset.** `AbilityLoadoutModule.defaultUnarmedAttack` / `defaultUnarmedDefense`
   already existed and were already wired to `RevertToDefaultAbilities()` — assign a punch chain
   and a guard ability there.
6. **Animator.** Add an `IsUnarmed` bool and drive the fist idle/locomotion from it. Skipped
   safely if the parameter doesn't exist.

## Decisions worth knowing about

**Equipping draws, unequipping sheathes.** `CombatStanceModule` subscribes to
`EquipmentSystem.OnEquipmentChanged` and forces the stance to match the slot. R refuses to draw
an empty hand.

**Transitions are optional.** Assign `sheatheAbility` / `drawAbility` (put them on the
`FullBodyActions` layer so `AbilitySystem` blocks attacks until `AnimUnlocked`) and R plays them.
Leave them empty and the swap is instant. `transitionDelay` holds the visual swap for a few
frames so the sword doesn't teleport on frame one; `ApplyStance()` is public so an animation
event can commit it at the exact frame instead.

## One-time cleanup this caused

`PlayerInputControls.cs` is generated code and Unity writes it beside its `.inputactions` asset —
`Assets/CrabSystem/`. A copy had been moved into `Input/` by hand, which went unnoticed only
because the asset was never re-imported. Adding `ToggleStance` forced a regeneration, Unity wrote
the wrapper at its default path, and both files declared the same class.

`Input/PlayerInputControls.cs` has been emptied to a comment so the project compiles.
**Delete it and its `.meta`.** The live wrapper is `CrabSystem/PlayerInputControls.cs`. To pin the
output location instead, set `wrapperCodePath` in `PlayerInputControls.inputactions.meta`.

## Open

- **`R` is already bound to `Consume`.** Nothing in `InputSystem` reads `Consume`, so it looks
  dead — but both actions now fire on R. Confirm and delete the binding, or move one of them.
- **Nothing wires a weapon to a moveset.** `AbilityLoadoutModule.SetWeaponAbilities()` exists and
  has no callers; `ItemBaseType` has no ability fields. The stance module caches whatever was in
  the basic-attack slot before going unarmed and restores it on draw, with inspector fields
  (`defaultArmedAttack` / `defaultArmedDefense`) as the first-draw fallback. That is the seam
  where a real `ItemBaseType` → moveset lookup belongs.
- **Unarmed doesn't scale.** 1d4+2 is flat forever. Whether fists should read a Martial Arts stat
  or a level-scaled die is a progression question, not a combat-architecture one.
- **`WeaponData.attackSpeed` and `reach` are ignored** by the unarmed path, same as by the armed
  path — nothing reads them yet.
