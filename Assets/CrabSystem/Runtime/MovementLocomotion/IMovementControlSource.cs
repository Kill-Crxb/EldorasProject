using UnityEngine;

// Interface for pluggable movement control sources
//
// Control sources provide input to the movement system.
// They answer the question: "WHO is controlling this entity?"
//
// Examples:
// - PlayerControlSource: Reads keyboard/gamepad input
// - AIControlSource: Reads PathfindingModule waypoints
// - AdminControlSource: Admin possession for debugging
// - NetworkControlSource: Reads network packets
// - ScriptedControlSource: Follows predefined waypoint paths

namespace CrabSystem
{
    public interface IMovementControlSource
    {
        // Get movement input for this frame
        // Called by MovementSystem every frame
        MovementInput GetMovementInput();

        // Called when this control source becomes active
        // Setup references, subscribe to events, etc.
        void OnActivated();

        // Called when this control source is deactivated
        // Cleanup, unsubscribe from events, etc.
        void OnDeactivated();

        // Called every frame while this source is active
        // Use for any per-frame updates the source needs
        void UpdateSource();

        // Is this control source currently active?
        bool IsActive { get; }

        // Name of this control source (for debugging/UI)
        string SourceName { get; }
    }
}
