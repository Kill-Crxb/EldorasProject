# Hotbar bars as assets — design

**Status:** designed, nothing built. Awaiting sign-off.
**Date:** 2026-09-19
**Verified against:** `HotbarSystem.cs`, `HotbarSaveData.cs`, `ActionBarConfig.cs`, `ActionBarView.cs`,
`ActionBarSlotView.cs`, `AbilityPickerPanel.cs`, `InputSystem.cs`, `HotbarPageDefinition.cs`,
`Zoo.unity`, `Base_PC.prefab`.
**Related:** `Hotbar_Pages.md`, `Focus_Bars.md`, `UI_Panel_Model.md`, `UI_Audit_And_Architecture.md`.

---

## The defect

A bar's identity lives in **three places**, joined by a hand-typed string, and nothing checks that
they agree.

| Where | Holds | Authored in |
|---|---|---|
| `HotbarSystem.barDefinitions` | barId, slots, rows | `Base_PC.prefab` |
| `InputSystem.keybindRoutes` | barId → keybind set | `Base_PC.prefab` |
| `ActionBarView.barId` | barId | `Zoo.unity` |

There is no registry of views. Each `ActionBarView` independently walks
`ManagerBrain → SaveManager → PlayerBrain` in `HandleLoadCompleted` and subscribes on its own. So
**nothing in the project knows which views exist**, and the two halves are authored in different
files — you cannot see them together.

Three ways that fails, all of them silently:

- A bar with no view. No warning. *(This is what happened with `mouse` and `modifier` — the Zoo
  canvas has exactly three `ActionBarView` objects, `centre` / `bottomLeft` / `bottomRight`.)*
- A view whose `barId` is typo'd. `GetConfig` returns null, `Refresh` returns early, nothing draws.
- A keybind route naming a bar that no longer exists. `FindRoute` returns null, keys do nothing.

None of these produce an error, a warning, or a missing-reference slot in the inspector.

⚠ **The `Focus_Bars.md` pass made this worse.** Generalising the registry to a list was right, but
it added a *second* string-keyed list and left the view side hand-authored — three string joins
where there were two, with the same invisible failure.

**What the redesign fixes: a bar is one asset. Everything else references that asset.** A bar with
no view becomes impossible, and the remaining failures become loud.

---

## Shape

```
ActionBarDefinition (SO)        ← the bar. One asset per bar.
        ▲           ▲
        │           │
HotbarSystem      HotbarHud     ← what this entity CAN have · what is on screen now
.availableBars      │
        │           ├── HotbarAnchor "bottomCentre"  → spawns viewPrefab
HotbarSaveData      └── HotbarAnchor "leftEdge"      → spawns viewPrefab
.activeBarIds
  (what the player HAS on)
```

### `ActionBarDefinition` — ScriptableObject

```csharp
[CreateAssetMenu(fileName = "New Action Bar", menuName = "RPG/Action Bar")]
public class ActionBarDefinition : ScriptableObject
{
    public string barId;            // save key — see the rename warning
    public string displayName;

    [Range(1, 12)] public int slots = 4;
    [Range(1, 3)]  public int rows = 1;

    public ActionBarView viewPrefab;
    public string anchorId;
    public int sortOrder;

    public bool alwaysOn;           // player cannot switch it off
    public bool defaultActive = true;
}
```

⚠ **Nothing runtime-mutable goes on this asset.** Not slot contents, not the on/off flag. That is
the standing decision — *SOs are static authored assets; runtime player data uses plain serializable
classes via `ISaveable`* — and breaking it here would make every character share one hotbar layout,
with edits that appear to work in the editor and vanish in a build. Slot contents stay in
`HotbarPageState.bars`; the active list is new save data, below.

⚠ **`barId` is the save key, not the asset name.** Renaming the asset is free. Renaming `barId`
orphans whatever the player arranged on that bar — the same trap as `pageId` in `Hotbar_Pages.md`,
same family as the `SecondryStats` incident. Treat it as write-once.

### Activation — `HotbarSaveData` v4

```csharp
public List<string> activeBarIds;   // new in v4
```

`HotbarSystem` gains `IsBarActive(barId)`, `SetBarActive(barId, bool)` and
`event Action OnActiveBarsChanged`. `availableBars` is `List<ActionBarDefinition>` on the prefab —
what this entity *can* have. The save says which are *on*.

- v3 save (no `activeBarIds`) → every `defaultActive` bar in `availableBars`.
- `alwaysOn` bars are forced into the list on load and refuse `SetBarActive(false)`.
- **A deactivated bar keeps its contents.** Turning it back on restores the arrangement. Same
  reasoning as the orphaned page state in `Hotbar_Pages.md` — never delete a player's layout as a
  side effect of a toggle.

### Anchors and spawning

`HotbarAnchor` is a MonoBehaviour with an `anchorId`, dropped on a canvas object wherever a bar
should live. It registers itself into a static lookup on enable and unregisters on disable — the
same shape as `UIPanelRegistry`, which already does exactly this for Left/Right panels. No new
vocabulary, and the bug class it avoids is the one `UI_Audit_And_Architecture.md` describes as
"hunting for singletons or walking the hierarchy".

⚠ **Note what `UIPanelRegistry` keys on: `PanelSide`, an enum — not a string.** That is the better
precedent, and it is an open question below whether anchors should follow it. An enum makes a
typo'd anchor a compile error instead of a runtime log; a string makes adding an anchor free.

`HotbarHud` is one component per canvas. On `GameEvents.OnLoadCompleted` it resolves the player
through `PlayerBrainAccess.Find()` — the existing single way UI finds the player, which already
falls back to a scene search for scenes that skip character select — reads `HotbarSystem`, and for
each active bar instantiates `viewPrefab` into its anchor, ordered by `sortOrder`. It re-spawns on
`OnActiveBarsChanged`.

This also removes five duplicated player lookups: every `ActionBarView` currently runs its own
`ManagerBrain → SaveManager → PlayerBrain` walk, none of which has the `PlayerBrainAccess` fallback.

`ActionBarView` loses its `barId` string **and its own player-finding**. It gains
`Bind(ActionBarDefinition, HotbarSystem, SlotTransformationSystem)` and becomes a view that is told
what it is rather than one that interprets a string. The `GameEvents.OnLoadCompleted` subscription
in `Start` and the picker-button wiring in `Awake` both move into `Bind`.

**This is the part that removes the failure class.** `HotbarHud` knows the full set of bars that
should be on screen, so it can say so when one cannot be:

| Condition | Response |
|---|---|
| Active bar's `anchorId` matches no anchor | `Debug.LogError` naming bar and anchor |
| Active bar has no `viewPrefab` | `Debug.LogError` naming the bar |
| Anchor exists, nothing assigned to it | silent — an empty anchor is legitimate |

---

## Pages and activation are different axes

They will blur if the line is not drawn now.

| | Pages | Activation |
|---|---|---|
| Answers | which **contents** a bar shows | which **bars exist** for this player |
| Driven by | blackboard fact + priority | the player, via UI |
| Lifetime | transient, automatic | persistent, saved |
| Owner | `HotbarPageDefinition` | `HotbarSaveData.activeBarIds` |

A page swaps what is *in* the mouse bar when you go unarmed. Activation decides whether you have a
mouse bar at all. **Do not implement "hide this bar" as a page** — a page with no supplied bars and
a fact that is always true will appear to work and will fight the active list the first time both
change in one frame.

`HotbarPageDefinition.suppliedBars` becomes `List<ActionBarDefinition>` rather than `List<string>`,
for the same reason as everything else.

---

## What this costs

⚠ **The three existing scene bars get deleted.** `centre`, `bottomLeft` and `bottomRight` are
hand-placed in `Zoo.unity` with tuned positions, `GridLayoutGroup` cell sizes and spacing, and
picker-button wiring. Those become anchors plus a prefab.

**Build the view prefab from one of the existing scene objects** so the look is preserved rather
than rebuilt from memory. Do that first, check it renders in place, and only then delete the other
two. The cell size and spacing are the parts most likely to be lost.

Everything else is additive:

| File | Change |
|---|---|
| `Hotbar/ActionBarDefinition.cs` | **New.** The SO above. |
| `Hotbar/HotbarAnchor.cs` | **New.** `anchorId` + static registry. |
| `Hotbar/HotbarHud.cs` | **New.** Spawns and despawns views. |
| `Hotbar/HotbarSystem.cs` | `barDefinitions` → `availableBars`; active list; `IsBarActive` / `SetBarActive` / `OnActiveBarsChanged`. `BarDefinition` retires. |
| `Hotbar/HotbarSaveData.cs` | v4 — `activeBarIds`. |
| `Hotbar/ActionBarView.cs` | `barId` → `Bind(definition, …)`; drops its own player lookup. |
| `Hotbar/HotbarPageDefinition.cs` | `suppliedBars` → `List<ActionBarDefinition>`. |
| `HotbarDebugger.cs` | Iterate `availableBars`, mark which are active. |

`BarDefinition` — the plain class added four days ago in `Focus_Bars.md` — is replaced by the SO. It
never reached a save file, so nothing migrates.

---

## The one join this pass does not close

Scope for this pass is definition and activation. **`InputSystem.keybindRoutes` stays string-keyed**,
so the view↔definition join closes and the input↔definition join does not.

That residual is smaller than it was — the route and the bar list sit on the same prefab, so they
are at least visible together, where the view was in a different file. But it is still a string that
can silently name nothing.

**Cheap optional fix, not in scope unless you want it:** change `BarKeybindRoute.barId` from a
string to an `ActionBarDefinition` reference, with `barId => bar != null ? bar.barId : ""`. The route
still *saves* as a barId string, so `InputProfileSaveData` v2 is unchanged. One field, no migration,
third join closed. Say the word and it goes in; otherwise it is an Open item below.

---

## Decided

- **A bar is one asset.** Definition, shape, view prefab and anchor in one place; everything else
  holds a reference to it, not a copy of its name.
- **The SO holds no runtime state.** Contents and the on/off flag live in `HotbarSaveData`.
- **Views are spawned, never hand-placed.** A bar that cannot appear says so; a bar with no view is
  not a state the project can reach.
- **`ActionBarView` is told what it is.** It stops resolving a string and stops finding the player.
- **Pages swap contents; activation decides existence.** Two axes, two mechanisms, no overlap.
- **Deactivating a bar keeps its contents.** A toggle never destroys a layout.

## Open

- **The keybind route join** — the optional one-field change above. Yes or no.
- **Anchor id: string or enum?** `UIPanelRegistry` keys on the `PanelSide` enum. An
  `anchorId` enum makes a mismatch a compile error and finishes the job of removing strings from the
  spawn path; a string keeps adding an anchor to pure authoring. Recommendation: enum, because the
  whole point of this pass is that a bar cannot silently fail to appear, and an enum is the only
  option where that is guaranteed rather than logged.
- **Anchor vocabulary.** What the initial set is (`BottomCentre`, `BottomLeft`, `BottomRight`,
  `LeftEdge`, …) and whether an anchor can hold more than one bar. `sortOrder` assumes it can.
- **Who drives `SetBarActive`?** No UI for it yet. The list is saved and the API exists; the panel
  that exposes it is a later pass, and probably a `UIPanelView` per `UI_Panel_Model.md`.
- **Does an NPC need any of this?** `HotbarSystem` is an `IBrainModule`, so `Base_NPC` can carry
  bars. Nothing spawns views for it and nothing should — but `availableBars` on an NPC should
  probably be empty rather than inheriting the player's five.
- **Right-click clears a slot** and **`Consume`/`ToggleStance` both on R** — still open from
  `Focus_Bars.md`, untouched here.
