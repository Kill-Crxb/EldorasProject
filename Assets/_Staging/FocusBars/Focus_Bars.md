# Focus Bars — mouse and modifier

**Status:** built, not yet run. **Date:** 2026-09-16.
**Verified against:** `HotbarSystem.cs`, `HotbarSaveData.cs`, `ActionBarConfig.cs`, `ActionBarView.cs`,
`ActionBarSlotView.cs`, `InputSystem.cs`, `PlayerInputControls.inputactions`, `HotbarDebugger.cs`,
`ParkourLocomotionHandler.cs`, `IInputProvider.cs`.
**Related:** `Hotbar_Pages.md`, `Movement_Ability_Interface.md`, `UI_Audit_And_Architecture.md`.

---

## What this adds

Two bars whose shape is the point:

| Bar | barId | Slots | Rows | Keys |
|---|---|---|---|---|
| Mouse | `mouse` | 2 | 1 | LMB, RMB |
| Modifier | `modifier` | 3 | 3 | Shift, Ctrl, Q |

**No new view scripts.** Horizontal and vertical are `rowCount`: `ActionBarConfig.ColumnsPerRow` is
`ceil(slots / rows)`, and `ActionBarView` feeds that straight to the `GridLayoutGroup` constraint.
`(2, 1)` is a horizontal pair; `(3, 3)` is a single column. The existing `ActionBarView` /
`ActionBarSlotView` pair renders both with icons, cooldown sweeps, override borders and drag-drop
already working.

That left one real problem: **`HotbarSystem` could only ever have three bars.**

---

## The registry was the actual work

Before this pass, "centre", "bottomLeft" and "bottomRight" were spelled out in seven places —
three fields on `HotbarPageState`, a `switch` in `BarIn()`, three `yield return`s in `AllBars()`,
three `BarSizeConfig` defaults, three keybind enum fields on `InputSystem`, three held-time arrays,
and a `switch` in `GetKeybindSetForBar()`. Adding two bars meant adding five of everything.

Bars are now a list. **Nothing in `HotbarSystem` or `InputSystem` names a bar.**

```
HotbarSystem.barDefinitions : List<BarDefinition>   { barId, slots, rows }
HotbarPageState.bars        : List<ActionBarConfig> keyed by barId
InputSystem.keybindRoutes   : List<BarKeybindRoute> { barId, keybinds }
```

A sixth bar is now three rows in two inspectors and a prefab. No code.

### Save format — v3

`HotbarSaveData.CurrentVersion` 2 → 3. `InputProfileSaveData.CurrentVersion` 1 → 2. Both keep their
older fields and fold them forward on load, the same shape the v1 → v2 page migration already used.

| From | What happens |
|---|---|
| v1 hotbar (three bars at the root) | folded into the default page, then into that page's bar list |
| v2 hotbar (pages with three named bars) | `MigrateNamedBars()` per page |
| v1 input profile (three enum fields) | mapped onto routes by barId |

⚠ **`JsonUtility` never writes `null` for a class field** — a v3 save still emits `centreBar` as
`{}` with an empty `slots` list. So the migration's test is `slots.Count == 0`, not `== null`. Test
it the other way and every load invents three junk bars.

Bars authored after a save was written are added empty by `EnsureBarsIn()`, which runs on every page
including ones already loaded. That is the same fallthrough `Hotbar_Pages.md` describes for pages,
one level down.

---

## Key claims

`HotbarKeybindSet` gains two values:

```
MouseLR      LMB=slot0  RMB=slot1
ShiftCtrlQ   Shift=slot0  Ctrl=slot1  Q=slot2
```

### Validation is now per key, not per set

The old rule compared enum values, with a fixed centre > bottomLeft > bottomRight priority. That
cannot see the conflict this pass creates: `QuickslotQ` and `ShiftCtrlQ` are different enum values
that want the same physical key. `KeysClaimedBy()` returns a set's keys and routes are checked in
**list order** — first listed keeps the key, later one is demoted to `None` with a warning naming the
key. Route order is therefore meaningful, and the modifier and mouse bars are listed first.

### What each key was doing before

| Key | Was | Now |
|---|---|---|
| LMB | `Attack` → `LightAttackPressed` — **nothing consumed it** | mouse slot 0 |
| RMB | `Block` → `BlockHeld` — **nothing consumed it** | mouse slot 1 |
| Shift | **unbound**. `Sprint` is gamepad-only (`leftStick/down`) | modifier slot 0 |
| Ctrl | `Crouch` → `MovementInput.Crouch` → `ParkourLocomotionHandler:205` — **live** | see below |
| Q | `QuickslotQ`, routed to the bottomLeft bar | modifier slot 2 |

LMB and RMB were free: `LightAttackPressed`, `HeavyAttackPressed`, `BlockHeld` and `ParryPressed`
are declared on `IInputProvider` and set every frame in `ReadCombatInput`, and **no consumer exists
anywhere in `Assets`**. They are now the mouse bar's source rather than dead state.

Shift is free and was already earmarked — `Movement_Ability_Interface.md` reserves it for movement
abilities and warns that when it is bound it must carry a press edge and a hold as **separate fields
the whole way down**. `BridgeBarInput` already does exactly that: `GetWasPressed` is the edge,
`GetIsPressed` is the hold, and they never merge. The Slice 1 `Jump = JumpPressed` bug cannot
recur through this path.

**Q moved off the bottomLeft bar.** It was that bar's only key. bottomLeft now defaults to
`Hotbar1234` (keys 1–4, previously unclaimed). Change it if you want something else — it is one
enum in the inspector.

---

## ⚠ Ctrl is the one unresolved key

Crouch is live on Ctrl and it is not an ability. `ParkourLocomotionHandler` reads
`input.Crouch` / `input.CrouchHold`, toggles `PostureState.Crouching` through
`StateMachineModule.TryTransitionPosture`, and runs slide off the hold. There is no ability path to
posture — that is channel 4 in `Movement_Ability_Interface.md`, which is not built.

So the `QuickslotCtrl` action exists in `PlayerInputControls.inputactions` **with no binding**. The
modifier bar's Ctrl slot renders, labels itself, accepts a drop and saves — it just has no key yet.
Crouch is untouched.

Three ways to close it, in preference order:

1. **Leave it.** Ctrl stays crouch; the slot is authoring-ready for when channel 4 lands and crouch
   becomes a slotted movement ability. Nothing to do.
2. **Bind `QuickslotCtrl` to `<Keyboard>/ctrl` and move `Crouch` to a free key.** Free on the
   keyboard right now: `g`, `h`, `j`, `k`, `l`, `b`, `n`, `m`, `t`, `y`, `u`, `o`, `p`, `caps`.
   Two drags in the Input Actions window, no code.
3. **Bind `QuickslotCtrl` and accept the double-fire.** Ctrl would crouch *and* fire the slot. Not
   recommended — it is exactly the kind of two-owners-one-key state the UI audit was written about.

---

## Files changed

| File | Change |
|---|---|
| `Hotbar/HotbarSaveData.cs` | `HotbarPageState.bars` list + `Find` / `MigrateNamedBars`; v3. |
| `Hotbar/HotbarSystem.cs` | `barDefinitions`; `ResolveBar` / `AllBars` / `EnsureBarsIn` off the list; `BarSizeConfig` → `BarDefinition`; `GetBarDefinitions()`, `HasBar()`. |
| `Hotbar/ActionBarView.cs` | `LMB` / `RMB` / `Shift` / `Ctrl` / `Q` key labels; `barId` tooltip. |
| `Input/InputSystem.cs` | `MouseLR` + `ShiftCtrlQ`; `BarKeybindRoute` list; per-key validation; per-route held time; `InputProfileSaveData` v2; route-driven debug GUI. |
| `PlayerInputControls.inputactions` | `QuickslotShift` → `<Keyboard>/shift`; `QuickslotCtrl` (unbound). |
| `HotbarDebugger.cs` | Iterates bar definitions instead of dumping three hardcoded names; prints the active page. |

Originals are backed up beside the sources as `*.bak` under `_Staging/FocusBars/_original/`.

⚠ **`BarSizeConfig` is gone.** It existed only to hold the three bar defaults. If anything outside
these files referenced it, that is the one compile error to expect.

⚠ **`PlayerInputControls.cs` regenerates on import.** The 84 KB wrapper is generated from the
`.inputactions` asset — `QuickslotShift` and `QuickslotCtrl` will not exist as properties until
Unity reimports it, so the first compile after the file lands may fail until the asset is picked up.

---

## Setup

1. Open `Base_PC`. `HotbarSystem` should list five bars — the three originals plus
   `mouse (2, 1)` and `modifier (3, 3)`. Add them if the prefab kept an empty list.
2. On the same prefab, `InputSystem` → Keybind Routing should list five routes.
   Confirm `modifier` is above `bottomLeft`; that ordering is what gives Q to the modifier bar.
3. Duplicate an existing `ActionBarView` object in the Zoo canvas twice.
   - Mouse bar: `barId` `mouse`, `GridLayoutGroup` constraint **Fixed Column Count**, count 2.
   - Modifier bar: `barId` `modifier`, constraint **Fixed Column Count**, count 1.
   The view overwrites `constraintCount` from `ColumnsPerRow` on refresh; setting it in the
   inspector only makes the editor preview match.
4. Any `HotbarPageDefinition` assets that should swap these bars per stance need `mouse` and
   `modifier` added to `suppliedBars`. Left out, they fall through to the default page — which is
   right for a bar that should stay put across stances.
5. Press R, drag abilities in, save, reload. Existing characters keep their bars.

---

## Decided

- **Bars are a list, not named fields.** A bar is data. The five names in the project today are
  authoring, not vocabulary.
- **Shape is `rowCount`, not a new view.** Horizontal, vertical and grid are the same component.
- **Key ownership is validated per physical key, first route wins.** Two sets wanting one key is a
  normal state, not an error, and must be caught rather than assumed away.
- **LMB and RMB belong to the hotbar.** Attack and Block were a parallel input path that never
  connected to anything. One path to abilities.
- **Press and hold stay separate fields.** `GetWasPressed` and `GetIsPressed` never merge, per the
  standing warning in `Movement_Ability_Interface.md`.

## Open

- **Ctrl.** The three options above. Nothing else in this pass is blocked on it.
- **Right-click clears a slot.** `ActionBarSlotView.OnPointerClick` clears on right-click, and RMB
  is now also a bar key. Harmless while the cursor is locked in gameplay and the Player map is
  disabled under a panel, but the mouse bar is the first bar where those two meanings meet.
- **`Consume` and `ToggleStance` are both bound to R.** Pre-existing, found while auditing the
  asset, not touched here.
- **No page-switch feedback**, **orphaned page state is never pruned**, **`AbilityPickerPanel`
  assigns into the active page only** — all still open from `Hotbar_Pages.md`, all now apply to
  five bars instead of three.
- **`BarSizeConfig` removal** — see the warning above.
