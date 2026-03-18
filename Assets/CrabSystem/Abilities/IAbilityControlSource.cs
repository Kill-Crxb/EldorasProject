using UnityEngine;

/// <summary>
/// Interface for pluggable ability control sources
/// 
/// Control sources provide ability input to the ability system.
/// They answer the question: "WHAT ability should be used right now?"
/// 
/// Examples:
/// - PlayerAbilityControlSource: Reads keyboard/gamepad input for player abilities
/// - AIAbilityControlSource: Reads GOAP decisions for NPC abilities
/// - TestAbilityControlSource: Scripted ability sequences for testing
/// - NetworkAbilityControlSource: Reads network packets for multiplayer
/// 
/// Architecture:
/// This mirrors IMovementControlSource - proven pattern from MovementSystem.
/// Each frame, the active control source is polled for which slot to trigger.
/// </summary>
public interface IAbilityControlSource
{
    /// <summary>
    /// Get ability input for this frame.
    /// Returns the slot key to trigger ("BasicAttack", "Q", "Z", etc.) or null if nothing.
    /// Called by AbilityLoadoutModule every frame.
    /// </summary>
    string GetAbilitySlotToTrigger();

    /// <summary>
    /// Called when this control source becomes active.
    /// Setup references, subscribe to events, etc.
    /// </summary>
    void OnActivated();

    /// <summary>
    /// Called when this control source is deactivated.
    /// Cleanup, unsubscribe from events, etc.
    /// </summary>
    void OnDeactivated();

    /// <summary>
    /// Called every frame while this source is active.
    /// Use for any per-frame updates the source needs.
    /// </summary>
    void UpdateSource();

    /// <summary>
    /// Is this control source currently active?
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Name of this control source (for debugging/UI).
    /// </summary>
    string SourceName { get; }
}