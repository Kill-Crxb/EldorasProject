using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Context object passed to all BT nodes during evaluation
/// Provides access to blackboard data and entity providers
/// Design: Single source of truth for all node data needs
/// </summary>
public class BTContext
{
    // ========================================
    // Core References
    // ========================================

    public ControllerBrain Brain { get; private set; }
    public Transform Transform => Brain?.transform;
    public GameObject GameObject => Brain?.gameObject;

    // ========================================
    // Blackboard - Shared Data Storage
    // ========================================

    private Dictionary<string, object> blackboard = new Dictionary<string, object>();

    // ========================================
    // Cached Providers (Performance)
    // ========================================

    private ICombatStatsProvider combatStats;
    private IHealthProvider health;
    private IResourceProvider resources;
    private IAbilityProvider ability;
    private IDefenseProvider defense;
    private IMovementProvider movement;
    private IAnimationProvider animation;

    // AI-specific
    private AIModule aiModule;
    private NPCMovementModule npcMovement;

    // ========================================
    // Initialization
    // ========================================

    public BTContext(ControllerBrain brain)
    {
        Brain = brain;
        CacheProviders();
    }

    private void CacheProviders()
    {
        if (Brain == null)
        {
            Debug.LogError("[BTContext] Brain is null! Cannot cache providers.");
            return;
        }

        // Cache all providers for fast access
        combatStats = Brain.GetModuleImplementing<ICombatStatsProvider>();
        health = Brain.GetModuleImplementing<IHealthProvider>();
        resources = Brain.GetModuleImplementing<IResourceProvider>();
        ability = Brain.GetModuleImplementing<IAbilityProvider>();
        defense = Brain.GetModuleImplementing<IDefenseProvider>();
        movement = Brain.GetModuleImplementing<IMovementProvider>();
        animation = Brain.GetModuleImplementing<IAnimationProvider>();

        // AI-specific modules
        aiModule = Brain.GetModule<AIModule>();
        npcMovement = Brain.GetModule<NPCMovementModule>();
    }

    // ========================================
    // Blackboard API - Generic Storage
    // ========================================

    public void SetValue(string key, object value)
    {
        blackboard[key] = value;
    }

    public T GetValue<T>(string key, T defaultValue = default)
    {
        if (blackboard.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    public bool HasValue(string key)
    {
        return blackboard.ContainsKey(key);
    }

    public void RemoveValue(string key)
    {
        blackboard.Remove(key);
    }

    public void ClearBlackboard()
    {
        blackboard.Clear();
    }

    // ========================================
    // Provider Accessors - Fast Access
    // ========================================

    public ICombatStatsProvider CombatStats => combatStats;
    public IHealthProvider Health => health;
    public IResourceProvider Resources => resources;
    public IAbilityProvider Ability => ability;
    public IDefenseProvider Defense => defense;
    public IMovementProvider Movement => movement;
    public IAnimationProvider Animation => animation;

    public AIModule AI => aiModule;
    public NPCMovementModule NPCMovement => npcMovement;

    // ========================================
    // Common Convenience Methods
    // ========================================

    /// <summary>
    /// Get current target from AI module
    /// </summary>
    public Transform GetTarget()
    {
        return GetValue<Transform>("target") ?? aiModule?.CurrentTarget;
    }

    /// <summary>
    /// Set current target
    /// </summary>
    public void SetTarget(Transform target)
    {
        SetValue("target", target);
    }

    /// <summary>
    /// Get distance to target
    /// </summary>
    public float GetDistanceToTarget()
    {
        Transform target = GetTarget();
        if (target == null || Transform == null) return float.MaxValue;
        return Vector3.Distance(Transform.position, target.position);
    }

    /// <summary>
    /// Check if entity is alive (health > 0)
    /// </summary>
    public bool IsAlive()
    {
        return health != null && health.GetCurrentHealth() > 0f;
    }

    /// <summary>
    /// Check if entity is in combat
    /// </summary>
    public bool IsInCombat()
    {
        return aiModule != null && aiModule.CurrentState == AIState.Combat;
    }

    /// <summary>
    /// Get health percentage (0-1)
    /// </summary>
    public float GetHealthPercentage()
    {
        if (health == null) return 1f;
        return health.GetHealthPercentage();
    }

    /// <summary>
    /// Check if ability is ready to cast (not on cooldown and can be used)
    /// </summary>
    public bool IsAbilityReady(string abilityId)
    {
        if (ability == null) return false;
        return ability.CanUseAbility(abilityId);
    }

    /// <summary>
    /// Log debug message with context
    /// </summary>
    public void Log(string message)
    {
        Debug.Log($"[BTContext:{GameObject?.name}] {message}");
    }

    /// <summary>
    /// Log warning with context
    /// </summary>
    public void LogWarning(string message)
    {
        Debug.LogWarning($"[BTContext:{GameObject?.name}] {message}");
    }
}