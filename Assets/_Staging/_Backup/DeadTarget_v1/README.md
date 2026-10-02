# DeadTarget_v1 backups (1 Oct 2026)

Two bugs from the 1 Oct mirror play-test.

- `PerceptionModule.cs.bak` — before the scan skipped dead brains and memory stopped holding a dead
  target. The mirror kept swinging at the player's corpse.
- `AbilityDefinition.HitState.cs.bak` — before `ApplyHitState` skipped a target the same hit had
  killed (the killing blow flinched the corpse).
- `FightAndGuardGoal.cs.bak` — before the guard only committed when Block was ready (a guard on
  cooldown left the mirror standing still).
- `GOAPModule.cs.bak` — before a dead entity cleared its goal. The mirror's corpse kept chasing
  and swinging.
- `ResourceSystem.cs.bak` — before regen stopped at zero health. Health crept back from the
  Regeneration pool after its 5 s delay, `IsAlive()` turned true, and the corpse's AI started again
  (still unkillable — `DamageSystem` stays dead).
