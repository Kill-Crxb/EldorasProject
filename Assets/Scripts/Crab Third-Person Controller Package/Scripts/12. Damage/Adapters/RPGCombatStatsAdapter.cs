using UnityEngine;

/// <summary>
/// Adapter that bridges RPGSecondaryStats to the universal damage system.
/// Implements ICombatStatsProvider to provide combat statistics for damage calculations.
/// 
/// Design:
/// - Translates RPG stat system to universal combat interface
/// - Auto-discovers RPGSecondaryStats from Brain
/// - Allows different attack types (melee vs magic)
/// - Works with universal DamageOut module
/// 
/// Usage:
/// 1. Add to entity (as child of Component_Damage or Component_Brain)
/// 2. Automatically discovers RPGSecondaryStats
/// 3. DamageOut finds this adapter automatically
/// </summary>
public class RPGCombatStatsAdapter : MonoBehaviour, ICombatStatsProvider
{
    [Header("Adapter Settings")]
    [SerializeField] private AttackType attackType = AttackType.Melee;
    [SerializeField] private bool debugAdapter = false;

    [Header("Manual References (Optional)")]
    [SerializeField] private RPGSecondaryStats rpgStats;

    private ControllerBrain brain;
    private bool isInitialized = false;

    // === Lifecycle ===

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // Find Brain
        brain = GetComponentInParent<ControllerBrain>();
        if (brain == null)
        {
            Debug.LogError($"[RPGCombatStatsAdapter] No ControllerBrain found in parent! " +
                          "Adapter must be child of Brain.");
            return;
        }

        // Auto-discover RPGSecondaryStats if not manually assigned
        if (rpgStats == null)
        {
            rpgStats = brain.RPGSecondaryStats;
        }

        if (rpgStats == null)
        {
            Debug.LogError($"[RPGCombatStatsAdapter] No RPGSecondaryStats found on {brain.name}! " +
                          "Cannot provide combat stats.");
            return;
        }

        isInitialized = true;

        if (debugAdapter)
        {
            Debug.Log($"[RPGCombatStatsAdapter] Initialized on {brain.name} " +
                     $"(Attack Type: {attackType})");
        }
    }

    // === ICombatStatsProvider Implementation ===

    public float GetAttackPower()
    {
        if (!ValidateStats()) return 10f;

        float power = attackType == AttackType.Melee
            ? rpgStats.MeleePowerFinal
            : rpgStats.MagicPowerFinal;

        if (debugAdapter)
            Debug.Log($"[Adapter] Attack Power: {power:F1}");

        return power;
    }

    public float GetCriticalChance()
    {
        if (!ValidateStats()) return 5f;

        float crit = attackType == AttackType.Melee
            ? rpgStats.MeleeCritChanceFinal
            : rpgStats.MagicCritChanceFinal;

        if (debugAdapter)
            Debug.Log($"[Adapter] Crit Chance: {crit:F1}%");

        return crit;
    }

    public float GetCriticalMultiplier()
    {
        if (!ValidateStats()) return 1.5f;

        float critMult = attackType == AttackType.Melee
            ? rpgStats.MeleeCritDamageFinal
            : rpgStats.MagicCritDamageFinal;

        if (debugAdapter)
            Debug.Log($"[Adapter] Crit Multiplier: {critMult:F2}x");

        return critMult;
    }

    public float GetArmorPenetration()
    {
        if (!ValidateStats()) return 0f;

        float pen = attackType == AttackType.Melee
            ? rpgStats.MeleePenetrationFinal
            : rpgStats.MagicPenetrationFinal;

        if (debugAdapter)
            Debug.Log($"[Adapter] Armor Penetration: {pen:F1}");

        return pen;
    }

    public float GetArmor()
    {
        if (!ValidateStats()) return 0f;

        return rpgStats.ArmorFinal;
    }

    public float GetMagicResistance()
    {
        if (!ValidateStats()) return 0f;

        return rpgStats.MagicResistanceFinal;
    }

    // === Helper Methods ===

    private bool ValidateStats()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (rpgStats == null)
        {
            Debug.LogWarning("[RPGCombatStatsAdapter] RPGSecondaryStats not available!");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Change attack type at runtime (e.g., when switching between melee and magic)
    /// </summary>
    public void SetAttackType(AttackType newType)
    {
        attackType = newType;

        if (debugAdapter)
        {
            Debug.Log($"[RPGCombatStatsAdapter] Attack type changed to: {attackType}");
        }
    }

    public AttackType GetAttackType() => attackType;

    // === Inspector Helpers ===

    [ContextMenu("Debug: Show All Combat Stats")]
    private void DebugShowAllStats()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[Adapter] Only works in Play Mode!");
            return;
        }

        if (!ValidateStats())
        {
            Debug.LogError("[Adapter] Cannot show stats - validation failed!");
            return;
        }

        Debug.Log($"=== RPG Combat Stats Adapter ({attackType}) ===\n" +
                  $"Attack Power: {GetAttackPower():F1}\n" +
                  $"Crit Chance: {GetCriticalChance():F1}%\n" +
                  $"Crit Multiplier: {GetCriticalMultiplier():F2}x\n" +
                  $"Armor Penetration: {GetArmorPenetration():F1}\n" +
                  $"Armor: {GetArmor():F1}\n" +
                  $"Magic Resistance: {GetMagicResistance():F1}");
    }

    [ContextMenu("Switch to Melee")]
    private void SwitchToMelee()
    {
        SetAttackType(AttackType.Melee);
    }

    [ContextMenu("Switch to Magic")]
    private void SwitchToMagic()
    {
        SetAttackType(AttackType.Magic);
    }
}

/// <summary>
/// Attack type determines which stats to use from RPGSecondaryStats
/// </summary>
public enum AttackType
{
    Melee,  // Uses MeleePower, MeleeCrit, etc.
    Magic   // Uses MagicPower, MagicCrit, etc.
}
