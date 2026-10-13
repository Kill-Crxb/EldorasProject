using System;
using System.Collections.Generic;
using UnityEngine;

// Which abilities a modifier touches. Every criterion that is set must match; nothing set matches every ability.

namespace CrabSystem
{
    [Serializable]
    public struct AbilityFilter
    {
        [Tooltip("One ability. Empty = any.")]
        [IdRef(IdKind.Ability)] public string abilityId;

        public bool matchType;
        [Tooltip("Offensive picks attacks; Defensive picks block and parry.")]
        public AbilityType type;

        public bool matchCategory;
        public AbilityCategory category;

        public bool matchSpeedClass;
        [Tooltip("Technique, Heavy, Quick… — the move's speed class.")]
        public SpeedClass speedClass;

        [Tooltip("An entry in the ability's tags. Empty = any.")]
        public string tag;

        // `tags` adds the character's granted tags (sockets, talents) to the asset's own; null = the asset's only.
        public bool Matches(AbilityDefinition ability, AbilityModifiers tags = null)
        {
            if (ability == null) return false;
            if (!string.IsNullOrEmpty(abilityId) && ability.abilityId != abilityId) return false;
            if (matchType && ability.abilityType != type) return false;
            if (matchCategory && ability.abilityCategory != category) return false;
            if (matchSpeedClass && ability.speedClass != speedClass) return false;
            if (string.IsNullOrEmpty(tag)) return true;
            return tags != null ? tags.HasTag(ability, tag) : ability.HasTag(tag);
        }

        public string Describe()
        {
            if (!string.IsNullOrEmpty(abilityId)) return abilityId;

            var parts = new List<string>();
            if (matchType) parts.Add(type.ToString());
            if (matchCategory) parts.Add(category.ToString());
            if (matchSpeedClass) parts.Add(speedClass.ToString());
            if (!string.IsNullOrEmpty(tag)) parts.Add(tag);
            return parts.Count > 0 ? string.Join(" ", parts) : "all";
        }
    }

    // One keyed change to abilities: flat deltas on a resource cost, the cooldown and the cast time.
    public struct AbilityModifier
    {
        public AbilityFilter Filter;
        public ResourceDefinition Resource;
        public float CostDelta;
        public float CooldownDelta;
        public float CastTimeDelta;
    }

    // The per-character changes to abilities — talents, gear, the burden track. Owned by AbilitySystem, which asks
    // it for the cost, cooldown and cast time of every use. Keyed like every reward, so re-applying replaces and
    // removal is exact. Flat deltas only, never below zero.
    //
    // Also the character's granted tags: a socket or a talent gives the abilities a filter picks a tag (a keyword)
    // for this character only. Granted tags count everywhere a tag is asked about, cost filters included; a grant's
    // own filter sees the asset's tags only, so grants never chain.
    //
    // And the character's replacements: "your Sinister abilities become Shadow Strike". Every use of an ability the
    // filter picks plays the replacement instead (AbilitySystem resolves it). The replacement carries the tag its
    // filter matched on, so Shadow Strike is still Sinister for gains, costs and hit bonuses.
    public class AbilityModifiers
    {
        private readonly Dictionary<string, AbilityModifier> byKey = new();
        private readonly Dictionary<string, (AbilityFilter filter, string tag)> tagGrants = new();
        private readonly Dictionary<string, (AbilityFilter filter, AbilityDefinition replacement)> replacements = new();

        public event Action Changed;

        public void Set(string key, AbilityModifier modifier)
        {
            byKey[key] = modifier;
            Changed?.Invoke();
        }

        public void Remove(string key)
        {
            if (byKey.Remove(key)) Changed?.Invoke();
        }

        public void SetTag(string key, AbilityFilter filter, string tag)
        {
            tagGrants[key] = (filter, tag);
            Changed?.Invoke();
        }

        public void RemoveTag(string key)
        {
            if (tagGrants.Remove(key)) Changed?.Invoke();
        }

        public void SetReplacement(string key, AbilityFilter filter, AbilityDefinition replacement)
        {
            replacements[key] = (filter, replacement);
            Changed?.Invoke();
        }

        public void RemoveReplacement(string key)
        {
            if (replacements.Remove(key)) Changed?.Invoke();
        }

        public bool IsReplacement(AbilityDefinition ability)
        {
            foreach ((AbilityFilter _, AbilityDefinition replacement) in replacements.Values)
            {
                if (replacement == ability) return true;
            }
            return false;
        }

        // What this character plays when it uses the ability: the first replacement whose filter picks it, or itself.
        public AbilityDefinition Replace(AbilityDefinition ability)
        {
            if (ability == null) return null;

            foreach ((AbilityFilter filter, AbilityDefinition replacement) in replacements.Values)
            {
                if (replacement != null && replacement != ability && filter.Matches(ability, this)) return replacement;
            }
            return ability;
        }

        public bool HasTag(AbilityDefinition ability, string tag)
        {
            if (ability == null || string.IsNullOrEmpty(tag)) return false;
            if (ability.HasTag(tag)) return true;

            foreach ((AbilityFilter filter, string granted) in tagGrants.Values)
            {
                if (granted == tag && filter.Matches(ability)) return true;
            }

            foreach ((AbilityFilter filter, AbilityDefinition replacement) in replacements.Values)
            {
                if (replacement == ability && filter.tag == tag) return true;
            }
            return false;
        }

        public float Cost(AbilityDefinition ability, ResourceDefinition resource, float baseCost)
        {
            // A cost the ability lists at 0 can still be raised (the burden track's +1 on a free dodge).
            float cost = baseCost;
            foreach (AbilityModifier modifier in byKey.Values)
            {
                if (modifier.Resource == resource && modifier.Filter.Matches(ability, this)) cost += modifier.CostDelta;
            }
            return Mathf.Max(0f, cost);
        }

        public float Cooldown(AbilityDefinition ability)
        {
            float cooldown = ability.cooldown;
            foreach (AbilityModifier modifier in byKey.Values)
            {
                if (modifier.Filter.Matches(ability, this)) cooldown += modifier.CooldownDelta;
            }
            return Mathf.Max(0f, cooldown);
        }

        public float CastTime(AbilityDefinition ability)
        {
            float castTime = ability.castTime;
            foreach (AbilityModifier modifier in byKey.Values)
            {
                if (modifier.Filter.Matches(ability, this)) castTime += modifier.CastTimeDelta;
            }
            return Mathf.Max(0f, castTime);
        }
    }
}
