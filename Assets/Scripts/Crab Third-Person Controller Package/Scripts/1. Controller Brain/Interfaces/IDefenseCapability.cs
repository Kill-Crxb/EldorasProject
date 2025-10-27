using UnityEngine;

/// <summary>
/// Interface for modules that provide defensive capabilities.
/// Allows the damage system to discover defense mechanics without knowing specific implementations.
/// 
/// Design:
/// - Enables modular defense discovery (any module can provide defense)
/// - No coupling between damage system and specific combat modules
/// - Multiple modules can provide defensive capabilities simultaneously
/// 
/// Usage Example:
/// - ActiveDefenseModule implements this for blocking/parrying
/// - ShieldModule could implement this for shield-specific defense
/// - ArmorModule could implement this for passive armor effects
/// </summary>
public interface IDefenseCapability
{
    /// <summary>Is this entity currently blocking?</summary>
    bool IsBlocking { get; }

    /// <summary>Is this entity currently in parry window?</summary>
    bool IsParrying { get; }

    /// <summary>Can this entity perform defensive actions right now?</summary>
    bool CanDefend { get; }

    /// <summary>
    /// Process incoming damage through defensive mechanics.
    /// Returns the damage multiplier (0.0 = full block, 1.0 = no reduction).
    /// </summary>
    /// <param name="attackDirection">Direction the attack is coming from</param>
    /// <returns>Damage multiplier after defense calculations</returns>
    float GetDefensiveMultiplier(Vector3 attackDirection);
}