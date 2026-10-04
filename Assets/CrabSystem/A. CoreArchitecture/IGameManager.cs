using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enhanced validation result supporting fatal errors, warnings, and info messages
/// Allows managers to report issues without blocking startup
/// </summary>
public struct ValidationResult
{
    /// <summary>
    /// Is this a fatal error that prevents startup?
    /// </summary>
    public bool IsFatal { get; set; }

    /// <summary>
    /// Fatal errors (prevent startup)
    /// </summary>
    public List<string> Errors { get; set; }

    /// <summary>
    /// Warnings (log but continue)
    /// </summary>
    public List<string> Warnings { get; set; }

    /// <summary>
    /// Informational messages
    /// </summary>
    public List<string> Info { get; set; }

    /// <summary>
    /// Is validation completely successful?
    /// </summary>
    public bool IsValid => !IsFatal && (Errors == null || Errors.Count == 0);

    /// <summary>
    /// Create a successful validation result
    /// </summary>
    public static ValidationResult Success()
    {
        return new ValidationResult
        {
            IsFatal = false,
            Errors = new List<string>(),
            Warnings = new List<string>(),
            Info = new List<string>()
        };
    }

    /// <summary>
    /// Create a fatal error result
    /// </summary>
    public static ValidationResult Fatal(string error)
    {
        return new ValidationResult
        {
            IsFatal = true,
            Errors = new List<string> { error },
            Warnings = new List<string>(),
            Info = new List<string>()
        };
    }

    /// <summary>
    /// Create a warning result
    /// </summary>
    public static ValidationResult Warning(string warning)
    {
        return new ValidationResult
        {
            IsFatal = false,
            Errors = new List<string>(),
            Warnings = new List<string> { warning },
            Info = new List<string>()
        };
    }
}

/// <summary>
/// Core interface for all game managers
/// ENHANCED VERSION with ValidationResult and explicit dependencies
/// </summary>
public interface IGameManager
{
    // Identity
    string ManagerName { get; }
    int InitializationPriority { get; }
    bool IsEnabled { get; }
    bool IsInitialized { get; }

    // Lifecycle
    void Initialize();
    void LateInitialize();
    void Shutdown();

    // Enhanced Validation (supports warnings vs errors)
    ValidationResult Validate();
}

/// <summary>
/// Optional: Declare explicit manager dependencies
/// ManagerBrain validates these against initialization priority
/// </summary>
public interface IManagerDependency
{
    /// <summary>
    /// Types of managers this manager depends on
    /// Must initialize BEFORE this manager
    /// </summary>
    IEnumerable<Type> DependsOn { get; }
}

/// <summary>
/// Optional: Manager supports per-frame updates
/// </summary>
public interface IUpdatableManager : IGameManager
{
    void UpdateManager();
}

/// <summary>
/// Optional: Manager supports runtime hot-reloading
/// </summary>
public interface IHotReloadable : IGameManager
{
    void HotReload();
}
