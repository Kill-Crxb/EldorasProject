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
