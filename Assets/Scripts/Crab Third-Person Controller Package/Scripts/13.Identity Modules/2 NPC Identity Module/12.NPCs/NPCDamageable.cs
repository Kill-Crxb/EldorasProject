using UnityEngine;
using System;

public class NPCDamageable : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Death Settings")]
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelay = 2f;
    [SerializeField] private GameObject deathEffectPrefab;

    [Header("Visual Feedback")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip deathSound;

    // Events
    public event Action<float> OnDamageTaken;
    public event Action OnDeath;

    // IDamageable implementation
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;  // ← ADDED THIS
    public float HealthPercentage => currentHealth / maxHealth;  // ← ADDED THIS

    public bool IsAlive => currentHealth > 0;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public bool TakeDamage(float damage, Vector3 source = default)
    {
        if (!IsAlive) return false;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        OnDamageTaken?.Invoke(damage);

        Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");

        // Play hit sound
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
        Debug.Log($"{gameObject.name} healed {amount}. Health: {currentHealth}/{maxHealth}");
    }

    private void Die()
    {
        OnDeath?.Invoke();

        Debug.Log($"{gameObject.name} died!");

        // Play death sound
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }

        // Spawn death effect
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, transform.rotation);
        }

        // Destroy or disable
        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // Optional: For respawning enemies
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        gameObject.SetActive(true);
    }
}