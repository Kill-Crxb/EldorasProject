# Quest system — staged scripts

Built 29 Sep 2026 from `Quest_System.md` / `World_Progression.md` (Q1 no banking, Q3 flags per
character by default). **Not in Assets** — nothing here compiles in Unity until moved.

## Moving it in

1. Move the `.cs` files to a new `Assets/CrabSystem/Quests/`, and `Editor/QuestValidator.cs` to
   an `Editor` folder under it.
2. Paste the three blocks in `GameEvents_QuestAdditions.txt` into `GameEvents.cs`. The scripts
   won't compile without them.
3. Add `QuestManager` to the `Manager_Brain` prefab (Known Issues A1/A2 still apply — Zoo's
   manager is inactive unless entered from MenuScene).
4. Add `QuestSystem` to the player prefab. It saves as `quests.json`; it isn't in
   SaveManager's `LoadOrder`, so it loads after the listed modules — which is what it needs
   (stats and inventory first).
5. Put `QuestTarget` on any enemy or dummy prefab that should count for Kill objectives.

## Files

| File | What |
|---|---|
| `QuestDefinition.cs` | SO: stages → objectives, rewards, item grants, flag changes |
| `QuestSystem.cs` | Player module + `ISaveable` + `IFlagSource`. All progress, flags, timers, rewards |
| `QuestManager.cs` | `IGameManager` id lookup, same shape as `ItemManager` (`Resources/QuestDatabase/`) |
| `QuestSaveData.cs` | Save shape: active states with counters, completed, failed, flags, paid stages |
| `FlagGate.cs` | `FlagRequirement`, `FlagChange`, `Gate`, `IFlagSource`, prefix scoping |
| `GameplayEvent.cs` | The generic event struct |
| `QuestTarget.cs` | Kill id on an entity |
| `QuestLocation.cs` | Trigger for Reach objectives |
| `QuestProvider.cs` | NPC: counts Interact, auto-accepts first available quest |
| `Editor/QuestValidator.cs` | `NinjaGame ▸ Quests ▸ Validate Quest Definitions` |

## Where it differs from the design doc

- **Kills and pickups don't go through the bus.** `OnKill` turned out to be an event on each
  character's own `DamageSystem`, and inventory events are per-character too, so `QuestSystem`
  subscribes to its own. Nothing in `DamageSystem` or `InventorySystem` changes, and it can't
  double-count in a host build. The bus carries Reach / Interact / Submit, filtered to the actor.
- **`QuestTarget` instead of archetype ids.** The NPC archetype is private to `AISystem` and
  dummies don't have one.
- **`requires` is a `Gate`**, not a list — matches World_Progression's single gate type.
- **Branching:** an objective can carry its own `nextStage`, and a stage has `completesQuest`
  (a branch stage that isn't last in the list needs it, or it runs on into the other branch).
- **A stage with no objectives completes on entry** — useful for pure reward/flag steps.
- **Quest rewards are re-applied on every load**, keyed `quest:{questId}:{stage}`, because
  `StatSystem` doesn't save contributions.
- **`QuestLocation` isn't a ghost trigger** — it's always on and QuestSystem ignores arrivals
  nothing asks for. Cheap enough; switch if zones end up with hundreds.
- **Failed quests can be retaken.** Abandoned ones aren't marked failed.

## Not built yet

- Submit (hand-in panel), pick-one item choice, quest log / HUD tracker, notification toast.
- `ZoneDefinition`, `FlagRegistry`, `WorldStateSystem` — `QuestSystem.WorldFlags` is null, so
  `zone.` / `world.` flags read 0 and writes are refused.
- `QuestProvider` isn't wired to input (waits on AR7's interaction trace).
- Tree-level rewards — need a `Reward` type from the talent module (T1/T5).
- Multiplayer: shared objectives (Q2), participation credit.

## Watch for

- **Item grants with a full inventory are lost** (logged). No overflow yet.
- **Objective order is the save key.** Reordering objectives in a shipped stage scrambles
  counters; changing the count just resizes them.
- Tested outside Unity against stubs of the real signatures (18 checks: advance, branch,
  flags, gates, un-completing, timers, abandon). Not yet compiled in the editor.
