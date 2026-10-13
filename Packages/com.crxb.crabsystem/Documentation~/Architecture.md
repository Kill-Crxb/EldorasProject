# Architecture

## The brain

Every character (player, NPC, monster, dummy) has one `ControllerBrain`. In its `Awake` it:

1. creates the character's `EntityClock`,
2. finds every `IBrainModule` beneath it (`GetComponentsInChildren`, inactive included) and sorts them by `InitOrder`,
3. builds the provider cache,
4. calls `Initialize(brain)` on every module, then `LateInitialize()` on every module,
5. raises `OnInitialized`.

Its `Update` advances the clock, then calls `UpdateModule()` on every enabled module in `InitOrder`. Modules don't use Unity's own `Update`; the brain drives them.

A module signs up by being a child of the brain. There are no reference lists to fill in. Put a module under the system that owns it (a sub-component under its owner), not on the root.

### Modules

```csharp
public interface IBrainModule
{
    bool IsEnabled { get; set; }
    int InitOrder => 1000;        // lower first; core modules use 0–180 in steps of 10
    bool PlayerOnly => false;     // switched off on every character this machine doesn't drive
    void Initialize(ControllerBrain brain);
    void LateInitialize() { }     // after every module's Initialize: wire to other modules here
    void UpdateModule();
}
```

- **Initialize** caches the brain and the module's own references.
- **LateInitialize** subscribes to other modules. They all exist by then.
- **PlayerOnly** modules (camera, targeting, respawn) are switched off on NPCs and answer no interface, so an NPC can't steer itself by the player's camera.
- Two modules of the same concrete type under one brain is a prefab mistake. It logs an error and the first one wins.

### Finding other modules

- `brain.GetModule<T>()` returns a module by concrete type or interface. Call it in `Initialize` or `LateInitialize` and cache the result.
- `brain.GetProvider<T>()` reads the cache only. When several modules implement one interface (control sources, for example), the first in `InitOrder` answers.
- Typed shortcuts read the same cache: `brain.Stats`, `brain.Damage`, `brain.Resources`, `brain.Abilities`, `brain.Movement`, `brain.Blackboard`, `brain.Clock` and others.
- Anything outside the brain (a UI view, a world object) resolves in `Start`, not `Awake`, because the brain builds its cache in its own `Awake`.

### Load state

A character is **initialized** when its brain finishes `Awake`. It is **loaded** (`IsLoaded`, `OnLoaded`) once its data is in:
- NPCs mark themselves loaded in `Start`.
- The player is marked loaded when `SaveManager` finishes restoring its save.

Modules that react to saved state listen to their own brain's `OnLoaded`, never to a game-wide load event.

## Managers

Game-wide services are `IGameManager`s under one `ManagerBrain` (the `Manager_Brain` starter prefab). They're found the same way, initialised by `InitializationPriority`, and checked against `IManagerDependency.DependsOn`.

- **Definition registries** have a static `Instance` for read-only lookups: `StatsManager`, `ResourceManager`, `ItemManager`, `AbilityManager`, `DamageManager`, `FactionManager`.
- **Stateful managers** are reached only through `ManagerBrain.Instance.GetManager<T>()`: `SaveManager`, `AccountManager`.
- `ManagerBrain.Instance` is set in `Awake` with no scene search. Anything that runs earlier gets null and must cope.

## How systems talk

| To… | Use |
|---|---|
| tell your own character's modules something happened | a C# event on your module (`DamageSystem.OnDamageTaken`, `GuardModule.OnGuardBreak`) |
| publish a state anything can gate on | a **blackboard fact**, with one writer per key |
| read another character's combat state | a read-only interface: `brain.GetProvider<ICombatantState>()` |
| affect another character | its public entry point: `TakeDamage`, `StatusSystem.Apply`, `AddContribution` |
| announce a game-wide moment, or ask the UI to show something | `GameEvents` (`OnGameSceneReady`, `ContainerOpenRequested`, `DialogueStarted`) |
| get player input or AI intent | the control source: `IMovementControlSource`, `IAbilityControlSource` |

### The blackboard

Each character has a `Blackboard` (`BlackboardSystem`): typed bools, floats and ints keyed by an int hash.
- **Keys.** Hash string keys only with `BlackboardKey.Hash` (FNV-1a, stable across runs). Code uses the `BlackboardKey` constants; assets pick facts from the schema through `[IdRef(IdKind.Fact)]`.
- **Capability facts** deny actions: `CannotAct`, `CannotMove`, `CannotCast`, `CannotAttack`, `CannotBlock`, `CannotJump`, `CannotSprint`. Statuses raise them, and abilities and movement read them. Denial goes through facts, not ad-hoc bools.
- **Keyed claims** (`Claim(key, source)` / `Release(key, source)`): several sources hold one fact true, and it drops when the last one releases.

### Stats

`StatSystem` holds a value per stat id, defined by `StatSchema` assets.
- Gameplay never sets stats directly. Gear, statuses, talents and rewards add **contributions** keyed by their source (`AddContribution(statId, "item:<instance>", 2)`), and removing the source removes them all (`ClearContributions`).
- **Derived schemas** aren't saved: their sources stay the only truth.
- A renamed stat keeps answering to its old ids through `StatEntry.formerIds`.

## Time and randomness

- **One clock per character.** Gameplay timers (cooldowns, statuses, regen, guard, AI memory, ability expiry) read `brain.Clock.Now` / `Delta` through one private helper per module, never `Time.time`. Today the clock tracks `Time` at scale 1; action speed, hit-stop or a server tick change it in one place.
  - **Two exceptions, on purpose.** Physics-step movement (`CharacterMotor`, `ParkourLocomotionHandler`) stays on `Time`, because its timers share the fixed step with its velocities. The camera runs on real time, because it belongs to the viewer.
- **One random source.** Gameplay randomness (hit rolls, saves, procs, damage dice, loot, spawns, AI choices) goes through `DiceRoll`: `Die`, `Value`, `Range`, `InsideUnitCircle`. It's seedable (`DiceRoll.Seed`), so a server can own it. Only presentation (sparks, number jitter, flicker) may use `UnityEngine.Random`.

## Ids and data

- **Assets define ids.** Stats live in stat schemas, facts in the blackboard schema, statuses in `StatusDefinition`, damage types in `DamageTypeDefinition`, items in `ItemDefinition`, and so on.
- **Id fields are picked, not typed.** Tag them `[IdRef(IdKind.X)]` to get a dropdown. `Tools > CrabSystem > Validate Ids` reports any id that no asset defines.
- **Code names an id only through a constants class** the validator checks: `BlackboardKey` (facts), `StatIds` and `DialIds` (stats), `CrabLayers` (layers), `CrabResources` (Resources folder names), `DamageTypeIds.Physical` (the default damage type).
- **ScriptableObjects are authored data** and are never written at runtime. Runtime state is plain C# classes.

## Saving

A module that saves implements `ISaveable`:
- `GetSaveId` is its file (`"stats"` → `stats.json`).
- `GetSaveData` and `LoadSaveData` read and write its JSON.
- `GetSaveVersion` is written as a top-level `version`. A newer file than the code knows is skipped and never overwritten.
- `LoadOrder` puts stats first, then whatever derives from them.
- `Dirty` asks for a write before the next autosave.

`SaveManager` finds every saveable under the player's brain and names none of them. There's one owner per fact: level lives in one file, name in one, and nothing else re-saves them.

## Moves and animation

Animation events are the authority for move timing. A move's clip carries:

| Event | What it does |
|---|---|
| `Tell` | the frame a defender can first read the attack |
| `Strike(n)` | one hit check, using strike shape `n` |
| `Cue(n)` | releases effects, a spell or a projectile |
| `ChainOpen` | the next step of a string may start from here |
| `Unlocked` | the move is over |
| `Invuln(1/0)` | invulnerability on / off |
| `Travel(1/0)` | marks a dash or lunge |

`AnimationEventForwarder` relays them to `AbilitySystem`. A move's frame data is **baked** from its clip (`Tools > Combat > Bake Move Frames`, and automatically on import), never typed.

Hit-stop pauses the animator, and `AbilitySystem.HitStop` is its one owner. Change timing by changing playback (clip, speed parameter), never with a second clock.

## Authority (server-ready)

- **Gameplay state changes only through entry points** a server can own: `UseAbility`, `TakeDamage`, `StatusSystem.Apply`, `Equip`, `AddContribution`, `SetFact`.
- **Intent in, state out.** Input and AI produce intent through control sources, and modules turn it into state.
- **Presentation reads, never writes.** UI, VFX, camera and juice read events and facts, and don't change gameplay state.
- **Ids cross the wire, not references.**
