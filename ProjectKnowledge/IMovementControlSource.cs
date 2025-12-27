using UnityEngine;

/// <summary>
/// Interface for pluggable movement control sources
/// 
/// Control sources provide input to the movement system.
/// They answer the question: "WHO is controlling this entity?"
/// 
/// Examples:
/// - PlayerControlSource: Reads keyboard/gamepad input
/// - AIControlSource: Reads PathfindingModule waypoints
/// - AdminControlSource: Admin possession for debugging
/// - NetworkControlSource: Reads network packets
/// - ScriptedControlSource: Follows predefined waypoint paths
/// </summary>
public interface IMovementControlSource
{
    /// <summary>
    /// Get movement input for this frame
    /// Called by MovementSystem every frame
    /// </summary>
    MovementInput GetMovementInput();

    /// <summary>
    /// Called when this control source becomes active
    /// Setup references, subscribe to events, etc.
    /// </summary>
    void OnActivated();

    /// <summary>
    /// Called when this control source is deactivated
    /// Cleanup, unsubscribe from events, etc.
    /// </summary>
    void OnDeactivated();

    /// <summary>
    /// Called every frame while this source is active
    /// Use for any per-frame updates the source needs
    /// </summary>
    void UpdateSource();

    /// <summary>
    /// Is this control source currently active?
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Name of this control source (for debugging/UI)
    /// </summary>
    string SourceName { get; }
}