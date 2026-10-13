# Getting started

## Install

Package Manager > **+** > *Add package from git URL*:

```
https://github.com/Kill-Crxb/CrabSystem.git#v0.2.1
```

Or add it to `Packages/manifest.json` under `dependencies`. Pin a tag; `#main` follows the latest. With SSH keys, `ssh://git@github.com/Kill-Crxb/CrabSystem.git#v0.2.1` works too. To update, change the tag.

Unity 6000.6 or later. URP, the Input System and uGUI install with it. To run the package's tests, add `"testables": ["com.crxb.crabsystem"]` to `manifest.json`, then use Window > General > Test Runner > EditMode.

A git package is read-only and lives in `Library/PackageCache/com.crxb.crabsystem@<hash>/`. These docs are in its `Documentation~` folder.

## Set up a project

1. **Tools > CrabSystem > Dashboard > Project Setup.** It checks URP, the Input System, TextMesh Pro essentials, the layers in `CrabLayers`, the `CrabResources` folders, a stat schema entry for every `StatIds` / `DialIds` id, and a damage config listing `physical`. Each gap has a fix button. Fixes only add.
2. **Project Settings > CrabSystem.** Where this project keeps its data, items, talents, staging and downloaded packs. Editor tools read these instead of fixed paths.
3. **Starter prefabs.** Make a variant of each prefab in `Runtime/Prefabs` (`Manager_Brain`, `Bootstrapper`, `UIManager`, `Debugtools`, `SceneLoader`, `MenuUi`, `EventSystem`), fill its slots, and put the variants in your scenes. Assign the damage config on the `Manager_Brain` variant's `DamageManager`.
4. **Validate.** `Tools > CrabSystem > Validate Ids` and `Validate` report missing ids and standard breaks.

## What a game must provide

- A stat schema defining every `StatIds` / `DialIds` id (Project Setup creates them at 0).
- Hit-state statuses in `Resources/Statuses/HitStates/` (`Status_Flinched`, `Status_Staggered`, `Status_Prone` and the rest), if it uses `HitReactionSystem`.
- A `DamageCalculationConfig` with at least the `physical` type.
- Character prefabs: a base with the core modules, and variants per character.
- Socket names on each model prefab (`SocketEventRelay`); there are no defaults.
- Its own input actions for anything beyond `PlayerInputControls`.

## Framework opinions

Deliberate for now; each is a candidate to become data.
- **Stat model.** `StatIds` / `DialIds` assume a d20 hit roll, soak, saves and creation stats.
- **Default damage type** is `physical`. Every other type is an asset.
- **Brain states.** `StateMachineModule`'s `BrainState` enum still carries template states (to be cut down to posture).
- **Player input.** `PlayerInputControls` is the framework's binding set. Wrapper generation is off because a git package is read-only.
- **Animator parameter names** default to `HitLight`, `Stagger`, `Death`, `IsDead`, `DrawSpeed` and similar, overridable per component.

## Fit by genre (first-person shooter example)

| Carries over as is | Shaped by third-person melee | Art-style specific |
|---|---|---|
| Brain, modules, managers, blackboard, entity clock, `DiceRoll`, ids and validators | `CameraModule` (third-person orbit) | `Runtime/Rendering/Shaders`: `FlatToonOutlinePost`, `FlatToonSky`, `ScreenSpeedLines` |
| Stats, dials, resources, progression, talents | `TargetingModule` (soft lock in front of the camera) | `TerrainColorGlobals`, `LanternFlicker` |
| `DamageSystem`, `DamageManager`, damage types, soak | `GuardModule`, `PostureModule` (block, parry, posture) | |
| `StatusSystem`, capability facts, crowd control | `MovesetModule`, `CombatStanceModule` (melee strings, draw/sheathe) | |
| Abilities and cooldowns, hotbar | `ParkourLocomotionHandler`, `ParkourAssistant` | |
| Projectiles (pool, emitter, payload, area ticks) | `HitReactionSystem`, `ActionsLayerDriver` (full-body reactions and layers) | |
| Inventory, equipment, items, loot, vendors, interaction | Strike shapes on clip events (melee hit checks) | |
| AI (GOAP, goals, perception), factions, NPC spawning | | |
| Saving, scenes, menus, UI windows, dialogue, VFX | | |

The shaders are only used if a game assigns their materials; they cost nothing otherwise.

**Framework jobs a shooter needs** (none exist yet):
- A first-person camera module beside `CameraModule`, answering `ICameraProvider` and `CursorWanted`.
- Hitscan weapons: a ray query that builds the same damage packet a strike or projectile does and calls `TakeDamage`, with spread from `DiceRoll`.
- Weapon fire timing: an ability whose shots are paced by clip events or a rate, through `AbilitySystem`.
- Moving the toon rendering into an optional add-on package.

**Still to come for every game:** a starter character prefab and a sample scene, so a blank project plays straight after Project Setup.

## Changing CrabSystem

CrabSystem is developed embedded in NinjaGame (`Packages/com.crxb.crabsystem/`, editable in place). To publish:

1. Bump `version` in `package.json` and add a `CHANGELOG.md` line.
2. Commit in NinjaGame.
3. Run `Tools/publish-crabsystem.sh` from NinjaGame's root. It copies the package into a clone of the CrabSystem repo (`../CrabSystem` by default), commits, tags `v<version>` and pushes. It refuses if the package has uncommitted changes or the tag exists.
4. In other projects, change the tag in the git URL.

To develop CrabSystem from another project instead, clone the CrabSystem repo into that project as `Packages/com.crxb.crabsystem/` (an embedded package wins over the git one) and remove the git line from `manifest.json`. Bring those changes back into NinjaGame's copy before the next publish: the script replaces the repo's contents with NinjaGame's copy, so anything committed only to the CrabSystem repo is overwritten. Edit one copy at a time.
