# Decisions

Settled choices, with the reason and the case that settled them. Reopen one only with a new reason, not because the old one was forgotten.

| Decision | Why | Precedent |
|---|---|---|
| **Auto-discovery is the one wiring path.** Modules sign up by being under a brain; order is `InitOrder`. | Inspector lists drifted from the hierarchy and broke silently when a module was added or removed. | 4 Oct 2026: brain lists replaced by discovery. |
| **Animation events are the authority for move timing.** Frame data is baked from clips. | Two copies of the same timing (typed frames and clip events) fell out of step the first day they existed. | 5 Oct 2026 reversed the 4 Oct "frames are the clock" rule. |
| **One clock per character** (`brain.Clock`). | Mixed `Time.time` and module timers made action speed, pause and server ticks impossible to add in one place. | 12–13 Oct 2026, code review pass B. |
| **Physics-step movement and the camera stay on `Time`.** | Movement timers share the fixed step with its velocities; splitting them across clocks breaks one simulation in two. The camera belongs to the viewer. | Pass B exemption, recorded in `EntityClock` and the validator. |
| **One random source** (`DiceRoll`, seedable). | Replays and a server need every roll from one place. | Pass B: AI, loot, spawns, spread and creation moved onto it. |
| **Fact keys hash with FNV-1a** (`BlackboardKey.Hash`). | `string.GetHashCode` is randomised per process in .NET, so hashes didn't survive a restart or another machine. | Pass B. |
| **No engine calls in field initialisers.** | Unity forbids `Time`, `Application` and similar during construction. | 13 Oct `EntityClock` bug: built in a field initialiser, threw on load. Now built in `Awake`. |
| **One writer per fact, one owner per saved fact.** | Two writers produce flicker and load-order bugs that only show in play. | Level, name and model each re-applied from two places before Audit 2. |
| **Ids are renamed through `formerIds`.** | A rename without a map orphans every asset and save that used the old id. | The `SecondryStats` incident: a renamed stat silently read as zero everywhere it was used. |
| **Damage types are assets** (`DamageTypeDefinition`). | An enum meant a code change per type, and a game-specific list inside the framework. | Pass C / G1: 38 assets migrated from enum ints to ids. |
| **ScriptableObjects are static.** | A mutated SO keeps its play-mode changes in the editor and leaks state between characters. | Standing rule since Audit 1. |
| **Dice, not percentages.** | Flat modifiers and die sizes stay readable and bounded; stacked multipliers don't. | Stat model, CF series. |
| **Restrict by tuning; deny with capability facts.** | Disabling components left modules half-wired and was invisible to other systems. | `CannotAct` / `CannotMove` replaced ad-hoc bools. |
| **`GameEvents` only for game-wide moments and UI requests.** | A global bus for per-character events made every listener filter by sender, and hid ownership. | Pass D: chests and vendors raise `ContainerOpenRequested` instead of calling the window manager. |
| **The cursor belongs to the viewer**, asked through `UIPanelRegistry.CursorWanted`. | It isn't a fact about any character. | Pass D. |
| **Diagnostics live in debugger components**, not flags on gameplay code. | Debug flags and log chatter buried real errors and cost time in play. | Pass E2 log sweep. |
| **The framework never references paid or third-party assets.** | The package must install into a project that doesn't own them. | Feel and Magica integrations live game-side. |
| **Framework defaults name no game content.** | A default socket or folder name from one game is a silent bug in the next. | Socket names moved to model prefabs; editor folders moved to Project Settings. |
| **Roll back over debugging mixed state.** | When a change leaves a scene or prefab half-migrated, restoring from git and redoing it cleanly is faster than untangling it. | Several prefab migrations in Oct 2026. |
| **The package is published from the project it's developed in.** | That project's history is large; the package repo carries one commit per version. | `Tools/publish-crabsystem.sh`, v0.2.0. |
