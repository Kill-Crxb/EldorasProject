using UnityEngine;

/// <summary>
/// Target Dummy Behavior - Testing component for combat system
/// 
/// DESIGN: Minimal script that adds ONLY dummy-specific behavior:
/// - Auto-heal
/// - Damage logging
/// - Respawn on death
/// 
/// Everything else uses the REAL combat systems:
/// - DamageModule (damage calculations)
/// - RPGCoreStats (armor, resistances)
/// - HealthProvider (health management)
/// - CombatDamagePacket (proper damage pipeline)
/// 
/// Usage:
/// 1. Use Enemy_Universal prefab as base
/// 2. Remove AISystem, GOAPModule, PerceptionModule (no AI)
/// 3. Add this component
/// 4. Configure archetype with desired stats
/// </summary>
public class DummyBehavior : MonoBehaviour, IDamageable
{
    [Header("Auto-Heal Settings")]
    [SerializeField] private bool enableAutoHeal = true;
    [SerializeField] private float healInterval = 3f;
    [Tooltip("Instant heal to full, or gradual regen?")]
    [SerializeField] private bool instantHeal = true;

    [Header("Respawn Settings")]
    [SerializeField] private bool autoRespawn = true;
    [SerializeField] private float respawnDelay = 2f;

    [Header("Debug Logging")]
    [SerializeField] private bool logDamage = true;
    [SerializeField] private bool logDetailed = false;
    [Tooltip("Show damage numbers above dummy (requires UI setup)")]
    [SerializeField] private bool showFloatingDamage = false;

    [Header("Visual Feedback")]
    [SerializeField] private ParticleSystem hitEffect;
    [SerializeField] private ParticleSystem deathEffect;
    [SerializeField] private ParticleSystem respawnEffect;

    // System references (use REAL systems)
    private ControllerBrain brain;
    private IHealthProvider health;
    private DamageModule damageModule;
    private ICombatStatsProvider combatStats;

    // State
    private float lastHealTime;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool isDead;

    void Start()
    {
        brain = GetComponent<ControllerBrain>();

        if (brain == null)
        {
            Debug.LogError("[DummyBehavior] No ControllerBrain found! This should be on Enemy_Universal prefab.");
            enabled = false;
            return;
        }

        // Get real systems
        health = brain.GetModuleImplementing<IHealthProvider>();
        damageModule = brain.GetModule<DamageModule>();
        combatStats = brain.GetModuleImplementing<ICombatStatsProvider>();

        if (health == null)
        {
            Debug.LogError("[DummyBehavior] No IHealthProvider found!");
            enabled = false;
            return;
        }

        // Store spawn location for respawn
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        // Subscribe to damage events from DamageModule
        if (damageModule != null)
        {
            damageModule.OnDamageTaken += HandleDamageTaken;
        }

        // Subscribe to death events from health system
        if (health != null)
        {
            // Try to subscribe to death event (if your health system has one)
            // If not, we'll check in Update()
        }

        Debug.Log($"[DummyBehavior] Initialized on {gameObject.name}");

        if (logDetailed)
        {
            LogCurrentStats();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (damageModule != null)
        {
            damageModule.OnDamageTaken -= HandleDamageTaken;
        }
    }

    void Update()
    {
        if (health == null) return;

        // Check for death
        if (!isDead && !health.IsAlive())
        {
            HandleDeath();
        }

        // Auto-heal if enabled
        if (enableAutoHeal && health.IsAlive() && Time.time - lastHealTime >= healInterval)
        {
            PerformAutoHeal();
            lastHealTime = Time.time;
        }
    }

    #region Event Handlers

    /// <summary>
    /// Called when DamageModule processes incoming damage
    /// This uses the REAL damage packet from your combat system!
    /// </summary>
    private void HandleDamageTaken(CombatDamagePacket packet)
    {
        if (logDamage)
        {
            // Basic log
            Debug.Log($"[Dummy] Took {packet.finalDamage:F1} damage" +
                     (packet.isCriticalHit ? " (CRITICAL!)" : ""));

            // Detailed log
            if (logDetailed)
            {
                Debug.Log($"  Base: {packet.baseDamage:F1} | Final: {packet.finalDamage:F1} | " +
                         $"Type: {packet.damageType} | Crit: {packet.isCriticalHit} | " +
                         $"Attacker: {packet.attackerId}");
            }
        }

        // Visual feedback
        if (hitEffect != null)
        {
            // Spawn at hit point if available, otherwise at dummy position
            Vector3 effectPos = packet.hitPoint != Vector3.zero ?
                packet.hitPoint : transform.position + Vector3.up * 1.5f;

            Instantiate(hitEffect, effectPos, Quaternion.identity).Play();
        }

        // Floating damage numbers (if you have a system for this)
        if (showFloatingDamage)
        {
            ShowFloatingDamage(packet);
        }
    }

    private void HandleDeath()
    {
        isDead = true;

        Debug.Log($"[Dummy] Died! Final HP: {health.GetCurrentHealth():F1}");

        // Visual feedback
        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position + Vector3.up, Quaternion.identity).Play();
        }

        // Auto-respawn if enabled
        if (autoRespawn)
        {
            Invoke(nameof(Respawn), respawnDelay);
        }
    }

    #endregion

    #region Auto-Heal

    private void PerformAutoHeal()
    {
        if (health == null || !health.IsAlive()) return;

        float currentHP = health.GetCurrentHealth();
        float maxHP = health.GetMaxHealth();

        if (currentHP >= maxHP) return; // Already full

        if (instantHeal)
        {
            // Full heal
            health.SetHealth(maxHP);

            if (logDamage)
            {
                Debug.Log($"[Dummy] Auto-healed to full ({maxHP:F1} HP)");
            }
        }
        else
        {
            // Gradual heal (20% of max HP per interval)
            float healAmount = maxHP * 0.2f;
            health.SetHealth(Mathf.Min(maxHP, currentHP + healAmount));

            if (logDamage)
            {
                Debug.Log($"[Dummy] Regenerated {healAmount:F1} HP ({health.GetCurrentHealth():F1}/{maxHP:F1})");
            }
        }
    }

    #endregion

    #region Respawn

    private void Respawn()
    {
        if (health == null) return;

        // Reset position
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;

        // Reset health
        health.SetHealth(health.GetMaxHealth());
        isDead = false;

        Debug.Log($"[Dummy] Respawned at full health ({health.GetMaxHealth():F1} HP)");

        // Visual feedback
        if (respawnEffect != null)
        {
            Instantiate(respawnEffect, transform.position, Quaternion.identity).Play();
        }

        // Reset heal timer
        lastHealTime = Time.time;
    }

    #endregion

    #region IDamageable Implementation

    /// <summary>
    /// IDamageable interface properties - forwards to health system
    /// </summary>
    public float CurrentHealth => health?.GetCurrentHealth() ?? 0f;
    public float MaxHealth => health?.GetMaxHealth() ?? 0f;
    public float HealthPercentage => health != null && health.GetMaxHealth() > 0
        ? health.GetCurrentHealth() / health.GetMaxHealth()
        : 0f;

    /// <summary>
    /// IDamageable interface - required for hitbox system
    /// This forwards damage to the real health system
    /// </summary>
    public bool TakeDamage(float damage, Vector3 source = default)
    {
        if (health == null || !health.IsAlive())
            return false;

        // Apply damage through health system
        float currentHealth = health.GetCurrentHealth();
        float newHealth = Mathf.Max(0, currentHealth - damage);
        health.SetHealth(newHealth);

        if (logDamage)
        {
            Debug.Log($"[Dummy] Took {damage:F1} damage (Health: {newHealth:F1}/{health.GetMaxHealth():F1})");
        }

        return newHealth > 0; // Return true if still alive
    }

    /// <summary>
    /// Heal method - forwards to health system
    /// </summary>
    public void Heal(float amount)
    {
        if (health == null || !health.IsAlive())
            return;

        float currentHealth = health.GetCurrentHealth();
        float maxHealth = health.GetMaxHealth();
        float newHealth = Mathf.Min(maxHealth, currentHealth + amount);
        health.SetHealth(newHealth);

        if (logDamage)
        {
            Debug.Log($"[Dummy] Healed {amount:F1} (Health: {newHealth:F1}/{maxHealth:F1})");
        }
    }

    #endregion

    #region Debug Helpers

    [ContextMenu("Log Current Stats")]
    private void LogCurrentStats()
    {
        if (health == null) return;

        Debug.Log("=== TARGET DUMMY STATS ===");
        Debug.Log($"Health: {health.GetCurrentHealth():F1} / {health.GetMaxHealth():F1}");

        if (combatStats != null)
        {
            Debug.Log($"Armor: {combatStats.GetArmor():F1}");
            Debug.Log($"Attack Power: {combatStats.GetAttackPower():F1}");
            Debug.Log($"Crit Chance: {combatStats.GetCriticalChance():F1}%");
            Debug.Log($"Crit Multiplier: {combatStats.GetCriticalMultiplier():F2}x");
        }

        Debug.Log("==========================");
    }

    [ContextMenu("Reset Dummy")]
    private void ResetDummy()
    {
        if (health != null)
        {
            health.SetHealth(health.GetMaxHealth());
            isDead = false;
            Debug.Log("[Dummy] Manually reset to full health");
        }
    }

    [ContextMenu("Kill Dummy")]
    private void KillDummy()
    {
        if (health != null)
        {
            health.SetHealth(0);
            Debug.Log("[Dummy] Manually killed");
        }
    }
     
    private void ShowFloatingDamage(CombatDamagePacket packet)
    {
        // TODO: Implement floating damage numbers
        // This would spawn a UI element that floats upward and fades out
        // For now, just log it
        // You can implement this later when you have a floating damage prefab
    }

    #endregion
}