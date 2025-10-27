using UnityEngine;

/// <summary>
/// Handles defensive mechanics like blocking and parrying.
/// Implemented by adapters that bridge to specific defense systems.
/// 
/// Design:
/// - Decouples damage system from specific defense implementations
/// - Processes incoming damage through defensive mechanics
/// - Returns modified damage values based on defensive state
/// </summary>
public interface IDefenseProvider
{
    /// <summary>
    /// Process incoming damage through defensive mechanics.
    /// Returns the final damage amount after defense calculations.
    /// </summary>
    float ProcessIncomingDamage(float damage, Vector3 attackDirection);

    /// <summary>Is this entity currently blocking?</summary>
    bool IsBlocking();

    /// <summary>Is this entity currently in parry window?</summary>
    bool IsParrying();

    /// <summary>Can this entity perform defensive actions right now?</summary>
    bool CanDefend();
}