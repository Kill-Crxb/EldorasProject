using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Root of the game-wide managers. Like ControllerBrain for modules: a manager signs up by being a
// child, is initialised in InitializationPriority order, and is reached through GetManager<T>
// (stateful managers) or its own static Instance (read-only definition databases).
public class ManagerBrain : MonoBehaviour
{
    [Tooltip("Persist managers across scene loads")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Tooltip("Initialize managers on Awake")]
    [SerializeField] private bool autoInitialize = true;

    // Set in Awake. No scene search: anything that runs before the brain wakes gets null and
    // must cope, which is what it already got from an uninitialised registry.
    private static ManagerBrain instance;
    public static ManagerBrain Instance => instance;

    private readonly List<IGameManager> allManagers = new List<IGameManager>();
    private readonly List<IUpdatableManager> updatableManagers = new List<IUpdatableManager>();
    private readonly Dictionary<Type, IGameManager> managerRegistry = new Dictionary<Type, IGameManager>();
    private bool isShuttingDown;

    public bool IsInitialized { get; private set; }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[ManagerBrain] Duplicate ManagerBrain on {gameObject.name}; destroying it.", this);
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
        if (autoInitialize) InitializeManagers();
    }

    void Update()
    {
        if (!IsInitialized || isShuttingDown) return;
        for (int i = 0; i < updatableManagers.Count; i++)
            if (updatableManagers[i].IsEnabled) updatableManagers[i].UpdateManager();
    }

    // Also fires when play mode stops, which is what lets SaveManager save on quit.
    void OnApplicationQuit()
    {
        ShutdownManagers();
    }

    void OnDestroy()
    {
        if (instance == this) ShutdownManagers();
    }

    public void InitializeManagers()
    {
        if (IsInitialized) return;

        DiscoverManagers();
        ValidateManagerDependencies();

        var ordered = allManagers.Where(m => m.IsEnabled).OrderBy(m => m.InitializationPriority).ToList();
        foreach (var manager in ordered) Run(manager, manager.Initialize, "initialize");
        foreach (var manager in ordered) Run(manager, manager.LateInitialize, "late-initialize");

        ValidateManagers();
        IsInitialized = true;
    }

    public void ShutdownManagers()
    {
        if (isShuttingDown) return;
        isShuttingDown = true;

        foreach (var manager in allManagers.OrderByDescending(m => m.InitializationPriority))
            Run(manager, manager.Shutdown, "shut down");
    }

    // One manager failing must not stop the rest from starting or saving, so each call is caught,
    // and logged in full.
    private void Run(IGameManager manager, Action step, string what)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ManagerBrain] {manager.ManagerName} failed to {what}: {ex}", this);
        }
    }

    // Discovery is the one wiring path: a manager signs up by being a child of the brain (AU16).
    private void DiscoverManagers()
    {
        allManagers.Clear();
        updatableManagers.Clear();
        managerRegistry.Clear();

        foreach (var manager in GetComponentsInChildren<IGameManager>(true))
        {
            Type managerType = manager.GetType();
            if (managerRegistry.TryGetValue(managerType, out IGameManager existing))
            {
                Debug.LogError($"[ManagerBrain] Two {managerType.Name}s ({existing.ManagerName}, {manager.ManagerName}); using the first.", this);
                continue;
            }

            managerRegistry[managerType] = manager;
            allManagers.Add(manager);
            if (manager is IUpdatableManager updatable) updatableManagers.Add(updatable);
        }
    }

    public T GetManager<T>() where T : class, IGameManager
    {
        if (managerRegistry.TryGetValue(typeof(T), out IGameManager manager))
            return manager as T;

        Debug.LogWarning($"[ManagerBrain] No {typeof(T).Name} registered.", this);
        return null;
    }

    private void ValidateManagerDependencies()
    {
        foreach (var manager in allManagers)
        {
            if (!(manager is IManagerDependency dependent)) continue;

            foreach (var dependencyType in dependent.DependsOn)
                CheckDependency(manager, dependencyType);
        }
    }

    private void CheckDependency(IGameManager manager, Type dependencyType)
    {
        if (!managerRegistry.TryGetValue(dependencyType, out IGameManager dependency))
        {
            Debug.LogError($"[ManagerBrain] {manager.ManagerName} depends on {dependencyType.Name}, which isn't registered.", this);
            return;
        }

        if (dependency.InitializationPriority >= manager.InitializationPriority)
            Debug.LogError($"[ManagerBrain] {manager.ManagerName} (priority {manager.InitializationPriority}) depends on {dependency.ManagerName} (priority {dependency.InitializationPriority}); the dependency needs the lower number.", this);
    }

    // The one boot line: which managers are ready, then any warnings and errors as single blocks.
    // Kept as a Log (not a debugger) because it is the managers' health check on every start.
    public bool ValidateManagers()
    {
        var passed = new List<string>();
        var warnings = new List<string>();
        var errors = new List<string>();
        bool anyFatal = false;

        foreach (var manager in allManagers.Where(m => m.IsEnabled))
        {
            var result = manager.Validate();
            anyFatal |= result.IsFatal;

            if (result.IsValid && (result.Warnings == null || result.Warnings.Count == 0))
                passed.Add(result.Info != null && result.Info.Count > 0 ? $"{manager.ManagerName} ({string.Join(", ", result.Info)})" : manager.ManagerName);

            if (result.Warnings != null) warnings.AddRange(result.Warnings.Select(w => $"  [{manager.ManagerName}] {w}"));
            if (result.Errors != null) errors.AddRange(result.Errors.Select(e => $"  [{manager.ManagerName}] {e}"));
        }

        if (passed.Count > 0) Debug.Log($"[ManagerBrain] ✓ Managers ready ({passed.Count}): {string.Join(" | ", passed)}", this);
        if (warnings.Count > 0) Debug.LogWarning($"[ManagerBrain] Warnings:\n{string.Join("\n", warnings)}", this);
        if (errors.Count > 0) Debug.LogError($"[ManagerBrain] Errors:\n{string.Join("\n", errors)}", this);
        if (anyFatal) Debug.LogError("[ManagerBrain] Fatal validation errors, see above.", this);

        return !anyFatal;
    }

    // Swaps in edited definitions without restarting; GM5 builds on this.
    public void HotReloadAll()
    {
        foreach (var manager in allManagers)
            if (manager.IsEnabled && manager is IHotReloadable reloadable) Run(manager, reloadable.HotReload, "hot reload");
    }

    [ContextMenu("Validate All Managers")]
    private void ContextValidateManagers()
    {
        if (Application.isPlaying) ValidateManagers();
    }

    [ContextMenu("Hot Reload All")]
    private void ContextHotReloadAll()
    {
        if (Application.isPlaying) HotReloadAll();
    }
}
