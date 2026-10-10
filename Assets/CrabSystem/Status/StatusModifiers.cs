using System;
using System.Collections.Generic;
using UnityEngine;

// Which statuses a modifier touches. Every criterion that is set must match; nothing set matches every status.
[Serializable]
public struct StatusFilter
{
    [Tooltip("One status. Empty = any.")]
    public StatusDefinition status;

    public bool matchDisposition;
    [Tooltip("Ticked = beneficial statuses; unticked = harmful ones.")]
    public bool beneficial;

    [Tooltip("An entry in the status's tags, e.g. control. Empty = any.")]
    public string tag;

    public bool Matches(StatusDefinition definition)
    {
        if (definition == null) return false;
        if (status != null && status.id != definition.id) return false;
        if (matchDisposition && definition.beneficial != beneficial) return false;
        return string.IsNullOrEmpty(tag) || definition.HasTag(tag);
    }

    public string Describe()
    {
        if (status != null) return string.IsNullOrEmpty(status.displayName) ? status.id : status.displayName;

        var parts = new List<string>();
        if (matchDisposition) parts.Add(beneficial ? "beneficial" : "harmful");
        if (!string.IsNullOrEmpty(tag)) parts.Add(tag);
        return parts.Count > 0 ? string.Join(" ", parts) : "any status";
    }
}

// Taken: statuses landing on this character (Resist, Immune). Dealt: statuses this character applies to others
// (Intensify).
public enum StatusSide { Taken, Dealt }

public struct StatusModifier
{
    public StatusFilter Filter;
    public StatusSide Side;
    public bool Immune;
    public float Seconds;
    public int Stacks;
}

// The per-character changes to statuses — talents, gear, other statuses. Owned by StatusSystem, which asks it on
// every application: the bearer's Taken entries and the source's Dealt entries both count. Keyed like every
// reward, so re-applying replaces and removal is exact. Flat seconds and whole stacks, never multipliers.
public class StatusModifiers
{
    private readonly Dictionary<string, StatusModifier> byKey = new();

    public void Set(string key, StatusModifier modifier) => byKey[key] = modifier;

    public void Remove(string key) => byKey.Remove(key);

    public bool Immune(StatusDefinition definition)
    {
        foreach (StatusModifier modifier in byKey.Values)
        {
            if (modifier.Immune && modifier.Side == StatusSide.Taken && modifier.Filter.Matches(definition)) return true;
        }
        return false;
    }

    public float Seconds(StatusDefinition definition, StatusSide side)
    {
        float total = 0f;
        foreach (StatusModifier modifier in byKey.Values)
        {
            if (modifier.Side == side && modifier.Filter.Matches(definition)) total += modifier.Seconds;
        }
        return total;
    }

    public int Stacks(StatusDefinition definition, StatusSide side)
    {
        int total = 0;
        foreach (StatusModifier modifier in byKey.Values)
        {
            if (modifier.Side == side && modifier.Filter.Matches(definition)) total += modifier.Stacks;
        }
        return total;
    }
}
