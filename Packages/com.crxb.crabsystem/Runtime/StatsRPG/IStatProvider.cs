using System;

// Read/write access to one entity's stat values.
// Implemented by StatSystem; kept as an interface so consumers never depend on the store.

namespace CrabSystem
{
    public interface IStatProvider
    {
        // Effective value — base plus contributions — or defaultValue when not loaded.
        float GetValue(string statId, float defaultValue = 0f);

        // Sets what the character owns. Rejected for derived stats, whose value belongs to
        // their contributors. For anything that can be taken away again, use AddContribution.
        void SetValue(string statId, float value);

        // Adds or replaces one source's contribution. The key identifies the source
        // — "item:{instanceId}", "talent:{id}" — and re-adding under it replaces the amount.
        void AddContribution(string statId, string sourceKey, float amount);

        void RemoveContribution(string statId, string sourceKey);

        // Drops everything one source gave, across every stat.
        void ClearContributions(string sourceKey);

        bool HasStat(string statId);

        // True when this stat is produced by its contributors rather than owned.
        bool IsDerived(string statId);

        // Fired after a value changes. Args: statId, oldValue, newValue.
        event Action<string, float, float> OnStatChanged;
    }
}
