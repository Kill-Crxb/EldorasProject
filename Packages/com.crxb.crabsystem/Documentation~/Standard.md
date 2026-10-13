# The CrabSystem Standard

The standard fixes the *outside* of a system: where it lives, where its tuning and state are, and how it talks. You should be able to say what any system does, and where to change it, before reading its code. The inside is the author's call.

## 1. The six questions

| Question | The answer |
|---|---|
| **Where does it live?** | A module (`IBrainModule`) under the brain that owns it. It signs up by being there, with no inspector lists. Managers (`IGameManager`) sit under `ManagerBrain` the same way. |
| **Where is its tuning?** | On an asset: a ScriptableObject, schema, definition or profile. Never a number or id literal in code. |
| **Where is its runtime state?** | In plain C# classes on the module, saved through `ISaveable`, one owning file per fact. ScriptableObjects are never written at runtime. |
| **Who does it talk to?** | Its own brain's modules, through the brain. Another character only through events, facts, a read-only interface, or that character's public entry points. |
| **How does it say what happened?** | **Events** on the module for "this just happened". **Blackboard facts** for "this is true now". **`GameEvents`** only for game-wide moments no character owns. |
| **Where are its diagnostics?** | In a separate debugger component (`DebugTools/`). Production code has terse `LogError` guards only. |

A system that answers all six this way is standard, however it's written inside.

## 2. Free to vary

Algorithms, internal structure and file size (split by concern, not line count) are free. So are comments that explain why, partial classes for one concern spread over files, and measured performance tricks. Sameness for its own sake isn't the goal.

## 3. Rules

### Modules and wiring
- `Initialize(brain)` caches references, `LateInitialize()` wires across modules, `UpdateModule()` runs per frame.
- Auto-discovery is the one wiring path. Order comes from `InitOrder` (default 1000; core modules use 0–180). Two modules of the same type under one brain is an error, and the first wins.
- Find siblings with `brain.GetModule<T>()` in `Initialize` and cache them. No `FindObjectOfType`, no `GetComponentInChildren` in gameplay, no static `Instance` for anything per-character.
- Player-only modules say so (`PlayerOnly => true`); the brain switches them off on NPCs.
- Static access is for read-only registries only (item, ability, stat, resource, damage and faction definitions). Stateful managers go through `ManagerBrain.Instance.GetManager<T>()`.

### Data
- Tuning a designer might change is a serialized field on an asset or component.
- The asset is the source of truth for ids. Id fields carry `[IdRef(IdKind.X)]`. Code that must name an id uses a validated constants class: `BlackboardKey`, `StatIds`, `DialIds`, `CrabLayers`, `CrabResources`, `DamageTypeIds.Physical`. A literal id string in code is a defect.
- String keys hash only through `BlackboardKey.Hash` (FNV-1a), never `string.GetHashCode`.
- Renames go through the schema's `formerIds` map and the validator.
- No percentages: dice are the unit of power, and modifiers are flat or a die size.
- ScriptableObjects are static. `Instantiate` one if a runtime variant is needed.
- Framework fields default to empty, never to a game's content. The game's prefab variant fills them.

### State and saving
- One owner per fact. Nothing else re-saves or re-applies it.
- A saveable raises `ISaveable.Dirty` when it changes and sets `LoadOrder` if it must load early. `SaveManager` never names the systems it saves.
- An unknown save version is an error, not a guess.

### Talking between systems
| Want to… | Use |
|---|---|
| tell your own character's modules something happened | an event on your module |
| publish a state anything can gate on | a blackboard fact with one writer class |
| read another character's combat state | a read-only provider (`GetProvider<ICombatantState>()`) |
| affect another character | its entry point (`TakeDamage`, `StatusSystem.Apply`) |
| announce a game-wide moment | `GameEvents` |
| ask the UI to show something | a `GameEvents` request the UI listens for; gameplay never calls a window manager |
| know whether the UI holds the cursor | `UIPanelRegistry.CursorWanted` |
| get player input or AI intent | a control source, never the keyboard |

Restrict by tuning rather than switching things off. Deny with capability facts (`CannotAct`, `CannotMove`), not ad-hoc bools.

### Diagnostics
- Production code: `Debug.LogError` for broken setup and nothing else. No `debugLogging` flags on gameplay components.
- Traces and probes live on the `Debugtools` prefab, off in the base. A game's variant switches on what it wants.
- Debug keys live on the F-row, never on a gameplay binding.

### The framework boundary
- CrabSystem is the package `com.crxb.crabsystem`: `Runtime/` (`CrabSystem.asmdef`), `Editor/` (`CrabSystem.Editor.asmdef`), `Tests/EditMode`.
- Runtime never references game code; the asmdef enforces it.
- It never references a paid or third-party asset. Integrations live game-side and listen to framework events.
- Editor tools take project paths from **Project Settings > CrabSystem** and never name a game folder.
- Game content and game code live in the game's own folders.

## 4. Style

- No `#region`, no `///` doc comments, no history comments ("Phase 2", "Refactored in March").
- A comment says **why** when it isn't obvious. Design notes go in a short `//` block at the top of the file.
- Early returns over nested `if`s: guards first, work after.
- No local functions, no lambdas on events you must unsubscribe from, no `try/catch` that swallows.
- One way to do each thing: no alias properties, no second accessor, no copied save shapes, no repeated helpers.
- No pass-through layers. Don't repeat the brain's work (`if (!IsEnabled) return;` in `UpdateModule`).

## 5. Recipes

**A module.** Implement `IBrainModule` in `namespace CrabSystem` (framework) or the game's namespace. Put it under the owning brain on the prefab, on the base prefab if every character needs it. Tuning in fields or an asset. Siblings in `Initialize`, wiring in `LateInitialize`. Events out for what happened, facts out for what's true. `ISaveable` if it saves. Subscribe and unsubscribe in pairs.

**A blackboard fact.** Name the system that reads it; if only UI or VFX reads it, it's a status. Add it to the fact schema with type, default and writer. Add a `BlackboardKey` constant only if code reads it. One writer class.

**A stat.** Add it to a stat schema (`formerIds` if renamed). Code reads it through `StatIds` only if needed. Feed it by contributions keyed by source, never `SetValue` from gameplay.

**An ability or move.** Animate it first. The clip carries the timing events (Architecture: Moves). Damage is dice. Frames are baked. Gating uses capability facts.

**An item.** An `ItemDefinition` asset. Instance state rides `ItemRecord`. Gear stats are contributions keyed by item instance.

**A status.** A `StatusDefinition` asset: duration, stacking, contributions, ticks and capability flags. Crowd control goes through its control category and diminishing returns.

**A character.** A prefab variant of the game's base character, never a hand copy. Override only its differences. AI characters use `AIControlSource`.

**A scene-level object every game needs.** A base prefab in `Runtime/Prefabs` with empty slots, plus a game variant that fills them.

## 6. When something doesn't fit

Break a rule with a short comment saying why, where it happens. The exception is then visible and the next review can judge it.

## 7. Authority

Write single-player code so a server can own it later without a rewrite.
- Gameplay state changes only through entry points: `UseAbility`, `TakeDamage`, `StatusSystem.Apply`, `Equip`, `AddContribution`, `SetFact`.
- Intent in, state out. Nothing reads the keyboard or mouse in gameplay code.
- Presentation (UI, VFX, camera, juice) reads events and facts and never writes gameplay state. UI that issues player intent goes through the same entry point.
- No local-only inputs to results: rolls through `DiceRoll`, timing through `brain.Clock`.
- Ids, not references, cross the wire.

## 8. Lifecycle

- Subscribe and unsubscribe in matching pairs: `OnEnable`/`OnDisable` for scene objects, `LateInitialize`/`OnDestroy` for modules. Named handlers.
- Pooled objects reset on take and on return.
- Death, despawn and respawn are states, not destruction, until the despawn is final.
- Nothing reaches into a character before its brain reports initialised.
- No engine calls (`Time`, `Application`) in field initialisers. Build such objects in `Awake`.

## 9. Timing

- Animation events are the authority for move timing. Frame data is baked from clips, never typed.
- `AbilitySystem.HitStop` is the one owner of `animator.speed`. Change timing by changing playback, never with a second clock.
- One clock per character, `brain.Clock`. Exempt on purpose: physics-step movement (timers share the fixed step with velocities) and the camera (it belongs to the viewer).
- Seconds or frames in data, never both for the same thing. Moves use frames; statuses, cooldowns and regen use seconds.

## 10. Performance

Nothing per-frame or per-hit allocates or searches: no `GetComponent*`, `Find*`, LINQ or string building on the hot path. Physics queries use `NonAlloc` with module-owned buffers. A switched-off debugger costs nothing. Profile before restructuring for speed.

## 11. Names

| Suffix | Means |
|---|---|
| `…System`, `…Module` | an `IBrainModule` (System owns a domain, Module is a focused piece) |
| `…Manager` | an `IGameManager` under `ManagerBrain`, nothing else |
| `…Definition`, `…Profile`, `…Table`, `…Schema`, `…Database` | an authored ScriptableObject |
| `…Data`, `…Record`, `…SaveData` | a plain serializable class |
| `…Handler` | a strategy inside a module, never a pass-through |
| `…Trace`, `…Probe`, `…Debugger` | a diagnostic component |
| `I…Provider`, `I…Source` | an interface other systems read through |

Framework types are in `namespace CrabSystem` (block form; Unity is on C# 9). Menus sit under `CrabSystem/` and `Tools/CrabSystem/`. Assets are named `Type_Name` (`Status_Rooted`, `Moveset_Katana`).

## 12. Review checklist

`Tools > CrabSystem > Validate` automates 1–6 and writes `Logs/CrabSystem_Validate.md`.

1. No `#region`, `///` or `Debug.Log` outside `DebugTools/` and editor code.
2. No `Find*` / `GetComponentInChildren` / `FindObjectOfType` in gameplay folders.
3. No third-party references in the package.
4. No literal fact, stat or layer id strings in code.
5. No `UnityEngine.Random` or `Time.time` in gameplay maths outside the exemptions.
6. Every `+=` on another object's event has a matching `-=`.
7. The six questions are answered for anything new.

Editor tools follow the same style at a lower bar: UI Toolkit for new windows, and a tool only when a job is repeated or error-prone. Pure logic gets edit-mode tests in `Tests/EditMode`.

## 13. Docs

Truth order: code, then the tracker, then design docs. When they disagree, the code wins and the doc is fixed in the same session. A change to documented behaviour updates that doc in the same change.
