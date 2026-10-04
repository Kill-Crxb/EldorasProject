using System;
using System.Collections.Generic;
using NinjaGame.Stats;
using UnityEngine;

/// <summary>
/// Holds this entity's stat values. Each stat has a base the character owns and any number
/// of contributions from elsewhere — gear, talents, core-stat bonuses. The effective value
/// is base + contributions, clamped to the range authored in its schema.
///
/// Contributions are keyed by their source, so removing one is exact: nothing subtracts a
/// remembered number, and two sources feeding the same stat cannot corrupt each other.
///
/// This module never asks who its contributors are — sources push, StatSystem sums. That
/// keeps it ignorant of equipment, talents and everything else that might feed a stat.
/// </summary>
public class StatSystem : MonoBehaviour, IBrainModule, IStatProvider, ISaveable
{
    public int InitOrder => 90;

    #region Inspector

    [Header("Schemas")]
    [Tooltip("Schema asset names to load from StatsManager, e.g. 'CoreStats'.")]
    [SerializeField] private List<string> schemaIds = new();

    #endregion

    #region State

    // What the character owns. This is what gets saved.
    private readonly Dictionary<string, float> baseValues = new();

    // base + contributions, clamped. This is what everything reads.
    private readonly Dictionary<string, float> values = new();

    // statId -> sourceKey -> amount
    private readonly Dictionary<string, Dictionary<string, float>> contributions = new();

    // Ids from schemas marked derived. Excluded from the save on both sides.
    private readonly HashSet<string> derivedIds = new();

    private ControllerBrain brain;

    public bool IsEnabled { get; set; } = true;

    public event Action<string, float, float> OnStatChanged;

    #endregion

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        LoadSchemas();
    }

    public void LateInitialize() { }

    public void UpdateModule() { }

    #endregion

    #region Schema Loading

    private void LoadSchemas()
    {
        values.Clear();
        baseValues.Clear();
        contributions.Clear();
        derivedIds.Clear();

        if (StatsManager.Instance == null)
        {
            Debug.LogError($"[StatSystem] No StatsManager in scene. {name} has no stats.");
            return;
        }

        foreach (var schemaId in schemaIds)
        {
            if (string.IsNullOrEmpty(schemaId)) continue;

            var schema = StatsManager.Instance.GetSchema(schemaId);
            if (schema == null)
            {
                Debug.LogWarning($"[StatSystem] Schema '{schemaId}' not found for {name}");
                continue;
            }

            foreach (var entry in schema.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;

                baseValues[entry.id] = entry.defaultValue;
                values[entry.id] = entry.Clamp(entry.defaultValue);

                if (schema.Derived) derivedIds.Add(entry.id);
            }
        }
    }

    #endregion

    #region IStatProvider

    public float GetValue(string statId, float defaultValue = 0f)
    {
        if (string.IsNullOrEmpty(statId)) return defaultValue;
        return values.TryGetValue(statId, out float value) ? value : defaultValue;
    }

    /// <summary>
    /// Sets what the character owns. Contributions still stack on top, so this is the
    /// wrong call for gear or a buff — use AddContribution for anything temporary.
    /// </summary>
    public void SetValue(string statId, float value)
    {
        if (string.IsNullOrEmpty(statId)) return;

        if (!baseValues.ContainsKey(statId))
        {
            Debug.LogWarning($"[StatSystem] '{statId}' is not loaded on {name}");
            return;
        }

        if (derivedIds.Contains(statId))
        {
            Debug.LogWarning($"[StatSystem] '{statId}' is derived — write it with AddContribution, not SetValue");
            return;
        }

        baseValues[statId] = value;
        Recalculate(statId);
    }

    public bool HasStat(string statId) => !string.IsNullOrEmpty(statId) && values.ContainsKey(statId);

    public IEnumerable<string> GetStatIds() => values.Keys;

    /// <summary>True when this stat comes from elsewhere and is not part of the save.</summary>
    public bool IsDerived(string statId) => derivedIds.Contains(statId);

    #endregion

    #region Contributions

    /// <summary>
    /// Adds or replaces one source's contribution to a stat. The key identifies the source
    /// — "item:{instanceId}", "talent:{id}", "core" — and re-adding under the same key
    /// replaces the previous amount rather than stacking with it.
    /// </summary>
    public void AddContribution(string statId, string sourceKey, float amount)
    {
        if (string.IsNullOrEmpty(statId) || string.IsNullOrEmpty(sourceKey)) return;

        if (!values.ContainsKey(statId))
        {
            Debug.LogWarning($"[StatSystem] '{statId}' is not loaded on {name}");
            return;
        }

        if (!contributions.TryGetValue(statId, out var sources))
        {
            sources = new Dictionary<string, float>();
            contributions[statId] = sources;
        }

        sources[sourceKey] = amount;
        Recalculate(statId);
    }

    public void RemoveContribution(string statId, string sourceKey)
    {
        if (!contributions.TryGetValue(statId, out var sources)) return;
        if (!sources.Remove(sourceKey)) return;

        Recalculate(statId);
    }

    /// <summary>Drops everything one source gave, across every stat. Unequipping an item.</summary>
    public void ClearContributions(string sourceKey)
    {
        if (string.IsNullOrEmpty(sourceKey)) return;

        foreach (var pair in contributions)
        {
            if (pair.Value.Remove(sourceKey)) Recalculate(pair.Key);
        }
    }

    /// <summary>What the character owns, before anything is added to it.</summary>
    public float GetBaseValue(string statId)
    {
        return baseValues.TryGetValue(statId, out float value) ? value : 0f;
    }

    private void Recalculate(string statId)
    {
        float total = baseValues.TryGetValue(statId, out float baseValue) ? baseValue : 0f;

        if (contributions.TryGetValue(statId, out var sources))
        {
            foreach (var amount in sources.Values) total += amount;
        }

        var entry = StatsManager.Instance != null ? StatsManager.Instance.GetEntry(statId) : null;
        float clamped = entry != null ? entry.Clamp(total) : total;

        float previous = values.TryGetValue(statId, out float existing) ? existing : 0f;
        if (Mathf.Approximately(clamped, previous)) return;

        values[statId] = clamped;
        OnStatChanged?.Invoke(statId, previous, clamped);
    }

    #endregion

    #region ISaveable

    public string GetSaveId() => "stats";

    public int GetSaveVersion() => 2;

    /// <summary>
    /// Writes base values, never effective ones — saving the total would bake a temporary
    /// bonus into the character permanently the first time they saved while wearing gear.
    /// </summary>
    public string GetSaveData()
    {
        var data = new StatSaveData { version = GetSaveVersion() };

        foreach (var pair in baseValues)
        {
            if (derivedIds.Contains(pair.Key)) continue;

            data.stats.Add(new StatValuePair { id = pair.Key, value = pair.Value });
        }

        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<StatSaveData>(json);
        if (data?.stats == null) return;

        // A save written before a stat became derived still lists it. Ignore those rather
        // than restoring a number the sources are about to recalculate anyway.
        foreach (var pair in data.stats)
        {
            if (derivedIds.Contains(pair.id)) continue;

            SetValue(pair.id, pair.value);
        }
    }

    [Serializable]
    private class StatSaveData
    {
        public int version;
        public List<StatValuePair> stats = new();
    }

    [Serializable]
    private class StatValuePair
    {
        public string id;
        public float value;
    }

    #endregion
}
