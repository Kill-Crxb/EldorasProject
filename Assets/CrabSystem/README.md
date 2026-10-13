# CrabSystem

Character framework for Unity 6 / URP. A `ControllerBrain` on each character discovers its
`IBrainModule` components, initialises them by `InitOrder`, and hands out providers. Game content
(abilities, items, characters, configs) lives outside this folder, in the game's own Database.

## Layout

- `Runtime/` — the `CrabSystem` assembly. One folder per subsystem; `Core` holds the brain,
  modules, events and the entity clock. `Prefabs` holds `Manager_Brain` and `EventSystem`.
- `Editor/` — the `CrabSystem.Editor` assembly. One folder per subsystem it serves.
- Shaders sit in `Runtime/Rendering/Shaders`.

## Rules

- Runtime code never references game code. Game builders and game-specific tools live in the game.
- Editor code only goes in `Editor/`. A nested `Editor` folder inside `Runtime/` would compile
  into the runtime assembly and break builds.
- No `Resources` folders here. Data the game loads by path belongs in the game's Database.
- Code style follows the CrabSystem Standard. `Tools > CrabSystem > Validate` checks it.

## Using it in another project

Copy this folder into `Assets/`, or into `Packages/com.crxb.crabsystem/` as an embedded package.
The dependencies in `package.json` must be installed. Layers, tags and the input actions in
`Runtime/Input` are the project's to set up.
