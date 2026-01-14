using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;

public enum FeetContactType
{
    Ground, Wall, Ceiling, Unknown
}

public class ControllerBrain : MonoBehaviour
{
    [Header("Root References")]
    [SerializeField] private Transform e_Root;
    [SerializeField] private Transform m_Root;
    [SerializeField] private Animator animator;

    [Header("Systems")]
    [Tooltip("Universal identity system (Player/NPC/Object)")]
    [SerializeField] private IdentitySystem identitySystem;
    [Tooltip("RPG progression system (level, XP, stat allocation) - Player only")]
    [SerializeField] private RPGSystem rpgSystem;
    [Header("Systems")]
    [SerializeField] private DamageSystem damageSystem;
    [Tooltip("Universal movement system (all entities)")]
    [SerializeField] private MovementSystem movementSystem;
    [Tooltip("Universal animation system (all entities)")]
    [SerializeField] private AnimationSystem animationSystem;
    [Tooltip("Universal ability system (cooldowns, effects, execution)")]
    [SerializeField] private AbilitySystem abilitySystem;
    [Tooltip("Universal stat system (data-driven stats with formulas and modifiers)")]
    [SerializeField] private StatSystem statSystem;
    [Tooltip("Universal input system (keyboard/gamepad/AI/network control)")]
    [SerializeField] private InputSystem inputSystem;

    [Header("Provider Coordinators (Legacy - Being Phased Out)")]
    [SerializeField] private InventoryProviderCoordinator inventoryProviderCoordinator;
    [SerializeField] private CameraCoordinator cameraCoordinator;

    [Header("Entity Identity")]
    [SerializeField] private EntityType entityType = EntityType.Entity;

    // Cached module arrays for performance
    private IBrainModule[] updateModules;
    private IPhysicsModule[] physicsModules;
    private IInputHandler[] inputHandlers;

    // Provider cache for fast lookup
    private Dictionary<Type, object> providerCache = new Dictionary<Type, object>();

    // Input
    private PlayerInputControls playerInputControls;

    // Feet detection
    private HashSet<Collider> groundContacts = new HashSet<Collider>();
    private FeetDetectionModule feetDetection;

    public bool IsInitialized { get; private set; }

    // Root references
    public Transform EntityRoot => e_Root;
    public Transform ModelRoot => m_Root;
    public Animator EntityAnimator
    {
        get
        {
            // Lazy-load animator if not cached
            if (animator == null)
            {
                if (m_Root != null)
                    animator = m_Root.GetComponentInChildren<Animator>();

                if (animator == null && e_Root != null)
                    animator = e_Root.GetComponentInChildren<Animator>();
            }
            return animator;
        }
    }

    // Systems
    public IdentitySystem Identity => identitySystem;
    public MovementSystem Movement => movementSystem;
    public AnimationSystem Animation => animationSystem;
    public AbilitySystem Abilities => abilitySystem;
    public StatSystem Stats => statSystem;
    public RPGSystem RPG => rpgSystem;
    public StatSystem StatSystem => statSystem; // Alias for consistency with migrated code
    public DamageSystem Damage => damageSystem;

    // Provider coordinators
    public InventoryProviderCoordinator InventoryProvider => inventoryProviderCoordinator;
    public CameraCoordinator CameraProvider => cameraCoordinator;

    // Identity
    public EntityType EntityType => entityType;
    public bool IsPlayer => entityType == EntityType.Player;
    public bool IsNPC => entityType == EntityType.NPC;
    public bool IsEntity => entityType == EntityType.Entity;

    // Feet detection events
    public event System.Action<Collider, FeetContactType> OnFeetEnter;
    public event System.Action<Collider, FeetContactType> OnFeetExit;
    public event System.Action<Collider, FeetContactType> OnFeetStay;

    // Grounded state
    public bool IsGrounded => feetDetection?.IsGrounded ?? false;

    void Awake()
    {
        ValidateRootReferences();
        ValidateAndInitializeProviderCoordinators();
        CacheModuleArrays();
        InitializeInputSystem();  // Create PlayerInputControls FIRST
        InitializeModules();      // Then initialize modules that need it
        IsInitialized = true;
    }

    void ValidateRootReferences()
    {
        if (e_Root == null)
            e_Root = transform.parent ?? transform;

        if (m_Root == null && e_Root != null)
        {
            m_Root = e_Root.Find("3D Model") ??
                     e_Root.Find("Model") ??
                     e_Root.Find("Visual") ??
                     e_Root.Find("Armature");
        }

        if (animator == null)
        {
            if (m_Root != null)
                animator = m_Root.GetComponentInChildren<Animator>();

            if (animator == null && e_Root != null)
                animator = e_Root.GetComponentInChildren<Animator>();
        }
    }

    void ValidateAndInitializeProviderCoordinators()
    {
        bool allValid = true;

        // Initialize Core Systems
        if (identitySystem != null && identitySystem is IBrainModule identityModule)
        {
            identityModule.Initialize(this);
        }
        else if (identitySystem == null)
        {
            Debug.LogWarning($"[{name}] No IdentitySystem assigned!", this);
        }

        // NOTE: MovementSystem initialized later in InitializeModules() after InputSystem is ready
        // NOTE: AnimationSystem initialized later in InitializeModules()

        if (statSystem != null && statSystem is IBrainModule statModule)
        {
            statModule.Initialize(this);
        }


        // Validate and initialize provider coordinators (Legacy)
        // NOTE: Input and Combat now handled by InputSystem and AbilitySystem

        // Optional coordinators (legacy - being phased out)
        inventoryProviderCoordinator?.Initialize(this);

        // Build the central provider cache
        BuildProviderCache();

        // Initialize child modules (they can now access the cache)
        inventoryProviderCoordinator?.InitializeChildModules();

        if (!allValid)
        {
            Debug.LogError($"[{name}] Brain validation FAILED! Check missing provider slots above.");
            return;
        }
    }

    void BuildProviderCache()
    {
        // Cache IdentitySystem
        if (identitySystem != null)
        {
            providerCache[typeof(IdentitySystem)] = identitySystem;
        }

        // Cache InputSystem (implements multiple interfaces)
        if (inputSystem != null)
        {
            providerCache[typeof(InputSystem)] = inputSystem;
            providerCache[typeof(IInputProvider)] = inputSystem;
            providerCache[typeof(IMovementControlSource)] = inputSystem;
            providerCache[typeof(IAbilityControlSource)] = inputSystem;
        }

        // Cache AbilitySystem (replaces CombatProviderCoordinator.Ability)
        if (abilitySystem != null)
        {
            providerCache[typeof(AbilitySystem)] = abilitySystem;
            providerCache[typeof(IAbilityProvider)] = abilitySystem;
        }

        // Cache AnimationSystem
        if (animationSystem != null)
        {
            providerCache[typeof(AnimationSystem)] = animationSystem;
            providerCache[typeof(IAnimationProvider)] = animationSystem;
        }

        // Cache MovementSystem
        if (movementSystem != null)
        {
            providerCache[typeof(MovementSystem)] = movementSystem;
        }

        // Cache CameraCoordinator
        if (cameraCoordinator != null)
        {
            providerCache[typeof(ICameraProvider)] = cameraCoordinator;
        }

        // Cache InventoryProviderCoordinator (legacy - being phased out)
        if (inventoryProviderCoordinator != null)
        {
            providerCache[typeof(IInventoryProvider)] = inventoryProviderCoordinator.Inventory;
            providerCache[typeof(IEquipmentProvider)] = inventoryProviderCoordinator.Equipment;
        }
    }

    void InitializeEmptyArrays()
    {
        // Initialize to empty arrays to prevent null reference
        updateModules = new IBrainModule[0];
        physicsModules = new IPhysicsModule[0];
        inputHandlers = new IInputHandler[0];
    }

    void CacheModuleArrays()
    {
        try
        {
            // Initialize empty arrays first to prevent null references
            InitializeEmptyArrays();

            var allModules = GetComponentsInChildren<IBrainModule>(true);

            var updateList = new List<IBrainModule>();
            var physicsList = new List<IPhysicsModule>();
            var inputList = new List<IInputHandler>();

            foreach (var module in allModules)
            {
                if (!module.IsEnabled) continue;

                // Skip static modules (they don't need per-frame updates)
                if (module is IStaticModule) continue;

                // Skip provider coordinators (they don't have update logic)
                if (module is ProviderCoordinator) continue;

                // Physics modules go in FixedUpdate
                if (module is IPhysicsModule physModule)
                {
                    physicsList.Add(physModule);
                    updateList.Add(module);
                }
                else
                {
                    updateList.Add(module);
                }

                // Input handlers
                if (module is IInputHandler handler)
                {
                    inputList.Add(handler);
                }
            }

            // Convert to arrays (faster iteration)
            updateModules = updateList.ToArray();
            physicsModules = physicsList.ToArray();
            inputHandlers = inputList.ToArray();

            // CRITICAL: Ensure assigned system references are in update list
            // (GetComponentsInChildren might miss them if they're disabled or in specific positions)
            if (inputSystem != null && !updateList.Contains(inputSystem))
            {
                var tempList = new List<IBrainModule>(updateModules);
                tempList.Add(inputSystem);
                updateModules = tempList.ToArray();
            }

            if (movementSystem != null && !updateList.Contains(movementSystem))
            {
                var tempList = new List<IBrainModule>(updateModules);
                tempList.Add(movementSystem);
                updateModules = tempList.ToArray();
            }

            if (animationSystem != null && !updateList.Contains(animationSystem))
            {
                var tempList = new List<IBrainModule>(updateModules);
                tempList.Add(animationSystem);
                updateModules = tempList.ToArray();
            }

            if (statSystem != null && !updateList.Contains(statSystem))
            {
                var tempList = new List<IBrainModule>(updateModules);
                tempList.Add(statSystem);
                updateModules = tempList.ToArray();
            }

            if (identitySystem != null && !updateList.Contains(identitySystem))
            {
                var tempList = new List<IBrainModule>(updateModules);
                tempList.Add(identitySystem);
                updateModules = tempList.ToArray();
            }

            Debug.Log($"[{name}] Module Lists: Update={updateModules.Length}, Physics={physicsModules.Length}, Input={inputHandlers.Length}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[{name}] Failed to cache modules: {ex.Message}");
            InitializeEmptyArrays();
        }
    }

    void InitializeModules()
    {
        // Find FeetDetectionModule for grounding
        feetDetection = GetComponentInChildren<FeetDetectionModule>();

        // Initialize CameraCoordinator
        if (cameraCoordinator != null)
            cameraCoordinator.Initialize(this);

        // Initialize core systems in correct order
        // InputSystem FIRST (needs PlayerInputControls from InitializeInputSystem)
        if (inputSystem != null && inputSystem is IBrainModule inputModule)
            inputModule.Initialize(this);

        // MovementSystem SECOND (needs InputSystem as control source)
        if (movementSystem != null && movementSystem is IBrainModule movementModule)
            movementModule.Initialize(this);

        // AnimationSystem THIRD
        if (animationSystem != null && animationSystem is IBrainModule animationModule)
            animationModule.Initialize(this);

        // AbilitySystem FOURTH (needs InputSystem as control source)
        if (abilitySystem != null && abilitySystem is IBrainModule abilityModule)
            abilityModule.Initialize(this);

        // Rebuild provider cache now that InputSystem is initialized
        BuildProviderCache();

        // Initialize remaining modules
        foreach (var module in updateModules)
        {
            // Skip core systems - already initialized above
            if (module == movementSystem || module == animationSystem ||
                module == abilitySystem || module == inputSystem || module == statSystem || module == identitySystem)

                continue;

            module.Initialize(this);
        }

        foreach (var module in physicsModules)
        {
            if (module is IBrainModule brainModule)
                brainModule.Initialize(this);
        }
    }

    void InitializeInputSystem()
    {
        if (!IsPlayer) return;

        playerInputControls = new PlayerInputControls();
        playerInputControls.Enable();  // Enable immediately after creation
    }

    void OnEnable()
    {
        if (playerInputControls != null)
        {
            playerInputControls.Enable();
            SubscribeToInputs();
        }
    }

    void OnDisable()
    {
        if (playerInputControls != null)
        {
            UnsubscribeFromInputs();
            playerInputControls.Player.Disable();
            playerInputControls.UI.Disable();
            playerInputControls.Disable();
        }
    }

    void OnDestroy()
    {
        if (playerInputControls != null)
        {
            UnsubscribeFromInputs();
            playerInputControls.Player.Disable();
            playerInputControls.UI.Disable();
            playerInputControls.Disable();
            playerInputControls.Dispose();
        }
    }

    void SubscribeToInputs()
    {
        if (playerInputControls == null) return;

        // Subscribe all input handlers
        foreach (var handler in inputHandlers)
        {
            handler.SubscribeToInputs(playerInputControls);
        }
    }

    void UnsubscribeFromInputs()
    {
        if (playerInputControls == null) return;


        // Unsubscribe all input handlers
        foreach (var handler in inputHandlers)
        {
            handler.UnsubscribeFromInputs(playerInputControls);
        }
    }

    void Update()
    {
        if (!IsInitialized || updateModules == null) return;

        // Direct array iteration - fastest possible
        for (int i = 0; i < updateModules.Length; i++)
        {
            if (updateModules[i] != null) updateModules[i].UpdateModule();
        }
    }

    void FixedUpdate()
    {
        if (!IsInitialized || physicsModules == null) return;

        // Only physics modules
        for (int i = 0; i < physicsModules.Length; i++)
        {
            if (physicsModules[i] != null) physicsModules[i].PhysicsUpdate();
        }
    }

    #region Feet Detection

    public void NotifyFeetEnter(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground)
        {
            groundContacts.Add(col);
        }

        OnFeetEnter?.Invoke(col, type);
    }

    public void NotifyFeetExit(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground)
        {
            groundContacts.Remove(col);
        }

        OnFeetExit?.Invoke(col, type);
    }

    public void NotifyFeetStay(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground)
        {
            groundContacts.Add(col);
        }

        OnFeetStay?.Invoke(col, type);
    }

    public int GetGroundContactCount() => groundContacts.Count;

    #endregion

    #region Provider Lookup

    /// <summary>
    /// Get a provider by interface type (zero allocation)
    /// </summary>
    public T GetProvider<T>() where T : class
    {
        Type type = typeof(T);
        if (providerCache.TryGetValue(type, out object provider))
        {
            return provider as T;
        }
        return null;
    }

    /// <summary>
    /// Get a module implementing an interface provider
    /// </summary>
    public T GetModuleImplementing<T>() where T : class
    {
        return GetProvider<T>();
    }

    #endregion

    #region Convenience Accessors

    public PlayerInputControls GetInputControls() => playerInputControls;

    // Combat shortcuts
    public ICombatStatsProvider CombatStats => GetProvider<ICombatStatsProvider>();
    public IHealthProvider Health => GetProvider<IHealthProvider>();
    public IResourceProvider Resources => GetProvider<IResourceProvider>();
    public IDefenseProvider Defense => GetProvider<IDefenseProvider>();

    // Camera shortcuts (via coordinator with backward compatibility)
    public ICameraProvider CameraInterface => cameraCoordinator;
    public SimpleThirdPersonCamera Camera => cameraCoordinator?.Camera;
    public TargetLockModule TargetLock => cameraCoordinator?.TargetLock;

    // Essential module accessors (commonly used by other modules)
    public Animator PlayerAnimator => EntityAnimator;
    public FeetDetectionModule FeetDetection => feetDetection;



    public CombatWrapper Combat => new CombatWrapper(this);

    #endregion

    #region Backward Compatibility - GetModule for specific types

    /// <summary>
    /// Get a specific module by type (for backward compatibility)
    /// Uses GetComponentInChildren for non-provider modules
    /// </summary>
    public T GetModule<T>() where T : class
    {
        // Check assigned system references FIRST (most reliable)
        if (typeof(T) == typeof(InputSystem) && inputSystem != null)
            return inputSystem as T;
        if (typeof(T) == typeof(MovementSystem) && movementSystem != null)
            return movementSystem as T;
        if (typeof(T) == typeof(AnimationSystem) && animationSystem != null)
            return animationSystem as T;
        if (typeof(T) == typeof(AbilitySystem) && abilitySystem != null)
            return abilitySystem as T;
        if (typeof(T) == typeof(StatSystem) && statSystem != null)
            return statSystem as T;
        if (typeof(T) == typeof(IdentitySystem) && identitySystem != null)
            return identitySystem as T;

        // Try provider cache
        T provider = GetProvider<T>();
        if (provider != null)
            return provider;

        // Fall back to component search for non-provider modules
        return GetComponentInChildren<T>();
    }

    #endregion

    #region Gizmos
    /// <summary>
    /// Refreshes the animator reference (call after model swap)
    /// </summary>
    public void RefreshAnimatorReference()
    {
        if (animator == null)
        {
            if (m_Root != null)
                animator = m_Root.GetComponentInChildren<Animator>();

            if (animator == null && e_Root != null)
                animator = e_Root.GetComponentInChildren<Animator>();

        }
    }
    #endregion
}

/// <summary>
/// Wrapper to provide old Stats.CoreStats style access
/// </summary>
public class StatsWrapper
{
    private ControllerBrain brain;

    public StatsWrapper(ControllerBrain brain)
    {
        this.brain = brain;
    }

    public RPGSystem Allocation => brain.GetModule<RPGSystem>(); 
}

/// <summary>
/// Wrapper to provide old Combat.Melee style access
/// </summary>
public class CombatWrapper
{
    private ControllerBrain brain;

    public CombatWrapper(ControllerBrain brain)
    {
        this.brain = brain;
    }

    public DamageSystem Damage => brain.GetModule<DamageSystem>();
    public ActiveDefenseModule ActiveDefense => brain.GetModule<ActiveDefenseModule>();
}