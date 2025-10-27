using UnityEngine;
/// <summary>
/// Marker interface for coordinator modules.
/// Coordinators provide logical grouping and convenience APIs for related modules.
/// 
/// Design Principles:
/// - Coordinators are discovered like any other IBrainModule
/// - They provide convenience methods for common operations
/// - Child modules remain independently accessible via Brain
/// - Coordinators are optional - modules work fine without them
/// 
/// Examples:
/// - CombatCoordinator: Groups MeleeModule, WeaponModule, DamageOut/In
/// - DamageCoordinator: Groups DamageOut, DamageIn, and adapters
/// - MovementCoordinator: Groups movement-related modules
/// - RPGSystemsCoordinator: Groups all RPG progression systems
/// </summary>
public interface ISystemCoordinator : IBrainModule
{
    // Marker interface - no additional methods required
    // Coordinators implement IBrainModule like any other module
    // They just provide logical grouping and convenience methods
}