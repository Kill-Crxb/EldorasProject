using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// AbilityDefinition - Chaining Partial
/// Policy helpers for combo system and ability chaining
/// </summary>
public partial class AbilityDefinition
{
    // ========================================
    // CHAINING (Combo System)
    // ========================================

    [Header("Chaining")]
    [Tooltip("Next ability in combo chain (optional)")]
    public AbilityDefinition nextInChain;

    [Tooltip("Time window after ComboWindowStart event to chain to next ability")]
    public float chainWindow = 0.5f;

    // ========================================
    // POLICY HELPERS (Zero-allocation, side-effect-free)
    // ========================================

    /// <summary>
    /// Can this ability chain to the next in sequence?
    /// Pure decision helper - checks if combo window is still open
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanChain(float timeSinceComboWindowStart)
    {
        return nextInChain != null
            && timeSinceComboWindowStart <= chainWindow;
    }

    /// <summary>
    /// Is this ability part of a combo chain?
    /// </summary>
    public bool HasNextInChain => nextInChain != null;

    /// <summary>
    /// Is this a chainable ability?
    /// Alias for clarity in different contexts
    /// </summary>
    public bool IsChainable => nextInChain != null;
}