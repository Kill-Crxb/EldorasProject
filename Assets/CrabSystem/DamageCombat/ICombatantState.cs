// What one fighter may read about another mid-fight (Audit 5 A5, CrabSystem_Standard §3.4). The
// guard, hit states and the AI read an opponent through this instead of learning AbilitySystem's
// internals. Read-only: affecting another fighter still goes through its entry points
// (TakeDamage, StatusSystem.Apply, GuardModule.TakeParried).
//
// AbilitySystem implements it, forwarding the guard's half, so any brain answers
// brain.GetProvider<ICombatantState>().
public interface ICombatantState
{
    // The move in flight, or null.
    AbilityDefinition CurrentAbility { get; }

    // Frames since the move started, at 60 fps (the move clock); -1 when nothing is executing.
    int CurrentMoveFrame { get; }

    // Startup / Active / Recovery of a move with frame data; None otherwise. CF4 reads it for
    // counter-hits (hit in startup) and punishes (hit in recovery).
    MovePhase CurrentPhase { get; }

    bool IsArmored { get; }
    bool HitboxesLive { get; }

    // Seconds the hitboxes have been live this swing; 0 when they aren't.
    float HitboxesLiveFor { get; }

    bool IsGuarding { get; }
    bool InBlockstun { get; }
}
