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

    [Header("Debug")]
    [Tooltip("Log animation events, forwarder binding, and hitbox toggling.")]
    [SerializeField] private bool debugLogging = false;

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
    private ModelModule modelModule;

    private Dictionary<string, AbilityState> abilityStates = new Dictionary<string, AbilityState>();

    private ItemInstance cachedEquippedWeapon;
    private DiceProfile cachedNaturalWeapon;

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

        runtimeAbilityManager = brain.GetModule<RuntimeAbilityManager>();

        animationProvider = brain.Animation;
        movementSystem = brain.Movement;
        resources = brain.GetProvider<IResourceProvider>();
        healthProvider = brain.GetProvider<IHealthProvider>();
        damageSystem = brain.GetModule<DamageSystem>();
        vfxSystem = brain.GetModule<VFXSystem>();

        

        modelModule = brain.GetModule<ModelModule>();
        if (modelModule != null)
            modelModule.OnModelChanged += HandleModelChanged;

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
        // Here, not in Initialize: ControllerBrain initializes this module before BlackboardSystem,
        // which only creates its Blackboard in its own Initialize. Read earlier, this was always
        // null — every forbidden fact passed and IsBlocking / IsInvincible were never written.
        blackboard = brain?.GetModule<BlackboardSystem>()?.Blackboard;
        if (blackboard == null)
        {
            Debug.LogError("[AbilitySystem] BlackboardSystem not found");
            return;
        }

        blackboard.OnBoolChanged += HandleFactChanged;
    }

    // Hard CC interrupts (Combat_Framework §5, F10): the move ends the moment CannotAct rises, not
    // when the committed swing finishes. CannotCast breaks only a spell still casting. No armoured
    // moves exist yet — when they do, they are the exception here.
    private void HandleFactChanged(int key, bool value)
    {
        if (!value) return;

        bool hardControl = key == BlackboardKey.CannotAct;
        bool silencedMidCast = key == BlackboardKey.CannotCast && IsCastingSpell();

        if (!hardControl && !silencedMidCast) return;

        CancelCurrentAbility();
        RestActionsLayer();
    }

    // currentlyCastingAbility holds the id, not the definition.
    private bool IsCastingSpell()
    {
        if (currentlyCastingAbility == null) return false;

        AbilityDefinition casting = GetAbility(currentlyCastingAbility);
        return casting != null && casting.abilityCategory == AbilityCategory.Spell;
    }

    // Gameplay cancelled, so the pose must not play on: send the action layer home. The synced
    // Actions Upper layer follows it. v1 controllers have no Actions layer and are skipped.
    private void RestActionsLayer()
    {
        AnimationSystem anim = brain != null ? brain.Animation : null;
        if (anim == null) return;

        int layer = anim.GetLayerIndex(AnimationLayerNames.Actions);
        if (layer < 0) return;

        anim.CrossFade("Rest", 0.1f, layer);
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

        // The model (and its forwarder, which lives next to the Animator) may have
        // been spawned during the load — always re-resolve rather than keeping a
        // forwarder found before the model existed.
        SetupAnimationEventForwarder();
    }

    /// <summary>
    /// SwapModel destroys the old model — and with it the forwarder we were
    /// subscribed to. Fires after the new Animator reference is set, so we can
    /// re-resolve immediately.
    /// </summary>
    private void HandleModelChanged(string modelId)
    {
        SetupAnimationEventForwarder();
    }

    private void SetupAnimationEventForwarder()
    {
        // Unity only delivers animation events to components on the same
        // GameObject as the Animator — resolve the forwarder from there first.
        AnimationEventForwarder found = null;

        Animator animator = brain.EntityAnimator;
        if (animator != null)
            found = animator.GetComponent<AnimationEventForwarder>();

        if (found == null)
        {
            // Fallback: hierarchy search (model may not be spawned yet;
            // HandleLoadCompleted / HandleModelChanged will re-resolve later).
            Transform playerRoot = brain.transform.parent;
            if (playerRoot != null)
                found = playerRoot.GetComponentInChildren<AnimationEventForwarder>(true);
            else
                found = brain.GetComponentInChildren<AnimationEventForwarder>(true);

            if (found != null && animator != null && found.GetComponent<Animator>() == null)
                Debug.LogWarning($"[AbilitySystem] AnimationEventForwarder on '{found.name}' is not on the Animator's GameObject ('{animator.name}') — animation events will not reach it. Move the component next to the Animator.");
        }

        BindEventForwarder(found);
    }

    private void BindEventForwarder(AnimationEventForwarder forwarder)
    {
        if (forwarder == eventForwarder && forwarder != null)
            return; // already bound to the right one

        if (eventForwarder != null)
        {
            eventForwarder.OnAnimationEvent -= HandleAnimationEvent;
            eventForwarder.OnStateTransitionEvent -= HandleStateTransition;
        }

        eventForwarder = forwarder;

        if (eventForwarder == null)
        {
            Debug.LogWarning($"[AbilitySystem] No AnimationEventForwarder found on {gameObject.name}");
            return;
        }

        eventForwarder.Initialize(brain);
        eventForwarder.OnAnimationEvent += HandleAnimationEvent;
        eventForwarder.OnStateTransitionEvent += HandleStateTransition;
        if (debugLogging)
            Debug.Log($"[AbilitySystem] Subscribed to AnimationEventForwarder on {eventForwarder.name}");
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

        if (blackboard != null)
            blackboard.OnBoolChanged -= HandleFactChanged;

        if (modelModule != null)
            modelModule.OnModelChanged -= HandleModelChanged;

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

        SetFact(BlackboardKey.MoveRooted, false);

        if (currentAbility != null)
            CompleteAbility(currentAbility);

        // A held guard is not currentAbility once its clip has ended, so it is dropped here too —
        // and through the one method that also unsubscribes the damage intercept.
        DeactivateDefensiveAbility();

        if (stateMachine != null)
            stateMachine.TryTransitionUpperBody(UpperBodyState.Idle);

        isAnimationLocked = false;
        isInvincible = false;
        SetFact(BlackboardKey.IsInvincible, false);
        UpdateExecutingFact();
    }

    private bool CheckBlackboardRequirements(AbilityDefinition ability)
    {
        if (blackboard == null) return true;

        var (requiredAny, requiredAll, forbidden) = ability.GetBlackboardRequirements();

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

        foreach (var fact in forbidden)
        {
            if (blackboard.GetBool(fact.GetHashCode()))
                return false;
        }

        return true;
    }

    private void StartCast(AbilityDefinition ability)
    {
        SetFact(BlackboardKey.MoveRooted, !ability.castWhileMoving);
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

        // Layers need nothing: the Actions layers show while their state plays (rest rule), and
        // ActionsLayerDriver picks arms-only or full body from speed. Rooting is the fact.
        SetFact(BlackboardKey.MoveRooted, !ability.castWhileMoving);
        EnterUpperBodyState(StartStateFor(ability));

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
        // A defensive ability that ends — by AnimUnlocked or the safety timeout — must drop the block,
        // or IsBlocking stays true and the intercept stays subscribed for the rest of the session.
        if (ability == currentDefensiveAbility)
            DeactivateDefensiveAbility();

        if (stateMachine != null)
            stateMachine.TryTransitionUpperBody(UpperBodyState.Idle);

        SetFact(BlackboardKey.MoveRooted, false);

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
        if (debugLogging)
            Debug.Log($"[AbilitySystem] HandleAnimationEvent received: {eventType} (currentAbility: {currentAbility?.abilityId ?? "null"})");

        OnAbilityAnimationEvent?.Invoke(eventType);

        switch (eventType)
        {
            case AnimationEventType.HitboxStart:
                EnableAbilityHitboxes();
                EnterMeleePhase(UpperBodyState.MeleeSwing);
                break;
            case AnimationEventType.HitboxEnd:
                DisableAbilityHitboxes();
                EnterMeleePhase(UpperBodyState.MeleeRecovery);
                break;
        }

        if (currentAbility == null) return;

        if (eventType == currentAbility.effectTrigger)
            ExecuteAbilityEffects(currentAbility);

        if (eventType == AnimationEventType.AnimUnlocked)
            HandleAnimationUnlocked();

        if (eventType == AnimationEventType.ComboWindowStart)
            OpenChainWindow();

        if (eventType == AnimationEventType.ComboWindowEnd)
            CloseChainWindow();
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

    // An animation event naming a state (OnStateTransition("…")) still wins over the defaults below.
    private void HandleStateTransition(UpperBodyState newState) => EnterUpperBodyState(newState);

    // ── Upper-body state ──────────────────────────────────────────────────
    // The state machine is the one answer to "what are the arms doing". AI, camera and the movement
    // permission matrix read it. Phases come from the hitbox events until the CF1 move clock lands.

    private void EnterUpperBodyState(UpperBodyState state)
    {
        if (stateMachine == null) return;
        stateMachine.TryTransitionUpperBody(state);
    }

    private void EnterMeleePhase(UpperBodyState phase)
    {
        if (currentAbility == null || !IsMelee(currentAbility)) return;
        EnterUpperBodyState(phase);
    }

    private static bool IsMelee(AbilityDefinition ability)
        => ability.abilityCategory == AbilityCategory.Physical || ability.abilityCategory == AbilityCategory.Natural;

    private static UpperBodyState StartStateFor(AbilityDefinition ability)
    {
        if (ability.abilityType == AbilityType.Defensive) return UpperBodyState.Blocking;
        if (ability.abilityCategory == AbilityCategory.Spell) return UpperBodyState.CastingWindUp;
        if (IsMelee(ability)) return UpperBodyState.MeleeWindUp;
        return UpperBodyState.Idle;
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

    /// <summary>
    /// Enables only the hitboxes the executing ability names. An ability with no tags enables
    /// every hitbox, which is how weapon abilities behaved before tagging existed.
    /// Enable() can still refuse on its own stance filter, so a match is not a guarantee.
    /// </summary>
    private void EnableAbilityHitboxes()
    {
        var hitboxes = brain.GetComponentsInChildren<WeaponHitbox>(true);
        var tags = currentAbility != null ? currentAbility.hitboxTags : null;

        int matched = 0;

        foreach (var hitbox in hitboxes)
        {
            if (!hitbox.MatchesTags(tags)) continue;

            hitbox.Enable();
            matched++;
        }

        if (matched == 0 && tags != null && tags.Count > 0)
            Debug.LogWarning($"[AbilitySystem] '{currentAbility.abilityName}' names hitbox tag(s) " +
                             $"[{string.Join(", ", tags)}] but nothing under {brain.name} carries them — " +
                             $"this attack cannot connect. Check the tags on the model's hitboxes.");

        if (debugLogging)
            Debug.Log($"[AbilitySystem] EnableAbilityHitboxes — {matched}/{hitboxes.Length} matched for " +
                      $"'{currentAbility?.abilityName ?? "(none)"}'");
    }

    private void DisableAbilityHitboxes()
    {
        var hitboxes = brain.GetComponentsInChildren<WeaponHitbox>(true);
        if (debugLogging)
            Debug.Log($"[AbilitySystem] DisableAbilityHitboxes — found {hitboxes.Length} WeaponHitbox component(s) under {brain.name}");
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
