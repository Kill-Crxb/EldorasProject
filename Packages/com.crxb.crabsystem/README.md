# CrabSystem

Character framework for Unity 6 / URP. A `ControllerBrain` on each character discovers its
`IBrainModule` components, initialises them by `InitOrder`, and hands out providers. Game content
(abilities, items, characters, configs) lives outside this folder, in the game's own Database.

## Layout

- `Runtime/` — the `CrabSystem` assembly. One folder per subsystem; `Core` holds the brain,
  modules, events and the entity clock. `Prefabs` holds the starter prefabs (below).
- `Editor/` — the `CrabSystem.Editor` assembly. One folder per subsystem it serves.
- Shaders sit in `Runtime/Rendering/Shaders`.

## Rules

- Runtime code never references game code. Game builders and game-specific tools live in the game.
- Every type is in the `CrabSystem` namespace (or a child of it: `CrabSystem.Progression`, `.Magic`,
  `.Stats`, `.Factions`, `.UI`). Game code adds `using CrabSystem;`.
- Editor code only goes in `Editor/`. A nested `Editor` folder inside `Runtime/` would compile
  into the runtime assembly and break builds.
- No `Resources` folders here. Data the game loads by path belongs in the game's Database.
- Code style follows the CrabSystem Standard. `Tools > CrabSystem > Validate` checks it.

## Starter prefabs

`Runtime/Prefabs` holds the scene objects every game needs. Each one ships with its game slots empty;
a game makes a prefab variant of each, fills the slots there, and puts the variants in its scenes.

- `Manager_Brain`: every manager. Its variant names the game's stat schemas, resource definitions,
  damage config, factions and starting items.
- `Bootstrapper`: starts a game scene, and lets a game scene be played directly (SceneQuickStart).
- `UIManager`: HUD, hotbar, inventory, tooltips, nameplates and dialogue.
- `Debugtools`: every Trace and Probe, all switched off. Switch on the ones you want in the variant.
- `SceneLoader`, `MenuUi`: the menu scene's loader, login and character creation.
- `EventSystem`: already nested in Manager_Brain.

## What a game provides

The framework names a few things in code. Each lives in one constants class, and
`Tools > CrabSystem > Validate Ids` checks the game has them.

- **Stats** (`StatIds`, `DialIds`): the stat model the combat and creation code runs on. The game's
  stat schemas must define every id.
- **Layers** (`CrabLayers`): Hurtbox, Terrain, StaticProp.
- **Resources folders** (`CrabResources`): `AbilityDatabase`, `ItemDatabase`, `StatDatabase` and
  `Keywords` under any `Resources/` folder, or assign the definitions on Manager_Brain instead.
  Hit-state statuses load by name from `Resources/Statuses/HitStates/`.
- **Socket names** are set on each model prefab (SocketEventRelay); there are no defaults.

## Installing

CrabSystem is a UPM package (`com.crxb.crabsystem`). Pick one:

- **Git** (other projects): Package Manager > + > *Add package from git URL*, then
  `https://github.com/Kill-Crxb/CrabSystem.git#v0.2.0`. Pin a tag; `#main` follows the latest.
  The same line can go straight into `Packages/manifest.json` under `dependencies`.
- **Embedded** (the project CrabSystem is developed in): this folder sits at
  `Packages/com.crxb.crabsystem/`, editable in place.
- **Local folder**: Package Manager > + > *Add package from disk*, pick `package.json`.

The dependencies in `package.json` install with it. Tests run from Window > General > Test Runner once
`"testables": ["com.crxb.crabsystem"]` is in the project's `manifest.json`.

## Setting up a project

1. Open `Tools > CrabSystem > Dashboard` and the **Project Setup** tab. It checks URP, the Input System,
   TextMesh Pro's essentials, the layers in `CrabLayers`, the `CrabResources` folders, a stat schema entry for
   every `StatIds` / `DialIds` id and a damage config with the `physical` type. Each missing piece has a fix
   button; fixes only add.
2. **Project Settings > CrabSystem** says where this project keeps its starter data, items, talents, staging
   and downloaded packs. The editor tools read these instead of fixed paths.
3. Make a variant of each starter prefab, fill its slots (assign the damage config on the Manager_Brain
   variant's DamageManager), and put the variants in the scenes.
4. `Runtime/Input/PlayerInputControls` is the framework's player input. Its wrapper code is committed and
   generation is off, because a git package is read-only; to change the bindings, edit the actions in the
   embedded copy, tick *Generate C# Class*, apply, then untick it. The project-wide input actions
   (Project Settings > Input System) belong to the game.

Still to come: a starter character prefab, so a blank project has someone to play.
