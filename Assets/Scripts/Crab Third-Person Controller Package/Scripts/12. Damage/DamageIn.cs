using UnityEngine;

/// <summary>
/// Universal damage receiving module.
/// Works with any health/defense system through adapter interfaces.
/// 
/// Design Philosophy:
/// - Knows nothing about specific health implementations (RPGResources, simple health, etc.)
/// - Uses IHealthProvider for health modification
/// - Uses IDefenseProvider or IDefenseCapability for defense processing
/// - Fires events for feedback systems (VFX, SFX, UI)
/// 
/// Usage:
/// 1. Add to entity under Component_Brain
/// 2. Assign adapters that implement IHealthProvider and IDefenseProvider
/// 3. Call TakeDamage() from external sources (weapons, projectiles, etc.)
/// 4. Subscribe to damage events for feedback
/// </summary>
public class DamageIn : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    [Header("Defense Settings")]
    [SerializeField] private bool allowDefense = true;
    [SerializeField] private bool debugDamageReceived = false;

    [Header("Adapters (Auto-discovered or Manual)")]
    [SerializeField] private MonoBehaviour healthAdapter;
    [SerializeField] private MonoBehaviour defenseAdapter;

    private ControllerBrain brain;
    private IHealthProvider healthProvider;
    private IDefenseProvider defenseProvider;
    private IDefenseCapability defenseCapability;
    private Transform entityTransform;

    // === Events ===

    /// <summary>Fired when damage is received (after defense calculations)</summary>
    public event System.Action<CombatDamagePacket, float> OnDamageReceived;

    /// <summary>Fired when damage is blocked/reduced by defense</summary>
    public event System.Action<CombatDamagePacket, float> OnDamageBlocked;

    /// <summary>Fired when this entity dies</summary>
    public event System.Action<CombatDamagePacket> OnDeath;

    // === Properties ===

    public bool IsAlive => healthProvider?.IsAlive() ?? true;
    public float CurrentHealth => healthProvider?.GetCurrentHealth() ?? 0f;
    public float MaxHealth => healthProvider?.GetMaxHealth() ?? 100f;
    public float HealthPercentage => healthProvider?.GetHealthPercentage() ?? 1f;

    // === IBrainModule Implementation ===

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        entityTransform = brain.transform.parent ?? brain.transform;

        // Try to find health adapter - SEARCH FROM BRAIN LEVEL
        if (healthAdapter == null)
        {
            healthAdapter = brain.GetComponentInChildren<IHealthProvider>() as MonoBehaviour;
        }

        if (healthAdapter != null && healthAdapter is IHealthProvider provider)
        {
            healthProvider = provider;
            if (debugDamageReceived)
            {
                Debug.Log($"[DamageIn] ✓ Health adapter found: {healthAdapter.GetType().Name}");
            }
        }
        else
        {
            Debug.LogWarning($"[DamageIn] ✗ No IHealthProvider found on {brain.name}. " +
                           "This entity cannot take damage!");
        }

        // Try to find defense adapter (optional) - SEARCH FROM BRAIN LEVEL
        if (defenseAdapter == null)
        {
            defenseAdapter = brain.GetComponentInChildren<IDefenseProvider>() as MonoBehaviour;

            // If no IDefenseProvider, try IDefenseCapability
            if (defenseAdapter == null)
            {
                var defenseCapabilityComp = brain.GetComponentInChildren<IDefenseCapability>() as MonoBehaviour;
                if (defenseCapabilityComp != null)
                {
                    defenseCapability = defenseCapabilityComp as IDefenseCapability;
                    if (debugDamageReceived)
                    {
                        Debug.Log($"[DamageIn] ✓ Defense capability found: {defenseCapabilityComp.GetType().Name}");
                    }
                }
            }
        }

        if (defenseAdapter != null && defenseAdapter is IDefenseProvider defProvider)
        {
            defenseProvider = defProvider;
            if (debugDamageReceived)
            {
                Debug.Log($"[DamageIn] ✓ Defense adapter found: {defenseAdapter.GetType().Name}");
            }
        }
    }

    public void UpdateModule()
    {
        // DamageIn doesn't need per-frame updates
        // It's called on-demand when damage is received
    }

    // === Public API ===

    /// <summary>
    /// Receive and process a damage packet.
    /// This is the main method external sources call.
    /// </summary>
    public bool TakeDamage(CombatDamagePacket damagePacket)
    {
        if (!isEnabled)
        {
            if (debugDamageReceived)
                Debug.LogWarning("[DamageIn] Module is disabled!");
            return false;
        }

        if (damagePacket == null)
        {
            Debug.LogError("[DamageIn] Damage packet is null!");
            return false;
        }

        if (healthProvider == null)
        {
            Debug.LogError("[DamageIn] No health provider! Cannot take damage.");
            return false;
        }

        if (!IsAlive)
        {
            if (debugDamageReceived)
                Debug.Log("[DamageIn] Already dead, ignoring damage.");
            return false;
        }

        // Step 1: Get initial damage
        float incomingDamage = damagePacket.finalDamage;
        float originalDamage = incomingDamage;

        // Step 2: Process through defense systems (if allowed)
        if (allowDefense && damagePacket.damageType != DamageType.True)
        {
            incomingDamage = ProcessDefense(incomingDamage, damagePacket);
        }

        // Step 3: Calculate blocked damage
        float blockedDamage = originalDamage - incomingDamage;

        // Step 4: Apply damage to health
        float healthBefore = healthProvider.GetCurrentHealth();
        healthProvider.ApplyDamage(incomingDamage);
        float healthAfter = healthProvider.GetCurrentHealth();
        float actualDamageDealt = healthBefore - healthAfter;

        // Step 5: Fire events
        OnDamageReceived?.Invoke(damagePacket, actualDamageDealt);

        if (blockedDamage > 0.1f)
        {
            OnDamageBlocked?.Invoke(damagePacket, blockedDamage);
        }

        // Step 6: Check for death
        if (!IsAlive)
        {
            OnDeath?.Invoke(damagePacket);
        }

        // Step 7: Debug logging
        if (debugDamageReceived)
        {
            LogDamageReceived(damagePacket, originalDamage, blockedDamage, actualDamageDealt);
        }

        return true;
    }

    /// <summary>
    /// Simple damage application without packet (for legacy systems or simple damage)
    /// </summary>
    public bool TakeDamage(float damage, Transform attacker = null)
    {
        Vector3 hitPoint = entityTransform.position;
        CombatDamagePacket simplePacket = CombatDamagePacket.CreateSimple(damage, attacker, hitPoint);
        return TakeDamage(simplePacket);
    }

    /// <summary>
    /// Heal this entity
    /// </summary>
    public void Heal(float amount)
    {
        if (healthProvider == null)
        {
            Debug.LogError("[DamageIn] No health provider! Cannot heal.");
            return;
        }

        healthProvider.ApplyHealing(amount);

        if (debugDamageReceived)
        {
            Debug.Log($"[DamageIn] {brain.name} healed for {amount:F1}. " +
                     $"Health: {CurrentHealth:F1}/{MaxHealth:F1}");
        }
    }

    // === Helper Methods ===

    private float ProcessDefense(float incomingDamage, CombatDamagePacket packet)
    {
        float processedDamage = incomingDamage;

        // Try IDefenseProvider first (priority)
        if (defenseProvider != null && defenseProvider.CanDefend())
        {
            processedDamage = defenseProvider.ProcessIncomingDamage(incomingDamage, packet.attackDirection);
        }
        // Fall back to IDefenseCapability
        else if (defenseCapability != null && defenseCapability.CanDefend)
        {
            float defenseMultiplier = defenseCapability.GetDefensiveMultiplier(packet.attackDirection);
            processedDamage = incomingDamage * defenseMultiplier;
        }

        return processedDamage;
    }

    private void LogDamageReceived(CombatDamagePacket packet, float originalDamage, float blockedDamage, float actualDamage)
    {
        string defenseInfo = blockedDamage > 0.1f
            ? $"Blocked: {blockedDamage:F1} | "
            : "";

        Debug.Log($"=== [DamageIn] Damage Received ===\n" +
                  $"Target: {brain.name}\n" +
                  $"Attacker: {packet.attackerId}\n" +
                  $"Original Damage: {originalDamage:F1}\n" +
                  $"{defenseInfo}" +
                  $"Actual Damage: {actualDamage:F1}\n" +
                  $"Critical: {packet.isCriticalHit}\n" +
                  $"Damage Type: {packet.damageType}\n" +
                  $"Health: {CurrentHealth:F1}/{MaxHealth:F1} ({HealthPercentage:P0})\n" +
                  $"Alive: {IsAlive}");
    }

    // === Inspector Helpers ===

    [ContextMenu("Test: Take 10 Damage")]
    private void TestTakeDamage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageIn] Test only works in Play Mode!");
            return;
        }

        TakeDamage(10f, null);
    }

    [ContextMenu("Test: Heal 25 HP")]
    private void TestHeal()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageIn] Test only works in Play Mode!");
            return;
        }

        Heal(25f);
    }

    [ContextMenu("Debug: Show Current Health")]
    private void DebugShowHealth()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[DamageIn] Health info only available in Play Mode!");
            return;
        }

        Debug.Log($"[DamageIn] {brain.name} Health: {CurrentHealth:F1}/{MaxHealth:F1} " +
                 $"({HealthPercentage:P0}) - Alive: {IsAlive}");
    }

    [ContextMenu("Debug: Show Adapters Status")]
    private void DebugShowAdapters()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DamageIn] Debug only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [DamageIn] Adapters Status ===\n" +
                  $"Brain: {(brain != null ? brain.name : "NOT FOUND")}\n" +
                  $"Health Provider: {(healthProvider != null ? healthProvider.GetType().Name : "NOT FOUND")}\n" +
                  $"Defense Provider: {(defenseProvider != null ? defenseProvider.GetType().Name : "NOT FOUND")}\n" +
                  $"Defense Capability: {(defenseCapability != null ? defenseCapability.GetType().Name : "NOT FOUND")}");
    }
}