// Melee Module - Updated for Animation Event System
using UnityEngine;
using UnityEngine.InputSystem;

public class MeleeModule : MonoBehaviour, IPlayerModule, IInputHandler
{
    [Header("Sub-Module References")]
    [SerializeField] private AttackModule attackModule;
    [SerializeField] private ComboModule comboModule;
    [SerializeField] private ActiveDefenseModule activeDefenseModule;

    [Header("Auto-Discovery")]
    [SerializeField] private bool autoDiscoverSubModules = true;

    [Header("Debug")]
    [SerializeField] private bool debugMelee = false;

    // Component references
    private ThirdPersonController controller;
    private AnimationStateModule animationState;
    private AdvancedMovementModule advancedMovement;
    private WeaponModule weaponModule;
    private ControllerBrain brain;
    private PlayerInputControls inputControls;
    private bool isSubscribedToInput;

    // Sub-modules list for easy iteration
    private IMeleeSubModule[] subModules;

    // Aggregated events (forwarded from sub-modules)
    public System.Action OnAttackBegin;
    public System.Action OnAttackComplete;
    public System.Action OnBlockBegin;
    public System.Action OnBlockComplete;
    public System.Action OnCancelWindowOpened;
    public System.Action OnCancelWindowClosed;
    public System.Action<int> OnComboHit;
    public System.Action OnPerfectBlock;
    public System.Action<float> OnDamageDealt;

    // NEW: Animation event forwarding
    public System.Action OnWeaponEnabled;  // Forwarded from Attack_On
    public System.Action OnWeaponDisabled; // Forwarded from Attack_Off
    public System.Action OnComboIncremented; // Forwarded from Combo_Up

    // IPlayerModule implementation
    public bool IsEnabled { get; set; } = true;

    // Aggregated properties (from sub-modules)
    public bool IsAttacking => attackModule?.IsAttacking ?? false;
    public bool IsBlocking => activeDefenseModule?.IsBlocking ?? false;
    public bool IsBusyWithMelee => IsAttacking || IsBlocking;
    public int CurrentComboCount => comboModule?.CurrentComboCount ?? 0;
    public bool CanCancelAttack => attackModule?.CanCombo ?? false; // Updated to use CanCombo

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        controller = brain.Controller;
        animationState = brain.AnimationState;
        advancedMovement = brain.AdvancedMovement;
        weaponModule = brain.GetModule<WeaponModule>();

        if (autoDiscoverSubModules)
        {
            DiscoverSubModules();
        }

        InitializeSubModules();
        SubscribeToSubModuleEvents();


    }

    public void UpdateModule()
    {
        UpdateSubModules();
        UpdateAnimationState();
    }

    void DiscoverSubModules()
    {
        if (attackModule == null)
            attackModule = GetComponentInChildren<AttackModule>();
        if (comboModule == null)
            comboModule = GetComponentInChildren<ComboModule>();
        if (activeDefenseModule == null)
            activeDefenseModule = GetComponentInChildren<ActiveDefenseModule>();

        subModules = new IMeleeSubModule[]
        {
            attackModule,
            comboModule,
            activeDefenseModule
        };

        if (debugMelee)
        {
            Debug.Log($"Melee: Discovered {subModules.Length} melee sub-modules");
        }
    }

    void InitializeSubModules()
    {
        foreach (var subModule in subModules)
        {
            if (subModule != null)
            {
                try
                {
                    subModule.Initialize(this);

                    if (subModule is IInputHandler inputHandler && inputControls != null)
                    {
                        inputHandler.SubscribeToInputs(inputControls);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to initialize {subModule.GetType().Name}: {e.Message}");
                }
            }
        }
    }

    void UpdateSubModules()
    {
        foreach (var subModule in subModules)
        {
            if (subModule != null && subModule.IsEnabled)
            {
                try
                {
                    subModule.UpdateSubModule();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error updating {subModule.GetType().Name}: {e.Message}");
                }
            }
        }
    }

    void SubscribeToSubModuleEvents()
    {
        // Attack events
        if (attackModule != null)
        {
            attackModule.OnAttackBegin += () => OnAttackBegin?.Invoke();
            attackModule.OnAttackComplete += () => OnAttackComplete?.Invoke();
            attackModule.OnCancelWindowOpened += () => OnCancelWindowOpened?.Invoke();
            attackModule.OnCancelWindowClosed += () => OnCancelWindowClosed?.Invoke();
            attackModule.OnDamageDealt += (damage) => OnDamageDealt?.Invoke(damage);
        }

        // Combo events
        if (comboModule != null)
        {
            comboModule.OnComboHit += (count) => OnComboHit?.Invoke(count);
        }

        // Blocking events
        if (activeDefenseModule != null)
        {
            activeDefenseModule.OnBlockBegin += () => OnBlockBegin?.Invoke();
            activeDefenseModule.OnBlockComplete += () => OnBlockComplete?.Invoke();
            activeDefenseModule.OnPerfectBlock += () => OnPerfectBlock?.Invoke();
        }
    }

    void UpdateAnimationState()
    {
        if (animationState == null) return;

        if (IsBusyWithMelee)
        {
            animationState.SetStateLocked();
        }
        else if (IsBlocking)
        {
            animationState.SetStateRestricted();
        }
        else
        {
            animationState.SetStateFree();
        }
    }

    #region Animation Event Handlers (Called by Animation Events)

    /// <summary>
    /// Called by Animation Event: Attack_On
    /// Enables weapon collision and notifies all listeners
    /// </summary>
    public void Attack_On()
    {
        // Forward to attack module
        attackModule?.Attack_On();

        // NOTE: Removed OnWeaponEnabled broadcast - AttackModule already enables current weapon

        if (debugMelee)
            Debug.Log("MeleeModule: Weapon collision ENABLED via animation event");
    }

    /// <summary>
    /// Called by Animation Event: Attack_Off  
    /// Disables weapon collision and notifies all listeners
    /// </summary>
    public void Attack_Off()
    {
        // Forward to attack module
        attackModule?.Attack_Off();

        // NOTE: Removed OnWeaponDisabled broadcast - AttackModule already disables current weapon

        if (debugMelee)
            Debug.Log("MeleeModule: Weapon collision DISABLED via animation event");
    }

    /// <summary>
    /// Called by Animation Event: Combo_Up
    /// Increments combo counter and opens combo window
    /// </summary>
    public void Combo_Up()
    {
        // Forward to attack module (handles combo increment + window opening)
        attackModule?.Combo_Up();

        // Notify other systems
        OnComboIncremented?.Invoke();

        if (debugMelee)
            Debug.Log($"MeleeModule: Combo incremented to {CurrentComboCount} via animation event");
    }

    /// <summary>
    /// Optional: Called by Animation Event to close combo window
    /// </summary>
    public void Combo_Window_Close()
    {
        attackModule?.Combo_Window_Close();

        if (debugMelee)
            Debug.Log("MeleeModule: Combo window CLOSED via animation event");
    }

    /// <summary>
    /// Optional: Called by Animation Event to end attack completely
    /// </summary>
    public void Attack_Complete()
    {
        attackModule?.Attack_Complete();

        if (debugMelee)
            Debug.Log("MeleeModule: Attack completed via animation event");
    }

    #endregion

    #region Input System

    void Start()
    {
        if (brain != null)
        {
            inputControls = brain.GetInputControls();
            SubscribeToInputs(inputControls);

            foreach (var subModule in subModules)
            {
                if (subModule is IInputHandler inputHandler)
                {
                    inputHandler.SubscribeToInputs(inputControls);
                }
            }
        }
    }

    void OnDestroy()
    {
        UnsubscribeFromInputs(inputControls);

        foreach (var subModule in subModules)
        {
            if (subModule is IInputHandler inputHandler)
            {
                inputHandler.UnsubscribeFromInputs(inputControls);
            }
        }
    }

    public void SubscribeToInputs(PlayerInputControls playerInputControls)
    {
        if (playerInputControls == null || isSubscribedToInput) return;

        try
        {
            if (attackModule != null)
            {
                playerInputControls.Player.Attack.started += attackModule.OnAttackStarted;
                playerInputControls.Player.Attack.canceled += attackModule.OnAttackCanceled;
            }

            isSubscribedToInput = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"MeleeModule: Some inputs not available: {e.Message}");
        }
    }

    public void UnsubscribeFromInputs(PlayerInputControls playerInputControls)
    {
        if (!isSubscribedToInput || playerInputControls == null) return;

        try
        {
            if (attackModule != null)
            {
                playerInputControls.Player.Attack.started -= attackModule.OnAttackStarted;
                playerInputControls.Player.Attack.canceled -= attackModule.OnAttackCanceled;
            }

            isSubscribedToInput = false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"MeleeModule: Error unsubscribing: {e.Message}");
        }
    }

    #endregion

    #region Public API (Delegates to Sub-Modules)

    // Attack delegation methods
    public bool CanAttack() => attackModule?.CanAttack() ?? false;
    public bool CanHeavyAttack() => attackModule?.CanHeavyAttack() ?? false;
    public void StartLightAttack() => attackModule?.StartLightAttack();
    public void StartHeavyAttack() => attackModule?.StartHeavyAttack();

    // Blocking delegation methods  
    public bool CanBlock() => activeDefenseModule?.CanBlock() ?? false;
    public void StartBlock() => activeDefenseModule?.StartBlock();
    public void StopBlock() => activeDefenseModule?.StopBlock();

    // Combo delegation methods
    public bool IsInCombo() => comboModule?.IsInCombo ?? false;

    // Movement integration
    public bool CanAllowMovementAction()
    {
        return attackModule?.CanAllowMovementAction() ?? true;
    }

    // Force end all actions
    public void ForceEndCurrentAction()
    {
        attackModule?.ForceEndCurrentAction();
        activeDefenseModule?.ForceEndCurrentAction();

        if (debugMelee)
            Debug.Log("Melee: All actions force-ended");
    }

    // Damage and combat calculations
    public float CalculateDamage(bool isHeavyAttack = false, bool isCritical = false)
    {
        return attackModule?.CalculateDamage(isHeavyAttack, isCritical) ?? 0f;
    }

    public bool TryPerfectBlock(float blockWindow = 0.2f)
    {
        return activeDefenseModule?.TryPerfectBlock(blockWindow) ?? false;
    }

    // Getters
    public float GetCurrentWeaponReach() => attackModule?.GetWeaponReach() ?? 1.5f;
    public float GetCurrentWeaponDamageMultiplier() => attackModule?.GetWeaponReach() ?? 1f;

    #endregion

    #region Sub-Module Access (for other modules)

    public AttackModule Attack => attackModule;
    public ComboModule Combo => comboModule;
    public ActiveDefenseModule ActiveDefense => activeDefenseModule;

    // Getters for shared resources
    public ThirdPersonController Controller => controller;
    public AnimationStateModule AnimationState => animationState;
    public ControllerBrain Brain => brain;
    public WeaponModule WeaponModule => weaponModule;

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        if (!debugMelee) return;

        foreach (var subModule in subModules)
        {
            if (subModule is MonoBehaviour mb)
            {
                mb.SendMessage("DrawMeleeGizmos", SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    void OnGUI()
    {
        if (!debugMelee) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 220));
        GUILayout.Label("=== MELEE COORDINATOR ===");
        GUILayout.Label($"Is Attacking: {IsAttacking}");
        GUILayout.Label($"Is Blocking: {IsBlocking}");
        GUILayout.Label($"Combo Count: {CurrentComboCount}");
        GUILayout.Label($"Can Combo: {attackModule?.CanCombo ?? false}");

        GUILayout.Label("Animation Event System: ✅");
        GUILayout.Label("Melee Sub-modules:");
        GUILayout.Label($"  Attack: {(attackModule != null ? "✅" : "❌")}");
        GUILayout.Label($"  Combo: {(comboModule != null ? "✅" : "❌")}");
        GUILayout.Label($"  ActiveDefense: {(activeDefenseModule != null ? "✅" : "❌")}");

        GUILayout.EndArea();
    }

    #endregion
}