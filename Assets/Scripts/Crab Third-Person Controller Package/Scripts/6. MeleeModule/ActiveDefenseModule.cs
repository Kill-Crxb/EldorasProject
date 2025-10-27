using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum DefenseType
{
    Block,
    Parry,
    Dodge,
    Counter
}

[System.Serializable]
public class ActiveDefenseSettings
{
    [Header("Block Settings")]
    public float blockDamageReduction = 0.8f;
    public float blockStaminaCost = 5f;
    public float blockStaminaDrainRate = 5f;
    public float blockAngle = 120f;

    [Header("Perfect Block")]
    public bool enablePerfectBlock = true;
    public float perfectBlockWindow = 0.15f;
    public float perfectBlockStaminaCost = 0f;
    public float perfectBlockReflectDamage = 0.5f;
    public float perfectBlockChance = 0.1f;

    [Header("Parry Settings")]
    public bool canParry = true;
    public float parryWindow = 0.3f;
    public float parryStaminaCost = 10f;
    public float parryCounterWindow = 0.8f;

    [Header("Timing")]
    public float blockStartupTime = 0.1f;
    public float parryRecoveryTime = 0.5f;
}

public class ActiveDefenseModule : MonoBehaviour, IMeleeSubModule, IInputHandler, IDefenseCapability
{
    [Header("Defense Settings")]
    [SerializeField] private ActiveDefenseSettings defenseSettings;

    [Header("Animation Parameters")]
    [SerializeField] private string blockParam = "IsBlocking";
    [SerializeField] private string parryParam = "ParryTrigger";
    [SerializeField] private string perfectBlockParam = "PerfectBlockTrigger";

    private MeleeModule parentMelee;
    private ThirdPersonController controller;
    private WeaponModule weaponModule;
    private AnimationStateModule animationState;
    private PlayerInputControls inputControls;
    private bool isSubscribedToInput;

    private bool isBlocking;
    private bool isParrying;
    private bool canPerfectBlock;
    private bool isInCounterWindow;
    private float blockStartTime;
    private float parryStartTime;
    private float lastIncomingDamageTime;
    private Vector3 lastAttackDirection;

    public System.Action OnBlockBegin;
    public System.Action OnBlockComplete;
    public System.Action OnPerfectBlock;
    public System.Action OnParryBegin;
    public System.Action OnParryEnd;
    public System.Action OnCounterWindowOpened;
    public System.Action OnCounterWindowClosed;

    public bool IsEnabled { get; set; } = true;
    public bool IsBlocking => isBlocking;
    public bool IsParrying => isParrying;
    public bool IsDefending => isBlocking || isParrying;
    public bool CanCounter => isInCounterWindow;
    public bool CanPerfectBlock => canPerfectBlock && defenseSettings.enablePerfectBlock;
    public float BlockDuration => isBlocking ? Time.time - blockStartTime : 0f;
    public ActiveDefenseSettings Settings => defenseSettings;

    public void Initialize(MeleeModule parentMelee)
    {
        this.parentMelee = parentMelee;
        controller = parentMelee.Controller;
        weaponModule = parentMelee.WeaponModule;
        animationState = parentMelee.AnimationState;
    }

    public void UpdateSubModule()
    {
        UpdateDefenseTimers();
        UpdatePerfectBlockWindow();
        UpdateCounterWindow();
        UpdateBlockStaminaDrain();
    }

    void Start()
    {
        if (parentMelee?.Brain != null)
        {
            inputControls = parentMelee.Brain.GetInputControls();
            SubscribeToInputs(inputControls);
        }
    }

    void OnDestroy()
    {
        UnsubscribeFromInputs(inputControls);
    }

    void UpdateDefenseTimers()
    {
        if (isParrying && Time.time - parryStartTime >= defenseSettings.parryRecoveryTime)
        {
            EndParry();
        }
    }

    void UpdatePerfectBlockWindow()
    {
        if (!defenseSettings.enablePerfectBlock || !isBlocking) return;

        if (!canPerfectBlock && BlockDuration >= defenseSettings.blockStartupTime &&
            BlockDuration <= defenseSettings.blockStartupTime + defenseSettings.perfectBlockWindow)
        {
            canPerfectBlock = true;
        }
        else if (canPerfectBlock && BlockDuration > defenseSettings.blockStartupTime + defenseSettings.perfectBlockWindow)
        {
            canPerfectBlock = false;
        }
    }

    void UpdateCounterWindow()
    {
        if (isInCounterWindow)
        {
            float counterTime = Time.time - parryStartTime;
            if (counterTime >= defenseSettings.parryCounterWindow)
            {
                isInCounterWindow = false;
                OnCounterWindowClosed?.Invoke();
            }
        }
    }

    void UpdateBlockStaminaDrain()
    {
        if (isBlocking && controller.CurrentStamina > 0)
        {
            float drainAmount = GetBlockStaminaDrain() * Time.deltaTime;
            if (!controller.ConsumeStamina(drainAmount))
            {
                ForceEndCurrentAction();
            }
        }
    }

    public void SubscribeToInputs(PlayerInputControls playerInputControls)
    {
        if (playerInputControls == null || isSubscribedToInput) return;

        try
        {
            var playerActions = playerInputControls.Player;

            TrySubscribeToAction(playerActions, "Block", action => {
                action.started += OnBlockStarted;
                action.canceled += OnBlockCanceled;
            });

            TrySubscribeToAction(playerActions, "Parry", action => {
                action.started += OnParryStarted;
            });

            isSubscribedToInput = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ActiveDefenseModule: Some inputs not available: {e.Message}");
        }
    }

    public void UnsubscribeFromInputs(PlayerInputControls playerInputControls)
    {
        if (!isSubscribedToInput || playerInputControls == null) return;

        try
        {
            var playerActions = playerInputControls.Player;

            TryUnsubscribeFromAction(playerActions, "Block", action => {
                action.started -= OnBlockStarted;
                action.canceled -= OnBlockCanceled;
            });

            TryUnsubscribeFromAction(playerActions, "Parry", action => {
                action.started -= OnParryStarted;
            });

            isSubscribedToInput = false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ActiveDefenseModule: Error unsubscribing: {e.Message}");
        }
    }

    void TrySubscribeToAction(object playerActions, string actionName, System.Action<InputAction> callback)
    {
        var actionProperty = playerActions.GetType().GetProperty(actionName);
        if (actionProperty?.GetValue(playerActions) is InputAction action)
        {
            callback(action);
        }
    }

    void TryUnsubscribeFromAction(object playerActions, string actionName, System.Action<InputAction> callback)
    {
        var actionProperty = playerActions.GetType().GetProperty(actionName);
        if (actionProperty?.GetValue(playerActions) is InputAction action)
        {
            callback(action);
        }
    }

    public void OnBlockStarted(InputAction.CallbackContext context)
    {
        if (CanBlock())
        {
            StartBlock();
        }
    }

    public void OnBlockCanceled(InputAction.CallbackContext context)
    {
        StopBlock();
    }

    public void OnParryStarted(InputAction.CallbackContext context)
    {
        if (CanParry())
        {
            StartParry();
        }
    }

    public bool CanBlock()
    {
        return !isParrying &&
               !isBlocking &&
               controller.CurrentStamina >= defenseSettings.blockStaminaCost &&
               (weaponModule == null || weaponModule.CanCurrentWeaponBlock()) &&
               (animationState == null || animationState.CanAct()) &&
               (parentMelee.Attack?.IsAttacking != true);
    }

    public bool CanParry()
    {
        return defenseSettings.canParry &&
               !isBlocking &&
               !isParrying &&
               controller.CurrentStamina >= defenseSettings.parryStaminaCost &&
               (weaponModule == null || weaponModule.CanCurrentWeaponParry()) &&
               (animationState == null || animationState.CanAct());
    }

    public void StartBlock()
    {
        if (!CanBlock()) return;

        isBlocking = true;
        blockStartTime = Time.time;
        canPerfectBlock = false;

        controller.SetAnimationBool(blockParam, true);
        OnBlockBegin?.Invoke();
    }

    public void StopBlock()
    {
        if (!isBlocking) return;

        isBlocking = false;
        canPerfectBlock = false;

        controller.SetAnimationBool(blockParam, false);
        OnBlockComplete?.Invoke();
    }

    void StartParry()
    {
        if (!controller.ConsumeStamina(defenseSettings.parryStaminaCost)) return;

        isParrying = true;
        parryStartTime = Time.time;

        controller.TriggerAnimation(parryParam);
        OnParryBegin?.Invoke();
    }

    void EndParry()
    {
        isParrying = false;
        OnParryEnd?.Invoke();
    }

    public void ForceEndCurrentAction()
    {
        if (isBlocking)
        {
            StopBlock();
        }

        if (isParrying)
        {
            EndParry();
        }

        isInCounterWindow = false;
        canPerfectBlock = false;
    }

    public bool IsWithinParryWindow()
    {
        if (!isParrying) return false;
        float parryDuration = Time.time - parryStartTime;
        return parryDuration <= defenseSettings.parryWindow;
    }

    public bool IsAttackWithinDefenseAngle(Vector3 attackDirection)
    {
        if (attackDirection == Vector3.zero) return true;

        Vector3 forwardDirection = transform.forward;
        float angle = Vector3.Angle(forwardDirection, -attackDirection);
        return angle <= defenseSettings.blockAngle * 0.5f;
    }

    public void TriggerPerfectBlock()
    {
        if (isBlocking && canPerfectBlock)
        {
            controller.TriggerAnimation(perfectBlockParam);
            OnPerfectBlock?.Invoke();
        }
    }

    public void TriggerSuccessfulParry()
    {
        if (isParrying)
        {
            isInCounterWindow = true;
            OnCounterWindowOpened?.Invoke();
            EndParry();
        }
    }

    public bool TryPerfectBlock(float blockWindow = 0.2f)
    {
        if (!isBlocking) return false;

        float window = blockWindow > 0 ? blockWindow : defenseSettings.perfectBlockWindow;
        bool isInPerfectWindow = BlockDuration <= defenseSettings.blockStartupTime + window;

        return isInPerfectWindow && defenseSettings.enablePerfectBlock;
    }

    public bool IsInPerfectBlockWindow()
    {
        return isBlocking && canPerfectBlock;
    }

    public float GetBlockAngle() => defenseSettings.blockAngle;
    public float GetBlockDamageReduction() => defenseSettings.blockDamageReduction;
    public float GetPerfectBlockReflectDamage() => defenseSettings.perfectBlockReflectDamage;

    float GetBlockStaminaDrain()
    {
        float drainRate = defenseSettings.blockStaminaDrainRate;

        if (weaponModule?.CurrentWeapon != null)
        {
            var blockStaminaField = weaponModule.CurrentWeapon.GetType().GetField("blockStamina");
            if (blockStaminaField != null && blockStaminaField.GetValue(weaponModule.CurrentWeapon) is float weaponBlockStamina)
            {
                drainRate = weaponBlockStamina;
            }
        }

        return drainRate;
    }

    public float GetDefenseEffectiveness()
    {
        if (isParrying && IsWithinParryWindow())
            return 1f;

        if (isBlocking && canPerfectBlock)
            return defenseSettings.blockDamageReduction + 0.15f;

        if (isBlocking)
            return defenseSettings.blockDamageReduction;

        return 0f;
    }

    public bool CanDefend => IsEnabled && parentMelee != null && !parentMelee.IsBusyWithMelee;

    public float GetDefensiveMultiplier(Vector3 attackDirection)
    {
        if (IsParrying && IsWithinParryWindow())
        {
            TriggerSuccessfulParry();
            return 0f;
        }

        if (IsBlocking && IsInPerfectBlockWindow())
        {
            TriggerPerfectBlock();
            return 0f;
        }

        if (IsBlocking)
        {
            if (!IsAttackWithinDefenseAngle(attackDirection))
            {
                return 1f;
            }

            return 1f - defenseSettings.blockDamageReduction;
        }

        return 1f;
    }
}