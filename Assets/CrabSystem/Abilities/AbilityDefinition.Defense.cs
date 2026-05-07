using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// AbilityDefinition - Defense Partial
/// Policy helpers for blocking, parrying, and counter-attack mechanics
/// </summary>
public partial class AbilityDefinition
{
    // ========================================
    // DEFENSIVE MECHANICS (AbilityType.Defensive)
    // ========================================

    [Header("Defense Mechanics (Defensive Abilities Only)")]
    [Tooltip("Damage reduction when blocking (0.7 = 70% reduction)")]
    [Range(0f, 1f)]
    public float blockDamageReduction = 0.5f;

    [Tooltip("Block arc in degrees (180 = half circle)")]
    public float blockAngle = 120f;

    [Tooltip("Perfect parry window duration (seconds from defense start)")]
    public float parryWindowDuration = 0.2f;

    [Tooltip("Damage reduction during parry window (1.0 = 100% blocked)")]
    [Range(0f, 1f)]
    public float parryDamageReduction = 1.0f;

    [Tooltip("Successful parry opens counter-attack window?")]
    public bool parryEnablesCounter = true;

    [Tooltip("Counter-attack window duration after successful parry")]
    public float counterWindowDuration = 1.0f;

    [Tooltip("Startup time before defense becomes active")]
    public float blockStartupTime = 0.1f;

    // ========================================
    // POLICY HELPERS (Zero-allocation, side-effect-free)
    // ========================================

    /// <summary>
    /// Can this ability block an incoming attack from the given direction?
    /// Pure decision helper - no side effects
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanBlock(float incomingAngle)
    {
        return abilityType == AbilityType.Defensive
            && blockDamageReduction > 0f
            && incomingAngle <= blockAngle * 0.5f;
    }

    /// <summary>
    /// Can this ability block an incoming attack from the given direction?
    /// Overload for Vector3 attack direction
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanBlock(Vector3 defenderForward, Vector3 attackDirection)
    {
        if (abilityType != AbilityType.Defensive) return false;
        if (blockDamageReduction <= 0f) return false;

        float angle = Vector3.Angle(defenderForward, -attackDirection);
        return angle <= blockAngle * 0.5f;
    }

    /// <summary>
    /// Is the defense in parry window (perfect block timing)?
    /// Pure timing query - no state modification
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanParry(float timeSinceDefenseStart)
    {
        return abilityType == AbilityType.Defensive
            && timeSinceDefenseStart >= blockStartupTime
            && timeSinceDefenseStart <= blockStartupTime + parryWindowDuration;
    }

    /// <summary>
    /// Get damage reduction multiplier for current defense state
    /// Returns: multiplier to apply to damage (0.5 = 50% reduction)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetDefenseMultiplier(float timeSinceDefenseStart, bool withinBlockAngle)
    {
        if (abilityType != AbilityType.Defensive) return 1f;
        if (!withinBlockAngle) return 1f;

        // Check if in parry window
        if (CanParry(timeSinceDefenseStart))
            return 1f - parryDamageReduction;

        // Regular block
        if (timeSinceDefenseStart >= blockStartupTime)
            return 1f - blockDamageReduction;

        // Still in startup - no defense yet
        return 1f;
    }

    /// <summary>
    /// Is this a defensive ability?
    /// </summary>
    public bool IsDefensive => abilityType == AbilityType.Defensive;
}