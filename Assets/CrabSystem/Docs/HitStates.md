# Hit states — roadmap CF2 (30 Sep 2026)

A melee hit now puts the move's hit state on the target as a status. The status raises
`CannotAct`, and AbilitySystem's hard-control cancel (F10) interrupts whatever the target was doing.

## What's here

| File | What |
|---|---|
| `AbilityDefinition.HitState.cs` | `ApplyHitState(target, caster, applied)` — the move's `hit.onHit` → status, length = `hit.onHitFrames` × clamp(applied ÷ average damage, 0.5, 1.5). |
| `Resources/Statuses/HitStates/` | `Status_Flinched` (CannotAct), `Status_Staggered` and `Status_Launched` and `Status_GuardBroken` (CannotAct + CannotBlock), `Status_Prone` (CannotDodge, CannotSprint, CannotJump). Frames on the asset are only a fallback; the move's frames win. |

Edited in place (backups in `_Backup/HitStates_v1/`): `StatusSystem`, `DamageEffect`,
`WeaponHitbox`, `AbilitySystem`.

## Rules

- No hit state when nothing landed — a block or parry applies 0.
- No hit state while the target's current move has armour frames (`IsArmored`). No move has
  armour authored yet.
- Refresh stacking: a second hit restarts the clock.
- Melee only. Projectiles and spells don't apply hit states yet.
- `HitReactionSystem` still picks the clip from damage share. The status is the mechanics, the clip
  is the look.

## Test (Zoo, mirror NPC, StatusTrace + HitTrace in the scene)

`BasicAttack1` is already authored: Flinch, 18 frames.

1. Stand still and let the mirror hit you. StatusTrace logs `+flinched` for ~0.15–0.45 s and you
   can't move or attack for that long.
2. Swing into its swing. Whoever lands first should cancel the other's attack (the F10 test).
3. Hit the mirror during its wind-up. Its swing should stop and no hit should arrive.
4. Hold block. Blocked hits log no `flinched`.
5. Glancing hits give short flinches, full hits long ones.

## Undo

Restore the four `.bak` files from `_Backup/HitStates_v1/` and delete this folder.
