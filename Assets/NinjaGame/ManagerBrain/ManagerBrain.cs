using RPG.Factions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manager Brain - Orchestrates all game managers
/// 
/// Architecture Pattern:
/// ManagerBrain → IGameManager implementations
/// 
/// Mirrors:
/// ControllerBrain → IBrainModule implementations
/// 
/// Responsibilities:
/// - Discover all game managers on startup
/// - Initialize managers in dependency order (by priority)
/// - Provide manager lookup/registry
/// - Handle persistence (DontDestroyOnLoad)
/// - Coordinate shutdown
/// - Provide cross-manager events
/// 
/// Benefits:
/// - Guaranteed initialization order
/// - Dependency management
/// - Single DontDestroyOnLoad call
/// - Easy to add new managers
/// - Centralized lifecycle control
/// - Clear logging of manager state
/// 
/// Usage:
/// Place on root "Managers" GameObject
/// All child components implementing IGameManager auto-discovered
/// 
/// Phase 1.7b: Universal Systems Consolidation
/// Created: January 2026
/// </summary>
public class ManagerBrain : MonoBehaviour
{
    #region Singleton

    private static ManagerBrain instance;
    public static ManagerBrain Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<ManagerBrain>();
            }
            return instance;
        }
    }

    #endregion

    #region Inspector Fields

    [Header("Core Managers (Required)")]
    [Tooltip("Global stat schemas and configuration")]
    [SerializeField] private StatsManager statsManager;

    [Tooltip("Resource definitions (health, mana, stamina)")]
    [SerializeField] private ResourceManager resourceManager;

    [Tooltip("Damage calculation rules and configs")]
    [SerializeField] private DamageManager damageManager;

    [Tooltip("Faction relationships and reputation")]
    [SerializeField] private FactionManager factionManager;

    [Header("Settings")]
    [Tooltip("Persist managers across scene loads")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Tooltip("Automatically initialize managers on Awake")]
    [SerializeField] private bool autoInitialize = true;

    [Tooltip("Validate all required managers assigned in Inspector")]
    [SerializeField] private bool validateRequiredManagers = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;
    [SerializeField] private bool verboseLogging = false;

    #endregion

    #region Private Fields

    // All discovered managers
    private List<IGameManager> allManagers = new List<IGameManager>();

    // Managers that need per-frame updates
    private List<IUpdatableManager> updatableManagers = new List<IUpdatableManager>();

    // Manager registry for fast lookup by type
    private Dictionary<Type, IGameManager> managerRegistry = new Dictionary<Type, IGameManager>();

    // Initialization state
    private bool isInitialized = false;
    private bool isShuttingDown = false;

    #endregion

    #region Properties

    public bool IsInitialized => isInitialized;
    public int ManagerCount => allManagers.Count;

    // Core manager properties (like ControllerBrain.Stats, etc.)
    public StatsManager Stats => statsManager;
    public ResourceManager Resources => resourceManager;
    public DamageManager Damage => damageManager;
    public FactionManager Factions => factionManager;

    #endregion

    #region Events

    /// <summary>Fired when all managers initialized</summary>
    public event Action OnAllManagersInitialized;

    /// <summary>Fired when managers shutting down</summary>
    public event Action OnManagersShutdown;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // Singleton pattern
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[ManagerBrain] Duplicate ManagerBrain on {gameObject.name}. Destroying.");
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Persist entire manager hierarchy
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);

            if (debugLogging)
                Debug.Log("[ManagerBrain] Managers will persist across scenes");
        }

        // Auto-initialize if enabled
        if (autoInitialize)
        {
            InitializeManagers();
        }
    }

    void Update()
    {
        if (!isInitialized || isShuttingDown) return;

        // Update managers that need per-frame updates
        for (int i = 0; i < updatableManagers.Count; i++)
        {
            if (updatableManagers[i].IsEnabled)
            {
                updatableManagers[i].UpdateManager();
            }
        }
    }

    void OnApplicationQuit()
    {
        ShutdownManagers();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            ShutdownManagers();
        }
    }

    #endregion

    #region Manager Discovery

    /// <summary>
    /// Discover all managers in hierarchy
    /// </summary>
    private void DiscoverManagers()
    {
        allManagers.Clear();
        updatableManagers.Clear();
        managerRegistry.Clear();

        // PHASE 1: Register explicitly assigned core managers first
        RegisterCoreManagers();

        // PHASE 2: Auto-discover additional managers
        var components = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);

        foreach (var component in components)
        {
            if (component is IGameManager manager)
            {
                // Skip if already registered as core manager
                if (managerRegistry.ContainsKey(component.GetType()))
                {
                    continue;
                }

                allManagers.Add(manager);

                // Track updatable managers separately
                if (component is IUpdatableManager updatable)
                {
                    updatableManagers.Add(updatable);
                }

                // Register by component type for lookup
                Type managerType = component.GetType();
                managerRegistry[managerType] = manager;

                if (verboseLogging)
                {
                    Debug.Log($"[ManagerBrain] Discovered (optional): {manager.ManagerName} " +
                             $"(Priority: {manager.InitializationPriority})");
                }
            }
        }

        if (debugLogging)
        {
            Debug.Log($"[ManagerBrain] Discovered {allManagers.Count} managers " +
                     $"({updatableManagers.Count} updatable)");
        }
    }

    /// <summary>
    /// Register core managers from explicit Inspector references
    /// </summary>
    private void RegisterCoreManagers()
    {
        RegisterManager(statsManager, "StatsManager");
        RegisterManager(resourceManager, "ResourceManager");
        RegisterManager(damageManager, "DamageManager");
        RegisterManager(factionManager, "FactionManager");
    }

    /// <summary>
    /// Register a single manager
    /// </summary>
    private void RegisterManager(MonoBehaviour component, string managerName)
    {
        if (component == null)
        {
            if (validateRequiredManagers)
            {
                Debug.LogError($"[ManagerBrain] Required manager missing: {managerName}!");
            }
            return;
        }

        if (component is IGameManager manager)
        {
            allManagers.Add(manager);

            // Track updatable managers
            if (component is IUpdatableManager updatable)
            {
                updatableManagers.Add(updatable);
            }

            // Register by type
            Type managerType = component.GetType();
            managerRegistry[managerType] = manager;

            if (verboseLogging)
            {
                Debug.Log($"[ManagerBrain] Registered (core): {manager.ManagerName} " +
                         $"(Priority: {manager.InitializationPriority})");
            }
        }
        else
        {
            Debug.LogError($"[ManagerBrain] Component {managerName} does not implement IGameManager!");
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize all managers in priority order
    /// </summary>
    public void InitializeManagers()
    {
        if (isInitialized)
        {
            Debug.LogWarning("[ManagerBrain] Already initialized!");
            return;
        }

        if (debugLogging)
            Debug.Log("=== ManagerBrain: Starting Initialization ===");

        // Validate required managers are assigned
        if (!ValidateRequiredManagersAssigned())
        {
            Debug.LogError("[ManagerBrain] Required managers missing! Initialization aborted.");
            return;
        }

        // Discover all managers
        DiscoverManagers();

        // Sort by initialization priority (lower = earlier)
        var sortedManagers = allManagers.OrderBy(m => m.InitializationPriority).ToList();

        // Validate dependencies
        ValidateManagerDependencies();

        // Phase 1: Initialize each manager
        foreach (var manager in sortedManagers)
        {
            if (!manager.IsEnabled)
            {
                if (verboseLogging)
                    Debug.Log($"[ManagerBrain] Skipping disabled: {manager.ManagerName}");
                continue;
            }

            try
            {
                if (debugLogging)
                    Debug.Log($"[ManagerBrain] Initializing: {manager.ManagerName} (Priority: {manager.InitializationPriority})");

                manager.Initialize();

                if (verboseLogging)
                    Debug.Log($"[ManagerBrain] ✓ {manager.ManagerName} initialized");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ManagerBrain] Failed to initialize {manager.ManagerName}: {ex.Message}");
            }
        }

        // Phase 2: Late initialization (after all managers initialized)
        foreach (var manager in sortedManagers)
        {
            if (!manager.IsEnabled) continue;

            try
            {
                if (verboseLogging)
                    Debug.Log($"[ManagerBrain] Late-Initializing: {manager.ManagerName}");

                manager.LateInitialize();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ManagerBrain] Failed late init for {manager.ManagerName}: {ex.Message}");
            }
        }

        // Phase 3: Validation
        bool allValid = ValidateManagers();

        isInitialized = true;

        if (debugLogging)
        {
            Debug.Log($"=== ManagerBrain: Initialization Complete ===");
            Debug.Log($"  Total Managers: {allManagers.Count}");
            Debug.Log($"  Updatable: {updatableManagers.Count}");
            Debug.Log($"  Validation: {(allValid ? "PASSED" : "FAILED")}");
        }

        // Fire event
        OnAllManagersInitialized?.Invoke();
    }

    /// <summary>
    /// Validate that all required managers are assigned
    /// </summary>
    private bool ValidateRequiredManagersAssigned()
    {
        if (!validateRequiredManagers) return true;

        bool allValid = true;

        if (statsManager == null)
        {
            Debug.LogError("[ManagerBrain] StatsManager is required but not assigned!");
            allValid = false;
        }

        if (resourceManager == null)
        {
            Debug.LogError("[ManagerBrain] ResourceManager is required but not assigned!");
            allValid = false;
        }

        if (damageManager == null)
        {
            Debug.LogError("[ManagerBrain] DamageManager is required but not assigned!");
            allValid = false;
        }

        if (factionManager == null)
        {
            Debug.LogWarning("[ManagerBrain] FactionManager not assigned (optional but recommended)");
            // Not an error - factions might be optional for some games
        }

        return allValid;
    }

    #endregion

    #region Shutdown

    /// <summary>
    /// Shutdown all managers (reverse priority order)
    /// </summary>
    public void ShutdownManagers()
    {
        if (isShuttingDown) return;

        isShuttingDown = true;

        if (debugLogging)
            Debug.Log("=== ManagerBrain: Starting Shutdown ===");

        // Shutdown in reverse order
        var sortedManagers = allManagers.OrderByDescending(m => m.InitializationPriority).ToList();

        foreach (var manager in sortedManagers)
        {
            try
            {
                if (debugLogging)
                    Debug.Log($"[ManagerBrain] Shutting down: {manager.ManagerName}");

                manager.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ManagerBrain] Failed to shutdown {manager.ManagerName}: {ex.Message}");
            }
        }

        OnManagersShutdown?.Invoke();

        if (debugLogging)
            Debug.Log("=== ManagerBrain: Shutdown Complete ===");
    }

    #endregion

    #region Manager Lookup

    /// <summary>
    /// Get manager by type
    /// </summary>
    public T GetManager<T>() where T : class, IGameManager
    {
        Type type = typeof(T);
        if (managerRegistry.TryGetValue(type, out IGameManager manager))
        {
            return manager as T;
        }

        Debug.LogWarning($"[ManagerBrain] Manager of type {type.Name} not found!");
        return null;
    }

    /// <summary>
    /// Check if a manager type exists
    /// </summary>
    public bool HasManager<T>() where T : class, IGameManager
    {
        return managerRegistry.ContainsKey(typeof(T));
    }

    /// <summary>
    /// Get all managers
    /// </summary>
    public List<IGameManager> GetAllManagers()
    {
        return new List<IGameManager>(allManagers);
    }

    /// <summary>
    /// Get managers by priority range
    /// </summary>
    public List<IGameManager> GetManagersByPriority(int minPriority, int maxPriority)
    {
        return allManagers
            .Where(m => m.InitializationPriority >= minPriority && m.InitializationPriority <= maxPriority)
            .ToList();
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validate all managers
    /// </summary>
    /// <summary>
    /// Validate manager dependencies against initialization priority
    /// </summary>
    private void ValidateManagerDependencies()
    {
        foreach (var manager in allManagers)
        {
            if (manager is IManagerDependency dependent)
            {
                foreach (var dependencyType in dependent.DependsOn)
                {
                    var dependency = allManagers.Find(m => m.GetType() == dependencyType);

                    if (dependency == null)
                    {
                        Debug.LogError($"[ManagerBrain] {manager.ManagerName} depends on {dependencyType.Name}, but it's not registered!");
                        continue;
                    }

                    // Validate priority order
                    if (dependency.InitializationPriority >= manager.InitializationPriority)
                    {
                        Debug.LogError($"[ManagerBrain] INVALID PRIORITY ORDER!\n" +
                                     $"  {manager.ManagerName} (priority: {manager.InitializationPriority}) depends on\n" +
                                     $"  {dependency.ManagerName} (priority: {dependency.InitializationPriority})\n" +
                                     $"  → Dependency must have LOWER priority to initialize first!");
                    }
                    else
                    {
                        if (verboseLogging)
                        {
                            Debug.Log($"[ManagerBrain] Dependency validated: {manager.ManagerName} → {dependency.ManagerName}");
                        }
                    }
                }
            }
        }
    }

    public bool ValidateManagers()
    {
        bool anyFatal = false;

        foreach (var manager in allManagers)
        {
            if (!manager.IsEnabled) continue;

            var result = manager.Validate();

            // Log all messages
            result.LogResult(manager.ManagerName);

            if (result.IsFatal)
            {
                anyFatal = true;
            }
        }

        if (anyFatal)
        {
            Debug.LogError("[ManagerBrain] Fatal validation errors detected!");
        }

        return !anyFatal;
    }

    #endregion

    #region Hot Reload

    /// <summary>
    /// Hot reload all managers that support it
    /// </summary>
    public void HotReloadAll()
    {
        if (debugLogging)
            Debug.Log("[ManagerBrain] Hot reloading all managers...");

        foreach (var manager in allManagers)
        {
            if (manager is IHotReloadable reloadable && manager.IsEnabled)
            {
                try
                {
                    if (verboseLogging)
                        Debug.Log($"[ManagerBrain] Hot reloading: {manager.ManagerName}");

                    reloadable.HotReload();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ManagerBrain] Failed to hot reload {manager.ManagerName}: {ex.Message}");
                }
            }
        }

        if (debugLogging)
            Debug.Log("[ManagerBrain] Hot reload complete");
    }

    /// <summary>
    /// Hot reload a specific manager
    /// </summary>
    public void HotReload<T>() where T : class, IGameManager, IHotReloadable
    {
        var manager = GetManager<T>();
        if (manager != null)
        {
            manager.HotReload();
            if (debugLogging)
                Debug.Log($"[ManagerBrain] Hot reloaded: {manager.ManagerName}");
        }
    }

    #endregion

    #region Context Menu Helpers

    [ContextMenu("Initialize Managers")]
    private void ContextInitializeManagers()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ManagerBrain] Can only initialize in Play Mode");
            return;
        }

        InitializeManagers();
    }

    [ContextMenu("Shutdown Managers")]
    private void ContextShutdownManagers()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ManagerBrain] Can only shutdown in Play Mode");
            return;
        }

        ShutdownManagers();
    }

    [ContextMenu("Validate All Managers")]
    private void ContextValidateManagers()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ManagerBrain] Can only validate in Play Mode");
            return;
        }

        bool valid = ValidateManagers();
        Debug.Log($"[ManagerBrain] Validation: {(valid ? "PASSED" : "FAILED")}");
    }

    [ContextMenu("Print Manager Summary")]
    private void PrintManagerSummary()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ManagerBrain] Can only print summary in Play Mode");
            return;
        }

        Debug.Log("=== Manager Brain Summary ===");
        Debug.Log($"Initialized: {isInitialized}");
        Debug.Log($"Total Managers: {allManagers.Count}");
        Debug.Log($"Updatable Managers: {updatableManagers.Count}");
        Debug.Log("");
        Debug.Log("Managers (in initialization order):");

        var sorted = allManagers.OrderBy(m => m.InitializationPriority).ToList();
        foreach (var manager in sorted)
        {
            string status = manager.IsEnabled ? "✓" : "✗";
            string initialized = manager.IsInitialized ? "INIT" : "NOT INIT";
            Debug.Log($"  [{status}] {manager.ManagerName} (Priority: {manager.InitializationPriority}, {initialized})");
        }
    }

    [ContextMenu("Hot Reload All")]
    private void ContextHotReloadAll()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ManagerBrain] Can only hot reload in Play Mode");
            return;
        }

        HotReloadAll();
    }

    [ContextMenu("Discover Managers (Preview)")]
    private void ContextDiscoverManagers()
    {
        // Works in Edit Mode - just for preview
        var components = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        int count = 0;

        Debug.Log("=== Discovering Managers ===");

        foreach (var component in components)
        {
            if (component is IGameManager manager)
            {
                count++;
                Debug.Log($"  Found: {component.GetType().Name} - {manager.ManagerName} (Priority: {manager.InitializationPriority})");
            }
        }

        Debug.Log($"Total: {count} managers found");
    }

    #endregion
}