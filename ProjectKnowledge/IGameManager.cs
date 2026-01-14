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

    /// <summary>
    /// Combine multiple validation results
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var combined = Success();

        foreach (var result in results)
        {
            if (result.IsFatal)
                combined.IsFatal = true;

            if (result.Errors != null)
                combined.Errors.AddRange(result.Errors);

            if (result.Warnings != null)
                combined.Warnings.AddRange(result.Warnings);

            if (result.Info != null)
                combined.Info.AddRange(result.Info);
        }

        return combined;
    }

    /// <summary>
    /// Print validation result to console
    /// </summary>
    public void LogResult(string context)
    {
        if (IsFatal)
        {
            Debug.LogError($"[{context}] FATAL VALIDATION FAILURE");
        }

        if (Errors != null && Errors.Count > 0)
        {
            foreach (var error in Errors)
            {
                Debug.LogError($"[{context}] ERROR: {error}");
            }
        }

        if (Warnings != null && Warnings.Count > 0)
        {
            foreach (var warning in Warnings)
            {
                Debug.LogWarning($"[{context}] WARNING: {warning}");
            }
        }

        if (Info != null && Info.Count > 0)
        {
            foreach (var info in Info)
            {
                Debug.Log($"[{context}] INFO: {info}");
            }
        }

        if (IsValid && (Warnings == null || Warnings.Count == 0))
        {
            Debug.Log($"[{context}] Validation: PASSED");
        }
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

/// <summary>
/// Optional: Manager supports save/load snapshots (future use)
/// </summary>
public interface ISnapshotableManager : IGameManager
{
    ManagerSnapshot CaptureSnapshot();
    void RestoreSnapshot(ManagerSnapshot snapshot);
}

/// <summary>
/// Manager snapshot for save/load (future use)
/// </summary>
[Serializable]
public class ManagerSnapshot
{
    public string ManagerName;
    public int ConfigVersion;
    public byte[] Data;
}