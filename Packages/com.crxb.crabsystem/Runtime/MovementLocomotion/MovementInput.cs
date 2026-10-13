using UnityEngine;
using System.Collections.Generic;

// Movement Input - Universal input structure
//
// Control sources provide this, handlers consume it.
// This is the contract between "who controls" and "how to move".
//
// All vectors are in world space (already transformed by control source).

namespace CrabSystem
{
    public struct MovementInput
    {
        // Movement direction in world space (-1 to 1 on X and Z axes)
        public Vector2 MoveDirection;

        // Look direction in world space (where entity should face)
        public Vector2 LookDirection;

        // Pressed this frame: start a jump. A discrete edge, so it is what jump BUFFERING keys off.
        // It is false again on the next frame and must never be used to ask "is jump still down".
        public bool Jump;

        // Jump is currently held. This is what variable jump height reads: releasing it cuts the
        // rising velocity. Reading the Jump edge here instead cuts every jump on the frame after
        // launch, which costs most of the height and reads as a broken jump rather than a bug.
        public bool JumpHold;

        // Should the entity dash?
        public bool Dash;

        // Pressed this frame: crouch, or start a slide. Which one is decided by SPEED at the moment
        // of the press, not by how long the key is held — a tap window cannot resolve until release,
        // which delays the crouch and starts the slide late, and the slide is the half already
        // losing the momentum it exists to spend.
        public bool Crouch;

        // Crouch is currently held. A slide runs while this is down; releasing ends it early.
        // Crouch itself is a toggle and does not need the hold.
        public bool CrouchHold;

        // Pressed this frame: swap the walk/run gait. A discrete edge, not a held state — the
        // handler owns which gait is current, this only asks it to flip.
        public bool ToggleGait;

        // Custom data for game-specific features (e.g., MilSim ADS, lean, stance)
        public Dictionary<string, object> CustomData;

        // Zero input (no movement, no actions)
        public static MovementInput Zero => new MovementInput
        {
            MoveDirection = Vector2.zero,
            LookDirection = Vector2.zero,
            Jump = false,
            JumpHold = false,
            Dash = false,
            Crouch = false,
            CrouchHold = false,
            ToggleGait = false,
            CustomData = new Dictionary<string, object>()
        };

        // Check if there is any movement input
        public bool HasMovementInput => MoveDirection.magnitude > 0.01f;

        // Check if there is any look input
        public bool HasLookInput => LookDirection.magnitude > 0.01f;
    }
}
