/// <summary>
/// Interface for identity handlers that support level tracking.
/// This decouples RPGSystem from concrete identity handler implementations.
/// 
/// Phase 1.7b: Universal systems architecture
/// Created: January 09, 2026
/// 
/// Why This Interface Exists:
/// - RPGSystem needs to sync level to IdentityHandler
/// - IdentityHandler type varies (PlayerIdentityHandler, NPCIdentityHandler, UniversalIdentityHandler)
/// - This interface provides a universal contract for level access
/// 
/// Implementing Classes:
/// - UniversalIdentityHandler (current)
/// - Any future identity handlers that track levels
/// 
/// Usage in RPGSystem:
///   private IIdentityLevel identityHandler;
///   identityHandler = identitySystem.Identity as IIdentityLevel;
///   identityHandler.Level = newLevel; // Safe, no casting errors
/// </summary>
public interface IIdentityLevel
{
    /// <summary>
    /// Current level of this entity (1-30 typically)
    /// RPGSystem writes to this, other systems read from it
    /// </summary>
    int Level { get; set; }
}