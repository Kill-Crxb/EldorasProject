using UnityEngine;

/// <summary>
/// Value source that reads how fast an entity is actually moving, so conditions can be authored
/// against real speed instead of against a boolean somebody else already decided.
///
/// WHY THIS AND NOT StateValueSource:
/// StateValueSource.Sprinting answers "is the sprint state on", which is MovementSystem's own
/// hysteretic verdict — useful, but already decided. This returns the NUMBER, so a condition can
/// pick its own threshold in metres per second, which is what a designer can actually reason
/// about in the inspector. "HorizontalSpeed >= 11" is legible; "SpeedBlend >= 0.74" is not.
///
/// Do not use this to re-derive IsRunning or IsSprinting. Those facts are published directly by
/// MovementSystem, and a condition writing the same OutputFactKey would give the blackboard two
/// writers for one key. Use it for facts that do not exist yet — "MovingFastEnoughToVault",
/// "TooFastToCast".
/// </summary>
[CreateAssetMenu(fileName = "MovementValue",
                 menuName = "NinjaGame/Blackboard/Value Sources/Movement")]
public class MovementValueSource : ValueSourceDefinition
{
    [Header("Movement Query")]
    [Tooltip("Which movement quantity to read.")]
    [SerializeField] private MovementValueProperty property = MovementValueProperty.HorizontalSpeed;

    public override float GetValue(ControllerBrain brain)
    {
        if (brain == null) return 0f;

        MovementSystem movement = brain.Movement;
        if (movement == null) return 0f;

        switch (property)
        {
            case MovementValueProperty.HorizontalSpeed:
                return movement.Speed;

            case MovementValueProperty.VerticalSpeed:
                return movement.Velocity.y;

            case MovementValueProperty.SpeedBlend:
                return movement.SpeedBlend;

            case MovementValueProperty.Grounded:
                return movement.IsGrounded ? 1f : 0f;
        }

        return 0f;
    }

    public override string GetDisplayName() => $"Movement: {property}";
}

/// <summary>
/// Movement quantities a condition can be built from.
///
/// Appended, never inserted — serialized by index on every asset that uses it.
/// </summary>
public enum MovementValueProperty
{
    /// <summary>Speed across the ground in m/s, ignoring vertical motion.</summary>
    HorizontalSpeed,

    /// <summary>Signed vertical speed in m/s. Negative is falling.</summary>
    VerticalSpeed,

    /// <summary>0 at the run threshold, 1 at the sprint threshold, damped.</summary>
    SpeedBlend,

    /// <summary>1 when grounded, 0 in the air.</summary>
    Grounded,
}
