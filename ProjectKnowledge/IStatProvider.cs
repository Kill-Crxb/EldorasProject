using System;

/// <summary>
/// Read/write access to one entity's stat values.
/// Implemented by StatSystem; kept as an interface so consumers never depend on the store.
/// </summary>
public interface IStatProvider
{
    /// <summary>Effective value — base plus contributions — or defaultValue when not loaded.</summary>
    float GetValue(string statId, float defaultValue = 0f);

    /// <summary>
    /// Sets what the character owns. Rejected for derived stats, whose value belongs to
    /// their contributors. For anything that can be taken away again, use AddContribution.
    /// </summary>
    void SetValue(string statId, float value);

    /// <summary>
    /// Adds or replaces one source's contribution. The key identifies the source
    /// — "item:{instanceId}", "talent:{id}" — and re-adding under it replaces the amount.
    /// </summary>
    void AddContribution(string statId, string sourceKey, float amount);

    void RemoveContribution(string statId, string sourceKey);

    /// <summary>Drops everything one source gave, across every stat.</summary>
    void ClearContributions(string sourceKey);

    bool HasStat(string statId);

    /// <summary>True when this stat is produced by its contributors rather than owned.</summary>
    bool IsDerived(string statId);

    /// <summary>Fired after a value changes. Args: statId, oldValue, newValue.</summary>
    event Action<string, float, float> OnStatChanged;
}
