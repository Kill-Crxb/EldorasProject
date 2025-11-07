using UnityEngine;

/// <summary>
/// Universal damage calculation module.
/// Works with any stat system through ICombatStatsProvider adapter.
/// 
/// Design Philosophy:
/// - Knows nothing about specific stat implementations (RPG, simple, etc.)
/// - Uses adapter pattern to get combat stats
/// - Calculates critical hits, multipliers, damage types
/// - Outputs standardized CombatDamagePacket
/// 
/// Usage:
/// 1. Add to entity under Component_Brain
/// 2. Assign an adapter that implements ICombatStatsProvider
/// 3. Call CalculateDamage() from combat systems
/// 4. Send resulting packet to targets via DamageIn
/// </summary>
public class DamageOut : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    [Header("Damage Settings")]
    [SerializeField] private DamageType defaultDamageType = DamageType.Physical;
    [SerializeField] private bool allowCriticalHits = true;
    [SerializeField] private bool debugDamageCalculations = false;

    [Header("Adapter (Auto-discovered or Manual)")]
    [SerializeField] private MonoBehaviour combatStatsAdapter;

    private ControllerBrain brain;
    private ICombatStatsProvider statsProvider;
    private Transform entityTransform;

    // === IBrainModule Implementation ===

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        entityTransform = brain.transform.parent ?? brain.transform;

        // Try to find combat stats adapter - SEARCH FROM BRAIN LEVEL
        if (combatStatsAdapter == null)
        {
            combatStatsAdapter = brain.GetComponentInChildren<ICombatStatsProvider>() as MonoBehaviour;
        }

        if (combatStatsAdapter != null && combatStatsAdapter is ICombatStatsProvider provider)
        {
            statsProvider = provider;
            if (debugDamageCalculations)
            {
                Debug.Log($"[DamageOut] ✓ Initialized with adapter: {combatStatsAdapter.GetType().Name}");
            }
        }
        else
        {
            Debug.LogWarning($"[DamageOut] ✗ No ICombatStatsProvider found on {brain.name}. " +
                           "Damage calculations will use default values.");
        }
    }

    public void UpdateModule()
    {
        // DamageOut doesn't need per-frame updates
        // It's called on-demand when attacks happen
    }

    // === Public API ===

    /// <summary>
    /// Calculate damage from attack data.
    /// This is the main method combat systems call.
    /// </summary>
    public CombatDamagePacket CalculateDamage(CombatAttackData attackData)
    {
        return CalculateDamage(attackData, defaultDamageType);
    }

    /// <summary>
    /// Calculate damage with specific damage type.
    /// </summary>
    public CombatDamagePacket CalculateDamage(CombatAttackData attackData, DamageType damageType)
    {
        if (!isEnabled)
        {
            Debug.LogWarning("[DamageOut] Module is disabled!");
            return null;
        }

        if (attackData == null)
        {
            Debug.LogError("[DamageOut] Attack data is null!");
            return null;
        }

        // Step 1: Get base attack power from adapter
        float basePower = GetAttackPower(damageType);

        // Step 2: Apply weapon multiplier
        float weaponModifiedDamage = basePower * attackData.weaponDamageMultiplier;

        // Step 3: Apply combo multiplier
        float comboModifiedDamage = weaponModifiedDamage * attackData.comboMultiplier;

        // Step 4: Apply heavy attack bonus
        float heavyAttackMultiplier = attackData.isHeavyAttack ? 1.5f : 1.0f;
        float baseDamage = comboModifiedDamage * heavyAttackMultiplier;

        // Step 5: Check for critical hit
        bool isCrit = CheckCriticalHit(damageType);
        float critMultiplier = isCrit ? GetCriticalMultiplier(damageType) : 1.0f;

        // Step 6: Calculate final damage
        float finalDamage = baseDamage * critMultiplier;

        // Step 7: Calculate attack direction
        Vector3 attackDirection = Vector3.zero;
        if (entityTransform != null && attackData.hitPoint != Vector3.zero)
        {
            attackDirection = (attackData.hitPoint - entityTransform.position).normalized;
        }

        // Step 8: Create damage packet
        CombatDamagePacket packet = new CombatDamagePacket(
            baseDamage: baseDamage,
            finalDamage: finalDamage,
            isCriticalHit: isCrit,
            criticalMultiplier: critMultiplier,
            damageType: damageType,
            attacker: entityTransform,
            attackerId: brain.name,
            hitPoint: attackData.hitPoint,
            hitNormal: attackData.hitNormal,
            attackDirection: attackDirection,
            comboCount: attackData.comboCount,
            isHeavyAttack: attackData.isHeavyAttack,
            weaponId: attackData.weaponId
        );

        if (debugDamageCalculations)
        {
            LogDamageCalculation(attackData, packet);
        }

        return packet;
    }

    /// <summary>
    /// Quick damage calculation without attack data.
    /// Useful for simple damage sources (traps, environment, etc.)
    /// </summary>
    public CombatDamagePacket CalculateSimpleDamage(float baseDamage, Vector3 hitPoint)
    {
        CombatAttackData simpleAttack = CombatAttackData.CreateBasic(entityTransform, hitPoint);

        // Override with custom base damage by modifying weapon multiplier
        float currentPower = GetAttackPower(defaultDamageType);
        if (currentPower > 0)
        {
            simpleAttack.weaponDamageMultiplier = baseDamage / currentPower;
        }

        return CalculateDamage(simpleAttack, defaultDamageType);
    }

    // === Helper Methods ===

    private float GetAttackPower(DamageType damageType)
    {
        if (statsProvider == null)
            return 10f; // Default fallback

        return statsProvider.GetAttackPower();
    }

    private bool CheckCriticalHit(DamageType damageType)
    {
        if (!allowCriticalHits || statsProvider == null)
            return false;

        float critChance = statsProvider.GetCriticalChance();
        float roll = Random.Range(0f, 100f);

        return roll <= critChance;
    }

    private float GetCriticalMultiplier(DamageType damageType)
    {
        if (statsProvider == null)
            return 1.5f; // Default crit multiplier

        return statsProvider.GetCriticalMultiplier();
    }

    private void LogDamageCalculation(CombatAttackData attackData, CombatDamagePacket packet)
    {
        Debug.Log($"=== [DamageOut] Damage Calculation ===\n" +
                  $"Attacker: {brain.name}\n" +
                  $"Base Damage: {packet.baseDamage:F1}\n" +
                  $"Final Damage: {packet.finalDamage:F1}\n" +
                  $"Critical Hit: {packet.isCriticalHit} (x{packet.criticalMultiplier:F2})\n" +
                  $"Damage Type: {packet.damageType}\n" +
                  $"Combo: {packet.comboCount}x (x{attackData.comboMultiplier:F2})\n" +
                  $"Heavy Attack: {packet.isHeavyAttack}\n" +
                  $"Weapon: {packet.weaponId}");
    }

    // === Inspector Helpers ===

    [ContextMenu("Test: Calculate Sample Damage")]
    private void TestCalculateDamage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageOut] Test only works in Play Mode!");
            return;
        }

        Vector3 testHitPoint = entityTransform.position + entityTransform.forward * 2f;
        CombatAttackData testData = CombatAttackData.CreateBasic(entityTransform, testHitPoint);

        CombatDamagePacket result = CalculateDamage(testData);

        if (result != null)
        {
            Debug.Log($"[DamageOut] Test damage: {result.finalDamage:F1} " +
                     $"(Crit: {result.isCriticalHit})");
        }
    }

    [ContextMenu("Debug: Show Adapter Status")]
    private void DebugShowAdapter()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageOut] Debug only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [DamageOut] Adapter Status ===\n" +
                  $"Brain: {(brain != null ? brain.name : "NOT FOUND")}\n" +
                  $"Stats Provider: {(statsProvider != null ? statsProvider.GetType().Name : "NOT FOUND")}\n" +
                  $"Adapter MonoBehaviour: {(combatStatsAdapter != null ? combatStatsAdapter.name : "NOT FOUND")}");
    }
}