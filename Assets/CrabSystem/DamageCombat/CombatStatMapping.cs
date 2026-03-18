using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Dynamic mapping between combat keys and StatSystem IDs.
/// Replaces hardcoded stat access in DamageSystem.
/// </summary>
[CreateAssetMenu(fileName = "CombatStatMapping", menuName = "RPG/Combat Stat Mapping")]
public class CombatStatMapping : ScriptableObject
{
    [System.Serializable]
    public struct StatEntry
    {
        public string key;      // e.g., "attack_power", "armor", "crit_chance"
        public string statId;   // StatSystem ID
    }

    [Tooltip("Map keys to StatSystem stat IDs")]
    public List<StatEntry> statEntries = new List<StatEntry>();

    private Dictionary<string, string> lookup;

    /// <summary>
    /// Initialize dictionary for fast lookups
    /// </summary>
    public void Initialize()
    {
        lookup = new Dictionary<string, string>();
        foreach (var entry in statEntries)
        {
            if (!lookup.ContainsKey(entry.key))
                lookup.Add(entry.key, entry.statId);
        }
    }

    /// <summary>
    /// Get StatSystem ID by key
    /// </summary>
    public string GetStatId(string key)
    {
        if (lookup == null) Initialize();
        return lookup.TryGetValue(key, out var statId) ? statId : null;
    }
}
