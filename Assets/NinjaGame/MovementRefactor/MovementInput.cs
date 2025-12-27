using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Movement Input - Universal input structure
/// 
/// Control sources provide this, handlers consume it.
/// This is the contract between "who controls" and "how to move".
/// 
/// All vectors are in world space (already transformed by control source).
/// </summary>
public struct MovementInput
{
    /// <summary>
    /// Movement direction in world space (-1 to 1 on X and Z axes)
    /// </summary>
    public Vector2 MoveDirection;

    /// <summary>
    /// Look direction in world space (where entity should face)
    /// </summary>
    public Vector2 LookDirection;

    /// <summary>
    /// Should the entity sprint?
    /// </summary>
    public bool Sprint;

    /// <summary>
    /// Should the entity jump?
    /// </summary>
    public bool Jump;

    /// <summary>
    /// Should the entity dash?
    /// </summary>
    public bool Dash;

    /// <summary>
    /// Custom data for game-specific features (e.g., MilSim ADS, lean, stance)
    /// </summary>
    public Dictionary<string, object> CustomData;

    /// <summary>
    /// Zero input (no movement, no actions)
    /// </summary>
    public static MovementInput Zero => new MovementInput
    {
        MoveDirection = Vector2.zero,
        LookDirection = Vector2.zero,
        Sprint = false,
        Jump = false,
        Dash = false,
        CustomData = new Dictionary<string, object>()
    };

    /// <summary>
    /// Check if there is any movement input
    /// </summary>
    public bool HasMovementInput => MoveDirection.magnitude > 0.01f;

    /// <summary>
    /// Check if there is any look input
    /// </summary>
    public bool HasLookInput => LookDirection.magnitude > 0.01f;
}