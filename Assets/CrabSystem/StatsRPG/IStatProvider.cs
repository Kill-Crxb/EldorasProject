using System;

/// <summary>
/// Universal stat provider interface
/// Allows different stat implementations (local, networked, cached)
/// 
/// Implementations:
/// - StatSystem: Local stat calculation (single-player, server)
/// - NetworkStatCache: Client-side read-only cache (multiplayer client)
/// - MockStatProvider: Testing/debugging
/// 
/// Phase 1.8: Server-Ready Architecture
/// </summary>
public interface IStatProvider
{
    /// <summary>
    /// Get final calculated stat value
    /// </summary>
    float GetValue(string statId, float defaultValue = 0f);

    /// <summary>
    /// Get base stat value (before modifiers)
    /// </summary>
    float GetBaseValue(string statId, float defaultValue = 0f);

    /// <summary>
    /// Set base stat value
    /// NOTE: Clients should NOT call this in multiplayer!
    /// </summary>
    void SetBaseValue(string statId, float value);

    /// <summary>
    /// Check if stat exists
    /// </summary>
    bool HasStat(string statId);

    /// <summary>
    /// Fired when a stat value changes
    /// Args: (statId, oldValue, newValue)
    /// </summary>
    event Action<string, float, float> OnStatChanged;
}