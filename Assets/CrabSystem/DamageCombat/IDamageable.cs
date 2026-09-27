using UnityEngine;

/// <summary>
/// LEGACY INTERFACE - IDamageable
/// 
/// ⚠️ USAGE GUIDANCE:
/// 
/// For Brain-based entities (Player, NPCs):
///   âŒ DON'T use IDamageable
///   âœ… DO use brain.GetModule<DamageSystem>()
///   
/// For simple destructibles without Brain (crates, barrels, props):
///   âœ… DO implement IDamageable
///   Example: Destructible objects that don't need full combat stats
/// 
/// MIGRATION:
/// - Old: NPCDamageable (OBSOLETE - removed)
/// - New: DamageSystem (universal, works for all entities)
/// 
/// Example (Legacy - simple destructible):
///   public class BreakableCrate : MonoBehaviour, IDamageable
///   {
///       // ... implementation
///   }
/// 
/// Example (Modern - Brain entity):
///   var targetBrain = hitObject.GetComponent<ControllerBrain>();
///   if (targetBrain != null)
///   {
///       var damageSystem = targetBrain.GetModule<DamageSystem>();
///       damageSystem.TakeDamage(damagePacket);
///   }
/// 
/// Phase 1.7b: Marked as legacy, kept for backwards compatibility
/// </summary>
public interface IDamageable
{
    /// <summary>Take damage and return whether target survived</summary>
    bool TakeDamage(float damage, Vector3 source = default);

    /// <summary>Heal the target</summary>
    void Heal(float amount);

    /// <summary>Current health value</summary>
    float CurrentHealth { get; }

    /// <summary>Maximum health value</summary>
    float MaxHealth { get; }

    /// <summary>Health as percentage (0-1)</summary>
    float HealthPercentage { get; }
}
