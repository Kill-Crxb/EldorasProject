// IDefenseProvider removed.
// Block/parry logic now lives in AbilitySystem.HandleDamageIntercept.
// DamageSystem fires OnDamageIntercept when the target is blocking (BlackboardKey.IsBlocking).
// Invincibility is checked via BlackboardKey.IsInvincible before damage is applied.
