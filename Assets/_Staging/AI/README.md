# Mirror NPC + first GOAP goal (29 Sep 2026)

A Base_PC variant driven by GOAP instead of input, with one goal: walk up to the target and
swing BasicAttack1 every 2 s. Roadmap A4, A5 (movement half), A7 (first cut).

## Setup (2 minutes)

1. Let Unity compile.
2. **Tools → AI → Build Mirror NPC.** Creates `Database/Characters/Mirror/Mirror_NPC.prefab`
   (variant of Base_PC). Re-running it rebuilds in place; scene instances keep their link.
3. Drag `Mirror_NPC` into Zoo, a few metres in front of the player spawn. Play.

It should load the `female_base_v1` model, take the Target Dummy faction (hostile to the
player), walk to you and hit you.

## What it adds

| File | What |
|---|---|
| `CrabSystem/AIGOAP/AIControlSource.cs` | The AI's movement control source. Goals call `Steer` / `Face` / `Stop`; takes over MovementSystem at LateInitialize. Holds per-entity goal memory (`LastAttackTime`) — goals are shared ScriptableObjects. |
| `CrabSystem/AIGOAP/Goals/FightTargetGoal.cs` | The goal. Range, stop range and swing interval on the asset. |
| `Database/AI/Goals/Goal_FightTarget.asset` | Attack = BasicAttack1, range 2.2, stop 1.8 (easing in over 1.5 m), steps back inside 1.1, every 2 s. |
| `Database/Characters/Mirror/Mirror-Archetype.asset` | Copy of TargetDummy-Archetype with model `female_base_v1`. |
| `_Staging/AI/Editor/MirrorNpcBuilder.cs` | The menu item. |

Edited: `GOAPContext` (+`aiControl`), `ControllerBrain` (`ICameraProvider` cached for players
only), `PerceptionModule` (scan ignores triggers, buffer 20 → 64, keeps a target through a
missed scan until memory runs out), `ModelModule` (re-hangs equipment on a new model; clears a
socket before filling it). Backups in `_Staging/_Backup/AI_v1/`.

## What the builder changes on the variant

- Brain `entityType` → NPC (InputSystem and CameraModule are declined as for any NPC).
- Every Camera, AudioListener and Volume under it is switched off — Base_PC carries the
  player's camera rig and post-processing.
- `AI_System` child under `Component_Brain`: AIControlSource, PerceptionModule (360°, no
  line-of-sight check), GOAPModule (goal pool = Fight Target, highest weight).
- `PersistentNPCConfigurator` on the root, entityId `mirror_01`.
- EquipmentSystem natural weapon: `steel_katana` in `mainwep` (the player's katana comes from
  their save; an NPC has none).

Do **not** add `AISystem` as well — the brain already updates every module it finds, and
AISystem updates GOAP and Perception a second time.

## Test

- It walks to you, eases to a stop at ~1.8 m, faces you, swings every 2 s.
- The hit lands through the real path: hit roll, soak, Juice, `HitReactionSystem` (does the
  player flinch? roadmap P3). `HitTrace` in the scene logs each roll.
- Hold RMB — block (CF3 not built; expect little).
- Apply a stun to it (StatusHotkeys apply to the player only — for now test F10 by getting
  hit by it while *you* are mid-swing, or add a hotkey target).

## Likely snags

- **Doesn't move:** check the log for `[MovementSystem] Activated: AIControlSource`.
- **No target:** Perception needs the player's CharacterController in `detectionLayers` (default
  everything) and a hostile stance between the two factions.
- **Faces the wrong way:** HeadLookSystem and anything else reading the camera may still assume
  a player. The parkour handler now falls back to the goal's `Face` direction.
- Base_PC's player-only modules (Hotbar, Inventory, Interaction, Spellcraft) come along. They
  should stay quiet on an NPC; anything that logs on spawn goes in Known Issues.
- `InputSystem` isn't used on an NPC, so it doesn't matter that it has no StubAIControlSource.

## Not yet

- Abilities go straight to `AbilitySystem.UseAbility`, not through an `IAbilityControlSource`.
  Fine for one move; revisit when server-side authority needs one input seam.
- No block, back-off or spacing — next goals once there is more than one animated move.
