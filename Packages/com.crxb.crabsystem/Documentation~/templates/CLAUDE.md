# <GameName>

<One paragraph: genre, camera, art style, multiplayer plans. Example: a first-person shooter with a painterly look, single player first, written so a server can own it later.>

Built on **CrabSystem** (`com.crxb.crabsystem`), installed by git URL. Read its docs before changing gameplay code:

- In this project: `Library/PackageCache/com.crxb.crabsystem@*/Documentation~/` (`index.md` first).
- On GitHub: https://github.com/Kill-Crxb/CrabSystem/tree/main/Documentation~

`Architecture.md` explains brains, modules, managers, facts, the clock and ids. `Systems.md` lists every module. `Standard.md` is the rule set and review checklist. `Decisions.md` holds settled choices; don't reopen one without a new reason.

## Where things live

- `Packages/com.crxb.crabsystem/` (or the PackageCache copy): the framework. Read-only here unless embedded (see below).
- `Assets/<GameName>/`: game code, data, prefab variants, scenes.
- `Assets/ThirdParty/`: downloaded packs, git-ignored where the licence requires it.
- `ProjectSettings/CrabSystemSettings.asset`: the folders CrabSystem's editor tools use.

## Working rules

- Every system answers the six questions in `Standard.md`. New gameplay is a module under a brain, tuned by assets, talking through events, facts and entry points.
- Code style: no `#region`, no `///`, no history comments. Comments explain why. Keep code flat: guard clauses and early returns, no local functions, no repeated helpers, no pass-through layers. Avoid generic AI-style boilerplate.
- Production code logs only `LogError` for broken setup. Diagnostics go in debugger components.
- Rolls use `DiceRoll`; gameplay timers use `brain.Clock`; ids come from constants classes or `[IdRef]` fields.
- Game code never needs a CrabSystem change to compile. If a feature needs one, it's a framework change (below).

## Changing CrabSystem

CrabSystem is developed embedded in NinjaGame and published from there (`Tools/publish-crabsystem.sh`, tag `v<version>`). Two ways to change it from this project:

1. **Small or occasional:** make the change in NinjaGame's copy, bump the version, publish, and update the tag in this project's `manifest.json`.
2. **A run of framework work:** clone https://github.com/Kill-Crxb/CrabSystem into `Packages/com.crxb.crabsystem/` here (an embedded package overrides the git one) and remove the git line. Before NinjaGame publishes again, copy the changes back into NinjaGame's copy, because publishing replaces the repo's contents.

Framework changes must stay game-neutral: no game content in defaults, no game folder names, no third-party references.

## Checking work

- Compile check outside the editor: `Documentation~/tools/build.py` in the package (`PROJ`, `UNITY_DATA`, targets `rt ed game gameed tests`). Run after any wide change.
- `Tools > CrabSystem > Validate` and `Validate Ids` after changes to ids, data or code style.
- Edit-mode tests: Window > General > Test Runner > EditMode (needs `"testables": ["com.crxb.crabsystem"]` in `manifest.json`).
- Play-test before calling anything done; the editor console log is the record.

## Git

- Commit as `Kill-Crxb <k1llcrabo@gmail.com>`. Crxb pushes himself.
- Don't commit editor churn Unity rewrites on its own (ProBuilder settings, TMP fallback atlases).

## FPS notes

<Keep this section current. Starting points from `GettingStarted.md`:>
- Not built yet in CrabSystem: a first-person camera module, hitscan weapons, rate-based fire timing.
- Third-person melee modules (`GuardModule`, `PostureModule`, `MovesetModule`, `TargetingModule`, parkour) are optional; leave them off the character base unless used.
- The toon shaders in `Runtime/Rendering` aren't used unless a material picks them.
