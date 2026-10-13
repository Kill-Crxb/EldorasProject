using System.Runtime.CompilerServices;
using UnityEngine;

// AbilityDefinition - Costs Partial
// Resource costs, cooldowns, and cast time management

namespace CrabSystem
{
    public partial class AbilityDefinition
    {
        [Header("Costs & Cooldown")]
        [Tooltip("Resource costs (can have multiple - mana + stamina, etc.)")]
        public ResourceCostEntry[] resourceCosts = new ResourceCostEntry[0];

        [Tooltip("Cooldown duration in seconds")]
        public float cooldown = 5f;

        [Tooltip("Cast time before execution (0 = instant)")]
        public float castTime = 0f;

        // Get total resource cost for a specific resource
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetResourceCost(ResourceDefinition resource)
        {
            if (resourceCosts == null || resource == null) return 0f;

            foreach (var entry in resourceCosts)
            {
                if (entry.resource == resource)
                    return entry.cost;
            }

            return 0f;
        }

        // Check if this ability uses a specific resource
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesResource(ResourceDefinition resource)
        {
            if (resourceCosts == null || resource == null) return false;

            foreach (var entry in resourceCosts)
            {
                if (entry.resource == resource)
                    return true;
            }

            return false;
        }

        // Does this ability have a cast time?
        public bool HasCastTime => castTime > 0f;

        // Is this an instant-cast ability?
        public bool IsInstant => castTime <= 0f;

        // Does this ability have a cooldown?
        public bool HasCooldown => cooldown > 0f;
    }
}
