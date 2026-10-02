# Block_v2 (29 Sep 2026)

`AbilitySystem.cs.bak` — before the guard became a hold. Defensive abilities no longer become
`currentAbility`, play a trigger or wait on `AnimUnlocked` (Known Issues B11: the guard timed out
on its safety timer). The guard lasts while the Block action is held; any hit inside the
ability's `blockAngle` from the front is blocked outright (damage 0). The time-in-guard parry
window is gone — parry is LMB-while-blocking in CF3.
