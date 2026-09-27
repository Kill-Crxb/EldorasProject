using System.Collections.Generic;
using NinjaGame.Stats;
using UnityEngine;

/// <summary>
/// Global registry of stat schemas. Loads every schema once at startup and answers
/// "which stats exist" for entities and for cross-manager validation.
/// </summary>
public class StatsManager : MonoBehaviour, IGameManager
{
    #region Inspector

    // Any folder named "Resources" anywhere under Assets is a resources root, so the
    // database folder is found by this name alone, wherever it sits in the project.
    private const string DatabaseFolder = "StatDatabase";

    [Header("Schemas")]
    [Tooltip("Optional. Everything in Resources/StatDatabase is registered regardless.")]
    [SerializeField] private List<StatSchema> globalSchemas = new();

    #endregion

    #region State

    private static StatsManager instance;
    public static StatsManager Instance => instance;

    private readonly Dictionary<string, StatSchema> schemasByName = new();
    private readonly Dictionary<string, StatEntry> entriesById = new();

    #endregion

    #region IGameManager

    public string ManagerName => "Stats Manager";
    public int InitializationPriority => 0;
    public bool IsEnabled => enabled;
    public bool IsInitialized { get; private set; }

    public void Initialize()
    {
        if (IsInitialized) return;

        instance = this;
        BuildCaches();
        IsInitialized = true;
    }

    public void LateInitialize() { }

    public void Shutdown()
    {
        if (instance == this) instance = null;
    }

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();

        if (!IsInitialized) return result;

        if (schemasByName.Count == 0)
        {
            result.Warnings.Add("No stat schemas loaded");
            return result;
        }

        foreach (var schema in schemasByName.Values)
        {
            foreach (var entry in schema.Entries)
            {
                if (entry == null || entry.Validate(out string error)) continue;
                result.Errors.Add($"{schema.name}: {error}");
            }
        }

        result.Info.Add($"{schemasByName.Count} schemas, {entriesById.Count} stats");
        return result;
    }

    #endregion

    #region Cache

    private void BuildCaches()
    {
        schemasByName.Clear();
        entriesById.Clear();

        Register(globalSchemas);
        Register(Resources.LoadAll<StatSchema>(DatabaseFolder));

        if (schemasByName.Count == 0)
        {
            Debug.LogWarning($"[{ManagerName}] No schemas loaded. Assign them in the inspector, " +
                             $"or drop them in a Resources/{DatabaseFolder} folder.");
        }
    }

    /// <summary>
    /// Called once for the inspector list and once for the database folder, so a schema in
    /// both is silently ignored the second time rather than reported as a clash.
    /// </summary>
    private void Register(IReadOnlyList<StatSchema> schemas)
    {
        if (schemas == null) return;

        foreach (var schema in schemas)
        {
            if (schema == null) continue;

            if (schemasByName.TryGetValue(schema.name, out var existing))
            {
                if (existing != schema)
                    Debug.LogError($"[{ManagerName}] Two schemas are both named '{schema.name}'. Names must be unique.");
                continue;
            }

            schemasByName[schema.name] = schema;

            foreach (var entry in schema.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;

                if (!entriesById.TryAdd(entry.id, entry))
                    Debug.LogError($"[{ManagerName}] Duplicate stat id '{entry.id}' in '{schema.name}'");
            }
        }
    }

    #endregion

    #region Queries

    public StatSchema GetSchema(string schemaName)
    {
        return schemasByName.GetValueOrDefault(schemaName);
    }

    public StatEntry GetEntry(string statId)
    {
        return string.IsNullOrEmpty(statId) ? null : entriesById.GetValueOrDefault(statId);
    }

    public bool HasStat(string statId) => GetEntry(statId) != null;

    public IEnumerable<string> GetSchemaNames() => schemasByName.Keys;

    public IEnumerable<string> GetStatIds() => entriesById.Keys;

    #endregion
}
