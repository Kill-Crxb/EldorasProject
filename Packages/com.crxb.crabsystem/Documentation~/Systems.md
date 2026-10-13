# Systems

Every brain module and manager in `Runtime/`, grouped by area.

- **Order** is `InitOrder`; anything not listed runs at 1000, after the core.
- **P** marks a player-only module.
- **Fit** says how general the module is:
  - **Core:** any game.
  - **RPG:** games with stats, progression and gear.
  - **3P:** shaped by third-person melee (NinjaGame). Usable elsewhere, but expect to replace or extend it.

Events listed are C# events on the module. Facts are blackboard keys it writes.

## Core

| Module | Order | Fit | What it does | Talks through |
|---|---|---|---|---|
| `ControllerBrain` | — | Core | Finds, orders, initialises and updates modules. Owns the provider cache, `Clock`, load state and the player's input instance. | `OnInitialized`, `OnLoaded` |
| `IdentitySystem` | 0 | Core | Display name and archetype from the character's config. Forwards entity type (brain) and level (`RPGSystem`); saves neither. | `ISaveable` |
| `FactionSystem` | 10 | Core | The character's `FactionDefinition`; hostility is read through `FactionManager` relationships. | `ISaveable` |
| `BlackboardSystem` | 110 | Core | The character's `Blackboard` (facts, keyed claims), seeded from a `BlackboardSchema`. | `OnBoolChanged` etc. on the blackboard |
| `SemanticBridgeSystem` | 1000 | Core | Turns numbers into facts: evaluates `BlackboardCondition` assets and writes their output facts. | facts |
| `CharacterConfigurationHandler` | 1000 | Core | Applies a `CharacterConfigData` (name, level, faction, archetype contributions) straight to its brain. Configs seed new characters only. | — |
| `InputSystem` | 30 | Core | Reads `PlayerInputControls` for the local player. Implements `IMovementControlSource` / `IAbilityControlSource`, and owns bar key sets. | control-source interfaces |

## Stats, resources, progression

| Module | Order | Fit | What it does | Talks through |
|---|---|---|---|---|
| `StatSystem` | 90 | Core | Values per stat id from the loaded `StatSchema`s, plus keyed contributions. Derived schemas aren't saved. `formerIds` renames. | `IStatProvider`, `OnStatChanged` |
| `StatDerivationSystem` | 1000 | RPG | Applies a `StatDerivationTable`: each threshold a stat passes grants its rewards, and they're taken back below it. | contributions |
| `ResourceSystem` | 100 | Core | Live pools (health, stamina, mana, charges). Each pool's max comes from a stat; regen and delay come from `ResourceDefinition` and `res.*` dials. | `IResourceProvider`, `IHealthProvider`, `OnResourceChanged`, `OnDeath` |
| `RPGSystem` | 140 | RPG | Level and XP. Publishes level as a stat contribution. The cap comes from `TalentRules` when the character has talents. | `OnLevelChanged`, `OnXPChanged` |
| `TalentModule` | 150 | RPG | Talent slots and points (`ITalentPointPool`). Spend, refund, respec. Grants rewards keyed per talent. | `Changed`, `ISaveable` |
| `KeywordModule` | 1000 | RPG | Keywords on abilities (asset, socket or talent): on-hit gains, and to-hit / advantage / bonus dice through `IHitModifier`. | `IHitModifier` |
| `TriggerModule` | 1000 | RPG | Talent triggers (hit landed or taken, kill, evade, parry, status gained…). Each rolls d20 vs DC, has an internal cooldown, and fires resource / status / cooldown / proc effects. | listens to combat events |

Rewards (`Progression/`) are the shared vocabulary for "this grants that": stat bonus, rate, roll bonus, ability, keyword grant/bonus, status modifier, ability modifier, flag, talent point and trigger. Talents, derivation tables and gear all pay out through them.

## Combat

| Module | Order | Fit | What it does | Talks through |
|---|---|---|---|---|
| `AbilitySystem` | 80 | Core | Owns the character's abilities: use, costs, cooldowns (entity clock), modifiers, the move clock and events, hit-stop (`HitStop`), invulnerability, ability replacement. | `IAbilityProvider`, `ICombatantState`, `OnAbilityUsed`, `OnAbilityRefused`, `OnHitLanded`, `OnTell`, `OnMoveEvent` |
| `RuntimeAbilityManager` | 1000 | Core | Granted ability instances by source (permanent, equipment, consumable, temporary, talent): uses and expiry. | `OnAbilityAdded`, `OnAbilityRemoved`, `OnAbilityExpired` |
| `SlotTransformationSystem` | 180 | RPG | Hotbar slot overrides and procs (d20 vs DC on `OnHitLanded`), and reactive transforms by fact. The view asks it what a slot shows. | `OnOverrideChanged` |
| `DamageSystem` | 120 | Core | Builds packets (`CalculateDamage`) and takes them (`TakeDamage`): the d20 hit roll vs defence, glancing hits, counter-hits, guard and shield, death and revive. Raises `IsDead`. | `OnDamageTaken`, `OnDamageApplied`, `OnDeath`, `OnRevived`, `OnKill`, `OnKilledBy`, `OnEvaded` |
| `ArmourShieldModule` | 1000 | RPG | A soak-dice shield for damage types flagged `absorbedByShield`. It refills after a delay and trickles under pressure. | `OnShieldHit`, `IBarSource` |
| `StatusSystem` | 1000 | Core | Applies `StatusDefinition`s: duration, stacks, contributions, ticks, facts, crowd-control category with diminishing returns, saving throws, resist / immune / intensify. | `OnStatusApplied`, `OnStatusRemoved`, `OnStatusSaved`, `OnStatusResisted` |
| `StatusVfxBinder` | 1000 | Core | Puts a status's aura on the bearer and removes it. | — |
| `HitReactionSystem` | 1000 | 3P | Plays the reaction for a hit-state status, and blends out when `CannotAct` drops. Handles the death pose. | `OnReactionPlayed` |
| `GuardModule` | 1000 | 3P | Held block, parry window, riposte, blockstun, Guard soak bar, Guard Break, flanks. | `OnBlockStart`, `OnGuardBreak`, `OnParryWindowOpened`… |
| `PostureModule` | 1000 | 3P | Posture bar (Sekiro-style). Full means Guard Break. | `IBarSource` |
| `CombatStanceModule` | 1000 | 3P | Armed or unarmed stance, and draw / sheathe. | `OnStanceChanged` |
| `MovesetModule` | 1000 | 3P | The context-resolved LMB string per weapon (`WeaponMoveset`): light, running, air, parry, plus an input buffer. | `OnPreviewChanged` |
| `RespawnModule` | 1000 P | Core | Player respawn at the spawn point with full pools, after a delay. | listens to death |
| `TargetingModule` | 1000 P | 3P | Soft target in front of the camera. Attacks track it during startup. | — |
| `ProjectileLauncher` | 1000 | Core | Fires `ProjectileData` for an ability, aimed through the camera's aim point with soft assist. | — |

Melee hit detection is `StrikeHandler` (reach, facing cone, height band, line of sight) on each `Strike(n)` event. Projectiles have their own sweep and pool.

## Movement and animation

| Module | Order | Fit | What it does | Talks through |
|---|---|---|---|---|
| `MovementSystem` | 60 | Core | Owns the active control source and the `LocomotionHandler`. Grounding, gait state, facing override, denials from facts. | — |
| `ParkourLocomotionHandler` | (handler) | 3P | Momentum movement on `CharacterMotor`: gaits, jump, air control, wall run, mantle, dash. Reads `mov.*` dials. | — |
| `ParkourAssistant` | 1000 | 3P | Reports the geometry around the character (walls, ledges). Decides nothing. | `OnContactChanged` |
| `FeetDetectionModule` | 1000 | Core | Ground check for the brain's `IsGrounded`. | — |
| `StateMachineModule` | 50 | Core (to trim) | Parallel brain / upper / lower / posture states with a permission matrix. Due to be cut down to posture, with the rest moving to facts. | `IStateProvider` |
| `AnimationSystem` | 70 | Core | Triggers, parameters and cross-fades on the character's animator. Skips parameters the controller lacks. | `IAnimationProvider` |
| `AnimationLayerController` | 1000 | Core | The one owner of layer weights: callers claim a weight with a priority and release it. | — |
| `AnimatorFactBridge` | 1000 | Core | Mirrors blackboard facts into animator bools. | — |
| `ActionsLayerDriver` | 1000 | 3P | Blends full-body and upper-body action layers by speed. | — |
| `AnimatorIKModule` (base), `BodyLeanSystem` | 1000 | Core | IK plumbing for foot IK and head look, and lean into turns. | — |
| `ModelModule` | 20 | Core | The character's model: swap by id, sockets, equipment visuals, appearance. | `OnModelChanged`, `ISaveable` |
| `VFXSystem` | 1000 | Core | Named effect anchors, and spawning effects for other systems. | — |

## World, items, AI, UI

| Module | Order | Fit | What it does | Talks through |
|---|---|---|---|---|
| `InventorySystem` | 130 | RPG | Grid inventory of `ItemInstance`s. The one `IInventoryProvider`. | `OnInventoryChanged`, `OnItemAdded` |
| `EquipmentSystem` | 1000 | RPG | Slots from `EquipmentSlotDefinition`s. Equipping adds item stats as contributions. | `OnEquipmentChanged`, `OnEquipmentVisual` |
| `LootSystem` | 1000 | RPG | Death drops (kept as a stub, due to be rebuilt). | `OnLootGenerated` |
| `VendorSystem` | 1000 | RPG | A shelf inventory plus `VendorDefinition` pricing. Opens through `GameEvents.ContainerOpenRequested`. | — |
| `InteractionSystem` | 150 | Core | Interact prompts and routing: talk, loot, direct use. | `GameEvents` requests |
| `DialogueSystem` / `DialogueMemory` | 160 / 1000 | Core | Conversations, and the one-shot options already taken (saved on the player). | `GameEvents.DialogueStarted/Ended` |
| `HotbarSystem` | 170 | Core | Action bars (`ActionBarDefinition`), pages, press and buffer. | `OnSlotPressed`, `OnSlotChanged` |
| `SpellcraftSystem` / `SpellcraftInput` | 1000 | RPG | Hand-sign spell grammar (v1). `SpellcraftInput` is a temporary keyboard driver. | `OnSpellCast`, `OnElementDrawn` |
| `AIControlSource` | 1000 | Core | The AI's hands on the controls: steer, face, stop, guard, ability roles, reaction memory. | control-source interfaces |
| `GOAPModule` | 1000 | Core | Utility goal selector (not a planner). Runs `GOAPGoal` assets on a `GOAPContext`. | — |
| `PerceptionModule` | 1000 | Core | Vision cone, line of sight, faction filter, target memory. | — |
| `CameraModule` | 40 P | 3P | Third-person orbit camera with mode rules, aim point and cursor lock (asks `UIPanelRegistry.CursorWanted`). | `ICameraProvider`, `OnModeChanged` |

UI views (`UIWindows/`, `Hotbar/`, `Status/StatusBar/`) aren't brain modules. They resolve their character's modules in `Start` and only read.

## Managers (`Manager_Brain`)

| Manager | What it does |
|---|---|
| `StatsManager` | Loads stat schemas, resolves former ids. |
| `ResourceManager` | Resource definitions. |
| `ItemManager` | Item definitions by id. |
| `AbilityManager` | Ability definitions by id, for saved bars. |
| `DamageManager` | The active `DamageCalculationConfig` (damage types, base defence) and faction damage modifiers. |
| `FactionManager` | Faction relationships. |
| `SaveManager` | Finds the player's saveables, autosaves, dirty flush, quit save, version check. |
| `AccountManager` | Local account and character list. |

## Editor tools

Open them from `Tools > CrabSystem > Dashboard`, or from their menus:

| Tool | Where |
|---|---|
| **Project Setup**: what a project must provide, with fixes | Dashboard |
| **Health**: Standard scan, Validate Ids, missing scripts, prefab lineage, staging leftovers | Dashboard |
| **Tools**: every CrabSystem command | Dashboard |
| Weapon wizard, Weapon Asset Report | Dashboard Weapons tab |
| Validate (style checklist), Validate Ids | `Tools > CrabSystem` |
| Move Report, Bake Move Frames | `Tools > Combat` |
| Layer Manager, Create Default Layer Config | `Tools > CrabSystem` |
| Talent wizard | its menu |
| Generate Items From Sprites | its menu |
| Settings | Project Settings > CrabSystem |

Diagnostics live in `Runtime/DebugTools/` on the Debugtools prefab, switched off in the base:
- `HitTrace`, `ActionTrace`, `StatusTrace`, `StrikeTrace`, `MoveClockTrace`, `MovesetProbe`
- `StatusHotkeys`
- the debug displays
- `SceneQuickStart`, which plays a game scene directly
