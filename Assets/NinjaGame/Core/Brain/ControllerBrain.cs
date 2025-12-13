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
    private const float BRAIN_GIZMO_HEIGHT = 3f;
    private const float BRAIN_GIZMO_RADIUS = 0.5f;

    [Header("Root References")]
    [SerializeField] private Transform e_Root;
    [SerializeField] private Transform m_Root;
    [SerializeField] private Animator animator;

    [Header("Provider Coordinators")]
    [SerializeField] private StatsProviderCoordinator statsProviderCoordinator;
    [SerializeField] private InputProviderCoordinator inputProviderCoordinator;
    [SerializeField] private IdentityProviderCoordinator identityProviderCoordinator;
    [SerializeField] private CombatProviderCoordinator combatProviderCoordinator;
    [SerializeField] private MovementProviderCoordinator movementProviderCoordinator;
    [SerializeField] private InventoryProviderCoordinator inventoryProviderCoordinator;
    [SerializeField] private CameraCoordinator cameraCoordinator;

    [Header("Entity Identity")]
    [SerializeField] private EntityType entityType = EntityType.Entity;

    [Header("UI")]
    [SerializeField] private ThirdPersonUI uiController;

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
    public ThirdPersonUI UIController => uiController;

    // Provider coordinators
    public StatsProviderCoordinator StatsProvider => statsProviderCoordinator;
    public InputProviderCoordinator InputProvider => inputProviderCoordinator;
    public IdentityProviderCoordinator IdentityProvider => identityProviderCoordinator;
    public CombatProviderCoordinator CombatProvider => combatProviderCoordinator;
    public MovementProviderCoordinator MovementProvider => movementProviderCoordinator;
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

        // Phase 1: Validate and cache providers
        if (identityProviderCoordinator != null)
        {
            identityProviderCoordinator.Initialize(this);
        }

        if (statsProviderCoordinator == null)
        {
            Debug.LogError($"[{name}] StatsProviderCoordinator is REQUIRED!");
            allValid = false;
        }
        else
        {
            statsProviderCoordinator.Initialize(this);
            if (!statsProviderCoordinator.IsEnabled)
                allValid = false;
        }

        if (combatProviderCoordinator == null)
        {
            Debug.LogError($"[{name}] CombatProviderCoordinator is REQUIRED!");
            allValid = false;
        }
        else
        {
            combatProviderCoordinator.Initialize(this);
            if (!combatProviderCoordinator.IsEnabled)
                allValid = false;
        }

        if (movementProviderCoordinator == null)
        {
            Debug.LogError($"[{name}] MovementProviderCoordinator is REQUIRED!");
            allValid = false;
        }
        else
        {
            movementProviderCoordinator.Initialize(this);
            if (!movementProviderCoordinator.IsEnabled)
                allValid = false;
        }

        // Optional coordinators - Phase 1
        inventoryProviderCoordinator?.Initialize(this);
        inputProviderCoordinator?.Initialize(this);

        // Phase 2: Build the central provider cache
        BuildProviderCache();

        // Phase 3: Initialize child modules (they can now access the cache)
        identityProviderCoordinator?.InitializeChildModules();
        statsProviderCoordinator?.InitializeChildModules();
        combatProviderCoordinator?.InitializeChildModules();
        movementProviderCoordinator?.InitializeChildModules();
        inventoryProviderCoordinator?.InitializeChildModules();
        inputProviderCoordinator?.InitializeChildModules();

        if (!allValid)
        {
            Debug.LogError($"[{name}] Brain validation FAILED! Check missing provider slots above.");
            enabled = false;
            return;
        }
    }

    void BuildProviderCache()
    {
        if (identityProviderCoordinator != null)
        {
            // Cache identity-related providers
            var identityHandler = identityProviderCoordinator.IdentityHandler;
            var npcModule = identityProviderCoordinator.NPCModule;

            if (identityHandler != null)
            {
                // Could add interface here if needed
                // providerCache[typeof(IIdentityProvider)] = identityHandler;
            }

            if (npcModule != null)
            {
                providerCache[typeof(NPCModule)] = npcModule;
            }
        }
        if (statsProviderCoordinator != null)
        {
            providerCache[typeof(ICombatStatsProvider)] = statsProviderCoordinator.CombatStats;
            providerCache[typeof(IHealthProvider)] = statsProviderCoordinator.Health;
            providerCache[typeof(IResourceProvider)] = statsProviderCoordinator.Resources;
        }

        if (combatProviderCoordinator != null)
        {
            // REMOVED: IAttackProvider - deprecated, use IAbilityProvider
            providerCache[typeof(IAbilityProvider)] = combatProviderCoordinator.Ability;
            providerCache[typeof(IDefenseProvider)] = combatProviderCoordinator.Defense;
        }

        if (movementProviderCoordinator != null)
        {
            providerCache[typeof(IMovementProvider)] = movementProviderCoordinator.Movement;
            providerCache[typeof(IAnimationProvider)] = movementProviderCoordinator.Animation;
        }

        if (inventoryProviderCoordinator != null)
        {
            providerCache[typeof(IInventoryProvider)] = inventoryProviderCoordinator.Inventory;
            providerCache[typeof(IEquipmentProvider)] = inventoryProviderCoordinator.Equipment;
        }

        if (inputProviderCoordinator != null)
        {
            providerCache[typeof(IInputProvider)] = inputProviderCoordinator.Input;
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

        // Initialize all active modules
        foreach (var module in updateModules)
        {
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

        // Find ThirdPersonController for connector
        var controller = GetComponentInChildren<ThirdPersonController>();
        if (controller != null)
        {
            var connector = controller.GetComponent<ControllerBrainConnector>();
            if (connector == null)
            {
                connector = controller.gameObject.AddComponent<ControllerBrainConnector>();
            }
            connector.Initialize(this);
        }
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

        // DEPRECATED:         // Subscribe ThirdPersonController to movement inputs
        // DEPRECATED:         var controller = GetComponentInChildren<ThirdPersonController>();
        // DEPRECATED:         if (controller != null)
        // DEPRECATED:         {
        // DEPRECATED:             playerInputControls.Player.Move.performed += controller.OnMoveInput;
        // DEPRECATED:             playerInputControls.Player.Move.canceled += controller.OnMoveInput;
        // DEPRECATED:             playerInputControls.Player.Sprint.started += controller.OnSprintStarted;
        // DEPRECATED:             playerInputControls.Player.Sprint.canceled += controller.OnSprintCanceled;
        // DEPRECATED:         }

        // Subscribe all input handlers
        foreach (var handler in inputHandlers)
        {
            handler.SubscribeToInputs(playerInputControls);
        }
    }

    void UnsubscribeFromInputs()
    {
        if (playerInputControls == null) return;

        // DEPRECATED:         // Unsubscribe ThirdPersonController
        // DEPRECATED:         var controller = GetComponentInChildren<ThirdPersonController>();
        // DEPRECATED:         if (controller != null)
        // DEPRECATED:         {
        // DEPRECATED:             playerInputControls.Player.Move.performed -= controller.OnMoveInput;
        // DEPRECATED:             playerInputControls.Player.Move.canceled -= controller.OnMoveInput;
        // DEPRECATED:             playerInputControls.Player.Sprint.started -= controller.OnSprintStarted;
        // DEPRECATED:             playerInputControls.Player.Sprint.canceled -= controller.OnSprintCanceled;
        // DEPRECATED:         }

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

    // Movement shortcuts
    public IMovementProvider Movement => GetProvider<IMovementProvider>();
    public IAnimationProvider Animation => GetProvider<IAnimationProvider>();

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
    public ThirdPersonController Controller => GetModule<ThirdPersonController>();
    public LocomotionModule Locomotion => GetModule<LocomotionModule>();
    public Animator PlayerAnimator => EntityAnimator;
    public AnimationStateModule AnimationState => GetModule<AnimationStateModule>();
    public FeetDetectionModule FeetDetection => feetDetection;

    // Stat module direct accessors
    public RPGCoreStats RPGCoreStats => GetModule<RPGCoreStats>();
    public RPGSecondaryStats RPGSecondaryStats => GetModule<RPGSecondaryStats>();
    public RPGResources RPGResources => GetModule<RPGResources>();

    // Old coordinator-style accessors (for migration)
    public StatsWrapper Stats => new StatsWrapper(this);
    public CombatWrapper Combat => new CombatWrapper(this);

    #endregion

    #region Backward Compatibility - GetModule for specific types

    /// <summary>
    /// Get a specific module by type (for backward compatibility)
    /// Uses GetComponentInChildren for non-provider modules
    /// </summary>
    public T GetModule<T>() where T : class
    {
        // Try provider cache first
        T provider = GetProvider<T>();
        if (provider != null)
            return provider;

        // Fall back to component search for non-provider modules
        return GetComponentInChildren<T>();
    }

    #endregion

    #region Gizmos

    void OnDrawGizmosSelected()
    {
        if (!IsInitialized) return;

        Color brainColor = IsPlayer ? Color.blue : IsNPC ? Color.red : Color.gray;
        if (IsGrounded)
            brainColor = Color.Lerp(brainColor, Color.green, 0.5f);

        Gizmos.color = brainColor;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * BRAIN_GIZMO_HEIGHT, BRAIN_GIZMO_RADIUS);

        Gizmos.color = Color.yellow;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child != null)
            {
                Gizmos.DrawLine(transform.position, child.position);
            }
        }

        if (groundContacts.Count > 0)
        {
            Gizmos.color = Color.red;
            foreach (var contact in groundContacts)
            {
                if (contact != null)
                {
                    Gizmos.DrawLine(transform.position, contact.transform.position);
                }
            }
        }
    }
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

            if (animator != null)
            {
                Debug.Log($"[ControllerBrain] Animator reference refreshed: {animator.gameObject.name}");
            }
        }
    }
    #endregion
}

public class ControllerBrainConnector : MonoBehaviour
{
    private ControllerBrain brain;
    private ThirdPersonController controller;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        controller = GetComponent<ThirdPersonController>();
    }

    public ControllerBrain GetBrain() => brain;
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

    public RPGCoreStats CoreStats => brain.GetModule<RPGCoreStats>();
    public RPGSecondaryStats SecondaryStats => brain.GetModule<RPGSecondaryStats>();
    public RPGResources Resources => brain.GetModule<RPGResources>();
    public StatAllocationSystem Allocation => brain.GetModule<StatAllocationSystem>();
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

    public DamageModule Damage => brain.GetModule<DamageModule>();
    public ActiveDefenseModule ActiveDefense => brain.GetModule<ActiveDefenseModule>();
}