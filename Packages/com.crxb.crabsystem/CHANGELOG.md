# Changelog

## 0.2.1 — 2026-10-13

- `Documentation~`: getting started, architecture, every system, the standard, design decisions, a genre fit table, a `CLAUDE.md` template for game projects and an outside-the-editor compile check.

## 0.2.0 — 2026-10-13

- Moved to `Packages/com.crxb.crabsystem` as a UPM package, installable by git URL.
- Project Settings > CrabSystem: the folders editor tools read and write, per project.
- Dashboard: Project Setup tab (requirements check with fixes), Health, Tools, Weapons.
- Edit-mode tests (`Tests/EditMode`).
- Damage types are `DamageTypeDefinition` assets (`IdKind.DamageType`).
- One entity clock for gameplay timers; one seeded random source (`DiceRoll`); blackboard keys hash with FNV-1a.
- Gameplay asks the UI through `GameEvents` and `UIPanelRegistry`.
- `ItemSystem`, `TargetLockModule`, `AbilityLoadoutModule` and other dead code removed.

## 0.1.0 — 2026-10-13

- First package layout: `Runtime/` and `Editor/` assemblies, `CrabSystem` namespace, starter prefabs.
