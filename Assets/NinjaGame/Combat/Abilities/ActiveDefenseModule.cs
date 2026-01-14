using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Modernized Active Defense Module (fully dynamic)
/// - Resource-agnostic: no hardcoded references
/// - DefenseAbilityData drives all defense costs and effects
/// - Integrates with AbilityLoadoutModule for weapon-based defense swapping
/// - Can consume any ResourceDefinition dynamically
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

    // Dynamic resource cache (per defense ability)
    private Dictionary<string, ResourceDefinition> defenseResourceCache = new();

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

        cachedDefense = loadoutModule.GetDefenseAbility();
        CacheDefenseResources(cachedDefense);

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

        // Hold input to block
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
        if (CurrentDefense == null || isDefenseActive) return false;
        return true;
    }

    private void ActivateDefense()
    {
        if (!CanActivateDefense()) return;

        isDefenseActive = true;
        defenseStartTime = Time.time;
        isInParryWindow = false;

        // Trigger animation
        if (animationProvider != null && !string.IsNullOrEmpty(CurrentDefense.blockAnimParam))
            animationProvider.SetBool(CurrentDefense.blockAnimParam, true);

        // Spawn VFX
        if (CurrentDefense.blockActivationVFX != null)
            Instantiate(CurrentDefense.blockActivationVFX, transform.position, transform.rotation);

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

        if (animationProvider != null && !string.IsNullOrEmpty(CurrentDefense.blockAnimParam))
            animationProvider.SetBool(CurrentDefense.blockAnimParam, false);

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
                CloseCounterWindow();
        }
    }

    private void UpdateResourceDrain()
    {
        if (!isDefenseActive || CurrentDefense == null) return;

        foreach (var kvp in defenseResourceCache)
        {
            var def = kvp.Value;
            float drainAmount = CurrentDefense.GetResourceDrain(def) * Time.deltaTime;

            if (drainAmount <= 0) continue;

            if (!resourceProvider.HasResource(def, drainAmount))
            {
                DeactivateDefense();
                if (debugDefense)
                    Debug.Log($"[ActiveDefenseModule] Out of {def.name} - defense broken!");
                return;
            }

            resourceProvider.ConsumeResource(def, drainAmount);
        }
    }

    #endregion

    #region Defense Mechanics

    private void TriggerSuccessfulParry()
    {
        if (!isInParryWindow) return;

        if (animationProvider != null && !string.IsNullOrEmpty(CurrentDefense.parryAnimTrigger))
            animationProvider.SetTrigger(CurrentDefense.parryAnimTrigger);

        // Refund resources dynamically
        foreach (var kvp in defenseResourceCache)
        {
            var def = kvp.Value;
            if (CurrentDefense.RefundsResource(def))
            {
                resourceProvider.RestoreResource(def, CurrentDefense.GetResourceRefund(def));
            }
        }

        if (CurrentDefense.parrySuccessVFX != null)
            Instantiate(CurrentDefense.parrySuccessVFX, transform.position, transform.rotation);

        if (CurrentDefense.parryEnablesCounter)
            OpenCounterWindow();

        OnSuccessfulParry?.Invoke();

        if (debugDefense)
            Debug.Log($"[ActiveDefenseModule] PARRY! Elapsed: {DefenseDuration:F2}s");
    }

    private void TriggerBlockHit()
    {
        if (animationProvider != null && !string.IsNullOrEmpty(CurrentDefense.blockHitAnimTrigger))
            animationProvider.SetTrigger(CurrentDefense.blockHitAnimTrigger);

        if (debugDefense)
            Debug.Log("[ActiveDefenseModule] Block impact animation triggered");
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

    public void SetDefense(DefenseAbilityData newDefense)
    {
        if (newDefense == cachedDefense) return;

        if (isDefenseActive)
            DeactivateDefense();

        cachedDefense = newDefense;
        CacheDefenseResources(newDefense);

        if (debugDefense)
            Debug.Log($"[ActiveDefenseModule] Defense changed to: {newDefense?.defenseName ?? "None"}");
    }

    public void ForceStopDefense()
    {
        if (isDefenseActive)
            DeactivateDefense();

        isInCounterWindow = false;
        isInParryWindow = false;
    }

    private void CacheDefenseResources(DefenseAbilityData defense)
    {
        defenseResourceCache.Clear();
        if (defense == null || resourceProvider == null) return;

        foreach (var def in defense.GetAllRequiredResources())
        {
            defenseResourceCache[def.name] = def;
        }
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

        if (!IsAttackWithinBlockAngle(attackDirection))
            return damage;

        bool isParry = isInParryWindow;
        float damageReduction = CurrentDefense.GetDamageReduction(isParry);

        // Consume all required resources dynamically
        foreach (var kvp in defenseResourceCache)
        {
            var def = kvp.Value;
            float cost = CurrentDefense.GetResourceCost(def, isParry);

            if (!resourceProvider.HasResource(def, cost))
            {
                DeactivateDefense();
                return damage;
            }

            resourceProvider.ConsumeResource(def, cost);
        }

        float finalDamage = damage * (1f - damageReduction);

        if (isParry) TriggerSuccessfulParry();
        else TriggerBlockHit();

        if (CurrentDefense.blockImpactVFX != null)
            Instantiate(CurrentDefense.blockImpactVFX, transform.position, Quaternion.LookRotation(-attackDirection));

        if (debugDefense)
        {
            string type = isParry ? "PARRY" : "BLOCK";
            Debug.Log($"[ActiveDefenseModule] {type}: {damage:F1} → {finalDamage:F1} (Reduction: {damageReduction * 100}%)");
        }

        return finalDamage;
    }

    float IDefenseProvider.GetDefensiveMultiplier(Vector3 attackDirection)
    {
        if (!isDefenseActive || CurrentDefense == null) return 1f;
        if (!IsAttackWithinBlockAngle(attackDirection)) return 1f;

        return 1f - CurrentDefense.GetDamageReduction(isInParryWindow);
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
