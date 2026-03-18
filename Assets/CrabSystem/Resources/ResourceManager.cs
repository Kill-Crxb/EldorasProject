using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class ResourceManager : MonoBehaviour, IGameManager, IManagerDependency, IHotReloadable
{
    #region Singleton

    private static ResourceManager instance;
    public static ResourceManager Instance => instance;

    #endregion

    #region IGameManager

    public string ManagerName => "Resource Manager";
    public int InitializationPriority => 10;
    public bool IsEnabled => enabled;
    public bool IsInitialized { get; private set; }
    public int ConfigVersion { get; private set; }

    public void Initialize()
    {
        if (IsInitialized) return;

        instance = this;
        BuildResourceLookup();
        IsInitialized = true;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Initialized with {resourceDefinitions.Count} resources");
    }

    public void LateInitialize()
    {
        ValidateStatReferences();
    }

    public void Shutdown()
    {
        if (debugLogging)
            Debug.Log($"[{ManagerName}] Shutdown");
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
                result.Errors.Add("Null resource definition");
                result.IsFatal = true;
                continue;
            }

            if (string.IsNullOrEmpty(def.resourceId))
                result.Errors.Add($"Resource '{def.displayName}' has no resourceId");

            if (!def.Validate(out string error))
                result.Warnings.Add($"Resource '{def.displayName}': {error}");
        }

        return result;
    }

    public void HotReload()
    {
        BuildResourceLookup();
        ValidateStatReferences();
        ConfigVersion++;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Hot-reloaded (v{ConfigVersion})");
    }

    private static readonly Type[] dependencies = new[] { typeof(StatsManager) };
    public IEnumerable<Type> DependsOn => dependencies;

    #endregion

    #region Inspector

    [Header("Resource Definitions")]
    [SerializeField] private List<ResourceDefinition> resourceDefinitions = new();

    [Header("Settings")]
    [SerializeField] private bool enableHotReload = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region Lookups

    private readonly Dictionary<string, ResourceDefinition> resourceById = new();

    #endregion

    #region Build & Validation

    private void BuildResourceLookup()
    {
        resourceById.Clear();

        foreach (var def in resourceDefinitions)
        {
            if (def == null || string.IsNullOrEmpty(def.resourceId))
                continue;

            if (resourceById.ContainsKey(def.resourceId))
            {
                Debug.LogWarning($"[ResourceManager] Duplicate resourceId '{def.resourceId}'");
                continue;
            }

            resourceById[def.resourceId] = def;
        }
    }

    private void ValidateStatReferences()
    {
        if (ManagerBrain.Instance == null)
        {
            Debug.LogWarning($"[{ManagerName}] Cannot validate stat references - ManagerBrain not initialized");
            return;
        }

        var stats = ManagerBrain.Instance.Stats;
        if (stats == null)
        {
            Debug.LogWarning($"[{ManagerName}] Cannot validate stat references - StatsManager not found");
            return;
        }

        foreach (var def in resourceDefinitions)
        {
            if (def == null) continue;

            if (!string.IsNullOrEmpty(def.maxStatId) && !stats.HasStat(def.maxStatId))
            {
                Debug.LogError($"[{ManagerName}] Resource '{def.resourceId}' references missing stat '{def.maxStatId}'");
            }
        }
    }

    #endregion

    #region Queries

    public ResourceDefinition Get(string resourceId)
    {
        if (!resourceById.TryGetValue(resourceId, out var def))
            return null;

        return def;
    }

    public bool Has(string resourceId)
    {
        return resourceById.ContainsKey(resourceId);
    }

    public IEnumerable<ResourceDefinition> GetAll()
    {
        return resourceById.Values;
    }

    #endregion

    #region Runtime Registration

    public void RegisterResource(ResourceDefinition def)
    {
        if (!enableHotReload || def == null) return;

        if (!resourceDefinitions.Contains(def))
            resourceDefinitions.Add(def);

        BuildResourceLookup();
        ConfigVersion++;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Registered resource '{def.resourceId}'");
    }

    public void UnregisterResource(string resourceId)
    {
        if (!enableHotReload) return;

        resourceDefinitions.RemoveAll(r => r != null && r.resourceId == resourceId);
        BuildResourceLookup();
        ConfigVersion++;
    }

    #endregion
}