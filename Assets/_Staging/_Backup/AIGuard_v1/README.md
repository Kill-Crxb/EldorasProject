# AIGuard_v1 backups (30 Sep 2026)

Mirror guard, Shift status hotkeys, Cleave_Shove as a staggering heavy. Restore these and delete
`_Staging/AIGuard/` to undo the pass.

- `AIControlSource.cs.bak` — before `GuardHeld`, `GuardUntil`, `ReadThisSwing`.
- `AbilitySystem.cs.bak` — before `IsGuardHeld` read `AIControlSource.GuardHeld` on AI entities
  (this copy already has the HitStates_v1 changes).
- `Mirror_NPC.prefab.bak` — before its goal pool pointed at `Goal_FightAndGuard` instead of
  `Goal_FightTarget`.
- `StatusHotkeys.cs.bak` — before Shift aimed the keys at the nearest non-player.
- `Cleave_Shove.asset.bak` — before it got move data (Heavy, Stagger 40f).
