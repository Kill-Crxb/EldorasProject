using UnityEngine;
using System;

/// <summary>
/// Modernized Active Defense Module
/// Uses DefenseAbilityData for swappable defense mechanics.
/// Integrates with AbilityLoadoutModule for automatic weapon-based defense swapping.
/// </summary>
public class ActiveDefenseModule : MonoBehaviour, IBrainModule, IDefenseProvider
{
    [Header("Debug")]
    [SerializeField] private bool debugDefense = false;

    // Component references
    private ControllerBrain brain;
    private IAnimationProvider animationProvider;
    private IResourceProvider resourceProvider;
    private IInputProvider inputProvider;
    private AbilityLoadoutModule loadoutModule;

    // Defense state
    private bool isDefenseActive;
    private float defenseStartTime;
    private float lastDefenseTime;
    private bool isInParryWindow;
    private bool isInCounterWindow;
    private float counterWindowStartTime;

    // Cache current defense for performance
    private DefenseAbilityData cachedDefense;

    // Events
    public event Action OnDefenseActivated;
    public event Action OnDefenseDeactivated;
    public event Action OnSuccessfulParry;
    public event Action OnCounterWindowOpened;
    public event Action OnCounterWindowClosed;

    public bool IsEnabled { get; set; } = true;

    // Properties
    public bool IsDefending => isDefenseActive;
    public bool IsInParryWindow => isInParryWindow;
    public bool IsInCounterWindow => isInCounterWindow;
    public DefenseAbilityData CurrentDefense => cachedDefense ?? (cachedDefense = loadoutModule?.GetDefenseAbility());
    public float DefenseDuration => isDefenseActive ? Time.time - defenseStartTime : 0f;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        animationProvider = brain.GetProvider<IAnimationProvider>();
        resourceProvider = brain.GetProvider<IResourceProvider>();
        inputProvider = brain.GetProvider<IInputProvider>();
        loadoutModule = brain.GetModule<AbilityLoadoutModule>();

        if (animationProvider == null && debugDefense)
            Debug.LogWarning("[ActiveDefenseModule] No IAnimationProvider found");

        if (resourceProvider == null && debugDefense)
            Debug.LogWarning("[ActiveDefenseModule] No IResourceProvider found");

        if (loadoutModule == null)
        {
            Debug.LogError("[ActiveDefenseModule] No AbilityLoadoutModule found! Defense system requires loadout module.");
            IsEnabled = false;
            return;
        }

        // Cache the defense ability
        cachedDefense = loadoutModule.GetDefenseAbility();

        if (debugDefense)
            Debug.Log($"[ActiveDefenseModule] Initialized with defense: {cachedDefense?.defenseName ?? "None"}");
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        HandleDefenseInput();
        UpdateParryWindow();
        UpdateCounterWindow();
        UpdateResourceDrain();
    }

    #region Input Handling

    private void HandleDefenseInput()
    {
        if (inputProvider == null || CurrentDefense == null) return;

        // Sekiro-style: Hold right-click to block
        if (inputProvider.BlockHeld && !isDefenseActive)
        {
            ActivateDefense();
        }
        else if (!inputProvider.BlockHeld && isDefenseActive)
        {
            DeactivateDefense();
        }
    }

    #endregion

    #region Defense Activation/Deactivation

    private bool CanActivateDefense()
    {
        if (CurrentDefense == null) return false;
        if (isDefenseActive) return false;

        // Sekiro-style blocking has no activation cost, only drain while active
        // and costs on hit
        return true;
    }

    private void ActivateDefense()
    {
        if (!CanActivateDefense()) return;

        isDefenseActive = true;
        defenseStartTime = Time.time;
        isInParryWindow = false; // Will be set to true in UpdateParryWindow

        // Trigger block animation
        if (animationProvider != null && !string.IsNullOrEmpty(CurrentDefense.blockAnimParam))
        {
            animationProvider.SetBool(CurrentDefense.blockAnimParam, true);
        }

        // Spawn activation VFX
        if (CurrentDefense.blockActivationVFX != null)
        {
            Instantiate(CurrentDefense.blockActivationVFX, transform.position, transform.rotation);
        }

        OnDefenseActivated?.Invoke();

        if (debugDefense)
            Debug.Log($"[ActiveDefenseModule] Block raised: {CurrentDefense.defenseName}");
    }

    private void DeactivateDefense()
    {
        if (!isDefenseActive) return;

        isDefenseActive = false;
        isInParryWindow = false;
        lastDefenseTime = Time.time;

        // Update animation
        if (animationProvider != null && !string.IsNullOrEmpty(CurrentDefense.blockAnimParam))
        {
            animationProvider.SetBool(CurrentDefense.blockAnimParam, false);
        }

        OnDefenseDeactivated?.Invoke();

        if (debugDefense)
            Debug.Log($"[ActiveDefenseModule] Block lowered: {CurrentDefense.defenseName}");
    }

    #endregion

    #region State Updates

    private void UpdateParryWindow()
    {
        if (!isDefenseActive || CurrentDefense.parryWindowDuration <= 0) return;

        float elapsed = DefenseDuration;
        float windowStart = CurrentDefense.blockStartupTime;
        float windowEnd = windowStart + CurrentDefense.parryWindowDuration;

        bool shouldBeInWindow = elapsed >= windowStart && elapsed <= windowEnd;

        if (!isInParryWindow && shouldBeInWindow)
        {
            isInParryWindow = true;
            if (debugDefense)
                Debug.Log($"[ActiveDefenseModule] Entered parry window (duration: {CurrentDefense.parryWindowDuration}s)");
        }
        else if (isInParryWindow && !shouldBeInWindow)
        {
            isInParryWindow = false;
            if (debugDefense)
                Debug.Log("[ActiveDefenseModule] Exited parry window - now regular blocking");
        }
    }

    private void UpdateCounterWindow()
    {
        if (isInCounterWindow)
        {
            float elapsed = Time.time - counterWindowStartTime;
            if (elapsed >= CurrentDefense.counterWindowDuration)
            {
                CloseCounterWindow();
            }
        }
    }

    private void UpdateResourceDrain()
    {
        if (!isDefenseActive) return;
        if (CurrentDefense.blockStaminaDrain <= 0) return;

        float drainAmount = CurrentDefense.blockStaminaDrain * Time.deltaTime;

        if (resourceProvider != null)
        {
            if (!resourceProvider.HasResource(ResourceType.Stamina, drainAmount))
            {
                // Out of stamina, force stop blocking
                DeactivateDefense();
                return;
            }

            resourceProvider.ConsumeResource(ResourceType.Stamina, drainAmount);
        }
    }

    #endregion

    #region Defense Mechanics

    private void TriggerSuccessfulParry()
    {
        if (!isInParryWindow) return;

        // Parry animation
        if (animationProvider != null && !string.IsNullOrEmpty(CurrentDefense.parryAnimTrigger))
        {
            animationProvider.SetTrigger(CurrentDefense.parryAnimTrigger);
        }

        // Refund parry stamina cost if enabled
        if (resourceProvider != null && CurrentDefense.parryRefundsStamina)
        {
            resourceProvider.RestoreResource(ResourceType.Stamina, CurrentDefense.parryStaminaCost);
        }

        // Spawn parry success VFX
        if (CurrentDefense.parrySuccessVFX != null)
        {
            Instantiate(CurrentDefense.parrySuccessVFX, transform.position, transform.rotation);
        }

        // Open counter window
        if (CurrentDefense.parryEnablesCounter)
        {
            OpenCounterWindow();
        }

        OnSuccessfulParry?.Invoke();

        if (debugDefense)
        {
            Debug.Log($"[ActiveDefenseModule] PARRY! (Window: {CurrentDefense.parryWindowDuration}s, " +
                     $"Elapsed: {DefenseDuration:F2}s)");
        }
    }

    private void TriggerBlockHit()
    {
        // Block hit animation (when attack is blocked, not parried)
        if (animationProvider != null && !string.IsNullOrEmpty(CurrentDefense.blockHitAnimTrigger))
        {
            animationProvider.SetTrigger(CurrentDefense.blockHitAnimTrigger);
        }

        if (debugDefense)
        {
            Debug.Log("[ActiveDefenseModule] Block impact animation triggered");
        }
    }

    private void OpenCounterWindow()
    {
        if (!CurrentDefense.parryEnablesCounter) return;

        isInCounterWindow = true;
        counterWindowStartTime = Time.time;

        OnCounterWindowOpened?.Invoke();

        if (debugDefense)
            Debug.Log("[ActiveDefenseModule] Counter window opened");
    }

    private void CloseCounterWindow()
    {
        isInCounterWindow = false;
        OnCounterWindowClosed?.Invoke();

        if (debugDefense)
            Debug.Log("[ActiveDefenseModule] Counter window closed");
    }

    private bool IsAttackWithinBlockAngle(Vector3 attackDirection)
    {
        if (attackDirection == Vector3.zero) return true;

        float angle = Vector3.Angle(transform.forward, -attackDirection);
        return angle <= CurrentDefense.blockAngle * 0.5f;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Change the current defense ability.
    /// Called by AbilityLoadoutModule when defense changes.
    /// </summary>
    public void SetDefense(DefenseAbilityData newDefense)
    {
        if (newDefense == cachedDefense) return;

        // Deactivate current defense if active
        if (isDefenseActive)
        {
            DeactivateDefense();
        }

        cachedDefense = newDefense;

        if (debugDefense)
            Debug.Log($"[ActiveDefenseModule] Defense changed to: {newDefense?.defenseName ?? "None"}");
    }

    /// <summary>
    /// Force stop any active defense
    /// </summary>
    public void ForceStopDefense()
    {
        if (isDefenseActive)
        {
            DeactivateDefense();
        }

        isInCounterWindow = false;
        isInParryWindow = false;
    }

    #endregion

    #region IDefenseProvider Implementation

    bool IDefenseProvider.IsBlocking() => isDefenseActive;

    bool IDefenseProvider.IsParrying() => isDefenseActive && isInParryWindow;

    bool IDefenseProvider.CanDefend() => IsEnabled && CurrentDefense != null;

    float IDefenseProvider.ProcessIncomingDamage(float damage, Vector3 attackDirection)
    {
        if (!isDefenseActive || CurrentDefense == null)
            return damage;

        // Check if attack is within block angle
        if (!IsAttackWithinBlockAngle(attackDirection))
        {
            if (debugDefense)
                Debug.Log("[ActiveDefenseModule] Attack from behind - no defense");
            return damage;
        }

        // Check if we're in the parry window
        bool isParry = isInParryWindow;

        // Get appropriate stamina cost and damage reduction
        float staminaCost = CurrentDefense.GetStaminaCost(isParry);
        float damageReduction = CurrentDefense.GetDamageReduction(isParry);

        // Consume stamina for defending
        if (resourceProvider != null && staminaCost > 0)
        {
            if (!resourceProvider.HasResource(ResourceType.Stamina, staminaCost))
            {
                // Not enough stamina - defense fails, force stop blocking
                if (debugDefense)
                    Debug.Log("[ActiveDefenseModule] Out of stamina - defense broken!");
                DeactivateDefense();
                return damage;
            }

            resourceProvider.ConsumeResource(ResourceType.Stamina, staminaCost);
        }

        // Calculate final damage
        float finalDamage = damage * (1f - damageReduction);

        // Trigger appropriate animation and effects
        if (isParry)
        {
            TriggerSuccessfulParry();
        }
        else
        {
            TriggerBlockHit();
        }

        // Spawn block impact VFX
        if (CurrentDefense.blockImpactVFX != null)
        {
            Instantiate(CurrentDefense.blockImpactVFX, transform.position, Quaternion.LookRotation(-attackDirection));
        }

        if (debugDefense)
        {
            string defenseType = isParry ? "PARRY" : "BLOCK";
            Debug.Log($"[ActiveDefenseModule] {defenseType}: {damage:F1} → {finalDamage:F1} " +
                     $"(Reduction: {damageReduction * 100}%, Stamina: -{staminaCost:F1})");
        }

        return finalDamage;
    }

    float IDefenseProvider.GetDefensiveMultiplier(Vector3 attackDirection)
    {
        if (!isDefenseActive || CurrentDefense == null)
            return 1f;

        if (!IsAttackWithinBlockAngle(attackDirection))
            return 1f;

        float reduction = CurrentDefense.GetDamageReduction(isInParryWindow);
        return 1f - reduction;
    }

    event Action IDefenseProvider.OnBlockStart
    {
        add { OnDefenseActivated += value; }
        remove { OnDefenseActivated -= value; }
    }

    event Action IDefenseProvider.OnBlockEnd
    {
        add { OnDefenseDeactivated += value; }
        remove { OnDefenseDeactivated -= value; }
    }

    event Action IDefenseProvider.OnPerfectBlock
    {
        add { OnSuccessfulParry += value; }
        remove { OnSuccessfulParry -= value; }
    }

    #endregion
}