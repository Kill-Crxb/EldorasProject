using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum FeetContactType
{
    Ground, Wall, Ceiling, Unknown
}

public class ControllerBrain : MonoBehaviour
{
    private const float BRAIN_GIZMO_HEIGHT = 3f;
    private const float BRAIN_GIZMO_RADIUS = 0.5f;

    [Header("Auto-Discovery")]
    [SerializeField] private bool autoDiscoverModules = true;
    [SerializeField] private bool debugModuleStatus = false;

    [Header("Player References")]
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private Animator playerAnimator;

    [Header("Core Modules (Auto-discovered)")]
    [SerializeField] private AnimationStateModule animationStateModule;
    [SerializeField] private RPGCoreStats rpgCoreStats;
    [SerializeField] private RPGSecondaryStats rpgSecondaryStats;
    [SerializeField] private RPGResources rpgResources;

    [Header("External References (Optional for NPCs)")]
    [SerializeField] private SimpleThirdPersonCamera cameraController;
    [SerializeField] private ThirdPersonUI uiController;

    private PlayerInputControls playerInputControls;
    private IBrainModule[] allModules;
    private Dictionary<System.Type, IBrainModule> moduleMap;
    private Dictionary<System.Type, IBrainModule> interfaceMap;

    private ThirdPersonController cachedController;
    private HashSet<Collider> groundContacts = new HashSet<Collider>();
    private bool isGroundedFromFeetDetection = false;

    public bool IsInitialized { get; private set; }

    public Transform PlayerRoot => playerRoot;
    public Transform ModelRoot => modelRoot;
    public Animator PlayerAnimator => playerAnimator;
    public SimpleThirdPersonCamera CameraController => cameraController;
    public ThirdPersonUI UIController => uiController;

    public AnimationStateModule AnimationState => animationStateModule;
    public RPGCoreStats RPGCoreStats => rpgCoreStats;
    public RPGSecondaryStats RPGSecondaryStats => rpgSecondaryStats;
    public RPGResources RPGResources => rpgResources;

    public ThirdPersonController Controller => cachedController ?? (cachedController = GetModule<ThirdPersonController>());
    public TargetLockModule TargetLock => GetModule<TargetLockModule>();
    public AdvancedMovementModule AdvancedMovement => GetModule<AdvancedMovementModule>();
    public FeetDetectionModule FeetDetection => GetModule<FeetDetectionModule>();

    public bool IsPlayer => cachedController != null;
    public bool IsNPC => GetModule<NPCMovementModule>() != null;

    public event System.Action<Collider, FeetContactType> OnFeetEnter;
    public event System.Action<Collider, FeetContactType> OnFeetExit;
    public event System.Action<Collider, FeetContactType> OnFeetStay;

    public bool IsGrounded
    {
        get
        {
            var feetModule = GetModule<FeetDetectionModule>();
            if (feetModule != null)
                return isGroundedFromFeetDetection;

            var movementState = GetModuleImplementing<IMovementState>();
            if (movementState != null)
                return movementState.IsGrounded;

            var charController = GetComponentInParent<CharacterController>();
            return charController?.isGrounded ?? false;
        }
    }

    void Awake()
    {
        SetupPlayerReferences();

        if (autoDiscoverModules)
        {
            DiscoverChildModules();
        }

        ValidateRequiredModules();
        InitializeInputSystem();
        InitializeModules();

        IsInitialized = true;

        if (debugModuleStatus)
        {
            LogModuleStatus();
        }
    }

    void SetupPlayerReferences()
    {
        if (playerRoot == null)
        {
            playerRoot = transform.parent ?? transform;
        }

        if (modelRoot == null && playerRoot != null)
        {
            modelRoot = playerRoot.Find("3D Model") ??
                        playerRoot.Find("Model") ??
                        playerRoot.Find("Visual") ??
                        playerRoot.Find("Armature");
        }

        if (playerAnimator == null)
        {
            if (modelRoot != null)
                playerAnimator = modelRoot.GetComponentInChildren<Animator>();

            if (playerAnimator == null && playerRoot != null)
                playerAnimator = playerRoot.GetComponentInChildren<Animator>();
        }
    }

    void DiscoverChildModules()
    {
        var childModules = new List<IBrainModule>();
        moduleMap = new Dictionary<System.Type, IBrainModule>();
        interfaceMap = new Dictionary<System.Type, IBrainModule>();

        for (int i = 0; i < transform.childCount; i++)
        {
            var packageObject = transform.GetChild(i);

            if (packageObject.name.StartsWith("Component_"))
            {
                var modulesInPackage = packageObject.GetComponentsInChildren<IBrainModule>(true);

                foreach (var module in modulesInPackage)
                {
                    if (!childModules.Contains(module))
                    {
                        childModules.Add(module);
                        moduleMap[module.GetType()] = module;
                        RegisterModuleInterfaces(module);
                    }
                }

                if (animationStateModule == null)
                    animationStateModule = packageObject.GetComponentInChildren<AnimationStateModule>(true);
                if (rpgCoreStats == null)
                    rpgCoreStats = packageObject.GetComponentInChildren<RPGCoreStats>(true);
                if (rpgSecondaryStats == null)
                    rpgSecondaryStats = packageObject.GetComponentInChildren<RPGSecondaryStats>(true);
                if (rpgResources == null)
                    rpgResources = packageObject.GetComponentInChildren<RPGResources>(true);
            }
        }

        allModules = childModules.ToArray();

        if (cameraController == null)
            cameraController = FindFirstObjectByType<SimpleThirdPersonCamera>();

        if (uiController == null)
            uiController = FindFirstObjectByType<ThirdPersonUI>();

        if (debugModuleStatus)
        {
            Debug.Log($"[Brain] Discovered {allModules.Length} modules");
        }
    }

    void RegisterModuleInterfaces(IBrainModule module)
    {
        var moduleType = module.GetType();
        var interfaces = moduleType.GetInterfaces();

        foreach (var iface in interfaces)
        {
            if (iface == typeof(IPlayerModule) || iface == typeof(IBrainModule))
                continue;

            if (!interfaceMap.ContainsKey(iface))
            {
                interfaceMap[iface] = module;
            }
        }
    }

    void InitializeInputSystem()
    {
        if (!IsNPC)
        {
            playerInputControls = new PlayerInputControls();
            cachedController = GetModule<ThirdPersonController>();

            if (cachedController != null)
            {
                var connector = cachedController.GetComponent<ControllerBrainConnector>();
                if (connector == null)
                {
                    connector = cachedController.gameObject.AddComponent<ControllerBrainConnector>();
                }
                connector.Initialize(this);
            }
        }
    }

    void ValidateRequiredModules()
    {
        bool hasErrors = false;

        var movementState = GetModuleImplementing<IMovementState>();
        if (movementState == null)
        {
            Debug.LogError($"[Brain] CRITICAL: No IMovementState found on {gameObject.name}! Add ThirdPersonController (Player) or NPCMovementModule (NPC).");
            hasErrors = true;
        }

        if (rpgCoreStats == null)
        {
            Debug.LogError($"[Brain] CRITICAL: No RPGCoreStats found on {gameObject.name}!");
            hasErrors = true;
        }

        if (rpgSecondaryStats == null)
        {
            Debug.LogError($"[Brain] CRITICAL: No RPGSecondaryStats found on {gameObject.name}!");
            hasErrors = true;
        }

        if (rpgResources == null)
        {
            Debug.LogError($"[Brain] CRITICAL: No RPGResources found on {gameObject.name}!");
            hasErrors = true;
        }

        if (animationStateModule == null)
        {
            Debug.LogError($"[Brain] CRITICAL: No AnimationStateModule found on {gameObject.name}!");
            hasErrors = true;
        }

        if (IsPlayer && cameraController == null)
        {
            Debug.LogWarning($"[Brain] Player character {gameObject.name} has no camera assigned.");
        }

        if (hasErrors)
        {
            Debug.LogError($"[Brain] {gameObject.name} is missing required modules. Brain disabled.");
            enabled = false;
            return;
        }

        if (debugModuleStatus)
        {
            string characterType = IsPlayer ? "Player" : IsNPC ? "NPC" : "Unknown";
            Debug.Log($"[Brain] {characterType} '{gameObject.name}' validated successfully!");
        }
    }

    void InitializeModules()
    {
        if (allModules == null || allModules.Length == 0)
        {
            Debug.LogWarning("[Brain] No modules found to initialize!");
            return;
        }

        foreach (var module in allModules)
        {
            try
            {
                module.Initialize(this);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Brain] Failed to initialize module {module.GetType().Name}: {e.Message}");
            }
        }

        if (uiController != null)
        {
            uiController.RefreshModules();
        }
    }

    void LogModuleStatus()
    {
        string characterType = IsPlayer ? "Player" : IsNPC ? "NPC" : "Unknown";
        Debug.Log($"[Brain] {characterType} with {allModules?.Length ?? 0} modules | " +
                  $"Movement: {GetModuleImplementing<IMovementState>() != null} | " +
                  $"TargetLock: {GetModule<TargetLockModule>() != null} | " +
                  $"AdvMovement: {GetModule<AdvancedMovementModule>() != null} | " +
                  $"FeetDetect: {GetModule<FeetDetectionModule>() != null}");

        if (allModules != null && debugModuleStatus)
        {
            foreach (var module in allModules)
            {
                string status = module.IsEnabled ? "✓" : "✗";
                Debug.Log($"  {status} {module.GetType().Name}");
            }
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
            playerInputControls.Disable();
        }
    }

    void OnDestroy()
    {
        playerInputControls?.Dispose();
        groundContacts.Clear();
    }

    void SubscribeToInputs()
    {
        if (playerInputControls == null || cachedController == null) return;

        playerInputControls.Player.Move.performed += cachedController.OnMoveInput;
        playerInputControls.Player.Move.canceled += cachedController.OnMoveInput;
        playerInputControls.Player.Sprint.started += cachedController.OnSprintStarted;
        playerInputControls.Player.Sprint.canceled += cachedController.OnSprintCanceled;

        if (allModules != null)
        {
            foreach (var module in allModules)
            {
                if (module is IInputHandler inputHandler && module.IsEnabled)
                {
                    try
                    {
                        inputHandler.SubscribeToInputs(playerInputControls);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[Brain] Failed to subscribe inputs for {module.GetType().Name}: {e.Message}");
                    }
                }
            }
        }
    }

    void UnsubscribeFromInputs()
    {
        if (playerInputControls == null || cachedController == null) return;

        playerInputControls.Player.Move.performed -= cachedController.OnMoveInput;
        playerInputControls.Player.Move.canceled -= cachedController.OnMoveInput;
        playerInputControls.Player.Sprint.started -= cachedController.OnSprintStarted;
        playerInputControls.Player.Sprint.canceled -= cachedController.OnSprintCanceled;

        if (allModules != null)
        {
            foreach (var module in allModules)
            {
                if (module is IInputHandler inputHandler)
                {
                    try
                    {
                        inputHandler.UnsubscribeFromInputs(playerInputControls);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[Brain] Failed to unsubscribe inputs for {module.GetType().Name}: {e.Message}");
                    }
                }
            }
        }
    }

    void Update()
    {
        if (!IsInitialized) return;

        if (allModules != null)
        {
            foreach (var module in allModules)
            {
                if (module != null && module.IsEnabled)
                {
                    try
                    {
                        module.UpdateModule();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[Brain] Module update failed for {module.GetType().Name}: {e.Message}");
                    }
                }
            }
        }
    }

    #region Feet Detection

    public void NotifyFeetEnter(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground)
        {
            groundContacts.Add(col);
            isGroundedFromFeetDetection = true;
        }

        OnFeetEnter?.Invoke(col, type);
    }

    public void NotifyFeetExit(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground)
        {
            groundContacts.Remove(col);
            isGroundedFromFeetDetection = groundContacts.Count > 0;
        }

        OnFeetExit?.Invoke(col, type);
    }

    public void NotifyFeetStay(Collider col, FeetContactType type)
    {
        if (type == FeetContactType.Ground)
        {
            groundContacts.Add(col);
            isGroundedFromFeetDetection = true;
        }

        OnFeetStay?.Invoke(col, type);
    }

    #endregion

    #region RPG Stats Convenience Methods

    public float GetCoreStatValue(string statName) => rpgCoreStats?.GetStatFinalValue(statName) ?? 0f;
    public float GetCurrentHealth() => rpgResources?.CurrentHealth ?? 100f;
    public float GetMaxHealth() => rpgResources?.MaxHealth ?? 100f;
    public float GetCurrentMana() => rpgResources?.CurrentMana ?? 50f;
    public float GetMaxMana() => rpgResources?.MaxMana ?? 50f;
    public float GetCurrentStamina() => rpgResources?.CurrentStamina ?? 100f;
    public float GetMaxStamina() => rpgResources?.MaxStamina ?? 100f;
    public float GetMeleePower() => rpgSecondaryStats?.MeleePowerFinal ?? 0f;
    public float GetMagicPower() => rpgSecondaryStats?.MagicPowerFinal ?? 0f;
    public float GetMeleeCritChance() => rpgSecondaryStats?.MeleeCritChanceFinal ?? 5f;
    public float GetMagicCritChance() => rpgSecondaryStats?.MagicCritChanceFinal ?? 5f;
    public float GetMeleeSpeed() => rpgSecondaryStats?.MeleeSpeedFinal ?? 1f;
    public float GetMagicSpeed() => rpgSecondaryStats?.MagicSpeedFinal ?? 1f;
    public float GetArmor() => rpgSecondaryStats?.ArmorFinal ?? 0f;
    public float GetDamageReduction() => rpgSecondaryStats?.DamageReductionFinal ?? 0f;
    public float GetMagicResistance() => rpgSecondaryStats?.MagicResistanceFinal ?? 0f;

    public bool HasRPGStats => rpgCoreStats != null && rpgSecondaryStats != null && rpgResources != null;

    #endregion

    #region Public API

    public PlayerInputControls GetInputControls() => playerInputControls;

    public T GetModule<T>() where T : class, IBrainModule
    {
        if (moduleMap != null && moduleMap.TryGetValue(typeof(T), out var module))
        {
            return module as T;
        }
        return null;
    }

    public T GetModuleImplementing<T>() where T : class
    {
        if (interfaceMap != null && interfaceMap.TryGetValue(typeof(T), out var module))
        {
            return module as T;
        }
        return null;
    }

    public T AddModule<T>(string componentName = null) where T : Component, IBrainModule
    {
        string moduleName = componentName ?? $"Component_{typeof(T).Name}";
        GameObject moduleObj = new GameObject(moduleName);
        moduleObj.transform.SetParent(transform);
        T module = moduleObj.AddComponent<T>();

        DiscoverChildModules();

        try
        {
            module.Initialize(this);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Brain] Failed to initialize dynamically added module: {e.Message}");
        }

        return module;
    }

    public void RemoveModule<T>() where T : class, IBrainModule
    {
        T module = GetModule<T>();
        if (module != null && module is Component comp)
        {
            DestroyImmediate(comp.gameObject);
            DiscoverChildModules();
        }
    }

    public bool HasModule<T>() where T : class, IBrainModule => GetModule<T>() != null;
    public bool HasModuleImplementing<T>() where T : class => GetModuleImplementing<T>() != null;

    public int GetGroundContactCount() => groundContacts.Count;

    #endregion

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
            Gizmos.color = Color.cyan;
            foreach (var contact in groundContacts)
            {
                if (contact != null)
                {
                    Gizmos.DrawLine(transform.position, contact.transform.position);
                }
            }
        }
    }
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