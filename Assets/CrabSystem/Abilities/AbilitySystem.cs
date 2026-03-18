using NinjaGame.Animation;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Universal Ability System
/// Executes abilities through animation-driven timing with state machine integration
/// and blackboard validation. Supports offensive/defensive/utility abilities with
/// multi-resource costs, cooldowns, and combo chaining. Works for all entity types.
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

    // Consolidated ability state (PHASE 1: No more parallel dictionaries)
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

    private Dictionary<string, AbilityState> abilityStates = new Dictionary<string, AbilityState>();

    // Cached equipment references (PHASE 1: Avoid string lookups)
    private ItemInstance cachedEquippedWeapon;
    private WeaponData cachedNaturalWeapon;

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
            Debug.Log($"  Registered Abilities: {abilityStates.Count}");
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
            eventForwarder.OnStateTransitionEvent += HandleStateTransition;  // NEW: Animation-driven states
        }
    }

    private void BuildAbilityLookup()
    {
        abilityStates.Clear();

        foreach (var ability in abilities)
        {
            if (ability == null)
            {
                Debug.LogWarning("[AbilitySystem] Null ability in abilities list");
                continue;
            }

            if (string.IsNullOrEmpty(ability.abilityId))
            {
                Debug.LogWarning($"[AbilitySystem] Ability '{ability.abilityName}' has no ID");
                continue;
            }

            abilityStates[ability.abilityId] = new AbilityState(ability);
        }

        if (showDebugInfo)
            Debug.Log($"[AbilitySystem] Registered {abilityStates.Count} abilities");
    }

    private void OnDestroy()
    {
        // Unsubscribe from animation events
        if (eventForwarder != null)
        {
            eventForwarder.OnAnimationEvent -= HandleAnimationEvent;
            eventForwarder.OnStateTransitionEvent -= HandleStateTransition;  // NEW: Animation-driven states
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
        // Validate ability ID (object state, not null)
        if (string.IsNullOrEmpty(ability.abilityId))
        {
            Debug.LogError($"[AbilitySystem] Ability {ability.abilityName} has no ID");
            return;
        }

        // Add to list
        if (!abilities.Contains(ability))
            abilities.Add(ability);

        // Add to state dictionary
        abilityStates[ability.abilityId] = new AbilityState(ability);

        if (showDebugInfo)
            Debug.Log($"[AbilitySystem] Added ability: {ability.abilityName} (ID: {ability.abilityId})");
    }

    /// <summary>
    /// Remove an ability from this entity's ability pool
    /// </summary>
    public void RemoveAbility(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return;

        abilities.Remove(state.definition);
        abilityStates.Remove(abilityId);

        if (showDebugInfo)
            Debug.Log($"[AbilitySystem] Removed ability: {abilityId}");
    }

    /// <summary>
    /// Get an ability definition by ID
    /// </summary>
    public AbilityDefinition GetAbility(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return null;
        return state.definition;
    }

    /// <summary>
    /// Get all registered abilities
    /// </summary>
    public List<AbilityDefinition> GetAllAbilities()
    {
        return new List<AbilityDefinition>(abilities);
    }

    // ========================================
    // Resource Cost Helpers (DRY - eliminates 4x repetition)
    // ========================================

    /// <summary>
    /// Check if entity has sufficient resources for ability
    /// </summary>
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

    /// <summary>
    /// Consume ability resource costs on use
    /// </summary>
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

    /// <summary>
    /// Drain resource costs per frame (for active defensive abilities)
    /// </summary>
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

    /// <summary>
    /// Refund resource costs (on successful parry, etc.)
    /// </summary>
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

    // ========================================
    // Ability Execution (IAbilityProvider)
    // ========================================

    public bool CanUseAbility(string abilityId)
    {
        // Guard clauses (flat logic - fail fast)
        if (!isEnabled) return false;
        if (currentAbility != null) return false;
        if (currentlyCastingAbility != null) return false;
        if (isAnimationLocked) return false;

        if (!abilityStates.TryGetValue(abilityId, out var state)) return false;
        if (state.IsOnCooldown) return false;

        var ability = state.definition;

        // PHASE 2: Check consumable uses (if using RuntimeAbilityManager)
        if (runtimeAbilityManager != null)
        {
            var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
            if (instance != null && !instance.IsUsable())
            {
                if (showDebugInfo)
                    Debug.Log($"[AbilitySystem] Cannot use {ability.abilityName} - instance not usable");
                return false;
            }
        }

        // NOTE: State transitions now handled by animation events (OnStateTransition)
        // The animator controls which states the ability goes through

        // Check blackboard requirements (semantic validation)
        if (!CheckBlackboardRequirements(ability))
        {
            if (showDebugInfo)
                Debug.Log($"[AbilitySystem] Cannot use {ability.abilityName} - blackboard requirements not met");
            return false;
        }

        // Check resource costs (use DRY helper)
        if (!HasSufficientResources(ability))
            return false;

        return true;
    }

    public void UseAbility(string abilityId)
    {
        // Guard clauses - fail fast
        if (!CanUseAbility(abilityId)) return;
        if (!abilityStates.TryGetValue(abilityId, out var state))
        {
            Debug.LogError($"[AbilitySystem] Ability {abilityId} not found");
            return;
        }

        var ability = state.definition;

        // PHASE 2: Notify RuntimeAbilityManager (for consumable tracking)
        if (runtimeAbilityManager != null)
        {
            var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
            if (instance != null)
                runtimeAbilityManager.OnAbilityUsed(instance.instanceId);
        }

        // Consume resources (use DRY helper)
        ConsumeResourceCosts(ability);

        // Start cast or execute immediately
        if (ability.castTime > 0f)
            StartCast(ability);
        else
            ExecuteAbility(ability);

        // Start cooldown
        StartCooldown(abilityId, ability.cooldown);

        // Raise event
        OnAbilityUsed?.Invoke(abilityId);

        if (showDebugInfo)
            Debug.Log($"[AbilitySystem] Used ability: {ability.abilityName} (ID: {abilityId})");
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

    // ========================================
    // Execution (NEW - Phase 1)
    // ========================================

    private void ExecuteAbility(AbilityDefinition ability)
    {
        // NOTE: State transitions now handled by animation events (OnStateTransition)
        // The animator sends events like OnStateTransition("MeleeWindup") at specific frames

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
        // Return to idle state
        // NOTE: Can also be handled by animation events (OnStateTransition("Idle"))
        if (stateMachine != null)
            stateMachine.TryTransitionUpperBody(UpperBodyState.Idle);

        // Track for chaining
        lastCompletedAbility = ability;
        lastAbilityCompleteTime = Time.time;

        // Clear current
        if (currentAbility == ability)
            currentAbility = null;

        // Stop safety timeout
        if (safetyTimeoutCoroutine != null)
        {
            StopCoroutine(safetyTimeoutCoroutine);
            safetyTimeoutCoroutine = null;
        }

        if (showDebugInfo)
            Debug.Log($"[AbilitySystem] Completed ability: {ability.abilityName}");
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

    /// <summary>
    /// Handle state transition events from animations
    /// Allows animator to control state phases (Windup → Swing → Recovery)
    /// </summary>
    private void HandleStateTransition(UpperBodyState newState)
    {
        // Guard clause
        if (stateMachine == null) return;

        // Attempt transition
        if (stateMachine.TryTransitionUpperBody(newState))
        {
            if (showDebugInfo)
                Debug.Log($"[AbilitySystem] State transition: {newState}");
        }
        else
        {
            if (showDebugInfo)
                Debug.LogWarning($"[AbilitySystem] Failed state transition to: {newState}");
        }
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

            // Use policy helper instead of direct field access
            if (!lastCompletedAbility.CanChain(elapsed))
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
        if (!lastCompletedAbility.HasNextInChain) return false;  // Use policy helper
        if (!chainWindowOpen) return false;

        // Use policy helper to check chain timing
        float timeSinceOpen = Time.time - chainWindowOpenTime;
        return lastCompletedAbility.CanChain(timeSinceOpen);
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
        return currentDefensiveAbility.CanParry(timeInDefense);  // Use policy helper
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
        bool withinBlockAngle = IsAttackWithinBlockAngle(attackDirection);
        if (!withinBlockAngle)
            return damage;

        // Use policy helper to get defense multiplier (handles parry vs block logic)
        float timeInDefense = Time.time - defenseStartTime;
        float damageMultiplier = currentDefensiveAbility.GetDefenseMultiplier(timeInDefense, withinBlockAngle);
        float finalDamage = damage * damageMultiplier;

        // Check if this was a parry for events/refunds
        bool isParry = currentDefensiveAbility.CanParry(timeInDefense);

        if (isParry)
        {
            OnPerfectBlock?.Invoke();

            // Refund resources (use DRY helper)
            RefundResourceCosts(currentDefensiveAbility);

            if (showDebugInfo)
                Debug.Log($"[AbilitySystem] PARRY! {damage:F1} → {finalDamage:F1}");
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

        bool withinBlockAngle = IsAttackWithinBlockAngle(attackDirection);
        if (!withinBlockAngle) return 1f;

        // Use policy helper instead of manual reduction calculation
        float timeInDefense = Time.time - defenseStartTime;
        return currentDefensiveAbility.GetDefenseMultiplier(timeInDefense, withinBlockAngle);
    }

    private bool IsAttackWithinBlockAngle(Vector3 attackDirection)
    {
        if (attackDirection == Vector3.zero) return true;
        if (currentDefensiveAbility == null) return false;

        // Use policy helper instead of manual angle calculation
        return currentDefensiveAbility.CanBlock(transform.forward, attackDirection);
    }

    // ========================================
    // Cooldowns
    // ========================================

    private void UpdateCooldownTimers()
    {
        // Update all cooldown timers in ability states
        foreach (var kvp in abilityStates)
        {
            var state = kvp.Value;
            if (state.cooldown == null) continue;

            state.cooldown.Tick(Time.deltaTime);

            // Emit cooldown changed event
            OnAbilityCooldownChanged?.Invoke(kvp.Key, state.cooldown.CurrentTime);

            // Clear finished cooldowns
            if (state.cooldown.IsFinished)
            {
                state.cooldown = null;

                if (showDebugInfo)
                    Debug.Log($"[AbilitySystem] Cooldown expired: {kvp.Key}");
            }
        }
    }

    private void StartCooldown(string abilityId, float duration)
    {
        if (duration <= 0f) return;
        if (!abilityStates.TryGetValue(abilityId, out var state)) return;

        state.cooldown = new CountdownTimer(duration);

        if (showDebugInfo)
            Debug.Log($"[AbilitySystem] Started cooldown: {abilityId} ({duration}s)");
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

    // IAbilityProvider interface methods
    public float GetAbilityCooldown(string abilityId)
    {
        return GetAbilityCooldownRemaining(abilityId);
    }

    public float GetAbilityMaxCooldown(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return 0f;
        return state.definition.cooldown;
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