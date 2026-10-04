using System;
using System.Collections.Generic;
using System.Linq;
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

    [Header("Entity Identity")]
    [SerializeField] private EntityType entityType = EntityType.Entity;

    #endregion

    #region Private Fields

    private IBrainModule[] updateModules;
    private IInputHandler[] inputHandlers;
    private Dictionary<Type, object> providerCache = new Dictionary<Type, object>();
    private PlayerInputControls playerInputControls;
    private FeetDetectionModule feetDetection;

    #endregion

    #region Properties — State

    public bool IsInitialized { get; private set; }

    // This character's data is in place: a save restored for the player, or simply spawned for
    // everyone else. Per character, so one entity's load never re-runs another's handlers.
    public bool IsLoaded { get; private set; }
    public event Action OnLoaded;
    public event Action<ControllerBrain> OnInitialized;

    #endregion

    #region Properties — Root Transforms

    public Transform EntityRoot => e_Root;
    public Transform ModelRoot => m_Root;
    public Animator EntityAnimator => animator;

    #endregion

    #region Properties — Systems (Ordered by Init Dependency)

    // Shortcuts into the provider cache, which discovery fills; null when the entity lacks the module.
    public IdentitySystem Identity => GetProvider<IdentitySystem>();
    public FactionSystem Faction => GetProvider<FactionSystem>();
    public ModelModule Model => GetProvider<ModelModule>();
    public InputSystem Input => GetProvider<InputSystem>();
    public StateMachineModule StateMachine => GetProvider<StateMachineModule>();
    public MovementSystem Movement => GetProvider<MovementSystem>();
    public AnimationSystem Animation => GetProvider<AnimationSystem>();
    public AbilitySystem Abilities => GetProvider<AbilitySystem>();
    public StatSystem Stats => GetProvider<StatSystem>();
    public ResourceSystem Resources => GetProvider<ResourceSystem>();
    public RPGSystem RPG => GetProvider<RPGSystem>();
    public DamageSystem Damage => GetProvider<DamageSystem>();
    public Blackboard Blackboard => GetProvider<BlackboardSystem>()?.Blackboard;
    public InventorySystem Inventory => GetProvider<InventorySystem>();
    public InteractionSystem Interaction => GetProvider<InteractionSystem>();
    public DialogueSystem Dialogue => GetProvider<DialogueSystem>();
    public HotbarSystem Hotbar => GetProvider<HotbarSystem>();
    public SlotTransformationSystem SlotTransform => GetProvider<SlotTransformationSystem>();

    #endregion

    #region Properties — Entity Identity

    public EntityType EntityType => entityType;
    public bool IsPlayer => entityType == EntityType.Player;

    /// <summary>
    /// The entity's name for logs — the root object ("TargetDummy_Heavy", "Porphi"), not the
    /// module child this brain sits on, which is "Component_Brain" on every entity.
    /// </summary>
    public string EntityName => transform.root.name;
    public bool IsNPC => entityType == EntityType.NPC;

    #endregion

    #region Properties — Feet Detection

    public bool IsGrounded => feetDetection?.IsGrounded ?? false;

    #endregion

    #region Properties — Convenience Accessors

    public IHealthProvider Health => GetProvider<IHealthProvider>();

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

    // The player is marked loaded by SaveManager once its save is restored.
    void Start()
    {
        if (!IsPlayer) MarkLoaded();
    }

    public void MarkLoaded()
    {
        if (IsLoaded) return;
        IsLoaded = true;
        OnLoaded?.Invoke();
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

    // OrderBy is stable, so modules sharing an InitOrder keep their hierarchy order.
    void CacheModuleArrays()
    {
        updateModules = GetComponentsInChildren<IBrainModule>(true).OrderBy(m => m.InitOrder).ToArray();
        inputHandlers = updateModules.OfType<IInputHandler>().ToArray();
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
        foreach (var module in updateModules)
        {
            RegisterProvider(module.GetType(), module);

            // A declined module stays findable by its own type but answers for no interface: an
            // NPC's switched-off camera answering ICameraProvider would steer it by the player's view.
            if (IsDeclined(module)) continue;

            foreach (var contract in module.GetType().GetInterfaces())
                if (!providerCache.ContainsKey(contract)) providerCache[contract] = module;
        }

        feetDetection = GetProvider<FeetDetectionModule>();
    }

    // Interfaces may have several implementers (control sources); the first in InitOrder answers.
    // Two modules of one concrete type is a prefab mistake.
    void RegisterProvider(Type type, IBrainModule module)
    {
        if (providerCache.TryGetValue(type, out object existing))
        {
            Debug.LogError($"[ControllerBrain] {EntityName} has two {type.Name} modules ({(existing as Component)?.name}, {(module as Component)?.name}); using the first.", this);
            return;
        }

        providerCache[type] = module;
    }

    bool IsDeclined(IBrainModule module) => module.PlayerOnly && !IsPlayer;

    #endregion

    #region Initialization — Phase 5: Module Initialization

    // A declined module is switched off, not merely skipped: CameraModule's Initialize locks the
    // cursor, so initializing it on an NPC would take the cursor from the player.
    void InitializeModules()
    {
        foreach (var module in updateModules)
        {
            if (IsDeclined(module))
            {
                module.IsEnabled = false;
                continue;
            }

            module.Initialize(this);
        }
    }

    #endregion

    #region Initialization — Phase 6: Late Initialization

    void LateInitializeModules()
    {
        foreach (var module in updateModules)
        {
            if (!ShouldRun(module)) continue;
            module.LateInitialize();
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

    #endregion

    #region Provider Lookup

    public T GetProvider<T>() where T : class
    {
        if (providerCache.TryGetValue(typeof(T), out object provider))
            return provider as T;
        return null;
    }

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

    public void SetAnimatorDirect(Animator newAnimator)
    {
        animator = newAnimator;
    }

    #endregion
}