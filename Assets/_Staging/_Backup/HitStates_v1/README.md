# HitStates_v1 backups (30 Sep 2026)

Roadmap CF2, hit states. Restore these and delete `_Staging/HitStates/` to undo the pass.

- `StatusSystem.cs.bak` — before `Apply` took an optional `seconds` override (a hit state's
  length is set per hit, not by the asset).
- `DamageEffect.cs.bak` — before `Apply` returned the damage applied, and before `AverageDamage`.
- `WeaponHitbox.cs.bak` — before a hit summed its applied damage and called `ApplyHitState`.
- `AbilitySystem.cs.bak` — before `CurrentMoveFrame` / `IsArmored`, and before
  `CancelCurrentAbility` switched the hitboxes off. An interrupted swing never reaches
  HitboxEnd, so its blade stayed live with no current ability and landed 10-damage fallback hits.
