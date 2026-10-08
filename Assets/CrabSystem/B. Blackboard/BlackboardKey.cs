using UnityEngine;

/// <summary>
/// Hashed integer key for blackboard fact lookups.
/// Uses deterministic hashing for fast O(1) dictionary access without string allocations.
/// 
/// Architecture:
/// - Hash computed once at registration
/// - Integer comparison in hot paths (faster than string)
/// - Debug name preserved for inspector/logging
/// 
/// Performance:
/// - sizeof(int) = 4 bytes (vs string overhead)
/// - Hash comparison ~1-2 CPU cycles
/// - Zero allocations at runtime
/// 
/// Usage:
/// Code-generated constants:
///   BlackboardKey.IsWounded = 12345;
///   blackboard.GetBool(BlackboardKey.IsWounded);
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
public struct BlackboardKey
{
    public readonly int hash;
    public readonly string debugName;

    public BlackboardKey(string name)
    {
        debugName = name;
        hash = GenerateHash(name);
    }

    /// <summary>
    /// Generate deterministic hash from string key
    /// Uses .NET's GetHashCode (stable within session)
    /// </summary>
    private static int GenerateHash(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        return name.GetHashCode();
    }

    public override string ToString() => debugName ?? $"Hash_{hash}";
    
    public override int GetHashCode() => hash;
    
    public override bool Equals(object obj)
    {
        if (obj is BlackboardKey other)
            return hash == other.hash;
        return false;
    }

    public static bool operator ==(BlackboardKey a, BlackboardKey b) => a.hash == b.hash;
    public static bool operator !=(BlackboardKey a, BlackboardKey b) => a.hash != b.hash;

    // Well-known key hashes — use these instead of raw string hashes.
    // Add new keys here as facts are introduced; name must match the condition's OutputFactKey exactly.
    public static readonly int IsExecutingAbility = "IsExecutingAbility".GetHashCode();
    public static readonly int IsInvincible       = "IsInvincible".GetHashCode();
    public static readonly int IsBlocking         = "IsBlocking".GetHashCode();
    public static readonly int IsInConversation   = "IsInConversation".GetHashCode();
    public static readonly int IsUnarmed          = "IsUnarmed".GetHashCode();

    // Published directly by MovementSystem, not by a BlackboardCondition — see
    // MovementSystem.UpdateSpeedState. Do NOT author conditions with these OutputFactKeys or
    // SemanticBridgeSystem becomes a second writer and the two fight every frame.
    //
    // IsRunning / IsSprinting are GAIT facts (2026-09-27): the run or sprint gait AND moving. A
    // momentum burst at walk gait is not running; no sprint grant, no sprinting. Only a handler
    // with no gait (Gait.None) falls back to speed hysteresis. SpeedBlend stays measured speed.
    public static readonly int IsRunning          = "IsRunning".GetHashCode();
    public static readonly int IsSprinting        = "IsSprinting".GetHashCode();

    /// <summary>Float, 0 at the run threshold and 1 at the sprint threshold.</summary>
    public static readonly int SpeedBlend         = "SpeedBlend".GetHashCode();

    /// <summary>
    /// Sprint GRANTED — a tag any system may raise: an ability, a status, a stat gate. While it is
    /// up, the run gait is served at sprint speed; walking is unaffected, because a grant cannot
    /// turn a walk into a sprint. CannotSprint beats it: granted and denied is a run.
    ///
    /// Distinct from IsSprinting above, and deliberately so. This one is the GRANT — "has something
    /// given them permission to". IsSprinting is the RESULT — MovementSystem publishes it when the
    /// grant has made the gait Sprint and the character is actually moving. Collapsing them would
    /// make MovementSystem and the granting system two writers on one key.
    ///
    /// Single writer, like the keys above: whatever raises it also lowers it.
    /// </summary>
    public static readonly int SprintGranted      = "SprintGranted".GetHashCode();

    /// <summary>
    /// Double jump GRANTED — read by ParkourLocomotionHandler.TryAirJump, which adds one to the
    /// profile's base air-jump budget while it is up.
    ///
    /// Same grant shape as SprintGranted: intent, raised by whatever confers the capability
    /// (Featherfall, Windwalk, a talent) and lowered by that same thing. There is deliberately no
    /// measured counterpart — how many air jumps remain is handler state, not a fact, because
    /// nothing outside movement reads it and the register's own test is "name the system that
    /// reads it".
    /// </summary>
    public static readonly int DoubleJumpGranted  = "DoubleJumpGranted".GetHashCode();

    // Capability denials (2026-09-28). Facts default to false, so "not denied" is the right state
    // for a character with nothing on them. Each one names what it stops, not what caused it:
    // Stunned, Paralyzed and Frozen all raise CannotAct, and StatusSystem's claim counting keeps it
    // up until the last of them expires. Raised only by statuses (StatusSystem.ClaimFlags).
    //
    //   CannotAct     every ability, spell drawing and all movement; cancels the ability in flight
    //   CannotMove    horizontal movement and Movement abilities
    //   CannotJump    jump and air jump
    //   CannotSprint  the sprint gait — a grant cannot win against it
    //   CannotAttack  Physical abilities
    //   CannotCast    Spell abilities and element drawing (a drawn element fizzles)
    //   CannotBlock   Defense abilities
    //   CannotDodge   Movement abilities
    public static readonly int CannotAct          = "CannotAct".GetHashCode();
    public static readonly int CannotMove         = "CannotMove".GetHashCode();
    public static readonly int CannotJump         = "CannotJump".GetHashCode();
    public static readonly int CannotSprint       = "CannotSprint".GetHashCode();
    public static readonly int CannotAttack       = "CannotAttack".GetHashCode();
    public static readonly int CannotCast         = "CannotCast".GetHashCode();
    public static readonly int CannotBlock        = "CannotBlock".GetHashCode();
    public static readonly int CannotDodge        = "CannotDodge".GetHashCode();

    // AbilitySystem only, while an ability with castWhileMoving off plays. Kept apart from
    // CannotMove so the ability and the statuses never share a key — MovementSystem ORs them.
    public static readonly int MoveRooted         = "MoveRooted".GetHashCode();

    // AbilitySystem only, while an ability with useRootMotion on plays. The movement handler takes the
    // clip's travel (RootMotionRelay) only while it is up; the rest of the time root motion is dropped.
    public static readonly int RootMotionDriven   = "RootMotionDriven".GetHashCode();

    // AbilitySystem only, as a root-motion move starts: the share of the clip's travel it keeps (a float).
    public static readonly int RootMotionScale    = "RootMotionScale".GetHashCode();

    /// <summary>
    /// SpellcraftSystem only: a sequence is being entered, so the hands hold the sign pose between
    /// signs. Mirrored to the animator's IsDrawing bool by AnimatorFactBridge.
    /// </summary>
    public static readonly int IsDrawingSigns     = "IsDrawingSigns".GetHashCode();
}
