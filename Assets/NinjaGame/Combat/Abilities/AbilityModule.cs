using UnityEngine;
using System;
using System.Collections.Generic;
using NinjaGame.Animation;

public class AbilityModule : MonoBehaviour, IBrainModule, IAbilityProvider
{
    [Header("Abilities")]
    [SerializeField] private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

    [Header("Debug")]
    [SerializeField] private bool debugAbilities = false;

    private ControllerBrain brain;
    private IAnimationProvider animation;
    private IResourceProvider resources;
    private IHealthProvider healthProvider;
    private IMovementProvider movementProvider;
    private DamageModule damageModule;
    private AnimationEventForwarder eventForwarder;

    private Dictionary<string, CountdownTimer> cooldownTimers = new Dictionary<string, CountdownTimer>();
    private Dictionary<string, AbilityDefinition> abilityLookup = new Dictionary<string, AbilityDefinition>();
    private string currentlyCastingAbility = null;
    private float castStartTime;

    // Animation event state tracking
    private bool isAnimationLocked = false;
    private bool isMovementLocked = false;
    private bool isInvincible = false;
    private HashSet<Collider> activeHitboxes = new HashSet<Collider>();

    public bool IsEnabled { get; set; } = true;

    public event Action<string> OnAbilityUsed;
    public event Action<string, float> OnAbilityCooldownChanged;
    public event Action<string> OnAbilityCastStart;
    public event Action<string> OnAbilityCastComplete;
    public event Action<AnimationEventType> OnAbilityAnimationEvent;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        animation = brain.GetProvider<IAnimationProvider>();
        resources = brain.GetProvider<IResourceProvider>();
        healthProvider = brain.GetProvider<IHealthProvider>();
        movementProvider = brain.GetProvider<IMovementProvider>();
        damageModule = brain.GetModule<DamageModule>();

        // Find the AnimationEventForwarder
        // Search the entire GameObject tree from player root
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
            Debug.LogWarning($"[AbilityModule] No AnimationEventForwarder found on {gameObject.name}");
        }
        else
        {
            // Subscribe to animation events
            eventForwarder.OnAnimationEvent += HandleAnimationEvent;
        }

        BuildAbilityLookup();

        if (resources == null)
            Debug.LogWarning($"[AbilityModule] No IResourceProvider found on {gameObject.name}");
    }

    private void OnDestroy()
    {
        // Unsubscribe from animation events
        if (eventForwarder != null)
        {
            eventForwarder.OnAnimationEvent -= HandleAnimationEvent;
        }
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        UpdateCooldownTimers();
        UpdateCasting();
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

        if (debugAbilities)
        {
            Debug.Log($"[AbilityModule] Registered {abilityLookup.Count} abilities");
        }
    }

    private void UpdateCooldownTimers()
    {
        float deltaTime = Time.deltaTime;
        foreach (var timer in cooldownTimers.Values)
        {
            timer.Tick(deltaTime);
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
        }
    }

    /// <summary>
    /// Handles animation events triggered during ability execution
    /// </summary>
    private void HandleAnimationEvent(AnimationEventType eventType)
    {
        if (debugAbilities)
        {
            Debug.Log($"[AbilityModule] Animation Event: {eventType}");
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
                if (movementProvider != null)
                {
                    // This could be expanded to actually lock movement in the movement provider
                }
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
        // This will be expanded when we integrate with the hitbox system
        // For now, just log
        if (debugAbilities)
        {
            Debug.Log($"[AbilityModule] Enabling hitboxes for ability");
        }
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

        if (debugAbilities)
        {
            Debug.Log($"[AbilityModule] Disabled all hitboxes");
        }
    }

    public bool CanUseAbility(string abilityId)
    {
        if (!IsEnabled) return false;
        if (currentlyCastingAbility != null) return false;
        if (isAnimationLocked) return false; // Can't use abilities while animation locked
        if (!abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability)) return false;
        if (IsAbilityOnCooldown(abilityId)) return false;

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
            Debug.LogWarning($"[AbilityModule] Ability {abilityId} not found");
            return;
        }

        if (resources != null && ability.resourceCost > 0)
        {
            if (!resources.ConsumeResource(ability.resourceType, ability.resourceCost))
                return;
        }

        currentlyCastingAbility = abilityId;
        castStartTime = Time.time;

        if (animation != null && !string.IsNullOrEmpty(ability.animationTrigger))
        {
            animation.TriggerCombatAnimation(ability.animationTrigger);
        }

        OnAbilityCastStart?.Invoke(abilityId);

        if (debugAbilities)
        {
            Debug.Log($"[AbilityModule] {gameObject.name} started casting {ability.abilityName}");
        }
    }

    private void ExecuteAbility(AbilityDefinition ability)
    {
        bool isMovementAbility = ability.movementEffects != null && ability.movementEffects.Count > 0;

        if (isMovementAbility && movementProvider != null)
        {
            ability.ExecuteMovement(movementProvider);

            if (debugAbilities)
                Debug.Log($"[AbilityModule] Executed movement ability: {ability.abilityName}");
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
                    ability.Execute(damageable, damageModule, healthProvider, transform);
                }
            }

            if (debugAbilities)
                Debug.Log($"[AbilityModule] Executed combat ability: {ability.abilityName}");
        }

        // Create and start cooldown timer
        if (cooldownTimers.ContainsKey(ability.abilityId))
        {
            cooldownTimers[ability.abilityId].Reset(ability.cooldown);
            cooldownTimers[ability.abilityId].Start();
        }
        else
        {
            var cooldownTimer = new CountdownTimer(ability.cooldown);
            cooldownTimer.OnTimerFinished += () => OnCooldownReady(ability.abilityId);
            cooldownTimer.Start();
            cooldownTimers[ability.abilityId] = cooldownTimer;
        }

        OnAbilityUsed?.Invoke(ability.abilityId);
        OnAbilityCastComplete?.Invoke(ability.abilityId);
        OnAbilityCooldownChanged?.Invoke(ability.abilityId, ability.cooldown);

        if (debugAbilities)
        {
            Debug.Log($"[AbilityModule] {gameObject.name} executed {ability.abilityName}");
        }
    }

    private void OnCooldownReady(string abilityId)
    {
        OnAbilityCooldownChanged?.Invoke(abilityId, 0f);
        if (debugAbilities) Debug.Log($"[AbilityModule] {abilityId} cooldown ready");
    }

    public float GetAbilityCooldown(string abilityId)
    {
        return cooldownTimers.TryGetValue(abilityId, out CountdownTimer timer)
            ? timer.CurrentTime
            : 0f;
    }

    public float GetAbilityMaxCooldown(string abilityId)
    {
        return abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability) ? ability.cooldown : 0f;
    }

    public bool IsAbilityOnCooldown(string abilityId)
    {
        return cooldownTimers.TryGetValue(abilityId, out CountdownTimer timer)
            && !timer.IsFinished;
    }

    public AbilityDefinition GetAbility(string abilityId)
    {
        return abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability)
            ? ability
            : null;
    }

    public void AddAbility(AbilityDefinition ability)
    {
        if (ability == null || string.IsNullOrEmpty(ability.abilityId)) return;

        if (!abilities.Contains(ability))
        {
            abilities.Add(ability);
            abilityLookup[ability.abilityId] = ability;

            if (debugAbilities)
            {
                Debug.Log($"[AbilityModule] Added ability: {ability.abilityName}");
            }
        }
    }

    public void RemoveAbility(string abilityId)
    {
        if (abilityLookup.TryGetValue(abilityId, out AbilityDefinition ability))
        {
            abilities.Remove(ability);
            abilityLookup.Remove(abilityId);
            cooldownTimers.Remove(abilityId);

            if (debugAbilities)
            {
                Debug.Log($"[AbilityModule] Removed ability: {ability.abilityName}");
            }
        }
    }

    public List<AbilityDefinition> GetAllAbilities()
    {
        return new List<AbilityDefinition>(abilities);
    }

    // Public accessors for animation state (useful for other systems)
    public bool IsAnimationLocked => isAnimationLocked;
    public bool IsMovementLocked => isMovementLocked;
    public bool IsInvincible => isInvincible;
}