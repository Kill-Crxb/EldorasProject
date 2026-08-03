using NinjaGame.Animation;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class AbilitySystem : MonoBehaviour, IBrainModule, IAbilityProvider
{
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private AbilityLoadoutModule loadoutModule;
    [SerializeField] private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

    private class AbilityState
    {
        public AbilityDefinition definition;
        public CountdownTimer cooldown;
        public bool IsOnCooldown => cooldown != null && !cooldown.IsFinished;

        public AbilityState(AbilityDefinition def)
        {
            definition = def;
        }
    }

    private ControllerBrain brain;
    private StateMachineModule stateMachine;
    private Blackboard blackboard;
    private RuntimeAbilityManager runtimeAbilityManager;
    private IAnimationProvider animationProvider;
    private IResourceProvider resources;
    private IHealthProvider healthProvider;
    private MovementSystem movementSystem;
    private DamageSystem damageSystem;
    private VFXSystem vfxSystem;
    private AnimationEventForwarder eventForwarder;

    private Dictionary<string, AbilityState> abilityStates = new Dictionary<string, AbilityState>();

    private ItemInstance cachedEquippedWeapon;
    private WeaponData cachedNaturalWeapon;

    private AbilityDefinition currentAbility = null;
    private float abilityStartTime;
    private Coroutine safetyTimeoutCoroutine;

    private string currentlyCastingAbility = null;
    private float castStartTime;

    private bool isAnimationLocked = false;
    private bool isInvincible = false;
    private HashSet<Collider> activeHitboxes = new HashSet<Collider>();

    private AbilityDefinition lastCompletedAbility = null;
    private float lastAbilityCompleteTime = 0f;
    private bool chainWindowOpen = false;
    private float chainWindowOpenTime = 0f;

    private AbilityDefinition currentDefensiveAbility = null;
    private float defenseStartTime = 0f;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public ControllerBrain Brain => brain;
    public AbilityLoadoutModule Loadout => loadoutModule;
    public bool IsExecuting => currentAbility != null || currentlyCastingAbility != null || isAnimationLocked;
    public AbilityDefinition CurrentAbility => currentAbility;
    public string CurrentAbilityId => currentAbility?.abilityId;
    public bool IsInvincible => isInvincible;

    public event Action<string> OnAbilityUsed;
    public event Action<string, float> OnAbilityCooldownChanged;
    public event Action<string> OnAbilityCastStart;
    public event Action<string> OnAbilityCastComplete;
    public event Action<AnimationEventType> OnAbilityAnimationEvent;
    public event Action OnBlockStart;
    public event Action OnBlockEnd;
    public event Action OnPerfectBlock;

    private void SetFact(int key, bool value) => blackboard?.SetBool(key, value);

    private void UpdateExecutingFact()
        => SetFact(BlackboardKey.IsExecutingAbility,
            currentAbility != null || currentlyCastingAbility != null || isAnimationLocked);

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        stateMachine = brain.GetModule<StateMachineModule>();
        if (stateMachine == null)
            Debug.LogError("[AbilitySystem] StateMachineModule not found");

        var blackboardSystem = brain.GetModule<BlackboardSystem>();
        if (blackboardSystem != null)
            blackboard = blackboardSystem.Blackboard;
        else
            Debug.LogError("[AbilitySystem] BlackboardSystem not found");

        runtimeAbilityManager = brain.GetModule<RuntimeAbilityManager>();

        animationProvider = brain.Animation;
        movementSystem = brain.Movement;
        resources = brain.GetProvider<IResourceProvider>();
        healthProvider = brain.GetProvider<IHealthProvider>();
        damageSystem = brain.GetModule<DamageSystem>();
        vfxSystem = brain.GetModule<VFXSystem>();

        

        SetupAnimationEventForwarder();
        GameEvents.OnLoadCompleted += HandleLoadCompleted;
        BuildAbilityLookup();

        if (resources == null)
            Debug.LogWarning("[AbilitySystem] No IResourceProvider found");
        if (animationProvider == null)
            Debug.LogError("[AbilitySystem] AnimationSystem not assigned");
        if (movementSystem == null)
            Debug.LogError("[AbilitySystem] MovementSystem not assigned");
    }

    public void LateInitialize()
    {
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        UpdateCooldownTimers();
        UpdateCasting();
        UpdateChainWindow();
        UpdateDefensiveAbility();
    }

    private void HandleLoadCompleted()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

        if (eventForwarder != null) return;

        SetupAnimationEventForwarder();
    }

    private void SetupAnimationEventForwarder()
    {
        Transform playerRoot = brain.transform.parent;
        if (playerRoot != null)
            eventForwarder = playerRoot.GetComponentInChildren<AnimationEventForwarder>();
        else
            eventForwarder = brain.GetComponentInChildren<AnimationEventForwarder>();

        if (eventForwarder == null)
        {
            Debug.LogWarning($"[AbilitySystem] No AnimationEventForwarder found on {gameObject.name}");
        }
        else
        {
            eventForwarder.Initialize(brain);
            eventForwarder.OnAnimationEvent += HandleAnimationEvent;
            eventForwarder.OnStateTransitionEvent += HandleStateTransition;
        }
    }

    private void BuildAbilityLookup()
    {
        abilityStates.Clear();

        foreach (var ability in abilities)
        {
            if (ability == null)
            {
                Debug.LogWarning("[AbilitySystem] Null ability in list");
                continue;
            }

            if (string.IsNullOrEmpty(ability.abilityId))
            {
                Debug.LogWarning($"[AbilitySystem] Ability '{ability.abilityName}' has no ID");
                continue;
            }

            abilityStates[ability.abilityId] = new AbilityState(ability);
        }
    }

    private void OnDestroy()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

        if (eventForwarder != null)
        {
            eventForwarder.OnAnimationEvent -= HandleAnimationEvent;
            eventForwarder.OnStateTransitionEvent -= HandleStateTransition;
        }
    }

    public void AddAbility(AbilityDefinition ability)
    {
        if (string.IsNullOrEmpty(ability.abilityId))
        {
            Debug.LogError($"[AbilitySystem] Ability {ability.abilityName} has no ID");
            return;
        }

        if (!abilities.Contains(ability))
            abilities.Add(ability);

        abilityStates[ability.abilityId] = new AbilityState(ability);
    }

    public void RemoveAbility(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return;

        abilities.Remove(state.definition);
        abilityStates.Remove(abilityId);
    }

    public AbilityDefinition GetAbility(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return null;
        return state.definition;
    }

    public List<AbilityDefinition> GetAllAbilities()
    {
        return new List<AbilityDefinition>(abilities);
    }

    private bool HasSufficientResources(AbilityDefinition ability)
    {
        if (ability.resourceCosts == null) return true;
        if (resources == null) return true;

        foreach (var cost in ability.resourceCosts)
        {
            if (cost.resource != null && cost.cost > 0 && !resources.HasResource(cost.resource, cost.cost))
                return false;
        }

        return true;
    }

    private void ConsumeResourceCosts(AbilityDefinition ability)
    {
        if (ability.resourceCosts == null) return;
        if (resources == null) return;

        foreach (var cost in ability.resourceCosts)
        {
            if (cost.resource != null && cost.cost > 0)
                resources.ConsumeResource(cost.resource, cost.cost);
        }
    }

    private void DrainResourceCosts(AbilityDefinition ability, float deltaTime)
    {
        if (ability.resourceCosts == null) return;
        if (resources == null) return;

        foreach (var cost in ability.resourceCosts)
        {
            if (cost.resource != null && cost.drain > 0)
            {
                float drainAmount = cost.drain * deltaTime;
                resources.ConsumeResource(cost.resource, drainAmount);
            }
        }
    }

    private void RefundResourceCosts(AbilityDefinition ability)
    {
        if (ability.resourceCosts == null) return;
        if (resources == null) return;

        foreach (var cost in ability.resourceCosts)
        {
            if (cost.resource != null && cost.refund > 0)
                resources.RestoreResource(cost.resource, cost.refund);
        }
    }

    public bool CanUseAbility(string abilityId)
    {
        if (!isEnabled) return false;
        if (currentAbility != null) return false;
        if (currentlyCastingAbility != null) return false;
        if (isAnimationLocked) return false;

        if (!abilityStates.TryGetValue(abilityId, out var state)) return false;
        if (state.IsOnCooldown) return false;

        var ability = state.definition;

        if (runtimeAbilityManager != null)
        {
            var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
            if (instance != null && !instance.IsUsable())
                return false;
        }

        if (!CheckBlackboardRequirements(ability))
            return false;

        if (!HasSufficientResources(ability))
            return false;

        return true;
    }

    public void UseAbility(string abilityId)
    {
        if (!CanUseAbility(abilityId)) return;
        if (!abilityStates.TryGetValue(abilityId, out var state))
        {
            Debug.LogError($"[AbilitySystem] Ability {abilityId} not found");
            return;
        }

        var ability = state.definition;

        if (runtimeAbilityManager != null)
        {
            var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
            if (instance != null)
                runtimeAbilityManager.OnAbilityUsed(instance.instanceId);
        }

        ConsumeResourceCosts(ability);

        if (ability.castTime > 0f)
            StartCast(ability);
        else
            ExecuteAbility(ability);

        StartCooldown(abilityId, ability.cooldown);
        OnAbilityUsed?.Invoke(abilityId);
    }

    public void CancelCurrentAbility()
    {
        if (currentlyCastingAbility != null)
            currentlyCastingAbility = null;

        if (currentAbility != null)
            CompleteAbility(currentAbility);

        isAnimationLocked = false;
        isInvincible = false;
        SetFact(BlackboardKey.IsInvincible, false);
        SetFact(BlackboardKey.IsBlocking, false);
        UpdateExecutingFact();
    }

    private bool CheckBlackboardRequirements(AbilityDefinition ability)
    {
        if (blackboard == null) return true;

        var (requiredAny, requiredAll, forbiddenAll) = ability.GetBlackboardRequirements();

        if (requiredAny.Count > 0)
        {
            bool anyMet = false;
            foreach (var fact in requiredAny)
            {
                if (blackboard.GetBool(fact.GetHashCode()))
                {
                    anyMet = true;
                    break;
                }
            }

            if (!anyMet)
                return false;
        }

        foreach (var fact in requiredAll)
        {
            if (!blackboard.GetBool(fact.GetHashCode()))
                return false;
        }

        if (forbiddenAll.Count > 0)
        {
            bool allForbidden = true;
            foreach (var fact in forbiddenAll)
            {
                if (!blackboard.GetBool(fact.GetHashCode()))
                {
                    allForbidden = false;
                    break;
                }
            }

            if (allForbidden)
                return false;
        }

        return true;
    }

    private void StartCast(AbilityDefinition ability)
    {
        currentlyCastingAbility = ability.abilityId;
        castStartTime = Time.time;
        OnAbilityCastStart?.Invoke(ability.abilityId);
    }

    private void UpdateCasting()
    {
        if (currentlyCastingAbility == null) return;

        if (!abilityStates.TryGetValue(currentlyCastingAbility, out var state))
        {
            currentlyCastingAbility = null;
            return;
        }

        var ability = state.definition;

        if (Time.time - castStartTime >= ability.castTime)
        {
            ExecuteAbility(ability);
            currentlyCastingAbility = null;
            OnAbilityCastComplete?.Invoke(ability.abilityId);
        }
    }

    private void ExecuteAbility(AbilityDefinition ability)
    {
        currentAbility = ability;
        abilityStartTime = Time.time;
        UpdateExecutingFact();

        if (animationProvider is AnimationSystem animSys)
        {
            int fullBodyLayer = animSys.GetLayerIndex("Full Body Actions");
            int upperBodyLayer = animSys.GetLayerIndex("Upper Body Combat");
            bool isFullBody = ability.animationLayer == AbilityAnimationLayer.FullBodyActions;

            if (fullBodyLayer >= 0) animSys.SetLayerWeight(fullBodyLayer, isFullBody ? 1f : 0f);
            if (upperBodyLayer >= 0) animSys.SetLayerWeight(upperBodyLayer, isFullBody ? 0f : 1f);
        }

        if (animationProvider != null && !string.IsNullOrEmpty(ability.animationTrigger))
            animationProvider.TriggerCombatAnimation(ability.animationTrigger);

        if (ability.castEffectPrefab != null)
            vfxSystem?.SpawnEffect(ability.castEffectPrefab, VFXAnchor.CastOrigin);

        if (ability.abilityType == AbilityType.Defensive)
            ActivateDefensiveAbility(ability);

        bool hasEffectTrigger = ability.effectTrigger == AnimationEventType.Effect1 ||
                               ability.effectTrigger == AnimationEventType.Effect2 ||
                               ability.effectTrigger == AnimationEventType.Effect3;

        if (!hasEffectTrigger)
        {
            ExecuteAbilityEffects(ability);

            if (!ability.waitForAnimUnlock)
                CompleteAbility(ability);
        }

        if (ability.maxDuration > 0f)
        {
            if (safetyTimeoutCoroutine != null)
                StopCoroutine(safetyTimeoutCoroutine);

            safetyTimeoutCoroutine = StartCoroutine(SafetyTimeoutCoroutine(ability));
        }
    }

    private IEnumerator SafetyTimeoutCoroutine(AbilityDefinition ability)
    {
        yield return new WaitForSeconds(ability.maxDuration);

        if (currentAbility == ability)
        {
            Debug.LogWarning($"[AbilitySystem] {ability.abilityName} timed out - AnimUnlocked never fired!");
            CompleteAbility(ability);
        }

        safetyTimeoutCoroutine = null;
    }

    private void ExecuteAbilityEffects(AbilityDefinition ability)
    {
        bool isMovementAbility = ability.movementEffects != null && ability.movementEffects.Count > 0;
        if (isMovementAbility && movementSystem != null)
            ability.ExecuteMovement(movementSystem);

        if (ability.targetType == AbilityTargetType.Self)
        {
            if (brain == null)
            {
                Debug.LogError("[AbilitySystem] Cannot execute self-targeted ability - no ControllerBrain!");
                return;
            }

            var effectManager = brain.GetModule<EffectManagerModule>();
            ability.ExecuteOnSelf(brain, effectManager, resources);
        }
    }

    private void CompleteAbility(AbilityDefinition ability)
    {
        if (stateMachine != null)
            stateMachine.TryTransitionUpperBody(UpperBodyState.Idle);

        if (animationProvider is AnimationSystem animSys)
        {
            int fullBodyLayer = animSys.GetLayerIndex("Full Body Actions");
            int upperBodyLayer = animSys.GetLayerIndex("Upper Body Combat");

            if (fullBodyLayer >= 0) animSys.SetLayerWeight(fullBodyLayer, 0f);
            if (upperBodyLayer >= 0) animSys.SetLayerWeight(upperBodyLayer, 0f);
        }

        lastCompletedAbility = ability;
        lastAbilityCompleteTime = Time.time;

        if (currentAbility == ability)
        {
            currentAbility = null;
            isAnimationLocked = false;
        }

        UpdateExecutingFact();

        if (safetyTimeoutCoroutine != null)
        {
            StopCoroutine(safetyTimeoutCoroutine);
            safetyTimeoutCoroutine = null;
        }
    }

    private void HandleAnimationEvent(AnimationEventType eventType)
    {
        OnAbilityAnimationEvent?.Invoke(eventType);

        if (currentAbility == null) return;

        if (eventType == currentAbility.effectTrigger)
            ExecuteAbilityEffects(currentAbility);

        if (eventType == AnimationEventType.AnimUnlocked)
            HandleAnimationUnlocked();

        if (eventType == AnimationEventType.ComboWindowStart)
            OpenChainWindow();

        if (eventType == AnimationEventType.ComboWindowEnd)
            CloseChainWindow();

        switch (eventType)
        {
            case AnimationEventType.HitboxStart:
                EnableAbilityHitboxes();
                break;
            case AnimationEventType.HitboxEnd:
                DisableAbilityHitboxes();
                break;
        }
    }

    private void HandleAnimationUnlocked()
    {
        if (currentAbility == null) return;

        if (!currentAbility.waitForAnimUnlock)
            return;

        CompleteAbility(currentAbility);
    }

    private void OpenChainWindow()
    {
        chainWindowOpen = true;
        chainWindowOpenTime = Time.time;
    }

    private void CloseChainWindow()
    {
        chainWindowOpen = false;
    }

    private void HandleStateTransition(UpperBodyState newState)
    {
        if (stateMachine == null) return;

        if (stateMachine.TryTransitionUpperBody(newState))
        {
        }
        else
        {
        }
    }

    private void UpdateChainWindow()
    {
        if (chainWindowOpen && lastCompletedAbility != null)
        {
            float elapsed = Time.time - chainWindowOpenTime;

            if (!lastCompletedAbility.CanChain(elapsed))
                CloseChainWindow();
        }
    }

    public bool CanChainToNext()
    {
        if (lastCompletedAbility == null) return false;
        if (!lastCompletedAbility.HasNextInChain) return false;
        if (!chainWindowOpen) return false;

        float timeSinceOpen = Time.time - chainWindowOpenTime;
        return lastCompletedAbility.CanChain(timeSinceOpen);
    }

    public void TryChainNext()
    {
        if (!CanChainToNext()) return;

        UseAbility(lastCompletedAbility.nextInChain.abilityId);
    }

    private void ActivateDefensiveAbility(AbilityDefinition ability)
    {
        currentDefensiveAbility = ability;
        defenseStartTime = Time.time;
        SetFact(BlackboardKey.IsBlocking, true);

        if (damageSystem != null)
            damageSystem.OnDamageIntercept += HandleDamageIntercept;

        OnBlockStart?.Invoke();
    }

    private void UpdateDefensiveAbility()
    {
        if (currentDefensiveAbility == null) return;
        if (resources == null) return;
        if (currentDefensiveAbility.resourceCosts == null) return;

        foreach (var cost in currentDefensiveAbility.resourceCosts)
        {
            if (cost.drain <= 0f) continue;

            float drainAmount = cost.drain * Time.deltaTime;

            if (!resources.HasResource(cost.resource, drainAmount))
            {
                DeactivateDefensiveAbility();
                return;
            }

            resources.ConsumeResource(cost.resource, drainAmount);
        }
    }

    private void DeactivateDefensiveAbility()
    {
        if (currentDefensiveAbility == null) return;

        if (damageSystem != null)
            damageSystem.OnDamageIntercept -= HandleDamageIntercept;

        SetFact(BlackboardKey.IsBlocking, false);
        currentDefensiveAbility = null;
        OnBlockEnd?.Invoke();
    }

    private void HandleDamageIntercept(DamageInterceptArgs args)
    {
        if (currentDefensiveAbility == null) return;
        if (stateMachine == null || stateMachine.GetUpperBodyState() != UpperBodyState.Blocking) return;
        if (!IsAttackWithinBlockAngle(args.attackDirection)) return;

        float timeInDefense = Time.time - defenseStartTime;
        args.damage *= currentDefensiveAbility.GetDefenseMultiplier(timeInDefense, true);

        if (currentDefensiveAbility.CanParry(timeInDefense))
        {
            OnPerfectBlock?.Invoke();
            RefundResourceCosts(currentDefensiveAbility);
        }
    }

    private bool IsAttackWithinBlockAngle(Vector3 attackDirection)
    {
        if (attackDirection == Vector3.zero) return true;
        if (currentDefensiveAbility == null) return false;

        return currentDefensiveAbility.CanBlock(transform.forward, attackDirection);
    }

    private void UpdateCooldownTimers()
    {
        foreach (var kvp in abilityStates)
        {
            var state = kvp.Value;
            if (state.cooldown == null) continue;

            state.cooldown.Tick(Time.deltaTime);
            OnAbilityCooldownChanged?.Invoke(kvp.Key, state.cooldown.CurrentTime);

            if (state.cooldown.IsFinished)
                state.cooldown = null;
        }
    }

    private void StartCooldown(string abilityId, float duration)
    {
        if (duration <= 0f) return;
        if (!abilityStates.TryGetValue(abilityId, out var state)) return;

        state.cooldown = new CountdownTimer(duration);
    }

    public bool IsAbilityOnCooldown(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return false;
        return state.IsOnCooldown;
    }

    public float GetAbilityCooldownRemaining(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return 0f;
        return state.cooldown?.CurrentTime ?? 0f;
    }

    public float GetAbilityCooldown(string abilityId)
    {
        return GetAbilityCooldownRemaining(abilityId);
    }

    public float GetAbilityMaxCooldown(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return 0f;
        return state.definition.cooldown;
    }

    private void EnableAbilityHitboxes()
    {
        var hitboxes = brain.GetComponentsInChildren<WeaponHitbox>(true);
        foreach (var hitbox in hitboxes)
            hitbox.Enable();
    }

    private void DisableAbilityHitboxes()
    {
        var hitboxes = brain.GetComponentsInChildren<WeaponHitbox>(true);
        foreach (var hitbox in hitboxes)
            hitbox.Disable();
    }

    public int GetAbilityRemainingUses(string abilityId)
    {
        if (runtimeAbilityManager == null) return -1;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return -1;

        return instance.remainingUses;
    }

    public int GetAbilityMaxUses(string abilityId)
    {
        if (runtimeAbilityManager == null) return -1;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return -1;

        return instance.maxUses;
    }

    public float GetAbilityRemainingDuration(string abilityId)
    {
        if (runtimeAbilityManager == null) return -1f;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return -1f;

        return instance.GetRemainingDuration();
    }

    public bool IsAbilityConsumable(string abilityId)
    {
        if (runtimeAbilityManager == null) return false;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return false;

        return instance.IsConsumable();
    }

    public bool IsAbilityTemporary(string abilityId)
    {
        if (runtimeAbilityManager == null) return false;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return false;

        return instance.IsTemporary();
    }

    public void NotifyHitLanded(string sourceAbilityId, ControllerBrain target)
    {
        if (!abilityStates.TryGetValue(sourceAbilityId, out var state)) return;
        var ability = state.definition;
        if (ability.hitProcs == null || ability.hitProcs.Count == 0) return;

        var hotbar = brain.GetModule<HotbarSystem>();
        var transforms = brain.GetModule<SlotTransformationSystem>();
        if (hotbar == null || transforms == null) return;

        var (barId, slotIndex) = hotbar.FindSlotForAbility(sourceAbilityId);
        if (barId == null) return;

        foreach (var proc in ability.hitProcs)
        {
            if (proc?.targetAbility == null) continue;
            if (UnityEngine.Random.value < proc.probability)
            {
                transforms.ApplyOverride(barId, slotIndex, proc.targetAbility,
                                         TransformationType.HitProc,
                                         proc.windowSeconds, 30);
            }
        }
    }
}