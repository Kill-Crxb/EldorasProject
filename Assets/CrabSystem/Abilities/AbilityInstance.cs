using UnityEngine;

// ========================================
// ENUMS
// ========================================

/// <summary>
/// Ability Source - Determines where an ability came from
/// Used for proper cleanup and lifecycle management
/// </summary>
public enum AbilitySource
{
    Permanent,      // Base character abilities (never removed)
    Equipment,      // Granted by equipped items (removed on unequip)
    Consumable,     // From consumable items (limited uses)
    Temporary       // Buffs/potions with duration (auto-expires)
}

// ========================================
// ABILITY INSTANCE
// ========================================

/// <summary>
/// AbilityInstance - Runtime wrapper for ability state
/// 
/// Architecture:
/// - Every active ability has an instance (even permanent ones)
/// - Stable instance ID for save/load
/// - Source tracking enables proper cleanup
/// - Usage tracking for consumables
/// - Expiration tracking for temporary buffs
/// 
/// Lifecycle:
/// 1. AddAbility() creates instance
/// 2. UseAbility() decrements remainingUses
/// 3. Update() checks expiration
/// 4. RemoveAbility() cleans up
/// 
/// Examples:
/// - Sword Attack: source=Permanent, maxUses=-1, expirationTime=-1
/// - Bow Shot: source=Equipment, sourceId="longbow_item", maxUses=-1
/// - Health Potion: source=Consumable, sourceId="health_potion", maxUses=3
/// - Speed Buff: source=Temporary, sourceId="haste_buff", expirationTime=10.0
/// </summary>
[System.Serializable]
public class AbilityInstance
{
    // ========================================
    // IDENTITY
    // ========================================

    /// <summary>
    /// Stable instance ID for save/load and tracking
    /// Format: "{definitionId}_{source}_{sourceId}_{guid}"
    /// Example: "fireball_equipment_staff_abc123"
    /// </summary>
    public string instanceId;

    /// <summary>
    /// The ability definition (ScriptableObject)
    /// Contains all static data (costs, effects, etc.)
    /// </summary>
    public AbilityDefinition definition;

    // ========================================
    // SOURCE TRACKING
    // ========================================

    /// <summary>
    /// Where this ability came from (determines lifecycle)
    /// </summary>
    public AbilitySource source;

    /// <summary>
    /// Source identifier for cleanup
    /// - Equipment: Item ID ("longbow_item")
    /// - Consumable: Item ID ("health_potion")
    /// - Temporary: Buff ID ("haste_buff")
    /// - Permanent: Empty string
    /// </summary>
    public string sourceId;

    // ========================================
    // USAGE TRACKING (Consumables)
    // ========================================

    /// <summary>
    /// Maximum uses for consumables (-1 = infinite)
    /// Example: Health potion with 3 uses
    /// </summary>
    public int maxUses = -1;

    /// <summary>
    /// Remaining uses for consumables (-1 = infinite)
    /// Decrements on each use, removed when reaches 0
    /// </summary>
    public int remainingUses = -1;

    // ========================================
    // EXPIRATION TRACKING (Temporary)
    // ========================================

    /// <summary>
    /// Absolute time when ability expires (-1 = permanent)
    /// Uses Time.time for deterministic expiration
    /// Example: Time.time + 30f = expires in 30 seconds
    /// </summary>
    public float expirationTime = -1f;

    // ========================================
    // COOLDOWN STATE
    // ========================================

    /// <summary>
    /// Absolute time when ability cooldown completes (-1 = ready)
    /// Uses Time.time for deterministic cooldown tracking
    /// Example: Time.time + 5f = ready in 5 seconds
    /// Benefits: No per-frame ticking, deterministic after load, cheaper multiplayer sync
    /// </summary>
    public float cooldownReadyTime = -1f;

    // ========================================
    // RUNTIME STATE
    // ========================================

    /// <summary>
    /// Is this instance currently active?
    /// False = pending removal
    /// </summary>
    public bool isActive = true;

    /// <summary>
    /// Time this instance was created (for debugging/analytics)
    /// </summary>
    public float creationTime;

    // ========================================
    // CONSTRUCTORS
    // ========================================

    /// <summary>
    /// Create a new ability instance
    /// </summary>
    public AbilityInstance(
        AbilityDefinition definition,
        AbilitySource source,
        string sourceId = "",
        int maxUses = -1,
        float durationSeconds = -1f)
    {
        // Guard clause
        if (definition == null)
        {
            Debug.LogError("[AbilityInstance] Cannot create instance with null definition!");
            return;
        }

        // Generate stable instance ID
        string guid = System.Guid.NewGuid().ToString().Substring(0, 8);
        this.instanceId = $"{definition.abilityId}_{source}_{sourceId}_{guid}";

        // Set core data
        this.definition = definition;
        this.source = source;
        this.sourceId = sourceId ?? "";

        // Usage tracking
        this.maxUses = maxUses;
        this.remainingUses = maxUses;

        // Expiration tracking
        if (durationSeconds > 0f)
        {
            this.expirationTime = Time.time + durationSeconds;
        }
        else
        {
            this.expirationTime = -1f;
        }

        // Runtime state
        this.isActive = true;
        this.creationTime = Time.time;
        this.cooldownReadyTime = -1f;  // Ready immediately
    }

    // ========================================
    // QUERY METHODS
    // ========================================

    /// <summary>
    /// Is this ability usable right now?
    /// Checks: active, has uses remaining, not expired
    /// </summary>
    public bool IsUsable()
    {
        // Guard clauses
        if (!isActive) return false;
        if (definition == null) return false;

        // Check uses remaining (consumables)
        if (maxUses >= 0 && remainingUses <= 0) return false;

        // Check expiration (temporary)
        if (expirationTime >= 0f && Time.time >= expirationTime) return false;

        // Check cooldown (absolute time comparison)
        if (cooldownReadyTime >= 0f && Time.time < cooldownReadyTime) return false;

        return true;
    }

    /// <summary>
    /// Should this instance be removed?
    /// Checks: deactivated, out of uses, expired
    /// </summary>
    public bool ShouldRemove()
    {
        // Deactivated
        if (!isActive) return true;

        // Out of uses (consumables)
        if (maxUses >= 0 && remainingUses <= 0) return true;

        // Expired (temporary)
        if (expirationTime >= 0f && Time.time >= expirationTime) return true;

        return false;
    }

    /// <summary>
    /// Is this a consumable with limited uses?
    /// </summary>
    public bool IsConsumable()
    {
        return source == AbilitySource.Consumable && maxUses >= 0;
    }

    /// <summary>
    /// Is this a temporary ability with expiration?
    /// </summary>
    public bool IsTemporary()
    {
        return source == AbilitySource.Temporary && expirationTime >= 0f;
    }

    /// <summary>
    /// Get remaining duration for temporary abilities (seconds)
    /// Returns -1 if not temporary or permanent
    /// </summary>
    public float GetRemainingDuration()
    {
        if (expirationTime < 0f) return -1f;

        float remaining = expirationTime - Time.time;
        return Mathf.Max(0f, remaining);
    }

    /// <summary>
    /// Get remaining uses as percentage (0-1)
    /// Returns 1.0 if infinite uses
    /// </summary>
    public float GetUsesPercent()
    {
        if (maxUses < 0) return 1f;
        if (maxUses == 0) return 0f;

        return (float)remainingUses / maxUses;
    }

    /// <summary>
    /// Is this instance expired? (Explicit runtime flag)
    /// </summary>
    public bool IsExpired => expirationTime >= 0f && Time.time >= expirationTime;

    /// <summary>
    /// Is this instance on cooldown? (Explicit runtime flag)
    /// </summary>
    public bool IsOnCooldown => cooldownReadyTime >= 0f && Time.time < cooldownReadyTime;

    /// <summary>
    /// Get remaining cooldown time (seconds)
    /// Returns 0 if ready
    /// </summary>
    public float GetRemainingCooldown()
    {
        if (cooldownReadyTime < 0f) return 0f;
        
        float remaining = cooldownReadyTime - Time.time;
        return Mathf.Max(0f, remaining);
    }

    // ========================================
    // MODIFICATION METHODS
    // ========================================

    /// <summary>
    /// Consume one use (for consumables)
    /// Returns: true if consumed, false if no uses left
    /// </summary>
    public bool ConsumeUse()
    {
        // Infinite uses
        if (maxUses < 0) return true;

        // No uses left
        if (remainingUses <= 0) return false;

        // Consume
        remainingUses--;
        return true;
    }

    /// <summary>
    /// Restore uses (for refills, etc.)
    /// </summary>
    public void RestoreUses(int amount)
    {
        if (maxUses < 0) return;  // Infinite, no restore needed

        remainingUses = Mathf.Min(remainingUses + amount, maxUses);
    }

    /// <summary>
    /// Extend expiration time (for temporary buffs)
    /// </summary>
    public void ExtendDuration(float additionalSeconds)
    {
        if (expirationTime < 0f) return;  // Permanent

        expirationTime += additionalSeconds;
    }

    /// <summary>
    /// Mark for removal
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
    }

    // ========================================
    // DEBUG
    // ========================================

    public override string ToString()
    {
        string usesStr = maxUses >= 0 ? $"{remainingUses}/{maxUses}" : "∞";
        string expireStr = expirationTime >= 0f ? $"{GetRemainingDuration():F1}s" : "∞";

        return $"[{definition?.abilityName}] Source:{source} Uses:{usesStr} Expires:{expireStr} Active:{isActive}";
    }
}