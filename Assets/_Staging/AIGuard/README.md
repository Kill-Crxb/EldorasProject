# Mirror guard + test kit (30 Sep 2026)

Three things to test alongside `_Staging/HitStates/` in one Zoo session.

## 1. The mirror blocks

| File | What |
|---|---|
| `FightAndGuardGoal.cs` | Fight Target plus a guard. When you start an attack within 3 m, it rolls once for that swing (50 %); on a success it stops, faces you and holds `Block` (BLK1) for 0.8 s instead of attacking. |
| `Goal_FightAndGuard.asset` | The tuning: same attack settings as `Goal_FightTarget`, plus guard ability, chance, hold, threat range. |

Edited in place: `AIControlSource` (`GuardHeld` — the AI's block key — and the goal's per-entity
memory), `AbilitySystem.IsGuardHeld` (reads `GuardHeld` on AI entities, RMB on the player).
`Mirror_NPC.prefab`'s goal pool now points at `Goal_FightAndGuard`.

⚠ `Tools → AI → Build Mirror NPC` puts `Goal_FightTarget` back. Re-drag `Goal_FightAndGuard` into
the GOAPModule goal pool if you rebuild.

Note: `Block` has a 5 s cooldown, so the mirror can guard at most once every 5 s.

## 2. Status hotkeys on the mirror

`_Staging/DebugTools/StatusHotkeys` — hold **Shift** with F5–F10 / F12 and they act on the nearest
non-player with a StatusSystem (logs `Target: <name>`). Without Shift, the player as before.
Put `Status_Staggered` / `Status_Flinched` (in `_Staging/HitStates/Resources/Statuses/HitStates/`)
in the list alongside the existing test statuses.

## 3. Cleave (Shove) staggers

`Database/Resources/AbilityDatabase/TestAbilities/Cleave_Shove.asset` now has move data: Heavy
(26 / 5 / 34), Stagger for 40 frames (scaled 20–60 by damage), block stamina 7, hit-stop 10.
Only the hit state reads it today; `Tools → Combat → Move Report` will show it and may flag
drift against the clip.

## Test

1. **Guard** — swing at the mirror repeatedly. About half your attacks it raises its guard; hits
   that land in the guard deal 0 and StatusTrace logs no `flinched` on it.
2. **Guard only in front** — circle behind it while it guards. Those hits should land and flinch it.
3. **Stagger breaks guard** — Cleave (Shove) it. A landed Shove logs `+staggered` (≈0.7 s) and it
   can't raise its guard until that ends. Blocked Shoves stagger nothing.
4. **F10 on the NPC** — Shift+F-key a stun or `Staggered` onto it mid-swing. The swing stops and no
   hit arrives.
5. **AI respects denials** — Shift+`Rooted`: it keeps facing you but can't walk; Shift+`Silenced` /
   `Crippled` ×3: no attacks.

## Undo

Restore the `.bak` files from `_Backup/AIGuard_v1/` and delete this folder. `.guids_tmp` in this
folder is a leftover scratch file (hidden from Unity) — safe to delete.
