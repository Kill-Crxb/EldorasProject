using UnityEngine;
using System;
using System.Collections.Generic;
using System;

/// <summary>
/// Resource Manager - Global resource configuration (Singleton)
/// 
/// Architecture Pattern:
/// ResourceManager (Global) → ResourceSystem (Per-Entity)
/// 
/// Mirrors:
/// - StatsManager → StatSystem
/// - DamageManager → DamageSystem
/// 
/// Responsibilities:
/// - Define available resource types
/// - Map resources to stat IDs
/// - Configure regeneration rules globally
/// - Provide resource definitions to all entities
/// - Server-authoritative in multiplayer
/// 
/// Benefits vs Hardcoded:
/// - Data-driven resource types
/// - Different configs per game mode
/// - Hot-reload resource rules at runtime
/// - Modding support (add custom resources)
/// - No hardcoded strings
/// 
/// Phase 1.7b: Universal Systems Consolidation
/// Created: January 2026
/// </summary>
public class ResourceManager : MonoBehaviour, IGameManager, IManagerDependency, IHotReloadable
{
    #region Singleton (Simplified)

    private static ResourceManager instance;
    public static ResourceManager Instance => instance;

    #endregion

    #region IGameManager Implementation

    public string ManagerName => "Resource Manager";
    public int InitializationPriority => 10; // After Stats (0)
    public bool IsEnabled => enabled;
    public bool IsInitialized { get; private set; }

    // Config versioning for hot-reload sync
    public int ConfigVersion { get; private set; }

    public void Initialize()
    {
        if (IsInitialized) return;

        instance = this;

        BuildResourceLookup();

        IsInitialized = true;

        if (debugLogging)
        {
            Debug.Log($"[{ManagerName}] Initialized with {resourceDefinitions.Count} resource types.");
        }
    }

    public void LateInitialize()
    {
        // COORDINATION: Validate stat references with StatsManager
        ValidateStatReferences();
    }

    public void Shutdown()
    {
        if (debugLogging)
            Debug.Log($"[{ManagerName}] Shutdown complete");
    }

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();

        if (resourceDefinitions == null || resourceDefinitions.Count == 0)
        {
            result.IsFatal = true;
            result.Errors.Add("No resource definitions configured");
            return result;
        }

        foreach (var def in resourceDefinitions)
        {
            if (def == null)
            {
                result.IsFatal = true;
                result.Errors.Add("Null resource definition in list");
                return result;
            }

            if (!def.Validate(out string defError))
            {
                result.Warnings.Add($"Resource '{def.displayName}' validation warning: {defError}");
            }
        }

        result.Info.Add($"Loaded {resourceDefinitions.Count} resources");
        return result;
    }

    public void HotReload()
    {
        BuildResourceLookup();
        ValidateStatReferences();

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Hot-reloaded");
    }

    // IManagerDependency implementation
    public IEnumerable<Type> DependsOn => new[] { typeof(StatsManager) };

    #endregion

    #region Inspector Fields

    [Header("Resource Definitions")]
    [Tooltip("All available resource types in the game")]
    [SerializeField] private List<ResourceDefinition> resourceDefinitions = new List<ResourceDefinition>();

    [Header("Settings")]
    [Tooltip("Allow runtime resource config hot-reloading")]
    [SerializeField] private bool enableHotReload = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region Private Fields

    // Fast lookup by resource type
    private Dictionary<ResourceType, ResourceDefinition> resourceLookup = new Dictionary<ResourceType, ResourceDefinition>();

    // Fast lookup by stat ID (reverse mapping)
    private Dictionary<string, ResourceType> statIdToResourceType = new Dictionary<string, ResourceType>();

    #endregion

    #region Initialization & Coordination

    private void BuildResourceLookup()
    {
        resourceLookup.Clear();
        statIdToResourceType.Clear();

        foreach (var def in resourceDefinitions)
        {
            if (def == null)
            {
                Debug.LogWarning("[ResourceManager] Null resource definition in list - skipping");
                continue;
            }

            // Register by resource type
            if (resourceLookup.ContainsKey(def.resourceType))
            {
                Debug.LogWarning($"[ResourceManager] Duplicate resource type '{def.resourceType}' - using first definition");
                continue;
            }

            resourceLookup[def.resourceType] = def;

            // Register reverse mapping (stat ID → resource type)
            if (!string.IsNullOrEmpty(def.maxStatId))
            {
                statIdToResourceType[def.maxStatId] = def.resourceType;
            }
        }
    }

    /// <summary>
    /// COORDINATION: Validate stat references with StatsManager
    /// </summary>
    private void ValidateStatReferences()
    {
        // Get StatsManager through ManagerBrain
        var stats = ManagerBrain.Instance?.Stats;
        if (stats == null)
        {
            Debug.LogError($"[{ManagerName}] StatsManager not available for validation!");
            return;
        }

        bool allValid = true;

        foreach (var def in resourceDefinitions)
        {
            if (def == null) continue;

            // Validate max stat ID exists
            if (!string.IsNullOrEmpty(def.maxStatId))
            {
                if (!stats.HasStat(def.maxStatId))
                {
                    Debug.LogError($"[{ManagerName}] Resource '{def.displayName}' " +
                                 $"references unknown max stat: {def.maxStatId}");
                    allValid = false;
                }
            }

            // Validate current stat ID if specified
            if (!string.IsNullOrEmpty(def.currentStatId))
            {
                if (!stats.HasStat(def.currentStatId))
                {
                    Debug.LogWarning($"[{ManagerName}] Resource '{def.displayName}' " +
                                   $"references unknown current stat: {def.currentStatId}");
                }
            }
        }

        if (allValid && debugLogging)
        {
            Debug.Log($"[{ManagerName}] All stat references validated successfully");
        }
    }

    #endregion

    #region Resource Queries

    /// <summary>
    /// Get resource definition by type
    /// </summary>
    public ResourceDefinition GetResourceDefinition(ResourceType type)
    {
        if (resourceLookup.TryGetValue(type, out ResourceDefinition def))
        {
            return def;
        }

        if (debugLogging)
        {
            Debug.LogWarning($"[ResourceManager] No definition found for resource type '{type}'");
        }

        return null;
    }

    /// <summary>
    /// Get stat ID for a resource's maximum value
    /// </summary>
    public string GetMaxStatId(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        return def?.maxStatId ?? GetFallbackStatId(type);
    }

    /// <summary>
    /// Get stat ID for a resource's current value (if tracked in stats)
    /// </summary>
    public string GetCurrentStatId(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        return def?.currentStatId ?? "";
    }

    /// <summary>
    /// Get regeneration rate for a resource
    /// </summary>
    public float GetRegenPerSecond(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        return def?.regenPerSecond ?? 0f;
    }

    /// <summary>
    /// Get regeneration delay for a resource
    /// </summary>
    public float GetRegenDelay(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        return def?.regenDelay ?? 0f;
    }

    /// <summary>
    /// Check if resource can regenerate out of combat
    /// </summary>
    public bool CanRegenOutOfCombat(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        return def?.regenOutOfCombatOnly ?? false;
    }

    /// <summary>
    /// Get combat timeout for a resource
    /// </summary>
    public float GetCombatTimeout(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        return def?.combatTimeout ?? 5f;
    }

    /// <summary>
    /// Check if resource triggers death when depleted
    /// </summary>
    public bool TriggerDeathOnDepletion(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        return def?.triggerDeathOnDepletion ?? (type == ResourceType.Health);
    }

    /// <summary>
    /// Check if resource allows negative values
    /// </summary>
    public bool AllowNegative(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        return def?.allowNegative ?? false;
    }

    /// <summary>
    /// Get minimum value for a resource
    /// </summary>
    public float GetMinValue(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        return def?.minValue ?? 0f;
    }

    /// <summary>
    /// Get UI color for a resource
    /// </summary>
    public Color GetResourceColor(ResourceType type)
    {
        var def = GetResourceDefinition(type);
        if (def != null)
            return def.resourceColor;

        // Fallback colors
        return type switch
        {
            ResourceType.Health => Color.red,
            ResourceType.Mana => Color.blue,
            ResourceType.Stamina => Color.green,
            ResourceType.Energy => Color.yellow,
            ResourceType.Rage => new Color(0.8f, 0.2f, 0.2f),
            ResourceType.Focus => Color.cyan,
            _ => Color.white
        };
    }

    /// <summary>
    /// Check if a resource type is defined
    /// </summary>
    public bool HasResourceDefinition(ResourceType type)
    {
        return resourceLookup.ContainsKey(type);
    }

    /// <summary>
    /// Get all defined resource types
    /// </summary>
    public List<ResourceType> GetDefinedResourceTypes()
    {
        return new List<ResourceType>(resourceLookup.Keys);
    }

    #endregion

    #region Resource Registration (for modding/runtime)

    /// <summary>
    /// Register a resource definition at runtime
    /// </summary>
    public void RegisterResource(ResourceDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogWarning("[ResourceManager] Cannot register null resource definition");
            return;
        }

        if (!enableHotReload)
        {
            Debug.LogWarning("[ResourceManager] Hot-reload disabled - cannot register resource at runtime");
            return;
        }

        // Add to list if not already present
        if (!resourceDefinitions.Contains(definition))
        {
            resourceDefinitions.Add(definition);
        }

        // Rebuild lookup
        BuildResourceLookup();

        if (debugLogging)
        {
            Debug.Log($"[ResourceManager] Registered resource: {definition.displayName}");
        }
    }

    /// <summary>
    /// Unregister a resource type
    /// </summary>
    public void UnregisterResource(ResourceType type)
    {
        if (!enableHotReload)
        {
            Debug.LogWarning("[ResourceManager] Hot-reload disabled");
            return;
        }

        var def = GetResourceDefinition(type);
        if (def != null)
        {
            resourceDefinitions.Remove(def);
            BuildResourceLookup();

            if (debugLogging)
            {
                Debug.Log($"[ResourceManager] Unregistered resource: {type}");
            }
        }
    }

    #endregion

    #region Runtime Adjustments (Hot-patching)

    /// <summary>
    /// Adjust regeneration rate at runtime
    /// </summary>
    public void AdjustRegenRate(ResourceType type, float multiplier)
    {
        if (!IsServer())
        {
            Debug.LogError("[ResourceManager] AdjustRegenRate is SERVER-ONLY!");
            return;
        }

        if (!enableHotReload)
        {
            Debug.LogWarning("[ResourceManager] Hot-reload disabled");
            return;
        }

        var def = GetResourceDefinition(type);
        if (def != null)
        {
            def.regenPerSecond *= multiplier;
            ConfigVersion++; // Increment version

            if (debugLogging)
            {
                Debug.Log($"[ResourceManager] Adjusted {type} regen by {multiplier:F2}x → {def.regenPerSecond:F2}/sec (version: {ConfigVersion})");
            }
        }
    }

    /// <summary>
    /// Set regeneration rate at runtime
    /// </summary>
    public void SetRegenRate(ResourceType type, float regenPerSecond)
    {
        if (!IsServer())
        {
            Debug.LogError("[ResourceManager] SetRegenRate is SERVER-ONLY!");
            return;
        }

        if (!enableHotReload)
        {
            Debug.LogWarning("[ResourceManager] Hot-reload disabled");
            return;
        }

        var def = GetResourceDefinition(type);
        if (def != null)
        {
            def.regenPerSecond = regenPerSecond;
            ConfigVersion++; // Increment version

            if (debugLogging)
            {
                Debug.Log($"[ResourceManager] Set {type} regen to {regenPerSecond:F2}/sec (version: {ConfigVersion})");
            }
        }
    }

    #endregion

    #region Fallback Handling

    /// <summary>
    /// Get fallback stat ID if no definition exists
    /// </summary>
    private string GetFallbackStatId(ResourceType type)
    {
        // Fallback to old hardcoded pattern for backward compatibility
        return type switch
        {
            ResourceType.Health => "character.max_health",
            ResourceType.Mana => "character.max_mana",
            ResourceType.Stamina => "character.max_stamina",
            ResourceType.Energy => "character.max_energy",
            ResourceType.Rage => "character.max_rage",
            ResourceType.Focus => "character.max_focus",
            _ => $"character.max_{type.ToString().ToLower()}"
        };
    }

    #endregion

    #region Validation & Debug

    /// <summary>
    /// Validate all resource definitions
    /// </summary>
    public bool ValidateAll()
    {
        bool allValid = true;

        foreach (var def in resourceDefinitions)
        {
            if (def == null)
            {
                Debug.LogError("[ResourceManager] Null resource definition in list!");
                allValid = false;
                continue;
            }

            if (!def.Validate(out string error))
            {
                Debug.LogError($"[ResourceManager] Invalid resource '{def.displayName}': {error}");
                allValid = false;
            }
        }

        if (allValid && debugLogging)
        {
            Debug.Log($"[ResourceManager] All {resourceDefinitions.Count} resource definitions validated successfully.");
        }

        return allValid;
    }

    [ContextMenu("Validate All Resources")]
    private void ContextValidateAll()
    {
        ValidateAll();
    }

    [ContextMenu("Print Resource Summary")]
    private void PrintResourceSummary()
    {
        Debug.Log($"=== Resource Manager Summary ===");
        Debug.Log($"Total Resources: {resourceDefinitions.Count}");

        foreach (var def in resourceDefinitions)
        {
            if (def != null)
            {
                Debug.Log($"  {def.displayName} ({def.resourceType}):");
                Debug.Log($"    Max Stat: {def.maxStatId}");
                Debug.Log($"    Regen: {def.regenPerSecond}/sec (delay: {def.regenDelay}s)");
            }
        }
    }

    [ContextMenu("Create Default Resources")]
    private void CreateDefaultResources()
    {
        Debug.Log("[ResourceManager] Use Create > NinjaGame > Resources > Resource Definition to create resources");
        Debug.Log("Then add them to the Resource Definitions list in this component");
    }

    #endregion


    #region Server Authority

    /// <summary>
    /// Check if we're running on server (for multiplayer authority)
    /// </summary>
    private bool IsServer()
    {
#if UNITY_SERVER
        return true;
#else
        // For single-player, always act as server
        // When networking added, replace with: NetworkServer.active (Mirror) or similar
        return true;
#endif
    }

    #endregion
}