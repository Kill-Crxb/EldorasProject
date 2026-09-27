using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using RPG.Factions;

public enum FeetContactType { Ground, Wall, Ceiling, Unknown }

public class ControllerBrain : MonoBehaviour
{
    #region Inspector

    [Header("Root References")]
    [SerializeField] private Transform e_Root;
    [SerializeField] private Transform m_Root;
    [SerializeField] private Animator animator;

    [Header("Core Systems")]
    [SerializeField] private IdentitySystem identitySystem;
    [SerializeField] private FactionSystem factionSystem;
    [SerializeField] private ModelModule modelModule;
    [SerializeField] private StateMachineModule stateMachineModule;
    [SerializeField] private MovementSystem movementSystem;
    [SerializeField] private AnimationSystem animationSystem;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private StatSystem statSystem;
    [SerializeField] private ResourceSystem resourceSystem;

    [Header("Player-Only Systems")]
    [SerializeField] private InputSystem inputSystem;
    [SerializeField] private CameraModule cameraModule;

    [Header("Gameplay Systems")]
    [SerializeField] private RPGSystem rpgSystem;
    [SerializeField] private DamageSystem damageSystem;
    [SerializeField] private BlackboardSystem blackboardSystem;
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private InteractionSystem interactionSystem;
    [SerializeField] private DialogueSystem dialogueSystem;
    [SerializeField] private HotbarSystem hotbarSystem;
    [SerializeField] private SlotTransformationSystem slotTransformationSystem;

    [Header("Entity Identity")]
    [SerializeField] private EntityType entityType = EntityType.Entity;

    #endregion

    #region Private Fields

    private IBrainModule[] updateModules;
    private IPhysicsModule[] physicsModules;
    private IInputHandler[] inputHandlers;
    private Dictionary<Type, object> providerCache = new Dictionary<Type, object>();
    private PlayerInputControls playerInputControls;
    private HashSet<Collider> groundContacts = new HashSet<Collider>();
    private FeetDetectionModule feetDetection;

    #endregion

    #region Properties — State

    public bool IsInitialized { get; private set; }
    public event Action<ControllerBrain> OnInitialized;

    #endregion

    #region Properties — Root Transforms

    public Transform EntityRoot => e_Root;
    public Transform ModelRoot => m_Root;
    public Animator EntityAnimator => animator;

    #endregion

    #region Properties — Systems (Ordered by Init Dependency)

    public IdentitySystem Identity => identitySystem;
    public FactionSystem Faction => factionSystem;
    public ModelModule Model => modelModule;
    public InputSystem Input => inputSystem;
    public StateMachineModule StateMachine => stateMachineModule;
    public MovementSystem Movement => movementSystem;
    public AnimationSystem Animation => animationSystem;
    public AbilitySystem Abilities => abilitySystem;
    public StatSystem Stats => statSystem;
    public ResourceSystem Resources => resourceSystem;
    public ResourceSystem ResourceSys => resourceSystem;
    public RPGSystem RPG => rpgSystem;
    public DamageSystem Damage => damageSystem;
    public Blackboard Blackboard => blackboardSystem?.Blackboard;
    public BlackboardSystem BlackboardModule => blackboardSystem;
    public InventorySystem Inventory => inventorySystem;
    public InteractionSystem Interaction => interactionSystem;
    public DialogueSystem Dialogue => dialogueSystem;
    public HotbarSystem Hotbar => hotbarSystem;
    public SlotTransformationSystem SlotTransform => slotTransformationSystem;

    #endregion

    #region Properties — Entity Identity

    public EntityType EntityType => entityType;
    public bool IsPlayer => entityType == EntityType.Player;
    public bool IsNPC => entityType == EntityType.NPC;
    public bool IsEntity => entityType == EntityType.Entity;

    #endregion

    #region Properties — Feet Detection

    public event Action<Collider, FeetContactType> OnFeetEnter;
    public event Action<Collider, FeetContactType> OnFeetExit;
    public event Action<Collider, FeetContactType> OnFeetStay;
    public bool IsGrounded => feetDetection?.IsGrounded ?? false;
    public FeetDetectionModule FeetDetection => feetDetection;
    public int GetGroundContactCount() => groundContacts.Count;

    #endregion

    #region Properties — Convenience Accessors

    public IHealthProvider Health => GetProvider<IHealthProvider>();
    public IResourceProvider Resources_Provider => GetProvider<IResourceProvider>();
    public Blackboard Blackboard_Direct => blackboardSystem?.Blackboard;

    #endregion

    #region Initialization — Main Pipeline

    void Awake()
    {
        ResolveRootReferences();
        CacheModuleArrays();
        InitializeInputSystem();
        BuildProviderCache();
        InitializeModules();
        LateInitializeModules();
        IsInitialized = true;
        OnInitialized?.Invoke(this);

        if (IsPlayer)
            ManagerBrain.Instance?.GetManager<SaveManager>()?.SetPlayerBrain(this);
    }

    #endregion

    #region Initialization — Phase 1: Root References

    void ResolveRootReferences()
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

        if (animator == null && m_Root != null)
            animator = m_Root.GetComponentInChildren<Animator>();
        if (animator == null && e_Root != null)
            animator = e_Root.GetComponentInChildren<Animator>();
    }

    #endregion

    #region Initialization — Phase 2: Module Discovery

    void CacheModuleArrays()
    {
        var allModules = GetComponentsInChildren<IBrainModule>(true);
        var updateList = new List<IBrainModule>();
        var physicsList = new List<IPhysicsModule>();
        var inputList = new List<IInputHandler>();

        foreach (var module in allModules)
        {
            updateList.Add(module);

            if (module is IPhysicsModule pm)
                physicsList.Add(pm);

            if (module is IInputHandler ih)
                inputList.Add(ih);
        }

        updateModules = updateList.ToArray();
        physicsModules = physicsList.ToArray();
        inputHandlers = inputList.ToArray();
    }

    #endregion

    #region Initialization — Phase 3: Input System

    void InitializeInputSystem()
    {
        if (!IsPlayer) return;
        playerInputControls = new PlayerInputControls();
        playerInputControls.Enable();
    }

    #endregion

    #region Initialization — Phase 4: Provider Cache

    void BuildProviderCache()
    {
        if (identitySystem != null) providerCache[typeof(IdentitySystem)] = identitySystem;
        if (factionSystem != null) providerCache[typeof(FactionSystem)] = factionSystem;
        if (modelModule != null) providerCache[typeof(ModelModule)] = modelModule;
        if (stateMachineModule != null)
        {
            providerCache[typeof(StateMachineModule)] = stateMachineModule;
            providerCache[typeof(IStateProvider)] = stateMachineModule;
        }
        if (statSystem != null) providerCache[typeof(StatSystem)] = statSystem;

        if (inputSystem != null)
        {
            providerCache[typeof(InputSystem)] = inputSystem;
            providerCache[typeof(IInputProvider)] = inputSystem;
            providerCache[typeof(IMovementControlSource)] = inputSystem;
            providerCache[typeof(IAbilityControlSource)] = inputSystem;
        }

        if (movementSystem != null)
        {
            providerCache[typeof(MovementSystem)] = movementSystem;
        }

        if (animationSystem != null)
        {
            providerCache[typeof(AnimationSystem)] = animationSystem;
            providerCache[typeof(IAnimationProvider)] = animationSystem;
        }

        if (abilitySystem != null)
        {
            providerCache[typeof(AbilitySystem)] = abilitySystem;
            providerCache[typeof(IAbilityProvider)] = abilitySystem;
        }

        if (resourceSystem != null)
        {
            providerCache[typeof(ResourceSystem)] = resourceSystem;
            providerCache[typeof(IResourceProvider)] = resourceSystem;
            providerCache[typeof(IHealthProvider)] = resourceSystem;
        }

        if (rpgSystem != null) providerCache[typeof(RPGSystem)] = rpgSystem;
        if (damageSystem != null) providerCache[typeof(DamageSystem)] = damageSystem;
        if (blackboardSystem != null) providerCache[typeof(BlackboardSystem)] = blackboardSystem;
        if (inventorySystem != null)
        {
            providerCache[typeof(InventorySystem)] = inventorySystem;
            providerCache[typeof(IInventoryProvider)] = inventorySystem;
        }
        if (interactionSystem != null) providerCache[typeof(InteractionSystem)] = interactionSystem;
        if (dialogueSystem != null) providerCache[typeof(DialogueSystem)] = dialogueSystem;
        if (hotbarSystem != null) providerCache[typeof(HotbarSystem)] = hotbarSystem;
        if (slotTransformationSystem != null) providerCache[typeof(SlotTransformationSystem)] = slotTransformationSystem;
        if (cameraModule != null) providerCache[typeof(ICameraProvider)] = cameraModule;
    }

    #endregion

    #region Initialization — Phase 5: Module Initialization

    void InitializeModules()
    {
        feetDetection = GetComponentInChildren<FeetDetectionModule>();
        var initialized = new HashSet<IBrainModule>();
        var declined = new HashSet<IBrainModule>();

        void InitOrdered(IBrainModule module)
        {
            if (module == null) return;
            module.Initialize(this);
            initialized.Add(module);
        }

        // A module the ordered pass deliberately skipped. Without this the IsPlayer guards below
        // only change ORDERING — the fallback loop initializes them anyway, and CameraModule's
        // Initialize ends by locking the cursor, so every NPC spawn steals it from the player.
        void Decline(IBrainModule module)
        {
            if (module == null) return;
            declined.Add(module);
            module.IsEnabled = false;
        }

        InitOrdered(identitySystem);
        InitOrdered(factionSystem);
        InitOrdered(modelModule);

        if (IsPlayer) InitOrdered(inputSystem);
        else Decline(inputSystem);

        if (IsPlayer) InitOrdered(cameraModule);
        else Decline(cameraModule);

        InitOrdered(stateMachineModule);

        InitOrdered(movementSystem);
        InitOrdered(animationSystem);
        InitOrdered(abilitySystem);

        InitOrdered(statSystem);
        InitOrdered(resourceSystem);

        InitOrdered(blackboardSystem);
        InitOrdered(damageSystem);
        InitOrdered(inventorySystem);
        InitOrdered(rpgSystem);
        InitOrdered(interactionSystem);
        InitOrdered(dialogueSystem);
        InitOrdered(hotbarSystem);
        InitOrdered(slotTransformationSystem);

        foreach (var module in updateModules)
        {
            if (!initialized.Contains(module) && !declined.Contains(module))
                module.Initialize(this);
        }

        foreach (var module in physicsModules)
        {
            if (module is IBrainModule brainModule && !initialized.Contains(brainModule))
                brainModule.Initialize(this);
        }
    }

    #endregion

    #region Initialization — Phase 6: Late Initialization

    void LateInitializeModules()
    {
        var lateInitialized = new HashSet<IBrainModule>();

        foreach (var module in updateModules)
        {
            if (!ShouldRun(module)) continue;

            module.LateInitialize();
            lateInitialized.Add(module);
        }

        // Dormant while nothing implements IPhysicsModule, but the first one written would be
        // both an update module and a physics module, and would get LateInitialize twice.
        foreach (var module in physicsModules)
        {
            if (!(module is IBrainModule brainModule)) continue;
            if (!lateInitialized.Add(brainModule)) continue;
            if (!ShouldRun(brainModule)) continue;

            brainModule.LateInitialize();
        }
    }

    #endregion

    #region Input Lifecycle

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
        for (int i = 0; i < inputHandlers.Length; i++)
            inputHandlers[i].SubscribeToInputs(playerInputControls);
    }

    void UnsubscribeFromInputs()
    {
        if (playerInputControls == null) return;
        for (int i = 0; i < inputHandlers.Length; i++)
            inputHandlers[i].UnsubscribeFromInputs(playerInputControls);
    }

    #endregion

    #region Update Loops

    /// <summary>
    /// Modules are discovered with includeInactive, and the loops below drive them directly rather
    /// than through Unity's own Update — so without this neither IsEnabled nor the inspector's
    /// enable checkbox did anything, and turning a module off was a silent no-op.
    /// </summary>
    static bool ShouldRun(IBrainModule module)
    {
        if (module == null) return false;
        if (!module.IsEnabled) return false;
        if (module is Behaviour behaviour && !behaviour.enabled) return false;
        return true;
    }


    void Update()
    {
        if (!IsInitialized || updateModules == null) return;
        for (int i = 0; i < updateModules.Length; i++)
            if (ShouldRun(updateModules[i])) updateModules[i].UpdateModule();
    }

    void FixedUpdate()
    {
        if (!IsInitialized || physicsModules == null) return;
        for (int i = 0; i < physicsModules.Length; i++)
        {
            if (physicsModules[i] == null) continue;

            // Only gate the ones that are also brain modules. A physics module that is not an
            // IBrainModule has no IsEnabled to consult, and silently dropping it would be a
            // behaviour change rather than a fix.
            if (physicsModules[i] is IBrainModule brainModule && !ShouldRun(brainModule)) continue;

            physicsModules[i].PhysicsUpdate();
        }
    }

    #endregion

    #region Feet Detection Events

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
        T cached = GetProvider<T>();
        if (cached != null) return cached;
        return GetComponentInChildren<T>();
    }

    #endregion

    #region Utilities

    public PlayerInputControls GetInputControls() => playerInputControls;

    public void RefreshAnimatorReference()
    {
        if (m_Root != null) animator = m_Root.GetComponentInChildren<Animator>();
        if (animator == null && e_Root != null) animator = e_Root.GetComponentInChildren<Animator>();
    }
    // Add this method to ControllerBrain.cs after RefreshAnimatorReference() (around line 449)



    public void SetAnimatorDirect(Animator newAnimator)
    {
        animator = newAnimator;
    }

    #endregion
}