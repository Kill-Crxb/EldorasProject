using UnityEngine;

/// <summary>
/// Base class for polymorphic value sources in semantic conditions.
/// Enables data-driven queries from any game state source.
/// 
/// Architecture Pattern:
/// - Abstract base class (polymorphic)
/// - ScriptableObject (designer-friendly assets)
/// - GetValue() implemented by concrete types
/// 
/// Concrete Implementations:
/// - ResourceValueSource: Query ResourceDefinition (health %, mana, etc.)
/// - StatValueSource: Query StatSystem (armor, crit chance, etc.)
/// - StateValueSource: Query entity state (grounded, in combat, etc.)
/// - CompositeValueSource: Combine multiple sources (health% + mana%)
/// 
/// Reusability:
/// Same value source assets work across game genres:
/// - RPG: Health percentage
/// - Racing: Fuel percentage
/// - Platformer: Jump meter percentage
/// 
/// Usage (Designer Workflow):
/// 1. Create value source asset in Project window
/// 2. Configure source-specific settings
/// 3. Reference in BlackboardCondition
/// 4. Condition evaluates: GetValue(brain) < threshold
/// 
/// Example Assets:
/// Health_Percentage.asset → ResourceValueSource
///   resource: Health.asset
///   property: Percentage
/// 
/// Armor_Stat.asset → StatValueSource
///   statId: "combat.armor"
/// 
/// IsGrounded_State.asset → StateValueSource
///   stateProperty: Grounded
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
public abstract class ValueSourceDefinition : ScriptableObject
{
    /// <summary>
    /// Get current value from this source for the given entity.
    /// Returns float for numerical comparisons (0/1 for booleans).
    /// </summary>
    /// <param name="brain">Entity to query value from</param>
    /// <returns>Current value (0 if source unavailable)</returns>
    public abstract float GetValue(ControllerBrain brain);

    /// <summary>
    /// Get display name for inspector/debugging.
    /// Override for custom formatting.
    /// </summary>
    public virtual string GetDisplayName() => name;

    /// <summary>
    /// Validate this value source configuration.
    /// Override to check source-specific requirements.
    /// </summary>
    public virtual bool Validate(out string error)
    {
        error = null;
        return true;
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (!Validate(out string error))
        {
            Debug.LogWarning($"[{GetType().Name}] Validation failed: {error}", this);
        }
    }
#endif
}
