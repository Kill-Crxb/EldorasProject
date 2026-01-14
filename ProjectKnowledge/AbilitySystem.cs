using UnityEngine;
using System;
using System.Collections.Generic;
using NinjaGame.Animation;

/// <summary>
/// Universal Ability System - Works for all entities
/// 
/// Architecture:
/// - ONE AbilityLoadoutModule (manages slots and combos)
/// - ONE active ControlSource (defines who controls - switchable at runtime)
/// - Universal execution (cooldowns, resources, effects - same for all)
/// 
/// This system enables:
/// - Player and NPC using same ability code
/// - Runtime control switching (possession, pets, cutscenes)
/// - GOAP and AI using same ability execution
/// - Zero code duplication
/// 
/// Core Responsibilities:
/// - Ability registration and lookup
/// - Cooldown management
/// - Resource cost validation
/// - Cast time handling
/// - Animation event processing
/// - Effect execution
/// - Hitbox management
/// </summary>
public class AbilitySystem : MonoBehaviour, IBrainModule, IAbilityProvider
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

    // Casting state
    private string currentlyCastingAbility = null;
    private float castStartTime;

    // Animation event state tracking
    private bool isAnimationLocked = false;
    private bool isMovementLocked = false;
    private bool isInvincible = false;
    private HashSet<Collider> activeHitboxes = new HashSet<Collider>();

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
    /// Used by GOAP to know when abilities are in progress.
    /// </summary>
    public bool IsExecuting => currentlyCastingAbility != null || isAnimationLocked;

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

    // ========================================
    // IBrainModule Implementation
    // ========================================

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Get core system references from brain (assigned in Inspector)
        animationProvider = brain.Animation;
        movementSystem = brain.Movement;

        // Get provider interfaces (may be implemented by various systems)
        resources = brain.GetProvider<IResourceProvider>();
        healthProvider = brain.GetProvider<IHealthProvider>();

        // Get optional modules
        damageSystem = brain.GetModule<DamageSystem>();

        // Validate loadout module (should be assigned in Inspector)
        if (loadoutModule == null)
        {
            Debug.LogError("[AbilitySystem] LoadoutModule not assigned! Assign AbilityLoadoutModule in Inspector.");
        }

        // Find the AnimationEventForwarder
        SetupAnimationEventForwarder();

        // Build ability lookup table
        BuildAbilityLookup();

        // Warn if missing critical dependencies
        if (resources == null)
            Debug.LogWarning($"[AbilitySystem] No IResourceProvider found - resource costs won't work");

        if (animationProvider == null)
            Debug.LogError($"[AbilitySystem] AnimationSystem not assigned in ControllerBrain!");

        if (movementSystem == null)
            Debug.LogError($"[AbilitySystem] MovementSystem not assigned in ControllerBrain!");

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Initialized on {brain.name}");
            Debug.Log($"  Animation: {(animationProvider != null ? "✓" : "✗ MISSING")}");
            Debug.Log($"  Movement: {(movementSystem != null ? "✓" : "✗ MISSING")}");
            Debug.Log($"  Loadout Module: {(loadoutModule != null ? "✓" : "✗ MISSING")}");
            Debug.Log($"  Registered Abilities: {abilityLookup.Count}");
            Debug.Log($"  Animation Events: {(eventForwarder != null ? "✓" : "✗ MISSING")}");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        UpdateCooldownTimers();
        UpdateCasting();
    }

    // ========================================
    // Setup
    // ========================================

    private void SetupAnimationEventForwarder()
    {
        // Find the AnimationEventForwarder
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
    /// Called by NPCConfigurationHandler during NPC setup
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
    /// Used by NPCConfigurationHandler to clear abilities before reconfiguring
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
        if (!isEnabled) return false;
        if (currentlyCastingAbility != null) return false;
        if (isAnimationLocked) return false;
        if (!abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability)) return false;
        if (IsAbilityOnCooldown(abilityId)) return false;

        // Check resource cost
        if (resources != null && ability.resourceCost > 0)
        {
            if (!resources.HasResource(ability.resourceType, ability.resourceCost))
                return false;
        }

        return true;
    }

    public void UseAbility(string abilityId)
    {
        if (!CanUseAbility(abilityId)) return;

        if (!abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability))
        {
            Debug.LogWarning($"[AbilitySystem] Ability {abilityId} not found");
            return;
        }

        // Consume resources
        if (resources != null && ability.resourceCost > 0)
        {
            resources.ConsumeResource(ability.resourceType, ability.resourceCost);
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
    /// Cancel the currently casting ability
    /// Used by GOAP to interrupt abilities when goals change
    /// </summary>
    public void CancelCurrentAbility()
    {
        if (currentlyCastingAbility != null)
        {
            if (showDebugInfo)
            {
                Debug.Log($"[AbilitySystem] Cancelled casting: {currentlyCastingAbility}");
            }

            currentlyCastingAbility = null;
        }

        // Reset animation locks (let animation finish naturally but allow state changes)
        isAnimationLocked = false;
        isMovementLocked = false;
    }

    // ========================================
    // Casting
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

    private void ExecuteAbility(AbilityDefinition ability)
    {
        // Play animation
        if (animationProvider != null && !string.IsNullOrEmpty(ability.animationTrigger))
        {
            animationProvider.TriggerCombatAnimation(ability.animationTrigger);
        }

        // Apply effects
        ApplyAbilityEffects(ability);

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Executed ability: {ability.abilityName}");
        }
    }

    // ========================================
    // Effects
    // ========================================

    private void ApplyAbilityEffects(AbilityDefinition ability)
    {
        // Check if this is a movement ability
        bool isMovementAbility = ability.movementEffects != null && ability.movementEffects.Count > 0;

        if (isMovementAbility && movementSystem != null)
        {
            // Execute movement ability
            ability.ExecuteMovement(movementSystem);

            if (showDebugInfo)
                Debug.Log($"[AbilitySystem] Executed movement ability: {ability.abilityName}");
        }
        else
        {
            // Combat ability - effects will be applied when hitbox triggers
            // Or for self-targeted abilities, apply now
            if (ability.targetType == AbilityTargetType.Self)
            {
                var damageable = GetComponent<IDamageable>();
                if (damageable != null)
                {
                    ability.Execute(damageable, damageSystem, healthProvider, transform);
                }
            }

            if (showDebugInfo)
                Debug.Log($"[AbilitySystem] Executed combat ability: {ability.abilityName}");
        }
    }

    // ========================================
    // Cooldowns
    // ========================================

    private void StartCooldown(string abilityId, float duration)
    {
        if (duration <= 0f) return;

        if (!cooldownTimers.ContainsKey(abilityId))
        {
            cooldownTimers[abilityId] = new CountdownTimer(duration);
        }

        cooldownTimers[abilityId].Reset(duration);
        cooldownTimers[abilityId].Start();

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Started cooldown for {abilityId}: {duration}s");
        }
    }

    private void UpdateCooldownTimers()
    {
        float deltaTime = Time.deltaTime;
        foreach (var kvp in cooldownTimers)
        {
            kvp.Value.Tick(deltaTime);

            if (kvp.Value.IsRunning)
            {
                OnAbilityCooldownChanged?.Invoke(kvp.Key, kvp.Value.CurrentTime);
            }
        }
    }

    public bool IsAbilityOnCooldown(string abilityId)
    {
        if (!cooldownTimers.TryGetValue(abilityId, out CountdownTimer timer))
            return false;

        return timer.IsRunning;
    }

    public float GetAbilityCooldownRemaining(string abilityId)
    {
        if (!cooldownTimers.TryGetValue(abilityId, out CountdownTimer timer))
            return 0f;

        return timer.CurrentTime;
    }

    /// <summary>
    /// Get remaining cooldown time (IAbilityProvider interface)
    /// </summary>
    public float GetAbilityCooldown(string abilityId)
    {
        return GetAbilityCooldownRemaining(abilityId);
    }

    /// <summary>
    /// Get max cooldown duration from ability definition (IAbilityProvider interface)
    /// </summary>
    public float GetAbilityMaxCooldown(string abilityId)
    {
        if (!abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability))
            return 0f;

        return ability.cooldown;
    }

    // ========================================
    // Animation Events
    // ========================================

    /// <summary>
    /// Handles animation events triggered during ability execution
    /// </summary>
    private void HandleAnimationEvent(AnimationEventType eventType)
    {
        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Animation Event: {eventType}");
        }

        OnAbilityAnimationEvent?.Invoke(eventType);

        // Handle common events that affect ability state
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

            case AnimationEventType.AnimUnlocked:
                isAnimationLocked = false;
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

            case AnimationEventType.Effect1:
            case AnimationEventType.Effect2:
            case AnimationEventType.Effect3:
                // These can be handled by specific abilities via the OnAbilityAnimationEvent event
                break;
        }
    }

    private void EnableAbilityHitboxes()
    {
        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Enabling hitboxes for ability");
        }

        // Hitbox enabling is handled by specific hitbox components
        // This event is here for custom ability logic if needed
    }

    private void DisableAbilityHitboxes()
    {
        // Disable all active hitboxes
        foreach (var hitbox in activeHitboxes)
        {
            if (hitbox != null)
            {
                hitbox.enabled = false;
            }
        }
        activeHitboxes.Clear();

        if (showDebugInfo)
        {
            Debug.Log($"[AbilitySystem] Disabled all hitboxes");
        }
    }

    // ========================================
    // Debug Visualization
    // ========================================

    private void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 200, 300, 200));
        GUILayout.Label("=== ABILITY SYSTEM ===");
        GUILayout.Label($"Enabled: {isEnabled}");
        GUILayout.Label($"Registered: {abilityLookup.Count} abilities");
        GUILayout.Label($"Casting: {currentlyCastingAbility ?? "None"}");
        GUILayout.Label($"Anim Locked: {isAnimationLocked}");
        GUILayout.Label($"Move Locked: {isMovementLocked}");
        GUILayout.Label($"Invincible: {isInvincible}");

        // Show active cooldowns
        GUILayout.Label("Cooldowns:");
        foreach (var kvp in cooldownTimers)
        {
            if (kvp.Value.IsRunning)
            {
                GUILayout.Label($"  {kvp.Key}: {kvp.Value.CurrentTime:F1}s");
            }
        }

        GUILayout.EndArea();
    }
}