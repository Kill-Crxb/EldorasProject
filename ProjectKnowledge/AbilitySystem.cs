using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using NinjaGame.Animation;

/// <summary>
/// Universal Ability System - Phase 1.5 Refactor
/// 
/// NEW - Phase 1 Features:
/// - State Machine Integration (validates state transitions)
/// - Blackboard Validation (semantic requirement checking)
/// - Animation Event System (Effect1/2/3, AnimUnlocked, ComboWindow)
/// - Unified Defense Processing (IDefenseProvider merged in)
/// - Chaining System (combo progression)
/// - Multi-Resource Costs (mana + stamina, etc.)
/// 
/// Architecture:
/// - ONE AbilityLoadoutModule (manages slots)
/// - ONE active ControlSource (defines control)
/// - Universal execution (works for all entities)
/// 
/// Integration:
/// - StateMachineModule: Validates and transitions states
/// - BlackboardSystem: Checks semantic requirements
/// - AnimationEventForwarder: Precise ability timing
/// - ResourceSystem: Multi-resource cost handling
/// </summary>
public class AbilitySystem : MonoBehaviour, IBrainModule, IAbilityProvider, IDefenseProvider
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Core Components")]
    [Tooltip("Manages ability slots, combos, and input routing")]
    [SerializeField] private AbilityLoadoutModule loadoutModule;

    [Header("Registered Abilities")]
    [Tooltip("All abilities available to this entity")]
    [SerializeField] private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // ========================================
    // References
    // ========================================

    private ControllerBrain brain;
    private StateMachineModule stateMachine;
    private Blackboard blackboard;
    private RuntimeAbilityManager runtimeAbilityManager;  // PHASE 2: Dynamic ability management
    private IAnimationProvider animationProvider;
    private IResourceProvider resources;
    private IHealthProvider healthProvider;
    private MovementSystem movementSystem;
    private DamageSystem damageSystem;
    private AnimationEventForwarder eventForwarder;

    // Ability lookup
    private Dictionary<string, AbilityDefinition> abilityLookup = new Dictionary<string, AbilityDefinition>();

    // Cooldown tracking
    private Dictionary<string, CountdownTimer> cooldownTimers = new Dictionary<string, CountdownTimer>();

    // Current ability execution state
    private AbilityDefinition currentAbility = null;
    private float abilityStartTime;
    private Coroutine safetyTimeoutCoroutine;

    // Casting state (pre-execution)
    private string currentlyCastingAbility = null;
    private float castStartTime;

    // Animation event state
    private bool isAnimationLocked = false;
    private bool isMovementLocked = false;
    private bool isInvincible = false;
    private HashSet<Collider> activeHitboxes = new HashSet<Collider>();

    // Chaining state
    private AbilityDefinition lastCompletedAbility = null;
    private float lastAbilityCompleteTime = 0f;
    private bool chainWindowOpen = false;
    private float chainWindowOpenTime = 0f;

    // Defensive ability state (IDefenseProvider)
    private AbilityDefinition currentDefensiveAbility = null;
    private float defenseStartTime = 0f;

    // ========================================
    // Properties
    // ========================================

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public ControllerBrain Brain => brain;
    public AbilityLoadoutModule Loadout => loadoutModule;

    /// <summary>
    /// Is the ability system currently executing an ability?
    /// </summary>
    public bool IsExecuting => currentAbility != null || currentlyCastingAbility != null || isAnimationLocked;

    /// <summary>
    /// Is movement currently locked by an ability animation?
    /// </summary>
    public bool IsMovementLocked => isMovementLocked;

    /// <summary>
    /// Is the entity currently invincible (iFrames)?
    /// </summary>
    public bool IsInvincible => isInvincible;

    // ========================================
    // Events
    // ========================================

    public event Action<string> OnAbilityUsed;
    public event Action<string, float> OnAbilityCooldownChanged;
    public event Action<string> OnAbilityCastStart;
    public event Action<string> OnAbilityCastComplete;
    public event Action<AnimationEventType> OnAbilityAnimationEvent;

    // IDefenseProvider events
    public event Action OnBlockStart;
    public event Action OnBlockEnd;
    public event Action OnPerfectBlock;

    // ========================================
    // IBrainModule Implementation
    // ========================================

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Get state machine (REQUIRED)
        stateMachine = brain.GetModule<StateMachineModule>();
        if (stateMachine == null)
        {
            Debug.LogError("[AbilitySystem] StateMachineModule not found! Ability system requires state machine.");
        }

        // Get blackboard (REQUIRED)
        var blackboardSystem = brain.GetModule<BlackboardSystem>();
        if (blackboardSystem != null)
        {
            blackboard = blackboardSystem.Blackboard;
        }
        else
        {
            Debug.LogError("[AbilitySystem] BlackboardSystem not found! Semantic validation won't work.");
        }

        // Get RuntimeAbilityManager (PHASE 2: Optional - enables dynamic abilities)
        runtimeAbilityManager = brain.GetModule<RuntimeAbilityManager>();
        if (runtimeAbilityManager != null && showDebugInfo)
        {
            Debug.Log("[AbilitySystem] RuntimeAbilityManager found - dynamic abilities enabled");
        }

        // Get core system references
        animationProvider = brain.Animation;
        movementSystem = brain.Movement;

        // Get provider interfaces
        resources = brain.GetProvider<IResourceProvider>();
        healthProvider = brain.GetProvider<IHealthProvider>();

        // Get optional modules
        damageSystem = brain.GetModule<DamageSystem>();

        // Validate loadout module
        if (loadoutModule == null)
        {
            Debug.LogError("[AbilitySystem] LoadoutModule not assigned! Assign AbilityLoadoutModule in Inspector.");
        }

        // Setup animation events
        SetupAnimationEventForwarder();

        // Build ability lookup
        BuildAbilityLookup();

        // Warnings for missing dependencies
        if (resources == null)
            Debug.LogWarning($"[AbilitySystem] No IResourceProvider found - resource costs won't work");

        if (animationProvider == null)
            Debug.LogError($"[AbilitySystem] AnimationSystem not assigned!");

        if (movementSystem == null)
            Debug.LogError($"[AbilitySystem] MovementSystem not assigned!");

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Initialized on {brain.name}");
            Debug.Log($"  StateMachine: {(stateMachine != null ? "✓" : "✗ MISSING")}");
            Debug.Log($"  Blackboard: {(blackboard != null ? "✓" : "✗ MISSING")}");
            Debug.Log($"  Animation: {(animationProvider != null ? "✓" : "✗ MISSING")}");
            Debug.Log($"  Movement: {(movementSystem != null ? "✓" : "✗ MISSING")}");
            Debug.Log($"  Registered Abilities: {abilityLookup.Count}");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        UpdateCooldownTimers();
        UpdateCasting();
        UpdateChainWindow();
        UpdateDefensiveAbility();
    }

    // ========================================
    // Setup
    // ========================================

    private void SetupAnimationEventForwarder()
    {
        // Find AnimationEventForwarder
        Transform playerRoot = brain.transform.parent;
        if (playerRoot != null)
        {
            eventForwarder = playerRoot.GetComponentInChildren<AnimationEventForwarder>();
        }
        else
        {
            eventForwarder = brain.GetComponentInChildren<AnimationEventForwarder>();
        }

        if (eventForwarder == null)
        {
            Debug.LogWarning($"[AbilitySystem] No AnimationEventForwarder found on {gameObject.name}");
        }
        else
        {
            // Subscribe to animation events
            eventForwarder.OnAnimationEvent += HandleAnimationEvent;
        }
    }

    private void BuildAbilityLookup()
    {
        abilityLookup.Clear();
        foreach (var ability in abilities)
        {
            if (ability != null && !string.IsNullOrEmpty(ability.abilityId))
            {
                abilityLookup[ability.abilityId] = ability;
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Registered {abilityLookup.Count} abilities");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from animation events
        if (eventForwarder != null)
        {
            eventForwarder.OnAnimationEvent -= HandleAnimationEvent;
        }
    }

    // ========================================
    // Ability Management
    // ========================================

    /// <summary>
    /// Add an ability to this entity's ability pool
    /// </summary>
    public void AddAbility(AbilityDefinition ability)
    {
        if (ability == null)
        {
            Debug.LogWarning("[AbilitySystem] Attempted to add null ability");
            return;
        }

        if (string.IsNullOrEmpty(ability.abilityId))
        {
            Debug.LogWarning($"[AbilitySystem] Ability {ability.abilityName} has no ID");
            return;
        }

        // Add to list
        if (!abilities.Contains(ability))
        {
            abilities.Add(ability);
        }

        // Add to lookup
        abilityLookup[ability.abilityId] = ability;

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Added ability: {ability.abilityName} (ID: {ability.abilityId})");
        }
    }

    /// <summary>
    /// Remove an ability from this entity's ability pool
    /// </summary>
    public void RemoveAbility(string abilityId)
    {
        if (abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability))
        {
            abilities.Remove(ability);
            abilityLookup.Remove(abilityId);

            if (showDebugInfo)
            {
                Debug.Log($"[AbilitySystem] Removed ability: {abilityId}");
            }
        }
    }

    /// <summary>
    /// Get an ability definition by ID
    /// </summary>
    public AbilityDefinition GetAbility(string abilityId)
    {
        abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability);
        return ability;
    }

    /// <summary>
    /// Get all registered abilities
    /// </summary>
    public List<AbilityDefinition> GetAllAbilities()
    {
        return new List<AbilityDefinition>(abilities);
    }

    // ========================================
    // Ability Execution (IAbilityProvider)
    // ========================================

    public bool CanUseAbility(string abilityId)
    {
        // Guard clauses (flat logic)
        if (!isEnabled) return false;
        if (currentAbility != null) return false;  // Already executing
        if (currentlyCastingAbility != null) return false;  // Currently casting
        if (isAnimationLocked) return false;
        if (!abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability)) return false;
        if (IsAbilityOnCooldown(abilityId)) return false;

        // PHASE 2: Check consumable uses (if using RuntimeAbilityManager)
        if (runtimeAbilityManager != null)
        {
            var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
            if (instance != null)
            {
                if (!instance.IsUsable())
                {
                    if (showDebugInfo)
                        Debug.Log($"[AbilitySystem] Cannot use {ability.abilityName} - instance not usable (uses: {instance.remainingUses}/{instance.maxUses})");
                    return false;
                }
            }
        }

        // Check state transition (can we enter the required state?)
        if (stateMachine != null)
        {
            if (!stateMachine.CanPerformUpperBodyAction(ability.setsUpperBodyState))
            {
                if (showDebugInfo)
                    Debug.Log($"[AbilitySystem] Cannot use {ability.abilityName} - state transition blocked");
                return false;
            }
        }

        // Check blackboard requirements (semantic validation)
        if (!CheckBlackboardRequirements(ability))
        {
            if (showDebugInfo)
                Debug.Log($"[AbilitySystem] Cannot use {ability.abilityName} - blackboard requirements not met");
            return false;
        }

        // Check resource costs
        if (ability.resourceCosts != null && resources != null)
        {
            foreach (var cost in ability.resourceCosts)
            {
                if (cost.resource != null && cost.cost > 0)
                {
                    if (!resources.HasResource(cost.resource, cost.cost))
                        return false;
                }
            }
        }

        return true;
    }

    public void UseAbility(string abilityId)
    {
        // Guard clause - validate ability can be used
        if (!CanUseAbility(abilityId)) return;

        if (!abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability))
        {
            Debug.LogWarning($"[AbilitySystem] Ability {abilityId} not found");
            return;
        }

        // PHASE 2: Notify RuntimeAbilityManager (for consumable tracking)
        if (runtimeAbilityManager != null)
        {
            var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
            if (instance != null)
            {
                runtimeAbilityManager.OnAbilityUsed(instance.instanceId);
            }
        }

        // Consume resources
        if (ability.resourceCosts != null && resources != null)
        {
            foreach (var cost in ability.resourceCosts)
            {
                if (cost.resource != null && cost.cost > 0)
                {
                    resources.ConsumeResource(cost.resource, cost.cost);
                }
            }
        }

        // Start cast or execute immediately
        if (ability.castTime > 0f)
        {
            StartCast(ability);
        }
        else
        {
            ExecuteAbility(ability);
        }

        // Start cooldown
        StartCooldown(abilityId, ability.cooldown);

        // Raise event
        OnAbilityUsed?.Invoke(abilityId);

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Used ability: {ability.abilityName} (ID: {abilityId})");
        }
    }

    /// <summary>
    /// Cancel the currently casting/executing ability
    /// </summary>
    public void CancelCurrentAbility()
    {
        if (currentlyCastingAbility != null)
        {
            if (showDebugInfo)
                Debug.Log($"[AbilitySystem] Cancelled casting: {currentlyCastingAbility}");

            currentlyCastingAbility = null;
        }

        if (currentAbility != null)
        {
            if (showDebugInfo)
                Debug.Log($"[AbilitySystem] Cancelled executing: {currentAbility.abilityName}");

            CompleteAbility(currentAbility);
        }

        // Reset locks
        isAnimationLocked = false;
        isMovementLocked = false;
    }

    // ========================================
    // Blackboard Validation (NEW - Phase 1)
    // ========================================

    /// <summary>
    /// Check if ability's blackboard requirements are met
    /// Includes category-based defaults + ability-specific requirements
    /// </summary>
    private bool CheckBlackboardRequirements(AbilityDefinition ability)
    {
        // Guard clause
        if (blackboard == null) return true;  // No blackboard, allow

        // Get combined requirements (category defaults + ability-specific)
        var (requiredAny, requiredAll, forbiddenAll) = ability.GetBlackboardRequirements();

        // Check required facts (ANY logic - at least one must be true)
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
            {
                if (showDebugInfo)
                    Debug.Log($"[AbilitySystem] Required facts (ANY) not met for {ability.abilityName}");
                return false;
            }
        }

        // Check required facts (ALL logic - all must be true)
        foreach (var fact in requiredAll)
        {
            if (!blackboard.GetBool(fact.GetHashCode()))
            {
                if (showDebugInfo)
                    Debug.Log($"[AbilitySystem] Required fact '{fact}' not met for {ability.abilityName}");
                return false;
            }
        }

        // Check forbidden facts (ALL logic - blocked if ALL are true)
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
            {
                if (showDebugInfo)
                    Debug.Log($"[AbilitySystem] Blocked by forbidden facts for {ability.abilityName}");
                return false;
            }
        }

        return true;
    }

    // ========================================
    // Casting (Pre-Execution)
    // ========================================

    private void StartCast(AbilityDefinition ability)
    {
        currentlyCastingAbility = ability.abilityId;
        castStartTime = Time.time;

        OnAbilityCastStart?.Invoke(ability.abilityId);

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Started casting: {ability.abilityName} (cast time: {ability.castTime}s)");
        }
    }

    private void UpdateCasting()
    {
        if (currentlyCastingAbility == null) return;

        if (!abilityLookup.TryGetValue(currentlyCastingAbility, out AbilityDefinition ability))
        {
            currentlyCastingAbility = null;
            return;
        }

        if (Time.time - castStartTime >= ability.castTime)
        {
            ExecuteAbility(ability);
            currentlyCastingAbility = null;

            OnAbilityCastComplete?.Invoke(ability.abilityId);
        }
    }

    // ========================================
    // Execution (NEW - Phase 1)
    // ========================================

    private void ExecuteAbility(AbilityDefinition ability)
    {
        // Transition state
        if (stateMachine != null)
        {
            bool transitioned = stateMachine.TryTransitionUpperBody(ability.setsUpperBodyState);
            if (!transitioned)
            {
                if (showDebugInfo)
                    Debug.LogWarning($"[AbilitySystem] Failed to transition to {ability.setsUpperBodyState} for {ability.abilityName}");
                return;
            }
        }

        // Track current ability
        currentAbility = ability;
        abilityStartTime = Time.time;

        // Play animation
        if (animationProvider != null && !string.IsNullOrEmpty(ability.animationTrigger))
        {
            animationProvider.TriggerCombatAnimation(ability.animationTrigger);
        }

        // Handle defensive abilities differently
        if (ability.abilityType == AbilityType.Defensive)
        {
            ActivateDefensiveAbility(ability);
        }

        // If effect trigger is not one of the standard effect events, execute immediately
        // (This handles instant abilities that don't wait for animation events)
        bool hasEffectTrigger = ability.effectTrigger == AnimationEventType.Effect1 ||
                               ability.effectTrigger == AnimationEventType.Effect2 ||
                               ability.effectTrigger == AnimationEventType.Effect3;

        if (!hasEffectTrigger)
        {
            ExecuteAbilityEffects(ability);

            // Complete immediately if not waiting for AnimUnlocked
            if (!ability.waitForAnimUnlock)
            {
                CompleteAbility(ability);
            }
        }

        // Start safety timeout if configured
        if (ability.maxDuration > 0f)
        {
            if (safetyTimeoutCoroutine != null)
            {
                StopCoroutine(safetyTimeoutCoroutine);
            }
            safetyTimeoutCoroutine = StartCoroutine(SafetyTimeoutCoroutine(ability));
        }

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Executed ability: {ability.abilityName}");
        }
    }

    private IEnumerator SafetyTimeoutCoroutine(AbilityDefinition ability)
    {
        yield return new WaitForSeconds(ability.maxDuration);

        // If still the current ability after timeout, force complete
        if (currentAbility == ability)
        {
            Debug.LogWarning($"[AbilitySystem] {ability.abilityName} timed out - AnimUnlocked never fired!");
            CompleteAbility(ability);
        }

        safetyTimeoutCoroutine = null;
    }

    /// <summary>
    /// Execute ability effects (damage, healing, movement, etc.)
    /// </summary>
    private void ExecuteAbilityEffects(AbilityDefinition ability)
    {
        // Movement abilities
        bool isMovementAbility = ability.movementEffects != null && ability.movementEffects.Count > 0;
        if (isMovementAbility && movementSystem != null)
        {
            ability.ExecuteMovement(movementSystem);

            if (showDebugInfo)
                Debug.Log($"[AbilitySystem] Executed movement ability: {ability.abilityName}");
        }

        // Self-targeted abilities
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

        // TODO Phase 4: Polymorphic effect execution
        // foreach (var effect in ability.effects)
        // {
        //     effect.Execute(new AbilityContext(brain, ...));
        // }
    }

    /// <summary>
    /// Complete ability execution and return to idle
    /// </summary>
    private void CompleteAbility(AbilityDefinition ability)
    {
        // Guard clause
        if (ability == null) return;

        // Return to idle state (if we're still in the ability's state)
        if (stateMachine != null && stateMachine.GetUpperBodyState() == ability.setsUpperBodyState)
        {
            stateMachine.TryTransitionUpperBody(UpperBodyState.Idle);
        }

        // Track for chaining
        lastCompletedAbility = ability;
        lastAbilityCompleteTime = Time.time;

        // Clear current
        if (currentAbility == ability)
        {
            currentAbility = null;
        }

        // Stop safety timeout
        if (safetyTimeoutCoroutine != null)
        {
            StopCoroutine(safetyTimeoutCoroutine);
            safetyTimeoutCoroutine = null;
        }

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Completed ability: {ability.abilityName}");
        }
    }

    // ========================================
    // Animation Events (NEW - Phase 1)
    // ========================================

    private void HandleAnimationEvent(AnimationEventType eventType)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Animation Event: {eventType}");
        }

        OnAbilityAnimationEvent?.Invoke(eventType);

        // Guard clause - no current ability
        if (currentAbility == null) return;

        // Check if this is our effect trigger
        if (eventType == currentAbility.effectTrigger)
        {
            ExecuteAbilityEffects(currentAbility);
        }

        // Check for animation unlock
        if (eventType == AnimationEventType.AnimUnlocked)
        {
            HandleAnimationUnlocked();
        }

        // Check for combo window
        if (eventType == AnimationEventType.ComboWindowStart)
        {
            OpenChainWindow();
        }

        if (eventType == AnimationEventType.ComboWindowEnd)
        {
            CloseChainWindow();
        }

        // Handle common events
        switch (eventType)
        {
            case AnimationEventType.HitboxStart:
                EnableAbilityHitboxes();
                break;

            case AnimationEventType.HitboxEnd:
                DisableAbilityHitboxes();
                break;

            case AnimationEventType.AnimLocked:
                isAnimationLocked = true;
                break;

            case AnimationEventType.MovementLocked:
                isMovementLocked = true;
                break;

            case AnimationEventType.MovementUnlocked:
                isMovementLocked = false;
                break;

            case AnimationEventType.IFrameStart:
                isInvincible = true;
                break;

            case AnimationEventType.IFrameEnd:
                isInvincible = false;
                break;
        }
    }

    private void HandleAnimationUnlocked()
    {
        // Guard clauses
        if (currentAbility == null) return;
        if (!currentAbility.waitForAnimUnlock) return;

        // Unlock animation
        isAnimationLocked = false;

        // Complete ability
        CompleteAbility(currentAbility);
    }

    // ========================================
    // Chaining System (NEW - Phase 1)
    // ========================================

    private void OpenChainWindow()
    {
        chainWindowOpen = true;
        chainWindowOpenTime = Time.time;

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Chain window opened");
        }
    }

    private void CloseChainWindow()
    {
        chainWindowOpen = false;

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Chain window closed");
        }
    }

    private void UpdateChainWindow()
    {
        // Auto-close chain window after duration
        if (chainWindowOpen && lastCompletedAbility != null)
        {
            float elapsed = Time.time - chainWindowOpenTime;
            if (elapsed >= lastCompletedAbility.chainWindow)
            {
                CloseChainWindow();
            }
        }
    }

    /// <summary>
    /// Check if can chain to next ability in combo
    /// </summary>
    public bool CanChainToNext()
    {
        // Guard clauses
        if (lastCompletedAbility == null) return false;
        if (lastCompletedAbility.nextInChain == null) return false;
        if (!chainWindowOpen) return false;

        // Check if within chain window
        float timeSinceOpen = Time.time - chainWindowOpenTime;
        return timeSinceOpen <= lastCompletedAbility.chainWindow;
    }

    /// <summary>
    /// Try to chain to next ability in combo
    /// </summary>
    public void TryChainNext()
    {
        if (!CanChainToNext()) return;

        UseAbility(lastCompletedAbility.nextInChain.abilityId);
    }

    // ========================================
    // Defensive Abilities (NEW - Phase 1)
    // ========================================

    private void ActivateDefensiveAbility(AbilityDefinition ability)
    {
        currentDefensiveAbility = ability;
        defenseStartTime = Time.time;

        OnBlockStart?.Invoke();

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Activated defensive ability: {ability.abilityName}");
        }
    }

    private void UpdateDefensiveAbility()
    {
        // Drain resources while defensive ability is active
        if (currentDefensiveAbility != null && resources != null)
        {
            if (currentDefensiveAbility.resourceCosts != null)
            {
                foreach (var cost in currentDefensiveAbility.resourceCosts)
                {
                    if (cost.drain > 0f)
                    {
                        float drainAmount = cost.drain * Time.deltaTime;

                        // Check if we have enough resource
                        if (!resources.HasResource(cost.resource, drainAmount))
                        {
                            // Out of resources, end defense
                            DeactivateDefensiveAbility();
                            return;
                        }

                        resources.ConsumeResource(cost.resource, drainAmount);
                    }
                }
            }
        }
    }

    private void DeactivateDefensiveAbility()
    {
        if (currentDefensiveAbility == null) return;

        OnBlockEnd?.Invoke();

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Deactivated defensive ability");
        }

        currentDefensiveAbility = null;
    }

    // ========================================
    // IDefenseProvider Implementation (NEW - Phase 1)
    // ========================================

    bool IDefenseProvider.IsBlocking()
    {
        return currentDefensiveAbility != null &&
               stateMachine != null &&
               stateMachine.GetUpperBodyState() == UpperBodyState.Blocking;
    }

    bool IDefenseProvider.IsParrying()
    {
        if (currentDefensiveAbility == null) return false;

        float timeInDefense = Time.time - defenseStartTime;
        return timeInDefense <= currentDefensiveAbility.parryWindowDuration;
    }

    bool IDefenseProvider.CanDefend()
    {
        return isEnabled && currentDefensiveAbility != null;
    }

    float IDefenseProvider.ProcessIncomingDamage(float damage, Vector3 attackDirection)
    {
        // Guard clauses
        if (currentDefensiveAbility == null) return damage;
        if (!((IDefenseProvider)this).IsBlocking()) return damage;

        // Check block angle
        if (!IsAttackWithinBlockAngle(attackDirection))
            return damage;

        // Determine if in parry window
        bool isParry = ((IDefenseProvider)this).IsParrying();
        float damageReduction = isParry ?
            currentDefensiveAbility.parryDamageReduction :
            currentDefensiveAbility.blockDamageReduction;

        // Calculate final damage
        float finalDamage = damage * (1f - damageReduction);

        // Handle parry
        if (isParry)
        {
            OnPerfectBlock?.Invoke();

            // Refund resources
            if (currentDefensiveAbility.resourceCosts != null && resources != null)
            {
                foreach (var cost in currentDefensiveAbility.resourceCosts)
                {
                    if (cost.refund > 0f)
                    {
                        resources.RestoreResource(cost.resource, cost.refund);
                    }
                }
            }

            if (showDebugInfo)
            {
                Debug.Log($"[AbilitySystem] PARRY! {damage:F1} → {finalDamage:F1}");
            }
        }
        else if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] BLOCK: {damage:F1} → {finalDamage:F1}");
        }

        return finalDamage;
    }

    float IDefenseProvider.GetDefensiveMultiplier(Vector3 attackDirection)
    {
        if (currentDefensiveAbility == null) return 1f;
        if (!((IDefenseProvider)this).IsBlocking()) return 1f;
        if (!IsAttackWithinBlockAngle(attackDirection)) return 1f;

        bool isParry = ((IDefenseProvider)this).IsParrying();
        float reduction = isParry ?
            currentDefensiveAbility.parryDamageReduction :
            currentDefensiveAbility.blockDamageReduction;

        return 1f - reduction;
    }

    private bool IsAttackWithinBlockAngle(Vector3 attackDirection)
    {
        if (attackDirection == Vector3.zero) return true;
        if (currentDefensiveAbility == null) return false;

        float angle = Vector3.Angle(transform.forward, -attackDirection);
        return angle <= currentDefensiveAbility.blockAngle * 0.5f;
    }

    // ========================================
    // Cooldowns
    // ========================================

    private void UpdateCooldownTimers()
    {
        // Update all active cooldown timers
        List<string> expiredCooldowns = null;

        foreach (var kvp in cooldownTimers)
        {
            var timer = kvp.Value;
            timer.Tick(Time.deltaTime);

            // Emit cooldown changed event
            OnAbilityCooldownChanged?.Invoke(kvp.Key, timer.CurrentTime);

            // Track expired
            if (timer.IsFinished)
            {
                if (expiredCooldowns == null)
                    expiredCooldowns = new List<string>();
                expiredCooldowns.Add(kvp.Key);
            }
        }

        // Remove expired cooldowns
        if (expiredCooldowns != null)
        {
            foreach (var abilityId in expiredCooldowns)
            {
                cooldownTimers.Remove(abilityId);

                if (showDebugInfo)
                    Debug.Log($"[AbilitySystem] Cooldown expired: {abilityId}");
            }
        }
    }

    private void StartCooldown(string abilityId, float duration)
    {
        if (duration <= 0f) return;

        var timer = new CountdownTimer(duration);
        cooldownTimers[abilityId] = timer;

        if (showDebugInfo)
            Debug.Log($"[AbilitySystem] Started cooldown: {abilityId} ({duration}s)");
    }

    public bool IsAbilityOnCooldown(string abilityId)
    {
        return cooldownTimers.ContainsKey(abilityId) &&
               !cooldownTimers[abilityId].IsFinished;
    }

    public float GetAbilityCooldownRemaining(string abilityId)
    {
        if (cooldownTimers.TryGetValue(abilityId, out CountdownTimer timer))
        {
            return timer.CurrentTime;
        }
        return 0f;
    }

    // IAbilityProvider interface methods
    public float GetAbilityCooldown(string abilityId)
    {
        return GetAbilityCooldownRemaining(abilityId);
    }

    public float GetAbilityMaxCooldown(string abilityId)
    {
        if (!abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability))
            return 0f;

        return ability.cooldown;
    }

    // ========================================
    // Hitbox Management
    // ========================================

    private void EnableAbilityHitboxes()
    {
        // TODO: Implement hitbox activation
        if (showDebugInfo)
            Debug.Log("[AbilitySystem] Hitboxes enabled");
    }

    private void DisableAbilityHitboxes()
    {
        // TODO: Implement hitbox deactivation
        if (showDebugInfo)
            Debug.Log("[AbilitySystem] Hitboxes disabled");
    }

    // ========================================
    // PHASE 2: Runtime Ability Query Methods
    // ========================================

    /// <summary>
    /// Get remaining uses for consumable abilities
    /// Returns -1 if infinite or not consumable
    /// </summary>
    public int GetAbilityRemainingUses(string abilityId)
    {
        if (runtimeAbilityManager == null) return -1;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return -1;

        return instance.remainingUses;
    }

    /// <summary>
    /// Get max uses for consumable abilities
    /// Returns -1 if infinite or not consumable
    /// </summary>
    public int GetAbilityMaxUses(string abilityId)
    {
        if (runtimeAbilityManager == null) return -1;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return -1;

        return instance.maxUses;
    }

    /// <summary>
    /// Get remaining duration for temporary abilities
    /// Returns -1 if permanent or not found
    /// </summary>
    public float GetAbilityRemainingDuration(string abilityId)
    {
        if (runtimeAbilityManager == null) return -1f;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return -1f;

        return instance.GetRemainingDuration();
    }

    /// <summary>
    /// Check if ability is consumable
    /// </summary>
    public bool IsAbilityConsumable(string abilityId)
    {
        if (runtimeAbilityManager == null) return false;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return false;

        return instance.IsConsumable();
    }

    /// <summary>
    /// Check if ability is temporary
    /// </summary>
    public bool IsAbilityTemporary(string abilityId)
    {
        if (runtimeAbilityManager == null) return false;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return false;

        return instance.IsTemporary();
    }

    // ========================================
    // Debug Commands
    // ========================================

    [ContextMenu("Debug/Print Runtime Abilities")]
    private void DebugPrintRuntimeAbilities()
    {
        if (runtimeAbilityManager == null)
        {
            Debug.Log("[AbilitySystem] No RuntimeAbilityManager");
            return;
        }

        Debug.Log("=== Runtime Abilities ===");
        var allInstances = runtimeAbilityManager.GetAllInstances();

        foreach (var instance in allInstances)
        {
            Debug.Log($"  {instance}");
        }

        Debug.Log($"Total: {allInstances.Count} instances");
    }
}