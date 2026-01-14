using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// GOAP-Compatible Humanoid Melee Combat Behavior
/// 
/// Designed for GOAP architecture where:
/// - GOAP Goals handle positioning (Approach, Circle, Retreat, Flank)
/// - This behavior handles ATTACK EXECUTION ONLY
/// 
/// Key Features:
/// - Uses AbilityLoadoutModule (same as player)
/// - Tactical ability selection based on distance, resources, context
/// - Compatible with both GOAP AttackGoal and Fate System
/// - No positioning logic (GOAP goals handle that)
/// 
/// Setup:
/// 1. Add to Enemy prefab's Component_AI (via AISystem)
/// 2. NPCConfigurationHandler adds this via archetype.combatBehaviorClassName
/// 3. AttackGoal calls PerformAttack() when ready to attack
/// </summary>
public class HumanoidMeleeCombatBehavior : AICombatBehaviorModule
{
    [Header("Combat Ranges")]
    [SerializeField] private float closeRange = 2f;       // Grapples, quick strikes
    [SerializeField] private float midRange = 3.5f;       // Standard attacks
    [SerializeField] private float farRange = 5f;         // Lunges, gap closers

    [Header("Attack Strategy")]
    [Tooltip("Higher = more special abilities, lower = more basic attacks")]
    [SerializeField][Range(0f, 1f)] private float specialAbilityFrequency = 0.3f;

    [Tooltip("Chance to use defensive ability when low health")]
    [SerializeField][Range(0f, 1f)] private float defensiveAbilityChance = 0.6f;

    [Tooltip("Low health threshold for defensive behavior")]
    [SerializeField][Range(0f, 1f)] private float lowHealthThreshold = 0.3f;

    [Header("Resource Management")]
    [Tooltip("Minimum stamina % to use special abilities")]
    [SerializeField][Range(0f, 1f)] private float minStaminaForSpecials = 0.4f;

    [Header("Cooldowns")]
    [SerializeField] private float specialAbilityCooldown = 2f;
    [SerializeField] private float comboResetDelay = 3f;

    // Core References
    private IAbilityProvider abilityProvider;
    private AbilityLoadoutModule loadoutModule;
    private IHealthProvider healthProvider;
    private IResourceProvider resourceProvider;

    // Timers
    private float lastSpecialAbilityTime;
    private float lastComboTime;

    // State tracking
    private int consecutiveBasicAttacks = 0;

    #region Initialization

    protected override void OnInitialize()
    {
        base.OnInitialize();

        // Get ability system
        abilityProvider = brain.GetModuleImplementing<IAbilityProvider>();
        if (abilityProvider == null)
        {
            Debug.LogError($"[HumanoidMeleeCombatBehavior] IAbilityProvider not found on {gameObject.name}!");
            isEnabled = false;
            return;
        }

        loadoutModule = brain.GetModule<AbilityLoadoutModule>();
        if (loadoutModule == null)
        {
            Debug.LogError($"[HumanoidMeleeCombatBehavior] AbilityLoadoutModule not found on {gameObject.name}!");
            isEnabled = false;
            return;
        }

        // Get health/resources
        healthProvider = brain.GetModuleImplementing<IHealthProvider>();
        resourceProvider = brain.GetModuleImplementing<IResourceProvider>();

        if (debugMode)
        {
            Debug.Log($"[HumanoidMeleeCombatBehavior] Initialized for GOAP on {gameObject.name}");
        }
    }

    #endregion

    #region GOAP-Compatible Interface (Called by AttackGoal)

    /// <summary>
    /// Main entry point for GOAP AttackGoal
    /// Executes an attack based on tactical situation
    /// </summary>
    public void PerformAttack(Transform target, GOAPContext context)
    {
        if (target == null || context == null)
        {
            Debug.LogWarning("[HumanoidMeleeCombatBehavior] PerformAttack called with null target or context");
            return;
        }

        float distance = context.distanceToTarget;
        float healthPercent = context.healthPercent;
        float staminaPercent = GetStaminaPercent();

        // Decision tree for attack selection

        // 1. Low health → Try defensive ability
        if (healthPercent < lowHealthThreshold && ShouldUseDefensiveAbility())
        {
            if (TryUseDefensiveAbility())
            {
                if (debugMode)
                    Debug.Log("[HumanoidMeleeCombatBehavior] Used defensive ability (low health)");
                return;
            }
        }

        // 2. Good conditions → Try special ability
        if (ShouldUseSpecialAbility(staminaPercent))
        {
            if (TryUseSpecialAbility(distance))
            {
                lastSpecialAbilityTime = Time.time;
                consecutiveBasicAttacks = 0;

                if (debugMode)
                    Debug.Log("[HumanoidMeleeCombatBehavior] Used special ability");
                return;
            }
        }

        // 3. Default → Basic attack combo
        if (CanAttack())
        {
            ExecuteBasicAttack();
            consecutiveBasicAttacks++;
            lastComboTime = Time.time;

            if (debugMode)
                Debug.Log($"[HumanoidMeleeCombatBehavior] Used basic attack (combo step {consecutiveBasicAttacks})");
        }
        else
        {
            if (debugMode)
                Debug.Log("[HumanoidMeleeCombatBehavior] Cannot attack (cooldown or busy)");
        }
    }

    /// <summary>
    /// Simplified PerformAttack for AttackGoal that doesn't have GOAPContext
    /// </summary>
    public void PerformAttack(Transform target)
    {
        if (target == null) return;

        // Create minimal context
        float distance = Vector3.Distance(transform.position, target.position);
        float healthPercent = healthProvider != null ?
            healthProvider.GetCurrentHealth() / healthProvider.GetMaxHealth() : 1f;

        // Build simple context
        var simpleContext = new GOAPContext();
        simpleContext.distanceToTarget = distance;
        simpleContext.healthPercent = healthPercent;
        simpleContext.target = target;

        PerformAttack(target, simpleContext);
    }

    #endregion

    #region Attack Execution

    /// <summary>
    /// Execute basic attack from BasicAttack slot (combo chain)
    /// </summary>
    private void ExecuteBasicAttack()
    {
        var basicAttack = loadoutModule.GetCurrentAbilityForSlot("BasicAttack");

        if (basicAttack == null)
        {
            Debug.LogWarning("[HumanoidMeleeCombatBehavior] No ability in BasicAttack slot!");
            return;
        }

        if (abilityProvider.CanUseAbility(basicAttack.abilityId))
        {
            // Use ability
            abilityProvider.UseAbility(basicAttack.abilityId);
            loadoutModule.MarkSlotUsed("BasicAttack");

            // Advance combo if it's a combo chain
            var slotData = loadoutModule.GetSlotData("BasicAttack");
            if (slotData != null && slotData.IsCombo)
            {
                loadoutModule.AdvanceCombo("BasicAttack");
            }

            RecordAttack(); // Base class tracking
        }
    }

    /// <summary>
    /// Try to use a special ability based on distance and situation
    /// </summary>
    private bool TryUseSpecialAbility(float distance)
    {
        // Define ability slots by distance preference
        List<string> closeRangeSlots = new List<string> { "Z", "X" };    // Quick abilities
        List<string> midRangeSlots = new List<string> { "Q", "C" };      // Standard abilities  
        List<string> farRangeSlots = new List<string> { "V" };           // Gap closers

        // Choose slots based on distance
        List<string> preferredSlots = new List<string>();

        if (distance <= closeRange)
        {
            preferredSlots.AddRange(closeRangeSlots);
            preferredSlots.AddRange(midRangeSlots);
        }
        else if (distance <= midRange)
        {
            preferredSlots.AddRange(midRangeSlots);
            preferredSlots.AddRange(closeRangeSlots);
        }
        else // far range
        {
            preferredSlots.AddRange(farRangeSlots);
            preferredSlots.AddRange(midRangeSlots);
        }

        // Try each slot in order of preference
        foreach (string slot in preferredSlots)
        {
            var ability = loadoutModule.GetCurrentAbilityForSlot(slot);

            if (ability != null && abilityProvider.CanUseAbility(ability.abilityId))
            {
                abilityProvider.UseAbility(ability.abilityId);
                loadoutModule.MarkSlotUsed(slot);

                if (debugMode)
                    Debug.Log($"[HumanoidMeleeCombatBehavior] Used special [{slot}]: {ability.abilityName} at {distance:F1}m");

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Try to use a defensive/support ability (healing, buff, defensive stance)
    /// </summary>
    private bool TryUseDefensiveAbility()
    {
        // Check defensive slots (typically V, C for support/defense)
        string[] defensiveSlots = { "V", "C", "X" };

        foreach (string slot in defensiveSlots)
        {
            var ability = loadoutModule.GetCurrentAbilityForSlot(slot);

            // Check if ability is defensive type (would need metadata on abilities)
            // For now, just try to use any available ability in defensive slots
            if (ability != null && abilityProvider.CanUseAbility(ability.abilityId))
            {
                abilityProvider.UseAbility(ability.abilityId);
                loadoutModule.MarkSlotUsed(slot);

                if (debugMode)
                    Debug.Log($"[HumanoidMeleeCombatBehavior] Used defensive [{slot}]: {ability.abilityName}");

                return true;
            }
        }

        return false;
    }

    #endregion

    #region Decision Logic

    private bool ShouldUseSpecialAbility(float staminaPercent)
    {
        // Don't use specials if low stamina
        if (staminaPercent < minStaminaForSpecials)
            return false;

        // On cooldown
        if (Time.time - lastSpecialAbilityTime < specialAbilityCooldown)
            return false;

        // Use frequency-based randomness
        if (Random.value > specialAbilityFrequency)
            return false;

        // Mix it up - use special after several basic attacks
        if (consecutiveBasicAttacks >= 3 && Random.value > 0.3f)
            return true;

        return true;
    }

    private bool ShouldUseDefensiveAbility()
    {
        // Random chance when low health
        return Random.value < defensiveAbilityChance;
    }

    private float GetStaminaPercent()
    {
        if (resourceProvider == null)
            return 1f;

        float current = resourceProvider.GetResource(ResourceDefinition.Stamina);
        float max = resourceProvider.GetMaxResource(ResourceDefinition.Stamina);

        return max > 0 ? current / max : 0f;
    }

    #endregion

    #region Legacy AIModule Support (Optional - for backwards compatibility)

    /// <summary>
    /// Legacy UpdateCombat - NOT USED in GOAP
    /// Kept for backwards compatibility if archetype still uses old AIModule
    /// </summary>
    public override void UpdateCombat(Transform target)
    {
        // In GOAP architecture, this should NOT be called
        // AttackGoal calls PerformAttack() instead

        if (debugMode)
            Debug.LogWarning("[HumanoidMeleeCombatBehavior] UpdateCombat called - should use GOAP instead!");
    }

    #endregion

    #region Utility

    /// <summary>
    /// Reset combo if too much time passed
    /// </summary>
    private void Update()
    {
        if (Time.time - lastComboTime > comboResetDelay)
        {
            consecutiveBasicAttacks = 0;
        }
    }

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        if (!debugMode) return;

        // Draw attack ranges
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, closeRange);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, midRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, farRange);
    }

    #endregion
}