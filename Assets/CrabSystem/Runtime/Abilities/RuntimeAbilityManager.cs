using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class RuntimeAbilityManager : MonoBehaviour, IBrainModule
{
    [SerializeField] private List<AbilityDefinition> starterAbilities;

    private ControllerBrain brain;
    private AbilitySystem abilitySystem;

    private Dictionary<string, AbilityInstance> instanceLookup = new Dictionary<string, AbilityInstance>();
    private Dictionary<AbilitySource, HashSet<string>> sourceTracking = new Dictionary<AbilitySource, HashSet<string>>();
    private Dictionary<string, HashSet<string>> sourceIdTracking = new Dictionary<string, HashSet<string>>();
    private Dictionary<string, HashSet<string>> definitionTracking = new Dictionary<string, HashSet<string>>();

    public bool IsEnabled { get; set; } = true;
    public ControllerBrain Brain => brain;
    public int InstanceCount => instanceLookup.Count;

    public event Action<AbilityInstance> OnAbilityAdded;
    public event Action<AbilityInstance> OnAbilityRemoved;
    public event Action<AbilityInstance> OnAbilityExpired;
    public event Action<AbilityInstance> OnAbilityUsesChanged;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        abilitySystem = brain.GetModule<AbilitySystem>();
        if (abilitySystem == null)
            Debug.LogError("[RuntimeAbilityManager] AbilitySystem not found!");

        foreach (AbilitySource source in Enum.GetValues(typeof(AbilitySource)))
            sourceTracking[source] = new HashSet<string>();

        foreach (var ability in starterAbilities)
            if (ability != null)
                AddAbility(ability, AbilitySource.Permanent);
    }

    public void LateInitialize()
    {
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        UpdateExpirations();
    }

    public string AddAbility(
        AbilityDefinition definition,
        AbilitySource source,
        string sourceId = "",
        int maxUses = -1,
        float durationSeconds = -1f)
    {
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

        if (source == AbilitySource.Permanent)
        {
            if (HasAbility(definition.abilityId))
                return null;
        }

        var instance = new AbilityInstance(
            definition,
            source,
            sourceId,
            maxUses,
            durationSeconds
        );

        instanceLookup[instance.instanceId] = instance;
        sourceTracking[source].Add(instance.instanceId);

        if (!string.IsNullOrEmpty(sourceId))
        {
            if (!sourceIdTracking.ContainsKey(sourceId))
                sourceIdTracking[sourceId] = new HashSet<string>();
            sourceIdTracking[sourceId].Add(instance.instanceId);
        }

        if (!definitionTracking.ContainsKey(definition.abilityId))
            definitionTracking[definition.abilityId] = new HashSet<string>();
        definitionTracking[definition.abilityId].Add(instance.instanceId);

        if (abilitySystem != null)
            abilitySystem.AddAbility(definition);

        OnAbilityAdded?.Invoke(instance);

        return instance.instanceId;
    }

    public bool RemoveAbility(string instanceId)
    {
        if (!instanceLookup.TryGetValue(instanceId, out AbilityInstance instance))
            return false;

        RemoveFromTracking(instance);

        if (definitionTracking.TryGetValue(instance.definition.abilityId, out var instances))
        {
            if (instances.Count == 0 && abilitySystem != null)
                abilitySystem.RemoveAbility(instance.definition.abilityId);
        }

        OnAbilityRemoved?.Invoke(instance);

        return true;
    }

    public int RemoveBySource(AbilitySource source, string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            Debug.LogWarning("[RuntimeAbilityManager] Cannot remove by source with empty sourceId");
            return 0;
        }

        if (!sourceIdTracking.TryGetValue(sourceId, out var instanceIds))
            return 0;

        var toRemove = new List<string>(instanceIds);
        int removedCount = 0;

        foreach (var instanceId in toRemove)
        {
            if (RemoveAbility(instanceId))
                removedCount++;
        }

        return removedCount;
    }

    public int RemoveBySourceType(AbilitySource source)
    {
        if (!sourceTracking.TryGetValue(source, out var instanceIds))
            return 0;

        var toRemove = new List<string>(instanceIds);
        int removedCount = 0;

        foreach (var instanceId in toRemove)
        {
            if (RemoveAbility(instanceId))
                removedCount++;
        }

        return removedCount;
    }

    private void RemoveFromTracking(AbilityInstance instance)
    {
        instanceLookup.Remove(instance.instanceId);
        sourceTracking[instance.source].Remove(instance.instanceId);

        if (!string.IsNullOrEmpty(instance.sourceId) &&
            sourceIdTracking.TryGetValue(instance.sourceId, out var sourceIdSet))
        {
            sourceIdSet.Remove(instance.instanceId);

            if (sourceIdSet.Count == 0)
                sourceIdTracking.Remove(instance.sourceId);
        }

        if (definitionTracking.TryGetValue(instance.definition.abilityId, out var defSet))
        {
            defSet.Remove(instance.instanceId);

            if (defSet.Count == 0)
                definitionTracking.Remove(instance.definition.abilityId);
        }
    }

    private void UpdateExpirations()
    {
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

    public void OnAbilityUsed(string instanceId)
    {
        if (!instanceLookup.TryGetValue(instanceId, out AbilityInstance instance))
            return;

        if (instance.IsConsumable())
        {
            if (instance.ConsumeUse())
                OnAbilityUsesChanged?.Invoke(instance);
        }
    }

    public AbilityInstance GetInstance(string instanceId)
    {
        instanceLookup.TryGetValue(instanceId, out AbilityInstance instance);
        return instance;
    }

    public AbilityInstance GetInstanceByDefinition(string abilityId)
    {
        if (!definitionTracking.TryGetValue(abilityId, out var instanceIds))
            return null;

        if (instanceIds.Count == 0)
            return null;

        string firstInstanceId = instanceIds.First();
        return GetInstance(firstInstanceId);
    }

    public List<AbilityInstance> GetInstancesByDefinition(string abilityId)
    {
        var result = new List<AbilityInstance>();

        if (!definitionTracking.TryGetValue(abilityId, out var instanceIds))
            return result;

        foreach (var instanceId in instanceIds)
        {
            if (instanceLookup.TryGetValue(instanceId, out var instance))
                result.Add(instance);
        }

        return result;
    }

    public List<AbilityInstance> GetInstancesBySource(AbilitySource source)
    {
        var result = new List<AbilityInstance>();

        if (!sourceTracking.TryGetValue(source, out var instanceIds))
            return result;

        foreach (var instanceId in instanceIds)
        {
            if (instanceLookup.TryGetValue(instanceId, out var instance))
                result.Add(instance);
        }

        return result;
    }

    public List<AbilityInstance> GetInstancesBySourceId(string sourceId)
    {
        var result = new List<AbilityInstance>();

        if (!sourceIdTracking.TryGetValue(sourceId, out var instanceIds))
            return result;

        foreach (var instanceId in instanceIds)
        {
            if (instanceLookup.TryGetValue(instanceId, out var instance))
                result.Add(instance);
        }

        return result;
    }

    public List<AbilityInstance> GetAllInstances()
    {
        return new List<AbilityInstance>(instanceLookup.Values);
    }

    public bool HasAbility(string abilityId)
    {
        return definitionTracking.ContainsKey(abilityId) &&
               definitionTracking[abilityId].Count > 0;
    }

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
}