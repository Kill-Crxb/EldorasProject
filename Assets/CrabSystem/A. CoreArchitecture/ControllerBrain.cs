using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using RPG.Factions;

// One per character. Discovers every IBrainModule beneath it, orders them by InitOrder, builds
// the provider cache that GetModule / GetProvider read, and drives their Update. Modules sign up
// by being children; the brain names none of them (the typed properties below are shortcuts into
// the cache, kept so the call sites read naturally).
public class ControllerBrain : MonoBehaviour
{
    [Header("Root References")]
    [SerializeField] private Transform e_Root;
    [SerializeField] private Transform m_Root;
    [SerializeField] private Animator animator;

    [Header("Entity Identity")]
    [SerializeField] private EntityType entityType = EntityType.Entity;

    private IBrainModule[] updateModules;
    private IInputHandler[] inputHandlers;
    private readonly Dictionary<Type, object> providerCache = new Dictionary<Type, object>();
    private PlayerInputControls playerInputControls;
    private FeetDetectionModule feetDetection;

    public bool IsInitialized { get; private set; }

    // This character's data is in place: a save restored for the player, or simply spawned for
    // everyone else. Per character, so one entity's load never re-runs another's handlers.
    public bool IsLoaded { get; private set; }
    public event Action OnLoaded;
    public event Action<ControllerBrain> OnInitialized;

    public Transform EntityRoot => e_Root;
    public Transform ModelRoot => m_Root;
    public Animator EntityAnimator => animator;

    // Null when the entity lacks the module.
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
    public IHealthProvider Health => GetProvider<IHealthProvider>();

    public EntityType EntityType => entityType;
    public bool IsPlayer => entityType == EntityType.Player;
    public bool IsNPC => entityType == EntityType.NPC;

    // The root object's name ("TargetDummy_Heavy", "Porphi") for logs; the brain itself sits on
    // "Component_Brain" in every entity.
    public string EntityName => transform.root.name;

    public bool IsGrounded => feetDetection != null && feetDetection.IsGrounded;

    void Awake()
    {
        ResolveRootReferences();
        CacheModuleArrays();
        CreatePlayerInput();
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

    void ResolveRootReferences()
    {
        if (e_Root == null)
            e_Root = transform.parent ?? transform;

        if (m_Root == null)
            m_Root = e_Root.Find("3D Model") ?? e_Root.Find("Model") ?? e_Root.Find("Visual") ?? e_Root.Find("Armature");

        if (animator == null)
            RefreshAnimatorReference();
    }

    // OrderBy is stable, so modules sharing an InitOrder keep their hierarchy order.
    void CacheModuleArrays()
    {
        updateModules = GetComponentsInChildren<IBrainModule>(true).OrderBy(m => m.InitOrder).ToArray();
        inputHandlers = updateModules.OfType<IInputHandler>().ToArray();
    }

    void CreatePlayerInput()
    {
        if (!IsPlayer) return;
        playerInputControls = new PlayerInputControls();
        playerInputControls.Enable();
    }

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

    void LateInitializeModules()
    {
        foreach (var module in updateModules)
            if (ShouldRun(module)) module.LateInitialize();
    }

    void OnEnable()
    {
        if (playerInputControls == null) return;
        playerInputControls.Enable();
        foreach (var handler in inputHandlers)
            handler.SubscribeToInputs(playerInputControls);
    }

    void OnDisable()
    {
        StopPlayerInput();
    }

    void OnDestroy()
    {
        StopPlayerInput();
        playerInputControls?.Dispose();
    }

    void StopPlayerInput()
    {
        if (playerInputControls == null) return;
        foreach (var handler in inputHandlers)
            handler.UnsubscribeFromInputs(playerInputControls);
        playerInputControls.Player.Disable();
        playerInputControls.UI.Disable();
        playerInputControls.Disable();
    }

    // Modules are discovered with includeInactive and driven from here rather than by Unity's own
    // Update, so both IsEnabled and the inspector's enable checkbox have to be checked by hand.
    static bool ShouldRun(IBrainModule module)
    {
        if (module == null || !module.IsEnabled) return false;
        return !(module is Behaviour behaviour) || behaviour.enabled;
    }

    void Update()
    {
        if (!IsInitialized) return;
        for (int i = 0; i < updateModules.Length; i++)
            if (ShouldRun(updateModules[i])) updateModules[i].UpdateModule();
    }

    public T GetProvider<T>() where T : class
    {
        return providerCache.TryGetValue(typeof(T), out object provider) ? provider as T : null;
    }

    // Every module is in the cache; the search only serves non-module components (CharacterMotor).
    public T GetModule<T>() where T : class
    {
        return GetProvider<T>() ?? GetComponentInChildren<T>();
    }

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
}
