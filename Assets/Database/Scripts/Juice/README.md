# Juice — staged

Feel presentation layer. Design and setup steps: `claude/Juice.md` in project knowledge.

| File | Role |
|---|---|
| `JuiceModule.cs` | Per-entity. Hit players, parry, death, landing; owns hit-stop (`animator.speed`). |
| `HitFlash.cs` | Per-entity. Drives FlatToon `_HitFlash` through a property block, cleared at 0. |
| `StepJuice.cs` | Adapter: `FootstepEmitter.onStep` → an `MMF_Player`. |

## Edited outside this folder

`CombatAttackData.cs`, `CombatDamagePacket.cs`, `DamageSystem.cs`, `DamageEffect.cs`,
`DamageOverTimeEffect.cs`, `WeaponHitbox.cs`, `ProjectilePayload.cs` — the `DamageSource` tag,
`OnDamageApplied`, and the contact point on `DamageEffect.Apply`.
`Database/Characters/PC-Messy-Porphi/FlatToon.shader` — `_HitFlash`, `_HitFlashColor`.

## Revert

Delete this folder, then copy each file in `_Staging/_Backup/Juice_v1/` back over its original
(drop the `.bak`). The CrabSystem edits are additive with defaults, so this folder can also be
deleted on its own and everything still compiles.
