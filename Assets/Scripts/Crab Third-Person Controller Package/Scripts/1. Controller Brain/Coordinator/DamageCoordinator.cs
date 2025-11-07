using UnityEngine;

/// <summary>
/// Coordinates damage processing systems (DamageOut, DamageIn, and adapters).
/// Provides convenience API for damage operations.
/// All child modules remain independently accessible via Brain.
/// </summary>
public class DamageCoordinator : MonoBehaviour, IBrainModule, ISystemCoordinator
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    [Header("Auto-Discovered Components (Read-Only)")]
    [SerializeField, ReadOnly] private DamageOut damageOut;
    [SerializeField, ReadOnly] private DamageIn damageIn;

    [Header("Auto-Discovered Adapters (Read-Only)")]
    [SerializeField, ReadOnly] private MonoBehaviour statsAdapterComponent;
    [SerializeField, ReadOnly] private MonoBehaviour healthAdapterComponent;
    [SerializeField, ReadOnly] private MonoBehaviour defenseAdapterComponent;

    private ControllerBrain brain;
    private ICombatStatsProvider statsAdapter;
    private IHealthProvider healthAdapter;
    private IDefenseProvider defenseAdapter;

    // === IBrainModule Implementation ===

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Discover DamageOut and DamageIn
        damageOut = GetComponentInChildren<DamageOut>();
        damageIn = GetComponentInChildren<DamageIn>();

        // Discover adapters
        DiscoverAdapters();

        // Log initialization
        if (showDebugInfo)
        {
            LogInitialization();
        }
    }

    public void UpdateModule()
    {
        // DamageCoordinator doesn't need per-frame updates
        // Child modules handle their own updates
    }

    // === Adapter Discovery ===

    private void DiscoverAdapters()
    {
        // Find ICombatStatsProvider adapter
        var statsAdapterComp = GetComponentInChildren<ICombatStatsProvider>() as MonoBehaviour;
        if (statsAdapterComp != null)
        {
            statsAdapter = statsAdapterComp as ICombatStatsProvider;
            statsAdapterComponent = statsAdapterComp; // For inspector visibility
        }

        // Find IHealthProvider adapter
        var healthAdapterComp = GetComponentInChildren<IHealthProvider>() as MonoBehaviour;
        if (healthAdapterComp != null)
        {
            healthAdapter = healthAdapterComp as IHealthProvider;
            healthAdapterComponent = healthAdapterComp; // For inspector visibility
        }

        // Find IDefenseProvider adapter
        var defenseAdapterComp = GetComponentInChildren<IDefenseProvider>() as MonoBehaviour;
        if (defenseAdapterComp != null)
        {
            defenseAdapter = defenseAdapterComp as IDefenseProvider;
            defenseAdapterComponent = defenseAdapterComp; // For inspector visibility
        }
    }

    // === Public API - Component Access ===

    public DamageOut GetDamageOut() => damageOut;
    public DamageIn GetDamageIn() => damageIn;
    public ICombatStatsProvider GetStatsAdapter() => statsAdapter;
    public IHealthProvider GetHealthAdapter() => healthAdapter;
    public IDefenseProvider GetDefenseAdapter() => defenseAdapter;

    // === Public API - Convenience Methods ===

    // Health queries
    public bool IsAlive() => healthAdapter?.IsAlive() ?? true;
    public float GetCurrentHealth() => healthAdapter?.GetCurrentHealth() ?? 0f;
    public float GetMaxHealth() => healthAdapter?.GetMaxHealth() ?? 100f;
    public float GetHealthPercentage() => healthAdapter?.GetHealthPercentage() ?? 0f;

    // Defense queries
    public bool CanDefend() => defenseAdapter?.CanDefend() ?? false;
    public bool IsDefending() => (defenseAdapter?.IsBlocking() ?? false) || (defenseAdapter?.IsParrying() ?? false);
    public bool IsBlocking() => defenseAdapter?.IsBlocking() ?? false;
    public bool IsParrying() => defenseAdapter?.IsParrying() ?? false;

    // Combat stat queries
    public float GetAttackPower() => statsAdapter?.GetAttackPower() ?? 0f;
    public float GetArmor() => statsAdapter?.GetArmor() ?? 0f;
    public float GetMagicResistance() => statsAdapter?.GetMagicResistance() ?? 0f;
    public float GetCriticalChance() => statsAdapter?.GetCriticalChance() ?? 0f;
    public float GetCriticalMultiplier() => statsAdapter?.GetCriticalMultiplier() ?? 1.5f;

    // === Debug Helpers ===

    private void LogInitialization()
    {
        Debug.Log($"=== [DamageCoordinator] Initialized on {brain.name} ===\n" +
                  $"DamageOut: {(damageOut != null ? "✓" : "✗")}\n" +
                  $"DamageIn: {(damageIn != null ? "✓" : "✗")}\n" +
                  $"Stats Adapter: {(statsAdapter != null ? statsAdapterComponent.GetType().Name : "✗")}\n" +
                  $"Health Adapter: {(healthAdapter != null ? healthAdapterComponent.GetType().Name : "✗")}\n" +
                  $"Defense Adapter: {(defenseAdapter != null ? defenseAdapterComponent.GetType().Name : "✗")}");
    }

    [ContextMenu("Debug: Show Discovered Components")]
    private void DebugShowComponents()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageCoordinator] Only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [DamageCoordinator] Component Discovery Status ===\n" +
                  $"DamageOut: {(damageOut != null ? $"✓ ({damageOut.name})" : "✗ NOT FOUND")}\n" +
                  $"DamageIn: {(damageIn != null ? $"✓ ({damageIn.name})" : "✗ NOT FOUND")}\n" +
                  $"Stats Adapter: {(statsAdapter != null ? $"✓ ({statsAdapterComponent.GetType().Name})" : "✗ NOT FOUND")}\n" +
                  $"Health Adapter: {(healthAdapter != null ? $"✓ ({healthAdapterComponent.GetType().Name})" : "✗ NOT FOUND")}\n" +
                  $"Defense Adapter: {(defenseAdapter != null ? $"✓ ({defenseAdapterComponent.GetType().Name})" : "✗ NOT FOUND")}");
    }

    [ContextMenu("Debug: Show Current Stats")]
    private void DebugShowCurrentStats()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageCoordinator] Only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [DamageCoordinator] Current Stats ===\n" +
                  $"Entity: {brain.name}\n" +
                  $"Health: {GetCurrentHealth():F1}/{GetMaxHealth():F1} ({GetHealthPercentage():P0})\n" +
                  $"Alive: {IsAlive()}\n" +
                  $"Attack Power: {GetAttackPower():F1}\n" +
                  $"Armor: {GetArmor():F1}\n" +
                  $"Magic Resistance: {GetMagicResistance():F1}\n" +
                  $"Crit Chance: {GetCriticalChance():F1}%\n" +
                  $"Crit Multiplier: {GetCriticalMultiplier():F2}x\n" +
                  $"Can Defend: {CanDefend()}\n" +
                  $"Is Defending: {IsDefending()}\n" +
                  $"Is Blocking: {IsBlocking()}\n" +
                  $"Is Parrying: {IsParrying()}");
    }

    [ContextMenu("Debug: Test Damage System")]
    private void DebugTestDamageSystem()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageCoordinator] Only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [DamageCoordinator] Testing Damage System ===");

        // Test 1: Health before
        float healthBefore = GetCurrentHealth();
        Debug.Log($"1. Health Before: {healthBefore:F1}/{GetMaxHealth():F1}");

        // Test 2: Apply 25 damage
        if (damageIn != null)
        {
            damageIn.TakeDamage(25f, null);
            Debug.Log($"2. Applied 25 damage");
        }
        else
        {
            Debug.LogError("DamageIn not found!");
        }

        // Test 3: Health after
        float healthAfter = GetCurrentHealth();
        Debug.Log($"3. Health After: {healthAfter:F1}/{GetMaxHealth():F1}");
        Debug.Log($"4. Damage Taken: {healthBefore - healthAfter:F1}");

        // Test 4: Test healing
        if (healthAdapter != null)
        {
            healthAdapter.ApplyHealing(50f);
            Debug.Log($"5. Applied 50 healing");
            Debug.Log($"6. Health Now: {GetCurrentHealth():F1}/{GetMaxHealth():F1}");
        }
    }
}