using UnityEngine;

namespace CrabSystem
{
    public enum AbilitySource
    {
        Permanent,      // Base character abilities (never removed)
        Equipment,      // Granted by equipped items (removed on unequip)
        Consumable,     // From consumable items (limited uses)
        Temporary,      // Buffs/potions with duration (auto-expires)
        Talent          // Granted by a learned talent (removed on refund, respec or tree swap)
    }

    // Runtime wrapper for one granted ability: where it came from (so the source can take it back), uses left and
    // when it expires. Times are on the owner's EntityClock, passed in as now. Cooldowns live on AbilitySystem only.
    [System.Serializable]
    public class AbilityInstance
    {
        // "{definitionId}_{source}_{sourceId}_{guid}", stable for save/load.
        public string instanceId;
        public AbilityDefinition definition;
        public AbilitySource source;

        // The item or buff id that granted it; empty for permanent abilities.
        public string sourceId;

        // -1 is infinite.
        public int maxUses = -1;
        public int remainingUses = -1;

        // Clock time it expires, -1 is never.
        public float expirationTime = -1f;

        // False marks it for removal.
        public bool isActive = true;

        public AbilityInstance(AbilityDefinition definition, AbilitySource source, float now, string sourceId = "",
            int maxUses = -1, float durationSeconds = -1f)
        {
            if (definition == null) { Debug.LogError("[AbilityInstance] null definition"); return; }

            string guid = System.Guid.NewGuid().ToString().Substring(0, 8);
            instanceId = $"{definition.abilityId}_{source}_{sourceId}_{guid}";

            this.definition = definition;
            this.source = source;
            this.sourceId = sourceId ?? "";
            this.maxUses = maxUses;
            remainingUses = maxUses;
            expirationTime = durationSeconds > 0f ? now + durationSeconds : -1f;
        }

        bool OutOfUses => maxUses >= 0 && remainingUses <= 0;

        public bool IsExpired(float now) => expirationTime >= 0f && now >= expirationTime;

        public bool IsUsable(float now) => isActive && definition != null && !OutOfUses && !IsExpired(now);

        public bool ShouldRemove(float now) => !isActive || OutOfUses || IsExpired(now);

        public bool IsConsumable() => source == AbilitySource.Consumable && maxUses >= 0;

        public bool IsTemporary() => source == AbilitySource.Temporary && expirationTime >= 0f;

        // Seconds left, -1 when it never expires.
        public float GetRemainingDuration(float now) => expirationTime < 0f ? -1f : Mathf.Max(0f, expirationTime - now);

        public float GetUsesPercent()
        {
            if (maxUses < 0) return 1f;
            if (maxUses == 0) return 0f;
            return (float)remainingUses / maxUses;
        }

        public bool ConsumeUse()
        {
            if (maxUses < 0) return true;
            if (remainingUses <= 0) return false;
            remainingUses--;
            return true;
        }

        public void RestoreUses(int amount)
        {
            if (maxUses < 0) return;
            remainingUses = Mathf.Min(remainingUses + amount, maxUses);
        }

        public void ExtendDuration(float additionalSeconds)
        {
            if (expirationTime < 0f) return;
            expirationTime += additionalSeconds;
        }

        public void Deactivate() => isActive = false;

        public override string ToString()
        {
            string uses = maxUses >= 0 ? $"{remainingUses}/{maxUses}" : "∞";
            string expires = expirationTime >= 0f ? $"{expirationTime:F1}" : "∞";
            return $"[{definition?.abilityName}] Source:{source} Uses:{uses} Expires:{expires} Active:{isActive}";
        }
    }
}
