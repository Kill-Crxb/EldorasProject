using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum FeetContactType { Ground, Wall, Ceiling, Unknown }

public class ControllerBrain : MonoBehaviour
{
    [Header("Root References")]
    [SerializeField] private Transform e_Root;
    [SerializeField] private Transform m_Root;
    [SerializeField] private Animator animator;

    [Header("Systems")]
    [SerializeField] private IdentitySystem identitySystem;
    [SerializeField] private RPGSystem rpgSystem;
    [SerializeField] private DamageSystem damageSystem;
    [SerializeField] private StateMachineModule stateMachineModule;
    [SerializeField] private MovementSystem movementSystem;
    [SerializeField] private AnimationSystem animationSystem;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private StatSystem statSystem;
    [SerializeField] private ResourceSystem resourceSystem;
    [SerializeField] private InputSystem inputSystem;
    [SerializeField] private BlackboardSystem blackboardSystem;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private InteractionSystem interactionSystem;

    [Header("Provider Coordinators")]
    [SerializeField] private CameraCoordinator cameraCoordinator;

    [Header("Entity Identity")]
    [SerializeField] private EntityType entityType = EntityType.Entity;

    private IBrainModule[] updateModules;
    private IPhysicsModule[] physicsModules;
    private IInputHandler[] inputHandlers;
    private Dictionary<Type, object> providerCache = new Dictionary<Type, object>();
    private PlayerInputControls playerInputControls;
    private HashSet<Collider> groundContacts = new HashSet<Collider>();
    private FeetDetectionModule feetDetection;

    public bool IsInitialized { get; private set; }
    public event Action<ControllerBrain> OnInitialized;

    // Root
    public Transform EntityRoot => e_Root;
    public Transform ModelRoot => m_Root;
    public Animator EntityAnimator
    {
        get
        {
            if (animator == null && m_Root != null) animator = m_Root.GetComponentInChildren<Animator>();
            if (animator == null && e_Root != null) animator = e_Root.GetComponentInChildren<Animator>();
            return animator;
        }
    }

    // Systems
    public IdentitySystem Identity => identitySystem;
    public MovementSystem Movement => movementSystem;
    public AnimationSystem Animation => animationSystem;
    public AbilitySystem Abilities => abilitySystem;
    public StateMachineModule StateMachine => stateMachineModule;
    public StatSystem Stats => statSystem;
    public StatSystem StatSystem => statSystem;
    public ResourceSystem ResourceSys => resourceSystem;
    public RPGSystem RPG => rpgSystem;
    public DamageSystem Damage => damageSystem;
    public BlackboardSystem BlackboardSys => blackboardSystem;
    public InventorySystem Inventory => inventorySystem;
    public InteractionSystem Interaction => interactionSystem;
    public CameraCoordinator CameraProvider => cameraCoordinator;

    // Identity
    public EntityType EntityType => entityType;
    public bool IsPlayer => entityType == EntityType.Player;
    public bool IsNPC => entityType == EntityType.NPC;
    public bool IsEntity => entityType == EntityType.Entity;

    // Feet
    public event Action<Collider, FeetContactType> OnFeetEnter;
    public event Action<Collider, FeetContactType> OnFeetExit;
    public event Action<Collider, FeetContactType> OnFeetStay;
    public bool IsGrounded => feetDetection?.IsGrounded ?? false;

    // Convenience
    public IHealthProvider Health => GetProvider<IHealthProvider>();
    public IResourceProvider Resources => GetProvider<IResourceProvider>();
    public IDefenseProvider Defense => GetProvider<IDefenseProvider>();
    public Blackboard Blackboard => blackboardSystem?.Blackboard;
    public ICameraProvider CameraInterface => cameraCoordinator;
    public SimpleThirdPersonCamera Camera => cameraCoordinator?.Camera;
    public TargetLockModule TargetLock => cameraCoordinator?.TargetLock;
    public Animator PlayerAnimator => EntityAnimator;
    public FeetDetectionModule FeetDetection => feetDetection;
    public CombatWrapper Combat => new CombatWrapper(this);

    void Awake()
    {
        ResolveRootReferences();
        CacheModuleArrays();
        InitializeInputSystem();
        BuildProviderCache();
        InitializeModules();
        LateInitializeModules();
        BuildProviderCache();
        IsInitialized = true;
        OnInitialized?.Invoke(this);

        if (IsPlayer)
            ManagerBrain.Instance?.GetManager<SaveManager>()?.SetPlayerBrain(this);
    }

    void ResolveRootReferences()
    {
        if (e_Root == null) e_Root = transform.parent ?? transform;
        if (m_Root == null && e_Root != null)
        {
            m_Root = e_Root.Find("3D Model") ??
                     e_Root.Find("Model") ??
                     e_Root.Find("Visual") ??
                     e_Root.Find("Armature");
        }
        if (animator == null && m_Root != null) animator = m_Root.GetComponentInChildren<Animator>();
        if (animator == null && e_Root != null) animator = e_Root.GetComponentInChildren<Animator>();
    }

    void BuildProviderCache()
    {
        if (identitySystem != null) providerCache[typeof(IdentitySystem)] = identitySystem;

        if (inputSystem != null)
        {
            providerCache[typeof(InputSystem)] = inputSystem;
            providerCache[typeof(IInputProvider)] = inputSystem;
            providerCache[typeof(IMovementControlSource)] = inputSystem;
            providerCache[typeof(IAbilityControlSource)] = inputSystem;
        }

        if (abilitySystem != null)
        {
            providerCache[typeof(AbilitySystem)] = abilitySystem;
            providerCache[typeof(IAbilityProvider)] = abilitySystem;
        }

        if (animationSystem != null)
        {
            providerCache[typeof(AnimationSystem)] = animationSystem;
            providerCache[typeof(IAnimationProvider)] = animationSystem;
        }

        if (movementSystem != null) providerCache[typeof(MovementSystem)] = movementSystem;
        if (cameraCoordinator != null) providerCache[typeof(ICameraProvider)] = cameraCoordinator;

        if (inventorySystem != null)
        {
            providerCache[typeof(InventorySystem)] = inventorySystem;
            providerCache[typeof(IInventoryProvider)] = inventorySystem;
        }

        if (interactionSystem != null) providerCache[typeof(InteractionSystem)] = interactionSystem;

        if (blackboardSystem?.Blackboard != null)
            providerCache[typeof(Blackboard)] = blackboardSystem.Blackboard;

        if (resourceSystem != null)
        {
            providerCache[typeof(IResourceProvider)] = resourceSystem;
            providerCache[typeof(IHealthProvider)] = resourceSystem;
        }
    }

    void CacheModuleArrays()
    {
        InitializeEmptyArrays();

        try
        {
            var allModules = GetComponentsInChildren<IBrainModule>(true);
            var updateList = new List<IBrainModule>();
            var physicsList = new List<IPhysicsModule>();
            var inputList = new List<IInputHandler>();

            foreach (var module in allModules)
            {
                if (!module.IsEnabled) continue;
                if (module is IStaticModule) continue;

                if (module is IPhysicsModule physModule)
                    physicsList.Add(physModule);

                updateList.Add(module);

                if (module is IInputHandler handler)
                    inputList.Add(handler);
            }

            updateModules = updateList.ToArray();
            physicsModules = physicsList.ToArray();
            inputHandlers = inputList.ToArray();

            // Ensure inspector-assigned systems are in update list
            EnsureInUpdateList(ref updateModules, updateList, inputSystem);
            EnsureInUpdateList(ref updateModules, updateList, movementSystem);
            EnsureInUpdateList(ref updateModules, updateList, animationSystem);
            EnsureInUpdateList(ref updateModules, updateList, statSystem);
            EnsureInUpdateList(ref updateModules, updateList, identitySystem);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{name}] Failed to cache modules: {ex.Message}");
            InitializeEmptyArrays();
        }
    }

    void EnsureInUpdateList(ref IBrainModule[] modules, List<IBrainModule> list, IBrainModule module)
    {
        if (module == null || list.Contains(module)) return;
        var temp = new List<IBrainModule>(modules) { module };
        modules = temp.ToArray();
    }

    void InitializeEmptyArrays()
    {
        updateModules = new IBrainModule[0];
        physicsModules = new IPhysicsModule[0];
        inputHandlers = new IInputHandler[0];
    }

    void InitializeModules()
    {
        feetDetection = GetComponentInChildren<FeetDetectionModule>();

        // Ordered initialization - dependencies first
        InitModule(identitySystem);
        cameraCoordinator?.Initialize(this);
        InitModule(inputSystem);
        InitModule(stateMachineModule);
        InitModule(movementSystem);
        InitModule(animationSystem);
        InitModule(abilitySystem);
        InitModule(statSystem);
        InitModule(resourceSystem);

        // Remaining modules
        foreach (var module in updateModules)
        {
            if (module == movementSystem || module == animationSystem ||
                module == abilitySystem || module == inputSystem ||
                module == statSystem || module == resourceSystem ||
                module == identitySystem)
                continue;

            module.Initialize(this);
        }

        foreach (var module in physicsModules)
        {
            if (module is IBrainModule brainModule)
                brainModule.Initialize(this);
        }
    }

    void InitModule(IBrainModule module)
    {
        if (module != null) module.Initialize(this);
    }

    void LateInitializeModules()
    {
        foreach (var module in updateModules)
            module.LateInitialize();

        foreach (var module in physicsModules)
        {
            if (module is IBrainModule brainModule)
                brainModule.LateInitialize();
        }
    }

    void InitializeInputSystem()
    {
        if (!IsPlayer) return;
        playerInputControls = new PlayerInputControls();
        playerInputControls.Enable();
    }

    void OnEnable()
    {
        if (playerInputControls == null) return;
        playerInputControls.Enable();
        SubscribeToInputs();
    }

    void OnDisable()
    {
        if (playerInputControls == null) return;
        UnsubscribeFromInputs();
        playerInputControls.Player.Disable();
        playerInputControls.UI.Disable();
        playerInputControls.Disable();
    }

    void OnDestroy()
    {
        if (playerInputControls == null) return;
        UnsubscribeFromInputs();
        playerInputControls.Player.Disable();
        playerInputControls.UI.Disable();
        playerInputControls.Disable();
        playerInputControls.Dispose();
    }

    void SubscribeToInputs()
    {
        if (playerInputControls == null) return;
        foreach (var handler in inputHandlers)
            handler.SubscribeToInputs(playerInputControls);
    }

    void UnsubscribeFromInputs()
    {
        if (playerInputControls == null) return;
        foreach (var handler in inputHandlers)
            handler.UnsubscribeFromInputs(playerInputControls);
    }

    void Update()
    {
        if (!IsInitialized || updateModules == null) return;
        for (int i = 0; i < updateModules.Length; i++)
            if (updateModules[i] != null) updateModules[i].UpdateModule();
    }

    void FixedUpdate()
    {
        if (!IsInitialized || physicsModules == null) return;
        for (int i = 0; i < physicsModules.Length; i++)
            if (physicsModules[i] != null) physicsModules[i].PhysicsUpdate();
    }

    #region Feet Detection

    public void NotifyFeetEnter(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground) groundContacts.Add(col);
        OnFeetEnter?.Invoke(col, type);
    }

    public void NotifyFeetExit(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground) groundContacts.Remove(col);
        OnFeetExit?.Invoke(col, type);
    }

    public void NotifyFeetStay(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground) groundContacts.Add(col);
        OnFeetStay?.Invoke(col, type);
    }

    public int GetGroundContactCount() => groundContacts.Count;

    #endregion

    #region Provider Lookup

    public T GetProvider<T>() where T : class
    {
        if (providerCache.TryGetValue(typeof(T), out object provider))
            return provider as T;
        return null;
    }

    public T GetModuleImplementing<T>() where T : class => GetProvider<T>();

    public T GetModule<T>() where T : class
    {
        if (typeof(T) == typeof(InputSystem) && inputSystem != null) return inputSystem as T;
        if (typeof(T) == typeof(MovementSystem) && movementSystem != null) return movementSystem as T;
        if (typeof(T) == typeof(AnimationSystem) && animationSystem != null) return animationSystem as T;
        if (typeof(T) == typeof(AbilitySystem) && abilitySystem != null) return abilitySystem as T;
        if (typeof(T) == typeof(StateMachineModule) && stateMachineModule != null) return stateMachineModule as T;
        if (typeof(T) == typeof(StatSystem) && statSystem != null) return statSystem as T;
        if (typeof(T) == typeof(IdentitySystem) && identitySystem != null) return identitySystem as T;
        if (typeof(T) == typeof(InventorySystem) && inventorySystem != null) return inventorySystem as T;
        if (typeof(T) == typeof(InteractionSystem) && interactionSystem != null) return interactionSystem as T;

        T cached = GetProvider<T>();
        if (cached != null) return cached;

        return GetComponentInChildren<T>();
    }

    #endregion

    public PlayerInputControls GetInputControls() => playerInputControls;

    public void RefreshAnimatorReference()
    {
        if (m_Root != null) animator = m_Root.GetComponentInChildren<Animator>();
        if (animator == null && e_Root != null) animator = e_Root.GetComponentInChildren<Animator>();
    }
}

public class StatsWrapper
{
    private ControllerBrain brain;
    public StatsWrapper(ControllerBrain brain) { this.brain = brain; }
    public RPGSystem Allocation => brain.GetModule<RPGSystem>();
}

public class CombatWrapper
{
    private ControllerBrain brain;
    public CombatWrapper(ControllerBrain brain) { this.brain = brain; }
    public DamageSystem Damage => brain.GetModule<DamageSystem>();
}