# Backups of production files edited from staging

Every file here is a copy taken immediately before the edit, named `<original>.bak` so Unity
does not compile it. Restore by copying back over the original and dropping the `.bak`.

## Character feel work

| Backup | Why the original was edited |
|---|---|
| `CrabSystem/MovementLocomotion/CharacterMotor.cs.bak` | Added read-only `CollisionLayers` and `MaxSlopeAngle` so foot IK probes the same mask the capsule collides with. |

## Animation layer arbiter

| Backup | Why the original was edited |
|---|---|
| `CrabSystem/Abilities/AbilitySystem.cs.bak` | Claims the layer it needs and releases the other, instead of writing 1 to one and 0 to the other. `CompleteAbility` releases rather than zeroing. |
| `CrabSystem/DamageCombat/HitReactionSystem.cs.bak` | Claims/releases at reaction priority. Also stopped gating on the pre-resolved `reactionLayer`, which is -1 on a spawned entity. |

## Character base audit

| Backup | Why the original was edited |
|---|---|
| `CrabSystem/A. CoreArchitecture/ControllerBrain.cs.bak` | **B4** — `ShouldRun` guard so `IsEnabled` and the inspector checkbox actually stop a module. **B2** — declined modules are recorded so the fallback loop no longer initializes `CameraModule`/`InputSystem` on NPCs. |
| `CrabSystem/ModelVisual/CharacterConfigurationHandler.cs.bak` | **B1** — `OnDestroy` unsubscribes from `GameEvents.OnCharacterConfigDataReady`. |
| `DebugScripts/*.cs.bak` | **C1** — 11 debug scripts wrapped in `#if UNITY_EDITOR`. |

## Not backed up

`_Staging/**` — revert by deleting the folder.
`Database/Characters/MC/Porphi.prefab.bak` — the jiggle setup has its own Remove menu command,
which is the cleaner revert.

## Combat correctness — 2026-09-27 (`Combat_v1/`)

| Backup | Why the original was edited |
|---|---|
| `HitReactionSystem.cs.bak` | **F4** — reacts on `OnDamageApplied` with the applied damage, skips `Tick` sources and zero-damage hits. |
| `DamageSystem.cs.bak` | **R3** — `Killer`, `OnKilledBy` (victim side), `OnKill` (attacker side). Attacker lookup hoisted out of the faction block. |
| `AbilityDefinition.cs.bak` | **F3** — `ApplyKnockback(target, attacker)` beside `ApplyStatuses`. |
| `WeaponHitbox.cs.bak` | **F3** — calls `ApplyKnockback` after statuses, attacker frame = wielder. |
| `ProjectilePayload.cs.bak` | **F3** — calls `ApplyKnockback` after statuses, attacker frame = the projectile. |
| `KnockbackEffect.cs.bak` | **F3** — default `direction` `back` → `forward`: relative to the attacker, `back` pulled the target in. No asset authored one. |
| `Base_PC_pretestcleave.prefab.bak` | Four test Cleave variants (`Database/Resources/AbilityDatabase/TestAbilities/`) appended to `RuntimeAbilityManager.starterAbilities`. Remove those four lines, or delete the folder, when testing is done. |
| `CharacterConfigurationHandler.cs.bak` | Archetype overrides on a derived stat (`atr.arm`) go in as an `archetype` contribution instead of a refused `SetValue`. |

## Resolution pipeline — 2026-09-27 (`Resolution_v1/`)

Implements `claude/Resolution_Build.md` stages 1–4.

| Backup | Why the original was edited |
|---|---|
| `CoreDerivation.asset.bak` | +1 modifier rewards at odd thresholds 3–19 per core (`StatDatabase/Rewards/Reward_Mod_*`). `Reward_Resources_PerPoint` no longer referenced — pools are at their authored base. |
| `DiceRoll.cs.bak`, `DiceProfile.cs.bak` | `RollExploding` (cap 5), `DiceRoll.RollModifier` (a modifier rolled as a die). |
| `CombatAttackData.cs.bak`, `CombatDamagePacket.cs.bak` | `explosionDamage`, `explosions`; packet also `accuracy`. Optional constructor params, so every call site compiles unchanged. |
| `DamageEffect.cs.bak`, `ProjectilePayload.cs.bak` | Weapon and bonus dice explode; the exploded part is carried separately. |
| `DamageSystem.cs.bak` | Stat die replaces flat attacker stats; Cunning die per explosion; hit roll + soak replace `ApplyMitigation`; `OnHitResolved`. New `HitResolution.cs`. |
| `DefensiveCore.asset.bak` | `atr.arm_dice`, `atr.arm_def` added; `atr.arm` range −20–20. |
| `Status_Stoneskin.asset`, `Status_Sundered.asset` | Stoneskin +1 `atr.arm_dice`; Sundered −1 `atr.arm` per stack. |
| `LightDummy/Mid DUmmy/HeavyDummy.asset.bak` | Armour re-authored to dice + flat + defense. |
| `AbilitySystem.cs.bak` | Blackboard read moved from `Initialize` to `LateInitialize` — it initializes before `BlackboardSystem`, so it was always null: no forbidden fact ever blocked, `IsBlocking` / `IsInvincible` never written. |
| `SpellcraftSystem.cs.bak` | Drawing while `IsSilenced`: the sign plays and costs its draw time, the element fizzles (grey burst, never enters the sequence); `OnElementFizzled`; draw layer released after fizzles. `PlayDrawBurst` takes a colour. |

## Overnight — 2026-09-27 (`Night_v1/`)

| Backup | Why the original was edited |
|---|---|
| `TargetDummy-Archetype.asset.bak` (+ Light/Mid/Heavy, unchanged) | Base TargetDummy cores 20 → 10 so hits stop mostly glancing. The three armoured variants were already at 10. |
| `AbilitySystem.cs.bak` | **B11 (4)** — `CompleteAbility` calls `DeactivateDefensiveAbility` when the defensive ability ends, so `IsBlocking` can't stick true. |
| `C11/*.prefab.bak` | **C11** — dead Cartoon FX components (script guids `474bcb49…`, `9205bc1b…`) removed from DrawElement, both CFXR2 Fireball, Jump, Sphere, NatureSpirit_Model 1, Floating Cube Light. |
| `SaveManager.cs.bak`, `IdentitySystem.cs.bak` | **B9** — empty `catch { }` now logs a warning. |

New, staging only (delete the folder to remove): `_Staging/DebugTools/HitTrace.cs`,
`_Staging/Sparring/`, `_Staging/CombatTools/Editor/MoveReport.cs`.

## Move block (CF1) — 2026-09-27

`Move_v1/` — copies taken before move data was appended.

| Backup | Why the original was edited |
|---|---|
| `BasicAttack1/2`, `Cleave`, `Whirlwind`, `Slam`, `Thrust` `.asset.bak` | Move data appended (frames, hit properties, routes). Originals live in `Database/Resources/AbilityDatabase/KatanaAbilities/`. |
| `MoveReport.cs.bak` | Move-data checks and clip drift added. |

New, no backup: `KatanaAbilities/BasicAttack3.asset` (copy of BasicAttack2 + BasicAttack1's damage effect), `CrabSystem/Abilities/AbilityDefinition.Move.cs`.

## Guard states (CF3 prep) — 2026-09-28

`Guard_v1/` — copies taken before `Tools → Combat → Build Guard States` ran.

| Backup | Why the original was edited |
|---|---|
| `HumanoidAnimator.controller.bak` | `Guard` sub-state machine added to Upper Body Combat, `Block` trigger added, Upper Body Block layer weight → 0. Original: `Database/3d/Humanoid/Animations/`. |
| `KatanaBlock.asset.bak` | `animationTrigger` `Ability` → `Block`. Original: `Database/Resources/AbilityDatabase/KatanaAbilities/`. |
