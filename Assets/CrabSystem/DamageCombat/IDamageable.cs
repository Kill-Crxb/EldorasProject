using UnityEngine;

/// <summary>
/// LEGACY INTERFACE - IDamageable
/// 
/// ⚠️ USAGE GUIDANCE:
/// 
/// For Brain-based entities (Player, NPCs):
///   âŒ DON'T use IDamageable
///   âœ… DO use brain.GetModule<DamageSystem>()
///   
/// For simple destructibles without Brain (crates, barrels, props):
///   âœ… DO implement IDamageable
///   Example: Destructible objects that don't need full combat stats
/// 
/// MIGRATION:
/// - Old: NPCDamageable (OBSOLETE - removed)
/// - New: DamageSystem (universal, works for all entities)
/// 
/// Example (Legacy - simple destructible):
///   public class BreakableCrate : MonoBehaviour, IDamageable
///   {
///       // ... implementation
///   }
/// 
/// Example (Modern - Brain entity):
///   var targetBrain = hitObject.GetComponent<ControllerBrain>();
///   if (targetBrain != null)
///   {
///       var damageSystem = targetBrain.GetModule<DamageSystem>();
///       damageSystem.TakeDamage(damagePacket);
///   }
/// 
/// Phase 1.7b: Marked as legacy, kept for backwards compatibility
/// </summary>
public interface IDamageable
{
    /// <summary>Take damage and return whether target survived</summary>
    bool TakeDamage(float damage, Vector3 source = default);

    /// <summary>Heal the target</summary>
    void Heal(float amount);

    /// <summary>Current health value</summary>
    float CurrentHealth { get; }

    /// <summary>Maximum health value</summary>
    float MaxHealth { get; }

    /// <summary>Health as percentage (0-1)</summary>
    float HealthPercentage { get; }
}

/// <summary>
/// Basic Damageable - Simple implementation for non-Brain entities
/// 
/// Use for:
/// - Destructible props (crates, barrels, walls)
/// - Environmental hazards
/// - Simple targets that don't need combat stats
/// 
/// Don't use for:
/// - Players (use Brain + DamageSystem)
/// - NPCs (use Brain + DamageSystem)
/// - Any entity with ControllerBrain component
/// </summary>
public class BasicDamageable : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private bool destroyOnDeath = true;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject deathEffect;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;

    // Events
    public System.Action<float> OnDamageTaken;
    public System.Action OnDeath;

    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => currentHealth / maxHealth;
    public bool IsAlive => currentHealth > 0;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public bool TakeDamage(float damage, Vector3 source = default)
    {
        if (!IsAlive) return false;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        OnDamageTaken?.Invoke(damage);

        // Play hit effects
        if (hitSound != null)
        {
            AudioSource.PlayClipAtPoint(hitSound, transform.position);
        }

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
            return false; // Target died
        }

        return true; // Target survived
    }

    public void Heal(float amount)
    {
        if (!IsAlive) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    void Die()
    {
        OnDeath?.Invoke();

        // Play death effects
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }

        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, transform.rotation);
        }

        // Destroy or disable
        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>Reset health (for respawning destructibles)</summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        gameObject.SetActive(true);
    }
}