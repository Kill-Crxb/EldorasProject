using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// RuntimeAbilityManager - Dynamic Ability Lifecycle Manager
/// 
/// Architecture:
/// - Manages AbilityInstance lifecycle (add/remove/expire)
/// - Tracks abilities by source for cleanup
/// - Auto-expires temporary abilities
/// - Integrates with AbilitySystem for execution
/// 
/// Usage:
/// - Equipment: AddAbility(def, Equipment, itemId) → RemoveBySource(Equipment, itemId)
/// - Consumable: AddAbility(def, Consumable, itemId, maxUses: 3)
/// - Temporary: AddAbility(def, Temporary, buffId, duration: 30f)
/// - Permanent: AddAbility(def, Permanent)
/// 
/// Integration:
/// - AbilitySystem queries this for CanUseAbility/UseAbility
/// - AbilityLoadoutModule queries for slot assignment
/// - ItemSystem adds/removes on equip/unequip
/// </summary>
public class RuntimeAbilityManager : MonoBehaviour, IBrainModule
{
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // ========================================
    // References
    // ========================================

    private ControllerBrain brain;
    private AbilitySystem abilitySystem;

    // ========================================
    // Runtime Storage
    // ========================================

    /// <summary>
    /// All active ability instances (lookup by instance ID)
    /// </summary>
    private Dictionary<string, AbilityInstance> instanceLookup = new Dictionary<string, AbilityInstance>();

    /// <summary>
    /// Abilities grouped by source for bulk operations
    /// Key: Source type, Value: List of instance IDs
    /// </summary>
    private Dictionary<AbilitySource, HashSet<string>> sourceTracking = new Dictionary<AbilitySource, HashSet<string>>();

    /// <summary>
    /// Abilities grouped by source ID for targeted cleanup
    /// Key: Source ID (item ID, buff ID, etc.), Value: List of instance IDs
    /// Example: "longbow_item" → ["shoot_arrow_instance", "power_shot_instance"]
    /// </summary>
    private Dictionary<string, HashSet<string>> sourceIdTracking = new Dictionary<string, HashSet<string>>();

    /// <summary>
    /// Quick lookup: Definition ID → Instance IDs
    /// Used to prevent duplicate permanent abilities
    /// </summary>
    private Dictionary<string, HashSet<string>> definitionTracking = new Dictionary<string, HashSet<string>>();

    // ========================================
    // Properties
    // ========================================

    public bool IsEnabled { get; set; } = true;
    public ControllerBrain Brain => brain;

    /// <summary>
    /// Total number of active ability instances
    /// </summary>
    public int InstanceCount => instanceLookup.Count;

    // ========================================
    // Events
    // ========================================

    public event Action<AbilityInstance> OnAbilityAdded;
    public event Action<AbilityInstance> OnAbilityRemoved;
    public event Action<AbilityInstance> OnAbilityExpired;
    public event Action<AbilityInstance> OnAbilityUsesChanged;

    // ========================================
    // IBrainModule Implementation
    // ========================================

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Get AbilitySystem reference
        abilitySystem = brain.GetModule<AbilitySystem>();
        if (abilitySystem == null)
        {
            Debug.LogError("[RuntimeAbilityManager] AbilitySystem not found!");
        }

        // Initialize tracking dictionaries
        foreach (AbilitySource source in Enum.GetValues(typeof(AbilitySource)))
        {
            sourceTracking[source] = new HashSet<string>();
        }

        if (showDebugInfo)
        {
            Debug.Log($"[RuntimeAbilityManager] Initialized on {brain.name}");
        }
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        UpdateExpirations();
    }

    // ========================================
    // Add Abilities
    // ========================================

    /// <summary>
    /// Add an ability instance dynamically
    /// Returns: Instance ID if successful, null if failed
    /// </summary>
    public string AddAbility(
        AbilityDefinition definition,
        AbilitySource source,
        string sourceId = "",
        int maxUses = -1,
        float durationSeconds = -1f)
    {
        // Guard clauses
        if (definition == null)
        {
            Debug.LogWarning("[RuntimeAbilityManager] Cannot add null ability definition");
            return null;
        }

        if (string.IsNullOrEmpty(definition.abilityId))
        {
            Debug.LogWarning($"[RuntimeAbilityManager] Ability {definition.abilityName} has no ID");
            return null;
        }

        // Check for duplicate permanent abilities
        if (source == AbilitySource.Permanent)
        {
            if (HasAbility(definition.abilityId))
            {
                if (showDebugInfo)
                    Debug.Log($"[RuntimeAbilityManager] Already has permanent ability: {definition.abilityName}");
                return null;
            }
        }

        // Create instance
        var instance = new AbilityInstance(
            definition,
            source,
            sourceId,
            maxUses,
            durationSeconds
        );

        // Store instance
        instanceLookup[instance.instanceId] = instance;

        // Track by source
        sourceTracking[source].Add(instance.instanceId);

        // Track by source ID (if provided)
        if (!string.IsNullOrEmpty(sourceId))
        {
            if (!sourceIdTracking.ContainsKey(sourceId))
            {
                sourceIdTracking[sourceId] = new HashSet<string>();
            }
            sourceIdTracking[sourceId].Add(instance.instanceId);
        }

        // Track by definition ID
        if (!definitionTracking.ContainsKey(definition.abilityId))
        {
            definitionTracking[definition.abilityId] = new HashSet<string>();
        }
        definitionTracking[definition.abilityId].Add(instance.instanceId);

        // Register with AbilitySystem
        if (abilitySystem != null)
        {
            abilitySystem.AddAbility(definition);
        }

        // Raise event
        OnAbilityAdded?.Invoke(instance);

        if (showDebugInfo)
        {
            Debug.Log($"[RuntimeAbilityManager] Added: {instance}");
        }

        return instance.instanceId;
    }

    // ========================================
    // Remove Abilities
    // ========================================

    /// <summary>
    /// Remove a specific ability instance
    /// </summary>
    public bool RemoveAbility(string instanceId)
    {
        // Guard clause
        if (!instanceLookup.TryGetValue(instanceId, out AbilityInstance instance))
        {
            if (showDebugInfo)
                Debug.LogWarning($"[RuntimeAbilityManager] Instance not found: {instanceId}");
            return false;
        }

        // Remove from lookups
        RemoveFromTracking(instance);

        // Unregister from AbilitySystem (if no other instances of this definition)
        if (definitionTracking.TryGetValue(instance.definition.abilityId, out var instances))
        {
            if (instances.Count == 0 && abilitySystem != null)
            {
                abilitySystem.RemoveAbility(instance.definition.abilityId);
            }
        }

        // Raise event
        OnAbilityRemoved?.Invoke(instance);

        if (showDebugInfo)
        {
            Debug.Log($"[RuntimeAbilityManager] Removed: {instance}");
        }

        return true;
    }

    /// <summary>
    /// Remove all abilities from a specific source
    /// Example: RemoveBySource(Equipment, "longbow_item") removes all bow abilities
    /// </summary>
    public int RemoveBySource(AbilitySource source, string sourceId)
    {
        // Guard clause
        if (string.IsNullOrEmpty(sourceId))
        {
            Debug.LogWarning("[RuntimeAbilityManager] Cannot remove by source with empty sourceId");
            return 0;
        }

        // Find instances with this source ID
        if (!sourceIdTracking.TryGetValue(sourceId, out var instanceIds))
        {
            if (showDebugInfo)
                Debug.Log($"[RuntimeAbilityManager] No abilities found for source: {sourceId}");
            return 0;
        }

        // Remove all instances (copy to list to avoid modification during iteration)
        var toRemove = new List<string>(instanceIds);
        int removedCount = 0;

        foreach (var instanceId in toRemove)
        {
            if (RemoveAbility(instanceId))
            {
                removedCount++;
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"[RuntimeAbilityManager] Removed {removedCount} abilities from source: {sourceId}");
        }

        return removedCount;
    }

    /// <summary>
    /// Remove all abilities of a specific source type
    /// Example: RemoveBySourceType(Temporary) removes all temporary buffs
    /// </summary>
    public int RemoveBySourceType(AbilitySource source)
    {
        if (!sourceTracking.TryGetValue(source, out var instanceIds))
        {
            return 0;
        }

        var toRemove = new List<string>(instanceIds);
        int removedCount = 0;

        foreach (var instanceId in toRemove)
        {
            if (RemoveAbility(instanceId))
            {
                removedCount++;
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"[RuntimeAbilityManager] Removed {removedCount} abilities of type: {source}");
        }

        return removedCount;
    }

    private void RemoveFromTracking(AbilityInstance instance)
    {
        // Remove from instance lookup
        instanceLookup.Remove(instance.instanceId);

        // Remove from source tracking
        sourceTracking[instance.source].Remove(instance.instanceId);

        // Remove from source ID tracking
        if (!string.IsNullOrEmpty(instance.sourceId) &&
            sourceIdTracking.TryGetValue(instance.sourceId, out var sourceIdSet))
        {
            sourceIdSet.Remove(instance.instanceId);

            // Clean up empty sets
            if (sourceIdSet.Count == 0)
            {
                sourceIdTracking.Remove(instance.sourceId);
            }
        }

        // Remove from definition tracking
        if (definitionTracking.TryGetValue(instance.definition.abilityId, out var defSet))
        {
            defSet.Remove(instance.instanceId);

            // Clean up empty sets
            if (defSet.Count == 0)
            {
                definitionTracking.Remove(instance.definition.abilityId);
            }
        }
    }

    // ========================================
    // Expiration & Usage
    // ========================================

    private void UpdateExpirations()
    {
        // Check for expired abilities
        List<string> toRemove = null;

        foreach (var kvp in instanceLookup)
        {
            var instance = kvp.Value;

            if (instance.ShouldRemove())
            {
                if (toRemove == null)
                    toRemove = new List<string>();

                toRemove.Add(kvp.Key);
            }
        }

        // Remove expired
        if (toRemove != null)
        {
            foreach (var instanceId in toRemove)
            {
                if (instanceLookup.TryGetValue(instanceId, out var instance))
                {
                    OnAbilityExpired?.Invoke(instance);
                    RemoveAbility(instanceId);
                }
            }
        }
    }

    /// <summary>
    /// Called when an ability is used (for consumable tracking)
    /// </summary>
    public void OnAbilityUsed(string instanceId)
    {
        if (!instanceLookup.TryGetValue(instanceId, out AbilityInstance instance))
            return;

        // Consume use for consumables
        if (instance.IsConsumable())
        {
            if (instance.ConsumeUse())
            {
                OnAbilityUsesChanged?.Invoke(instance);

                if (showDebugInfo)
                {
                    Debug.Log($"[RuntimeAbilityManager] Used consumable: {instance.definition.abilityName} ({instance.remainingUses}/{instance.maxUses} remaining)");
                }
            }
        }
    }

    // ========================================
    // Query Methods
    // ========================================

    /// <summary>
    /// Get ability instance by instance ID
    /// </summary>
    public AbilityInstance GetInstance(string instanceId)
    {
        instanceLookup.TryGetValue(instanceId, out AbilityInstance instance);
        return instance;
    }

    /// <summary>
    /// Get first instance of an ability definition
    /// Useful for checking if ability exists
    /// </summary>
    public AbilityInstance GetInstanceByDefinition(string abilityId)
    {
        if (!definitionTracking.TryGetValue(abilityId, out var instanceIds))
            return null;

        if (instanceIds.Count == 0)
            return null;

        string firstInstanceId = instanceIds.First();
        return GetInstance(firstInstanceId);
    }

    /// <summary>
    /// Get all instances of a specific ability definition
    /// </summary>
    public List<AbilityInstance> GetInstancesByDefinition(string abilityId)
    {
        var result = new List<AbilityInstance>();

        if (!definitionTracking.TryGetValue(abilityId, out var instanceIds))
            return result;

        foreach (var instanceId in instanceIds)
        {
            if (instanceLookup.TryGetValue(instanceId, out var instance))
            {
                result.Add(instance);
            }
        }

        return result;
    }

    /// <summary>
    /// Get all instances from a specific source
    /// </summary>
    public List<AbilityInstance> GetInstancesBySource(AbilitySource source)
    {
        var result = new List<AbilityInstance>();

        if (!sourceTracking.TryGetValue(source, out var instanceIds))
            return result;

        foreach (var instanceId in instanceIds)
        {
            if (instanceLookup.TryGetValue(instanceId, out var instance))
            {
                result.Add(instance);
            }
        }

        return result;
    }

    /// <summary>
    /// Get all instances from a specific source ID
    /// </summary>
    public List<AbilityInstance> GetInstancesBySourceId(string sourceId)
    {
        var result = new List<AbilityInstance>();

        if (!sourceIdTracking.TryGetValue(sourceId, out var instanceIds))
            return result;

        foreach (var instanceId in instanceIds)
        {
            if (instanceLookup.TryGetValue(instanceId, out var instance))
            {
                result.Add(instance);
            }
        }

        return result;
    }

    /// <summary>
    /// Get all active ability instances
    /// </summary>
    public List<AbilityInstance> GetAllInstances()
    {
        return new List<AbilityInstance>(instanceLookup.Values);
    }

    /// <summary>
    /// Check if entity has an ability (any source)
    /// </summary>
    public bool HasAbility(string abilityId)
    {
        return definitionTracking.ContainsKey(abilityId) &&
               definitionTracking[abilityId].Count > 0;
    }

    /// <summary>
    /// Check if entity has an ability from a specific source
    /// </summary>
    public bool HasAbilityFromSource(string abilityId, AbilitySource source)
    {
        if (!definitionTracking.TryGetValue(abilityId, out var instanceIds))
            return false;

        foreach (var instanceId in instanceIds)
        {
            if (instanceLookup.TryGetValue(instanceId, out var instance))
            {
                if (instance.source == source)
                    return true;
            }
        }

        return false;
    }

    // ========================================
    // Debug
    // ========================================

    [ContextMenu("Debug/Print All Instances")]
    public void DebugPrintAllInstances()
    {
        Debug.Log($"=== Runtime Ability Manager ({brain?.name}) ===");
        Debug.Log($"Total Instances: {instanceLookup.Count}");

        foreach (AbilitySource source in Enum.GetValues(typeof(AbilitySource)))
        {
            var instances = GetInstancesBySource(source);
            if (instances.Count > 0)
            {
                Debug.Log($"\n{source} ({instances.Count}):");
                foreach (var instance in instances)
                {
                    Debug.Log($"  {instance}");
                }
            }
        }
    }

    [ContextMenu("Debug/Clear All Temporary")]
    public void DebugClearAllTemporary()
    {
        int removed = RemoveBySourceType(AbilitySource.Temporary);
        Debug.Log($"Removed {removed} temporary abilities");
    }
}