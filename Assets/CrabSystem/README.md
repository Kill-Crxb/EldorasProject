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

## Using it in another project

1. Copy this folder into `Assets/`, or into `Packages/com.crxb.crabsystem/` as an embedded package.
   The dependencies in `package.json` must be installed, plus TextMesh Pro's essentials.
2. Layers: `Tools > CrabSystem > Create Default Layer Config`, then `Tools > CrabSystem > Layer Manager`
   and Apply. Code names layers only through `CrabLayers`; `Validate Ids` reports any that are missing.
3. Make a variant of each starter prefab and fill its slots.
4. `Runtime/Input/PlayerInputControls` is the framework's player input. The project-wide input actions
   (Project Settings > Input System) belong to the game.
