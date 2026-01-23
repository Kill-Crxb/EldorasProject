using UnityEngine;

/// <summary>
/// Value source that queries entity state flags (grounded, in combat, etc.)
/// Converts boolean states to float (0/1) for comparison operations.
/// 
/// Use Cases:
/// - Grounded state for "CanJump" condition
/// - In combat state for "InCombat" condition
/// - Stunned state for "IsStunned" condition
/// 
/// State Properties:
/// - Grounded: Entity feet touching ground
/// - InCombat: Recently dealt/received damage
/// - Stunned: Unable to act (from status effect)
/// - Blocking: Active defense state
/// - Sprinting: Movement speed boost active
/// 
/// Boolean to Float Conversion:
/// true → 1.0
/// false → 0.0
/// 
/// Example Configuration:
/// IsGrounded_State.asset:
///   stateProperty: Grounded
///   
/// InCombat_State.asset:
///   stateProperty: InCombat
/// 
/// Usage in Condition:
/// BlackboardCondition "CanJump":
///   valueSource: IsGrounded_State.asset
///   comparison: Equals
///   threshold: 1.0 (true)
///   outputFactKey: "CanJump"
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
[CreateAssetMenu(fileName = "StateValue", menuName = "NinjaGame/Blackboard/Value Sources/State")]
public class StateValueSource : ValueSourceDefinition
{
    [Header("State Query")]
    [Tooltip("Which state property to query")]
    [SerializeField] private StateProperty stateProperty;

    public override float GetValue(ControllerBrain brain)
    {
        // Guard clauses (flat logic)
        if (brain == null) return 0f;

        // Query appropriate state property
        bool state = false;

        switch (stateProperty)
        {
            case StateProperty.Grounded:
                state = brain.IsGrounded;
                break;

            case StateProperty.InCombat:
                // TODO: Query combat state module when implemented
                state = false;
                break;

            case StateProperty.Stunned:
                // TODO: Query status effect module when implemented
                state = false;
                break;

            case StateProperty.Blocking:
                // TODO: Query active defense module when implemented
                state = false;
                break;

            case StateProperty.Sprinting:
                if (brain.Movement != null)
                {
                    // TODO: Query sprint state when MovementSystem exposes it
                    state = false;
                }
                break;

            case StateProperty.Alive:
                if (brain.Health != null)
                {
                    state = brain.Health.IsAlive();
                }
                break;

            default:
                state = false;
                break;
        }

        // Convert bool to float (0.0 or 1.0)
        return state ? 1f : 0f;
    }

    public override string GetDisplayName()
    {
        return $"State: {stateProperty}";
    }

    public override bool Validate(out string error)
    {
        // State properties are enum-based, always valid
        error = null;
        return true;
    }
}

/// <summary>
/// Available entity state properties
/// </summary>
public enum StateProperty
{
    Grounded,   // Feet touching ground
    InCombat,   // Recently dealt/received damage
    Stunned,    // Unable to act
    Blocking,   // Active defense
    Sprinting,  // Movement boost
    Alive       // Health > 0
}
