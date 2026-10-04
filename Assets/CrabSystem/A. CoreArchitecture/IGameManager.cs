using System;
using System.Collections.Generic;

// What a manager reports from Validate. Fatal stops nothing on its own; ManagerBrain logs it as an
// error so a broken setup is loud without blocking the other managers.
public struct ValidationResult
{
    public bool IsFatal { get; set; }
    public List<string> Errors { get; set; }
    public List<string> Warnings { get; set; }
    public List<string> Info { get; set; }

    public bool IsValid => !IsFatal && (Errors == null || Errors.Count == 0);

    public static ValidationResult Success() => Make(false, null, null);
    public static ValidationResult Fatal(string error) => Make(true, error, null);
    public static ValidationResult Warning(string warning) => Make(false, null, warning);

    private static ValidationResult Make(bool fatal, string error, string warning)
    {
        return new ValidationResult
        {
            IsFatal = fatal,
            Errors = error != null ? new List<string> { error } : new List<string>(),
            Warnings = warning != null ? new List<string> { warning } : new List<string>(),
            Info = new List<string>()
        };
    }
}

// A game-wide manager. Lives under ManagerBrain, which initialises managers in
// InitializationPriority order (lower first) and shuts them down in reverse.
public interface IGameManager
{
    string ManagerName { get; }
    int InitializationPriority { get; }
    bool IsEnabled { get; }
    bool IsInitialized { get; }

    void Initialize();
    void LateInitialize();
    void Shutdown();
    ValidationResult Validate();
}

// Managers this one needs initialised first; ManagerBrain checks them against the priorities.
public interface IManagerDependency
{
    IEnumerable<Type> DependsOn { get; }
}

public interface IUpdatableManager : IGameManager
{
    void UpdateManager();
}

// Reloads definitions in place; called by ManagerBrain.HotReloadAll.
public interface IHotReloadable : IGameManager
{
    void HotReload();
}
