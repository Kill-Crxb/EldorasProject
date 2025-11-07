using UnityEngine;

/// <summary>
/// Adapter that connects DamageOut to RPGSecondaryStats.
/// Searches for RPGSecondaryStats from the Brain level (not from local position).
/// </summary>
public class RPGCombatStatsAdapter : MonoBehaviour, ICombatStatsProvider
{
    [Header("Manual Reference (Optional)")]
    [SerializeField] private RPGSecondaryStats secondaryStats;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private ControllerBrain brain;
    private bool isInitialized = false;

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // Find the Brain (should be parent of Component_Damage)
        brain = GetComponentInParent<ControllerBrain>();
        if (brain == null)
        {
            Debug.LogError($"[RPGCombatStatsAdapter] No ControllerBrain found in parent hierarchy!");
            return;
        }

        // If no manual reference, search from Brain level
        if (secondaryStats == null)
        {
            // Search DOWN from Brain to find RPGSecondaryStats
            secondaryStats = brain.GetComponentInChildren<RPGSecondaryStats>();
        }

        if (secondaryStats != null)
        {
            if (debugLogs)
            {
                Debug.Log($"[RPGCombatStatsAdapter] ✓ Found RPGSecondaryStats: {secondaryStats.name}");
            }
        }
        else
        {
            Debug.LogWarning($"[RPGCombatStatsAdapter] ✗ Could not find RPGSecondaryStats! " +
                           "DamageOut will use default values.");
        }

        isInitialized = true;
    }

    // === ICombatStatsProvider Implementation ===

    public float GetAttackPower()
    {
        if (!ValidateStats())
        {
            if (debugLogs)
                Debug.LogWarning("[RPGCombatStatsAdapter] No stats available, returning default power: 10");
            return 10f; // Default fallback
        }

        // Get MeleePower from RPGSecondaryStats
        float meleePower = secondaryStats.GetSecondaryStatFinalValue("MeleePower");

        if (debugLogs)
            Debug.Log($"[RPGCombatStatsAdapter] Attack Power: {meleePower:F1}");

        return meleePower;
    }

    public float GetCriticalChance()
    {
        if (!ValidateStats())
        {
            if (debugLogs)
                Debug.LogWarning("[RPGCombatStatsAdapter] No stats available, returning default crit chance: 5%");
            return 5f; // Default 5% crit chance
        }

        float critChance = secondaryStats.GetSecondaryStatFinalValue("MeleeCritChance");

        if (debugLogs)
            Debug.Log($"[RPGCombatStatsAdapter] Crit Chance: {critChance:F1}%");

        return critChance;
    }

    public float GetCriticalMultiplier()
    {
        if (!ValidateStats())
        {
            if (debugLogs)
                Debug.LogWarning("[RPGCombatStatsAdapter] No stats available, returning default crit multiplier: 1.5x");
            return 1.5f; // Default 150% crit damage
        }

        float critDamage = secondaryStats.GetSecondaryStatFinalValue("MeleeCritDamage");

        // Convert from percentage (e.g., 150) to multiplier (e.g., 1.5)
        float critMultiplier = critDamage / 100f;

        if (debugLogs)
            Debug.Log($"[RPGCombatStatsAdapter] Crit Multiplier: x{critMultiplier:F2}");

        return critMultiplier;
    }

    public float GetArmor()
    {
        if (!ValidateStats())
        {
            if (debugLogs)
                Debug.LogWarning("[RPGCombatStatsAdapter] No stats available, returning default armor: 0");
            return 0f;
        }

        float armor = secondaryStats.GetSecondaryStatFinalValue("Armor");

        if (debugLogs)
            Debug.Log($"[RPGCombatStatsAdapter] Armor: {armor:F1}");

        return armor;
    }

    public float GetArmorPenetration()
    {
        if (!ValidateStats())
        {
            if (debugLogs)
                Debug.LogWarning("[RPGCombatStatsAdapter] No stats available, returning default penetration: 0");
            return 0f;
        }

        float penetration = secondaryStats.GetSecondaryStatFinalValue("MeleePenetration");

        if (debugLogs)
            Debug.Log($"[RPGCombatStatsAdapter] Armor Penetration: {penetration:F1}");

        return penetration;
    }

    public float GetMagicResistance()
    {
        if (!ValidateStats())
        {
            if (debugLogs)
                Debug.LogWarning("[RPGCombatStatsAdapter] No stats available, returning default magic resist: 0");
            return 0f;
        }

        float magicResist = secondaryStats.GetSecondaryStatFinalValue("MagicResistance");

        if (debugLogs)
            Debug.Log($"[RPGCombatStatsAdapter] Magic Resistance: {magicResist:F1}");

        return magicResist;
    }

    private bool ValidateStats()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        return secondaryStats != null;
    }

    // === Inspector Helpers ===

    [ContextMenu("Debug: Test Get Stats")]
    private void DebugTestGetStats()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGCombatStatsAdapter] Test only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [RPGCombatStatsAdapter] Current Stats ===\n" +
                  $"Attack Power: {GetAttackPower():F1}\n" +
                  $"Crit Chance: {GetCriticalChance():F1}%\n" +
                  $"Crit Multiplier: x{GetCriticalMultiplier():F2}");
    }

    [ContextMenu("Debug: Show Component Paths")]
    private void DebugShowPaths()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGCombatStatsAdapter] Debug only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [RPGCombatStatsAdapter] Component Paths ===\n" +
                  $"This Adapter: {GetFullPath(transform)}\n" +
                  $"Brain: {(brain != null ? GetFullPath(brain.transform) : "NOT FOUND")}\n" +
                  $"RPGSecondaryStats: {(secondaryStats != null ? GetFullPath(secondaryStats.transform) : "NOT FOUND")}");
    }

    private string GetFullPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}